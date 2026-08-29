using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services.Forum;

namespace M2Hub.Desktop.Services;

/// Holt Eventkalender, globale Events und Itemshop-Aktionen direkt aus dem
/// offiziellen Forum und legt sie lokal ab.
///
/// Die App laeuft ohne Server, deshalb stellt sie die Requests selbst. Damit
/// das schonend bleibt, gelten dieselben Schutzmechanismen wie im Backend:
///
///  1. Board-Uebersicht nur bedingt (ETag/Last-Modified) - in aller Regel 304.
///  2. Nur Seite 1 der Uebersicht.
///  3. Thread-Seiten ausschliesslich fuer unbekannte Thread-IDs.
///  4. Hoechstens MaxNewPerRun neue Threads je Lauf, der Rest folgt spaeter.
///  5. RequestDelay zwischen zwei Requests.
///  6. Bei 429/5xx ein Cooldown mit exponentiellem Backoff.
///  7. Der Eventkalender wird nur alle CalendarInterval geladen.
///
/// Diese Grenzen nicht ohne Ruecksprache lockern.
public sealed class ForumService
{
    private const int MaxNewPerRun = 5;
    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan BackoffBase = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BackoffMax = TimeSpan.FromHours(12);
    private static readonly TimeSpan CalendarInterval = TimeSpan.FromHours(4);

    private const string UserAgent = "M2Hub Desktop (+https://m2hub.orfabs.de)";

    private const string ItemshopPrefix = "itemshop";
    private const string GlobalPrefix = "globalevents";

    private readonly LocalStore _store;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ForumService(LocalStore store)
    {
        _store = store;
        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(25),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public sealed record RefreshResult(int AddedEvents, int AddedItemshop, bool CalendarUpdated, string? Error);

    /// Ein Durchlauf ueber beide Boards und den Kalender. Laeuft schon einer,
    /// wird der Aufruf verworfen statt zu warten.
    public async Task<RefreshResult?> RefreshAsync(bool force, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct)) return null;
        try
        {
            string? error = null;
            var addedEvents = 0;
            var addedShop = 0;
            var calendar = false;

            try
            {
                addedEvents = await SyncBoardAsync(
                    GlobalPrefix, ForumSource.EventsBoardUrl, force, SaveGlobalEvent, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                error = "Events: " + Short(ex);
            }

            try
            {
                addedShop = await SyncBoardAsync(
                    ItemshopPrefix, ForumSource.ItemshopBoardUrl, force, SaveItemshop, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                error ??= "Itemshop: " + Short(ex);
            }

            try
            {
                calendar = await SyncCalendarAsync(force, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                error ??= "Kalender: " + Short(ex);
            }

            _store.Cache.LastRefreshAt = DateTime.UtcNow;
            _store.Cache.LastError = error;
            _store.SaveCache();

            return new RefreshResult(addedEvents, addedShop, calendar, error);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Short(Exception ex) =>
        ex is TaskCanceledException ? "Zeitüberschreitung." : ex.Message;

    /* ---------------- Board-Abgleich ---------------- */

    private async Task<int> SyncBoardAsync(
        string prefix,
        string boardUrl,
        bool force,
        Action<Board.BoardThread, Board.ParsedPost> save,
        CancellationToken ct)
    {
        var state = _store.Cache.State(prefix);

        // Der Cooldown nach einem Fehler gilt auch beim manuellen Knopf -
        // sonst haemmert ein ungeduldiger Klick gegen ein Limit.
        if (state.CooldownUntil is { } until && DateTime.UtcNow < until) return 0;

        var request = new HttpRequestMessage(HttpMethod.Get, boardUrl);
        if (!force)
        {
            // Bedingter Abruf: unveraendertes Board kostet nur ein 304.
            if (!string.IsNullOrEmpty(state.ETag))
                request.Headers.TryAddWithoutValidation("If-None-Match", state.ETag);
            if (!string.IsNullOrEmpty(state.LastModified))
                request.Headers.TryAddWithoutValidation("If-Modified-Since", state.LastModified);
        }

        using var res = await _http.SendAsync(request, ct).ConfigureAwait(false);
        state.LastFetchAt = DateTime.UtcNow;

        if (res.StatusCode == HttpStatusCode.NotModified)
        {
            NoteSuccess(state);
            return 0;
        }

        if ((int)res.StatusCode == 429 || (int)res.StatusCode >= 500)
        {
            NoteFailure(state);
            return 0;
        }

        if (!res.IsSuccessStatusCode)
        {
            NoteFailure(state);
            return 0;
        }

        state.ETag = res.Headers.ETag?.ToString();
        state.LastModified = res.Content.Headers.LastModified?.ToString("R");
        NoteSuccess(state);

        var html = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var threads = Board.ParseBoardList(html);

        var known = state.KnownThreads.ToHashSet(StringComparer.Ordinal);
        var added = 0;

        foreach (var thread in threads)
        {
            if (added >= MaxNewPerRun) break;
            if (known.Contains(thread.ThreadId)) continue;

            await Task.Delay(RequestDelay, ct).ConfigureAwait(false);

            var post = await LoadPostAsync(thread, state, ct).ConfigureAwait(false);
            if (post is null) continue;

            save(thread, post);
            state.KnownThreads.Add(thread.ThreadId);
            added++;
        }

        // Die Liste waechst sonst unbegrenzt; die Eintraege selbst laufen ohnehin
        // nach sieben Tagen ab.
        if (state.KnownThreads.Count > 300)
            state.KnownThreads.RemoveRange(0, state.KnownThreads.Count - 300);

        return added;
    }

    private async Task<Board.ParsedPost?> LoadPostAsync(
        Board.BoardThread thread, BoardState state, CancellationToken ct)
    {
        try
        {
            using var res = await _http.GetAsync(AbsoluteUrl(thread.Url), ct).ConfigureAwait(false);
            if ((int)res.StatusCode == 429 || (int)res.StatusCode >= 500)
            {
                NoteFailure(state);
                return null;
            }
            if (!res.IsSuccessStatusCode) return null;

            var html = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Board.ParsePost(html);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Ein einzelner Thread darf den Lauf nicht abbrechen; beim naechsten
            // Durchgang wird er erneut versucht (er bleibt unbekannt).
            return null;
        }
    }

    private static string AbsoluteUrl(string url) =>
        url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : ForumSource.BaseUrl + url.TrimStart('/');

    private void NoteFailure(BoardState state)
    {
        state.FailCount++;
        var wait = TimeSpan.FromMilliseconds(Math.Min(
            BackoffMax.TotalMilliseconds,
            BackoffBase.TotalMilliseconds * Math.Pow(2, state.FailCount - 1)));
        state.CooldownUntil = DateTime.UtcNow + wait;
    }

    private static void NoteSuccess(BoardState state)
    {
        state.FailCount = 0;
        state.CooldownUntil = null;
    }

    /* ---------------- Speichern ---------------- */

    private void SaveGlobalEvent(Board.BoardThread thread, Board.ParsedPost post)
    {
        var title = post.Title.Length > 0 ? post.Title : thread.Title;
        var period = Board.ParsePeriod(post.BodyText, title, post.PostedAt?.Year);

        _store.Cache.GlobalEvents.RemoveAll(e => e.Id == ThreadKey(thread.ThreadId));
        _store.Cache.GlobalEvents.Add(new GlobalEventDto
        {
            Id = ThreadKey(thread.ThreadId),
            Url = AbsoluteUrl(thread.Url),
            Title = title,
            Kind = Board.ClassifyGlobal(title, post.BodyText),
            Parts = Board.ParseParts(title),
            ImageUrl = post.ImageUrl,
            BodyText = post.BodyText,
            StartsAt = period.StartsAt,
            EndsAt = period.EndsAt,
            PostedAt = post.PostedAt,
            FetchedAt = DateTime.UtcNow,
        });
    }

    private void SaveItemshop(Board.BoardThread thread, Board.ParsedPost post)
    {
        var title = post.Title.Length > 0 ? post.Title : thread.Title;
        var period = Board.ParsePeriod(post.BodyText, title, post.PostedAt?.Year);

        _store.Cache.Itemshop.RemoveAll(e => e.Id == ThreadKey(thread.ThreadId));
        _store.Cache.Itemshop.Add(new ItemshopEventDto
        {
            Id = ThreadKey(thread.ThreadId),
            Url = AbsoluteUrl(thread.Url),
            Title = title,
            Kind = Board.Classify(title),
            ImageUrl = post.ImageUrl,
            BodyText = post.BodyText,
            StartsAt = period.StartsAt,
            EndsAt = period.EndsAt,
            PostedAt = post.PostedAt,
            FetchedAt = DateTime.UtcNow,
        });
    }

    /// Die Thread-ID ist die stabile Kennung; sie dient hier als lokale Id.
    private static int ThreadKey(string threadId) =>
        int.TryParse(threadId, out var id) ? id : threadId.GetHashCode();

    /* ---------------- Eventkalender ---------------- */

    /// Der Eventkalender: ein Thread, ein Abruf.
    ///
    /// Frueher wurden hier bis zu vier Threads gesucht und geladen, weil die
    /// anderen Foren jeden Monat einen neuen anlegen. Der deutsche Thread
    /// bleibt bestehen - damit faellt die ganze Suche weg.
    private async Task<bool> SyncCalendarAsync(bool force, CancellationToken ct)
    {
        var now = Html.BerlinNow();
        var cache = _store.Cache;

        var fresh = cache.CalendarFetchedAt is { } at
                    && cache.CalendarMonth == now.M
                    && DateTime.UtcNow - at.ToUniversalTime() < CalendarInterval
                    && cache.Servers.Count > 0;
        if (fresh && !force) return false;

        using var res = await _http.GetAsync(ForumSource.CalendarThreadUrl, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return false;

        var html = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var servers = EventCalendar.ParseEventPost(html, now.M);

        // Nur uebernehmen, wenn der Parser etwas gefunden hat - sonst bleibt der
        // alte Stand stehen (fail-open).
        if (servers.Count == 0) return false;

        cache.Servers = servers;
        cache.CalendarMonth = now.M;
        cache.CalendarFetchedAt = DateTime.UtcNow;
        return true;
    }
}
