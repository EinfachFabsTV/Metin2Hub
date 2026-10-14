using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Costumes;

namespace M2Hub.Desktop.ViewModels;

/// Kostuem-Set-Katalog: oben "Alle" (Standard) und alle Push-Ringe (waehlbar),
/// darunter Live-Suche und das Set-Raster; Klick auf ein Set zeigt die Details
/// (Effektstufen, Kostuem-Varianten, Reittier/Pet/Waffe mit eigenen Werten).
///
/// Reines Nachschlagewerk - die Daten stehen in Services/Costumes/CostumeCatalog,
/// eingelesen aus der gebuendelten sets.json.
public sealed class CostumesViewModel : ViewModelBase
{
    private readonly List<(PushRing Ring, CostumeSet Set)> _all = new();
    private RingFilter? _filter;
    private string _query = "";
    private SetDetailViewModel? _selected;

    public CostumesViewModel()
    {
        ChooseRingCommand = new RelayCommand(p => { if (p is RingFilter r) Choose(r); });
        OpenSetCommand = new RelayCommand(p => { if (p is SetCardViewModel c) Open(c); });
        BackCommand = new RelayCommand(_ => Selected = null);

        foreach (var ring in CostumeCatalog.Rings)
            foreach (var set in ring.Sets)
                _all.Add((ring, set));

        // "Alle" zuerst und standardmaessig aktiv, danach die Ringe.
        Filters.Add(new RingFilter(Loc.T("costumes.all"), null, _all.Count, isAll: true, null));
        foreach (var ring in CostumeCatalog.Rings)
            Filters.Add(new RingFilter(ring.Name, CostumeCatalog.Image(ring.Icon), ring.Sets.Count, false, ring));

        _filter = Filters[0];
        _filter.IsActive = true;
        RebuildSets();
    }

    public ObservableCollection<RingFilter> Filters { get; } = new();
    public ObservableCollection<SetCardViewModel> Sets { get; } = new();

    public RelayCommand ChooseRingCommand { get; }
    public RelayCommand OpenSetCommand { get; }
    public RelayCommand BackCommand { get; }

    /// Suchfeld: filtert sofort beim Tippen - ueber Set- UND Teilnamen.
    public string Query
    {
        get => _query;
        set { if (Set(ref _query, value ?? "")) RebuildSets(); }
    }

    public string RingHeader { get; private set; } = "";
    public string CountLabel { get; private set; } = "";
    public string EmptyText { get; private set; } = "";
    public bool ShowEmpty { get; private set; }

    public SetDetailViewModel? Selected
    {
        get => _selected;
        private set
        {
            if (!Set(ref _selected, value)) return;
            Raise(nameof(ShowList));
            Raise(nameof(ShowDetail));
        }
    }

    public bool ShowList => _selected is null;
    public bool ShowDetail => _selected is not null;

    private void Choose(RingFilter f)
    {
        if (_filter == f) return;
        _filter = f;
        foreach (var r in Filters) r.IsActive = r == f;
        _query = "";
        Raise(nameof(Query));
        Selected = null;
        RebuildSets();
    }

    private void Open(SetCardViewModel card) => Selected = new SetDetailViewModel(card.Ring, card.Set);

    private void RebuildSets()
    {
        Sets.Clear();
        var all = _filter is { IsAll: true };
        var source = all ? _all : _all.Where(p => p.Ring == _filter!.Ring).ToList();
        var total = source.Count;

        RingHeader = _filter is null
            ? ""
            : $"{(_filter.IsAll ? Loc.T("costumes.allRings") : _filter.Name)} · {Loc.T(total == 1 ? "costumes.setOne" : "costumes.setMany", total)}";

        if (total == 0)
        {
            ShowEmpty = true;
            EmptyText = Loc.T("costumes.emptyRing");
            CountLabel = "";
        }
        else
        {
            var q = _query.Trim();
            var hits = source.Where(p => q.Length == 0 || Matches(p.Set, q)).ToList();

            CountLabel = q.Length == 0 ? "" : $"{hits.Count} / {total}";
            ShowEmpty = hits.Count == 0;
            EmptyText = hits.Count == 0 ? Loc.T("costumes.noHits", q) : "";

            foreach (var (ring, set) in hits) Sets.Add(new SetCardViewModel(ring, set, all));
        }

        Raise(nameof(RingHeader));
        Raise(nameof(CountLabel));
        Raise(nameof(EmptyText));
        Raise(nameof(ShowEmpty));
    }

    /// Treffer auf Set-Namen oder einen beliebigen Teil-Namen (Kostuem, Bild,
    /// Reittier, Pet, Waffe).
    private static bool Matches(CostumeSet s, string q)
    {
        bool C(string? t) => t is not null && t.Contains(q, StringComparison.OrdinalIgnoreCase);
        if (C(s.Name)) return true;
        if (s.Costumes.Any(c => C(c.Name) || c.Images.Any(i => C(i.Name)))) return true;
        return s.Mounts.Concat(s.Pets).Concat(s.Weapons).Any(v => C(v.Name));
    }

    public void RelabelAfterLanguageChange()
    {
        if (Filters.Count > 0) Filters[0].Name = Loc.T("costumes.all");
        RebuildSets();
        Selected = null;
    }
}

/// Hilfsfunktionen fuer die Effektzeilen (EXP wird gruen hervorgehoben) und
/// Bildvorschau/Ordnung.
internal static class SetFmt
{
    public static bool IsExp(string t) => Regex.IsMatch(t, @"EXP\s*\+", RegexOptions.IgnoreCase);

    public static List<EffectLine> Lines(IEnumerable<string>? src) =>
        (src ?? []).Select(t => new EffectLine(t, IsExp(t))).ToList();

    public static int MaxTier(IEnumerable<string> keys) =>
        keys.Select(k => int.TryParse(k, out var n) ? n : 0).DefaultIfEmpty(0).Max();

    /// Vorschaubild eines Sets: einheitlich das Body-Bild (m), sonst Body,
    /// sonst irgendein Kostuembild, sonst Reittier/Pet/Waffe.
    public static string Preview(CostumeSet s)
    {
        var imgs = s.Costumes.SelectMany(c => c.Images).ToList();
        var body = imgs.FirstOrDefault(i => i.Part == "Body" && i.Gender == "m")
                   ?? imgs.FirstOrDefault(i => i.Part == "Body")
                   ?? imgs.FirstOrDefault();
        if (body is not null) return body.File;
        return s.Mounts.Concat(s.Pets).Concat(s.Weapons).FirstOrDefault()?.File ?? "";
    }

    // Reihenfolge der Kostuembilder: Frisur vor Body, m vor w.
    public static int PartRank(string part) => part == "Frisur" ? 0 : part == "Body" ? 1 : 2;
    public static int GenderRank(string g) => g == "m" ? 0 : g == "w" ? 1 : 2;
}

public sealed record EffectLine(string Text, bool IsExp);

public sealed record EffectTier(string Title, bool Highlight, IReadOnlyList<EffectLine> Lines);

/// Ein Filter in der oberen Leiste: "Alle" oder ein Push-Ring.
public sealed class RingFilter(string name, Bitmap? icon, int count, bool isAll, PushRing? ring) : ViewModelBase
{
    private bool _isActive;
    private string _name = name;

    public PushRing? Ring { get; } = ring;
    public string Name { get => _name; set => Set(ref _name, value); }
    public Bitmap? Icon { get; } = icon;
    public int Count { get; } = count;
    public bool IsAll { get; } = isAll;
    public bool HasIcon => Icon is not null;
    public bool HasSets => Count > 0;
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
}

/// Eine Set-Kachel im Raster.
public sealed class SetCardViewModel(PushRing ring, CostumeSet set, bool showRing)
{
    public PushRing Ring { get; } = ring;
    public CostumeSet Set { get; } = set;
    public string Name => Set.Name;
    public string RingName => Ring.Name;
    public bool ShowRing { get; } = showRing;
    public Bitmap? Icon => CostumeCatalog.Image(SetFmt.Preview(Set));

    private int Tier => SetFmt.MaxTier(Set.Effects.Keys);
    public bool HasTier => Tier > 0;
    public string TierLabel => Tier > 0 ? Loc.T("costumes.teile", Tier) : "";
    public IReadOnlyList<EffectLine> TopEffect =>
        SetFmt.Lines(Set.Effects.TryGetValue(Tier.ToString(), out var l) ? l : null);
}

/// Die Detailansicht eines Sets.
public sealed class SetDetailViewModel
{
    public SetDetailViewModel(PushRing ring, CostumeSet set)
    {
        RingName = ring.Name;
        Name = set.Name;
        Icon = CostumeCatalog.Image(SetFmt.Preview(set));

        var max = SetFmt.MaxTier(set.Effects.Keys);
        Tiers = set.Effects.Keys
            .Select(k => int.TryParse(k, out var n) ? n : 0)
            .Where(n => n > 0)
            .OrderBy(n => n)
            .Select(n => new EffectTier(
                Loc.T("costumes.teile", n), n == max,
                SetFmt.Lines(set.Effects.TryGetValue(n.ToString(), out var l) ? l : null)))
            .ToList();

        Costumes = set.Costumes.Select(c => new CostumeGroupViewModel(c)).ToList();

        // Waffen links, rechts Pet und Reittier (Pet zuerst).
        Weapons = set.Weapons.Select(w => new SlotViewModel(Loc.T("costumes.slot.weapon"), w)).ToList();
        RightSlots = set.Pets.Select(p => new SlotViewModel(Loc.T("costumes.slot.pet"), p))
            .Concat(set.Mounts.Select(m => new SlotViewModel(Loc.T("costumes.slot.mount"), m)))
            .ToList();

        HasCostumes = Costumes.Count > 0;
        HasWeapons = Weapons.Count > 0;
        HasRight = RightSlots.Count > 0;
        HasSlots = HasWeapons || HasRight;
    }

    public string RingName { get; }
    public string Name { get; }
    public Bitmap? Icon { get; }
    public IReadOnlyList<EffectTier> Tiers { get; }
    public IReadOnlyList<CostumeGroupViewModel> Costumes { get; }
    public IReadOnlyList<SlotViewModel> Weapons { get; }
    public IReadOnlyList<SlotViewModel> RightSlots { get; }
    public bool HasCostumes { get; }
    public bool HasWeapons { get; }
    public bool HasRight { get; }
    public bool HasSlots { get; }
}

/// Eine Kostuem-Variante (Tier) in der Detailansicht.
public sealed class CostumeGroupViewModel
{
    public CostumeGroupViewModel(CostumeGroup group)
    {
        Name = group.Name;
        Images = group.Images
            .OrderBy(i => SetFmt.PartRank(i.Part))
            .ThenBy(i => SetFmt.GenderRank(i.Gender))
            .Select(i => new SetImageViewModel(i))
            .ToList();
    }

    public string Name { get; }
    public IReadOnlyList<SetImageViewModel> Images { get; }
}

/// Ein Einzelbild einer Kostuem-Variante.
public sealed class SetImageViewModel
{
    public SetImageViewModel(SetImage img)
    {
        Name = img.Name;
        Icon = CostumeCatalog.Image(img.File);
        var gender = img.Gender switch { "m" => "♂", "w" => "♀", _ => "" };
        var part = string.IsNullOrEmpty(img.Part) ? "" : img.Part;
        Tag = string.Join(" · ", new[] { part, gender }.Where(s => s.Length > 0));
    }

    public string Name { get; }
    public string Tag { get; }
    public bool HasTag => Tag.Length > 0;
    public Bitmap? Icon { get; }
}

/// Eine Variante des 4. Slots (Reittier/Pet/Waffe) mit eigenen Werten.
public sealed class SlotViewModel
{
    public SlotViewModel(string typeLabel, SlotVariant v)
    {
        TypeLabel = typeLabel;
        Name = v.Name;
        Icon = CostumeCatalog.Image(v.File);
        Stats = SetFmt.Lines(v.Stats);
    }

    public string TypeLabel { get; }
    public string Name { get; }
    public Bitmap? Icon { get; }
    public IReadOnlyList<EffectLine> Stats { get; }
}
