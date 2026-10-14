using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Costumes;

namespace M2Hub.Desktop.ViewModels;

/// Kostuem-Set-Katalog: oben alle Push-Ringe (waehlbar), darunter Live-Suche
/// und das Set-Raster des gewaehlten Rings; Klick auf ein Set zeigt die Details
/// (Effektstufen 2/3/4, Kostuem-Varianten, 4. Slot mit eigenen Werten).
///
/// Reines Nachschlagewerk - die Daten stehen in Services/Costumes/CostumeCatalog,
/// eingelesen aus der gebuendelten sets.json.
public sealed class CostumesViewModel : ViewModelBase
{
    private RingItem? _ring;
    private string _query = "";
    private SetDetailViewModel? _selected;

    public CostumesViewModel()
    {
        ChooseRingCommand = new RelayCommand(p => { if (p is RingItem r) ChooseRing(r); });
        OpenSetCommand = new RelayCommand(p => { if (p is SetCardViewModel c) Open(c); });
        BackCommand = new RelayCommand(_ => Selected = null);

        foreach (var ring in CostumeCatalog.Rings)
            Rings.Add(new RingItem(ring));

        _ring = Rings.FirstOrDefault();
        if (_ring is not null) _ring.IsActive = true;
        RebuildSets();
    }

    public ObservableCollection<RingItem> Rings { get; } = new();
    public ObservableCollection<SetCardViewModel> Sets { get; } = new();

    public RelayCommand ChooseRingCommand { get; }
    public RelayCommand OpenSetCommand { get; }
    public RelayCommand BackCommand { get; }

    /// Suchfeld: filtert sofort beim Tippen.
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

    private void ChooseRing(RingItem ring)
    {
        if (_ring == ring) return;
        _ring = ring;
        foreach (var r in Rings) r.IsActive = r == ring;
        _query = "";
        Raise(nameof(Query));
        Selected = null;
        RebuildSets();
    }

    private void Open(SetCardViewModel card)
    {
        if (_ring is null) return;
        Selected = new SetDetailViewModel(_ring.Ring, card.Set);
    }

    private void RebuildSets()
    {
        Sets.Clear();
        var ring = _ring?.Ring;
        var count = ring?.Sets.Count ?? 0;

        RingHeader = ring is null
            ? ""
            : $"{ring.Name} · {Loc.T(count == 1 ? "costumes.setOne" : "costumes.setMany", count)}";

        if (ring is null || count == 0)
        {
            ShowEmpty = true;
            EmptyText = Loc.T("costumes.emptyRing");
            CountLabel = "";
        }
        else
        {
            var q = _query.Trim();
            var hits = ring.Sets
                .Where(s => q.Length == 0 || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();

            CountLabel = q.Length == 0 ? "" : $"{hits.Count} / {count}";
            ShowEmpty = hits.Count == 0;
            EmptyText = hits.Count == 0 ? Loc.T("costumes.noHits", q) : "";

            foreach (var s in hits) Sets.Add(new SetCardViewModel(s));
        }

        Raise(nameof(RingHeader));
        Raise(nameof(CountLabel));
        Raise(nameof(EmptyText));
        Raise(nameof(ShowEmpty));
    }

    public void RelabelAfterLanguageChange()
    {
        RebuildSets();
        Selected = null;
    }
}

/// Hilfsfunktionen fuer die Effektzeilen (EXP wird gruen hervorgehoben).
internal static class Eff
{
    public static bool IsExp(string t) => Regex.IsMatch(t, @"EXP\s*\+", RegexOptions.IgnoreCase);

    public static List<EffectLine> Lines(IEnumerable<string>? src) =>
        (src ?? []).Select(t => new EffectLine(t, IsExp(t))).ToList();
}

public sealed record EffectLine(string Text, bool IsExp);

public sealed record EffectTier(string Title, bool Highlight, IReadOnlyList<EffectLine> Lines);

/// Ein Push-Ring in der oberen Leiste.
public sealed class RingItem(PushRing ring) : ViewModelBase
{
    private bool _isActive;

    public PushRing Ring { get; } = ring;
    public string Name => Ring.Name;
    public Bitmap? Icon => CostumeCatalog.Image(Ring.Icon);
    public int Count => Ring.Sets.Count;
    public bool HasSets => Ring.Sets.Count > 0;
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
}

/// Eine Set-Kachel im Raster.
public sealed class SetCardViewModel(CostumeSet set)
{
    public CostumeSet Set { get; } = set;
    public string Name => Set.Name;
    public Bitmap? Icon => CostumeCatalog.Image(FirstImage());
    public IReadOnlyList<EffectLine> Effect4 =>
        Eff.Lines(Set.Effects.TryGetValue("4", out var l) ? l : null);

    /// Vorschaubild: erstes Kostuem-Bild, sonst erstes Reittier/Pet.
    private string FirstImage()
    {
        var img = Set.Costumes.SelectMany(c => c.Images).FirstOrDefault()?.File;
        if (!string.IsNullOrEmpty(img)) return img;
        return Set.Mounts.Concat(Set.Pets).Concat(Set.Weapons).FirstOrDefault()?.File ?? "";
    }
}

/// Die Detailansicht eines Sets.
public sealed class SetDetailViewModel
{
    public SetDetailViewModel(PushRing ring, CostumeSet set)
    {
        RingName = ring.Name;
        Name = set.Name;
        Icon = new SetCardViewModel(set).Icon;

        Tiers = new[] { "2", "3", "4" }
            .Select(n => new EffectTier(
                Loc.T("costumes.teile", n),
                n == "4",
                Eff.Lines(set.Effects.TryGetValue(n, out var l) ? l : null)))
            .Where(t => t.Lines.Count > 0)
            .ToList();

        Costumes = set.Costumes
            .Select(c => new CostumeGroupViewModel(c))
            .ToList();

        var slots = new List<SlotViewModel>();
        slots.AddRange(set.Mounts.Select(m => new SlotViewModel(Loc.T("costumes.slot.mount"), m)));
        slots.AddRange(set.Pets.Select(p => new SlotViewModel(Loc.T("costumes.slot.pet"), p)));
        slots.AddRange(set.Weapons.Select(w => new SlotViewModel(Loc.T("costumes.slot.weapon"), w)));
        Slots = slots;
        HasSlots = slots.Count > 0;
        HasCostumes = Costumes.Count > 0;
    }

    public string RingName { get; }
    public string Name { get; }
    public Bitmap? Icon { get; }
    public IReadOnlyList<EffectTier> Tiers { get; }
    public IReadOnlyList<CostumeGroupViewModel> Costumes { get; }
    public IReadOnlyList<SlotViewModel> Slots { get; }
    public bool HasSlots { get; }
    public bool HasCostumes { get; }
}

/// Eine Kostuem-Variante (Tier) in der Detailansicht.
public sealed class CostumeGroupViewModel
{
    public CostumeGroupViewModel(CostumeGroup group)
    {
        Name = group.Name;
        Images = group.Images.Select(i => new SetImageViewModel(i)).ToList();
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
        Stats = Eff.Lines(v.Stats);
    }

    public string TypeLabel { get; }
    public string Name { get; }
    public Bitmap? Icon { get; }
    public IReadOnlyList<EffectLine> Stats { get; }
}
