using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

public sealed class ItemshopItemViewModel : ViewModelBase
{
    private Bitmap? _image;

    public ItemshopItemViewModel(ItemshopEventDto dto)
    {
        Dto = dto;
        Title = dto.Title;
        Url = dto.Url;
        Text = dto.BodyText.Trim();
        ImageUrl = dto.ImageUrl;
        Range = GlobalEventViewModel.FormatRange(dto.StartsAt, dto.EndsAt);

        var now = DateTime.Now;
        var start = dto.StartsAt?.ToLocalTime();
        var end = dto.EndsAt?.ToLocalTime();
        Running = start is not null && end is not null && now >= start && now <= end;
        Upcoming = start is not null && now < start;
        Over = end is not null && now > end;

        // "neu" richtet sich nach dem Zeitpunkt der ersten Entdeckung.
        IsNew = dto.FetchedAt is { } f && (DateTime.UtcNow - f.ToUniversalTime()).TotalDays <= 3;

        Kind = Services.Forum.Board.KindLabels.TryGetValue(dto.Kind, out var label) ? label : "Aktion";

        State = Running ? Loc.T("events.running")
            : Upcoming ? Loc.T("events.upcoming")
            : Over ? Loc.T("itemshop.over")
            : Loc.T("events.unknownPeriod");
    }

    public ItemshopEventDto Dto { get; }
    public string Title { get; }
    public string Url { get; }
    public string Text { get; }
    public bool HasText => Text.Length > 0;
    public string? ImageUrl { get; }
    public string Range { get; }
    public string Kind { get; }
    public string State { get; }
    public bool Running { get; }
    public bool Upcoming { get; }
    public bool Over { get; }

    /// Reihenfolge: erst was demnaechst startet, darunter was gerade laeuft,
    /// dann der Rest.
    public int Rank => Upcoming ? 0 : Running ? 1 : 2;
    public bool IsNew { get; }

    public Bitmap? Image { get => _image; private set { if (Set(ref _image, value)) Raise(nameof(HasImage)); } }
    public bool HasImage => _image is not null;

    public async Task LoadImageAsync(ImageCache cache) => Image = await cache.GetAsync(ImageUrl);
}

/// Seite "Itemshop": die im Backend gepflegten Aktionen, nur lesend.
public sealed class ItemshopViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly ForumService _forum;
    private readonly ImageCache _images;
    private readonly List<ItemshopItemViewModel> _all = new();

    private bool _busy;
    private string? _error;
    private string _search = "";
    // Standard: was laeuft, dazu was demnaechst kommt. Abgelaufenes bleibt aus.
    private bool _showRunning = true;
    private bool _showUpcoming = true;
    private bool _showOver;

    public ItemshopViewModel(LocalStore store, ForumService forum, ImageCache images)
    {
        _store = store;
        _forum = forum;
        _images = images;
        ReloadCommand = new AsyncRelayCommand(_ => RefreshAsync());
        OpenInBrowserCommand = new RelayCommand(p => Platform.OpenUrl(p as string));
    }

    public bool Busy { get => _busy; private set { if (Set(ref _busy, value)) Raise(nameof(NotBusy)); } }
    public bool NotBusy => !_busy;
    public string? Error { get => _error; private set { if (Set(ref _error, value)) Raise(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(_error);

    public ObservableCollection<ItemshopItemViewModel> Items { get; } = new();

    public bool ShowRunning { get => _showRunning; set { if (Set(ref _showRunning, value)) ApplyFilter(); } }
    public bool ShowUpcoming { get => _showUpcoming; set { if (Set(ref _showUpcoming, value)) ApplyFilter(); } }
    public bool ShowOver { get => _showOver; set { if (Set(ref _showOver, value)) ApplyFilter(); } }
    public string Search { get => _search; set { if (Set(ref _search, value)) ApplyFilter(); } }

    public bool Empty => !Busy && Items.Count == 0;

    public AsyncRelayCommand ReloadCommand { get; }
    public RelayCommand OpenInBrowserCommand { get; }

    /// Zeigt den lokalen Stand an; geladen wird ueber den Zeitplan bzw. den Knopf.
    public void Reload()
    {
        _all.Clear();
        foreach (var dto in _store.Cache.Itemshop)
            _all.Add(new ItemshopItemViewModel(dto));

        Error = _store.Cache.LastError;
        ApplyFilter();

        foreach (var item in _all) _ = item.LoadImageAsync(_images);
    }

    private async Task RefreshAsync()
    {
        Busy = true;
        try
        {
            await _forum.RefreshAsync(force: true);
        }
        finally
        {
            Busy = false;
        }
        Reload();
    }

    private void ApplyFilter()
    {
        var term = _search.Trim();
        var cmp = StringComparison.CurrentCultureIgnoreCase;

        Items.Clear();
        foreach (var item in _all
                     .OrderBy(i => i.Rank)
                     .ThenBy(i => i.Dto.StartsAt ?? DateTime.MaxValue))
        {
            var stateOk = item.Running ? ShowRunning
                : item.Upcoming ? ShowUpcoming
                : item.Over ? ShowOver
                // Ohne erkannten Zeitraum laesst sich nichts zuordnen - dann
                // mitzeigen, solange ueberhaupt etwas eingeblendet ist.
                : ShowRunning || ShowUpcoming;
            if (!stateOk) continue;
            if (term.Length > 0 && !item.Title.Contains(term, cmp) && !item.Text.Contains(term, cmp)) continue;
            Items.Add(item);
        }
        Raise(nameof(Empty));
    }
}
