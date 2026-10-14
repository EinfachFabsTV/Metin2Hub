using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.ViewModels;

/// Biologen-Rechner: wie viele Akzeleratoren, Drachenmuenzen und Elixier-Packs
/// ein Bereich von Abgaben kostet.
///
/// Uebernommen aus der Website ('Veraltet/app/pages/rechner/biologen.vue'); der
/// Rechenweg liegt in Services/Calc/BioCalc, die Abgabenliste ebenfalls dort als
/// Katalog. Der Champion-Reiter ist vorgesehen, aber ausgegraut - dafuer fehlen
/// noch die Daten.
public sealed class BioCalcViewModel : ViewModelBase
{
    private BioOption? _start;
    private BioOption? _end;
    private string _cooldownText = "1h";
    private string _alreadyText = "0";
    private string _perDayText = BioCalc.Settings.DefaultPerDay.ToString();

    public BioCalcViewModel()
    {
        foreach (var b in BioCalc.Catalog.OrderBy(b => b.Level))
            Sorted.Add(new BioOption(b, Loc.T("calc.bio.lvl", b.Level)));

        _start = Sorted.FirstOrDefault();
        _end = Sorted.LastOrDefault();
        Recalculate();
    }

    /* ======================= Art (normal / Champion) ======================= */

    // Nur die normalen Bios sind vorhanden; der Champion-Reiter ist ausgegraut.
    public bool ChampionAvailable => false;

    /* ======================= Auswahl ======================= */

    public ObservableCollection<BioOption> Sorted { get; } = new();

    public BioOption? Start
    {
        get => _start;
        set { if (Set(ref _start, value)) Recalculate(); }
    }

    public BioOption? End
    {
        get => _end;
        set { if (Set(ref _end, value)) Recalculate(); }
    }

    public string CooldownText { get => _cooldownText; set { if (Set(ref _cooldownText, value)) Recalculate(); } }
    public string AlreadyText { get => _alreadyText; set { if (Set(ref _alreadyText, value)) Recalculate(); } }
    public string PerDayText { get => _perDayText; set { if (Set(ref _perDayText, value)) Recalculate(); } }

    /// Die Bios zwischen den beiden gewaehlten Enden, Reihenfolge egal.
    private IReadOnlyList<BioCalc.Bio> Selection()
    {
        var a = _start is null ? -1 : IndexOf(_start);
        var b = _end is null ? -1 : IndexOf(_end);
        if (a < 0 || b < 0) return [];
        var lo = Math.Min(a, b);
        var hi = Math.Max(a, b);
        return Sorted.Skip(lo).Take(hi - lo + 1).Select(o => o.Bio).ToList();
    }

    private int IndexOf(BioOption option)
    {
        for (var i = 0; i < Sorted.Count; i++) if (Sorted[i].Bio.Id == option.Bio.Id) return i;
        return -1;
    }

    /* ======================= Ergebnis ======================= */

    private BioCalc.BioResult Result()
    {
        var opts = new BioCalc.BioOpts(
            BioCalc.ParseCooldown(_cooldownText),
            ParseInt(_alreadyText),
            ParseInt(_perDayText));
        return BioCalc.Compute(Selection(), BioCalc.BioTab.Normal, opts);
    }

    public ObservableCollection<BioCardViewModel> Cards { get; } = new();

    // Grundinformationen
    public string TotalSubs { get; private set; } = "";
    public string TotalHours { get; private set; } = "";
    public string TotalDays { get; private set; } = "";

    // Akzeleratoren je Skip (normale und halbierte Reihe), Spalten 10/20/30 %
    public string PsN10 { get; private set; } = "";
    public string PsN20 { get; private set; } = "";
    public string PsN30 { get; private set; } = "";
    public string PsH10 { get; private set; } = "";
    public string PsH20 { get; private set; } = "";
    public string PsH30 { get; private set; } = "";

    // Gesamtbedarf
    public string Acc10 { get; private set; } = "";
    public string Acc20 { get; private set; } = "";
    public string Acc30 { get; private set; } = "";
    public string Dr10 { get; private set; } = "";
    public string Dr20 { get; private set; } = "";
    public string Dr30 { get; private set; } = "";
    public string Drs10 { get; private set; } = "";
    public string Drs20 { get; private set; } = "";
    public string Drs30 { get; private set; } = "";
    public string Drf30 { get; private set; } = "";

    // Elixier-Packs
    public string Packs { get; private set; } = "";
    public string PacksDr { get; private set; } = "";
    public string PacksFlash { get; private set; } = "";

    public string SelectionNote { get; private set; } = "";

    private void Recalculate()
    {
        var r = Result();

        TotalSubs = Fmt(r.TotalSubs);
        TotalHours = Fmt(r.TotalHoursNone);
        TotalDays = Fmt2(r.TotalDays);

        PsN10 = r.PsN10.ToString(); PsN20 = r.PsN20.ToString(); PsN30 = r.PsN30.ToString();
        PsH10 = r.PsH10.ToString(); PsH20 = r.PsH20.ToString(); PsH30 = r.PsH30.ToString();

        Acc10 = Fmt(r.Acc10); Acc20 = Fmt(r.Acc20); Acc30 = Fmt(r.Acc30);
        Dr10 = Fmt(r.Dr10); Dr20 = Fmt(r.Dr20); Dr30 = Fmt(r.Dr30);
        Drs10 = Fmt(r.Drs10); Drs20 = Fmt(r.Drs20); Drs30 = Fmt(r.Drs30);
        Drf30 = r.Drf30 is { } f ? Fmt(f) : "–";

        Packs = Fmt(r.Pk); PacksDr = Fmt(r.PkDr); PacksFlash = Fmt(r.PkFlash);

        var sel = Selection();
        SelectionNote = Loc.T("calc.bio.selectionNote", sel.Count, Sorted.Count);

        Cards.Clear();
        foreach (var b in sel) Cards.Add(new BioCardViewModel(b));

        foreach (var name in new[]
        {
            nameof(TotalSubs), nameof(TotalHours), nameof(TotalDays),
            nameof(PsN10), nameof(PsN20), nameof(PsN30), nameof(PsH10), nameof(PsH20), nameof(PsH30),
            nameof(Acc10), nameof(Acc20), nameof(Acc30),
            nameof(Dr10), nameof(Dr20), nameof(Dr30),
            nameof(Drs10), nameof(Drs20), nameof(Drs30), nameof(Drf30),
            nameof(Packs), nameof(PacksDr), nameof(PacksFlash),
            nameof(SelectionNote),
        }) Raise(name);
    }

    public void RelabelAfterLanguageChange()
    {
        foreach (var o in Sorted) o.Label = Loc.T("calc.bio.lvl", o.Bio.Level);
        Recalculate();
    }

    private static int ParseInt(string? text) =>
        int.TryParse((text ?? "").Trim(), out var v) ? Math.Max(0, v) : 0;

    private static string Fmt(double value) => Math.Round(value).ToString("N0", CultureInfo.CurrentCulture);
    private static string Fmt2(double value) => value.ToString("N2", CultureInfo.CurrentCulture);
}

/// Ein Eintrag der Auswahllisten: die Bio samt Beschriftung.
public sealed class BioOption(BioCalc.Bio bio, string label) : ViewModelBase
{
    private string _label = label;

    public BioCalc.Bio Bio { get; } = bio;
    public string Label { get => _label; set => Set(ref _label, value); }

    /// "Level 30 — Orkzahn" fuer die ComboBox.
    public string Display => $"{_label} — {Bio.Name}";
}

/// Eine Karte in der Liste der einbezogenen Bios.
public sealed class BioCardViewModel
{
    private static readonly Dictionary<string, Bitmap?> IconCache = new(StringComparer.Ordinal);

    public BioCardViewModel(BioCalc.Bio bio)
    {
        Name = bio.Name;
        LevelLabel = Loc.T("calc.bio.lvl", bio.Level);
        Submissions = Loc.T("calc.bio.submissions", bio.Submissions);
        Effects = string.Join(" · ", bio.Effects);
        Icon = LoadIcon(bio.IconFile);
    }

    public string Name { get; }
    public string LevelLabel { get; }
    public string Submissions { get; }
    public string Effects { get; }
    public Bitmap? Icon { get; }

    /// Mitgeliefertes Icon aus Assets/Bio laden; einmal je Datei.
    private static Bitmap? LoadIcon(string file)
    {
        if (IconCache.TryGetValue(file, out var cached)) return cached;

        Bitmap? bitmap = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://M2Hub/Assets/Bio/{file}"));
            bitmap = new Bitmap(stream);
        }
        catch
        {
            // Fehlt das Bild, bleibt die Karte ohne - besser als ein Absturz.
        }

        IconCache[file] = bitmap;
        return bitmap;
    }
}
