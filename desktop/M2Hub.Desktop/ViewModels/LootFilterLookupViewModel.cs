using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.LootFilter;

namespace M2Hub.Desktop.ViewModels;

/// Nachschlagen: Item-Namen tippen und sehen, in welche Kategorie es faellt;
/// darunter den Kategoriebaum durchblaettern.
public sealed class LootFilterLookupViewModel : ViewModelBase
{
    private const int MaxResults = 60;

    private readonly LocalStore _store;
    private readonly Action _editorRebuild;

    private string _query = "";
    private LootCategory? _top;
    private LootCategory? _sub;

    public LootFilterLookupViewModel(LocalStore store, Action editorRebuild)
    {
        _store = store;
        _editorRebuild = editorRebuild;
        PickTopCommand = new RelayCommand(p => { if (p is CatChip c) PickTop(c); });
        PickSubCommand = new RelayCommand(p => { if (p is CatChip c) PickSub(c); });
        ActivateAllCommand = new RelayCommand(_ => ActivateAll());
        BuildTopCats();
        BuildActive();
    }

    /// Nachdem der Filter geaendert wurde: Editor neu einlesen und die
    /// Aktiv-Uebersicht nachfuehren.
    public void AfterFilterChanged()
    {
        _editorRebuild();
        BuildActive();
    }

    /* ---------- Aktiv-Uebersicht ---------- */

    public ObservableCollection<ActiveEntry> Active { get; } = new();
    public RelayCommand ActivateAllCommand { get; }

    /// Ist alles an (Vorgabe)? Dann ist die lange Liste unnoetig.
    public bool AllActive { get; private set; } = true;
    public bool NotAllActive => !AllActive;
    public bool HasActive => Active.Count > 0;
    public bool ShowActiveList => NotAllActive && HasActive;
    public bool ShowNoneActive => NotAllActive && !HasActive;

    private void BuildActive()
    {
        Active.Clear();
        var data = _store.Settings.LootFilter.Categories;
        var allOn = true;

        foreach (var cat in LootFilterCatalog.Categories)
        {
            data.TryGetValue(cat.Key, out var st);
            var on = st?.On ?? true;

            if (cat.Subs.Count > 0)
            {
                if (!on) allOn = false;
                foreach (var sub in cat.Subs)
                {
                    var chk = st is null || !st.Subs.TryGetValue(sub.Name, out var v) || v;
                    if (on && chk)
                        Active.Add(new ActiveEntry($"{cat.Name} › {sub.Name}", cat.Key, sub.Name, _store, AfterFilterChanged));
                    else
                        allOn = false;
                }
            }
            else
            {
                if (on) Active.Add(new ActiveEntry(cat.Name, cat.Key, null, _store, AfterFilterChanged));
                else allOn = false;
            }
        }

        AllActive = allOn;
        Raise(nameof(AllActive));
        Raise(nameof(NotAllActive));
        Raise(nameof(HasActive));
        Raise(nameof(ShowActiveList));
        Raise(nameof(ShowNoneActive));
    }

    private void ActivateAll()
    {
        // Leeren = zurueck auf die Vorgabe „alles an".
        _store.Settings.LootFilter.Categories.Clear();
        _store.SaveSettings();
        AfterFilterChanged();
    }

    /* ---------- Suche ---------- */

    public string Query
    {
        get => _query;
        set
        {
            if (!Set(ref _query, value)) return;
            Search();
            Raise(nameof(HasQuery));
        }
    }

    public bool HasQuery => !string.IsNullOrWhiteSpace(_query);

    public ObservableCollection<LookupResultViewModel> Results { get; } = new();
    public bool HasResults => Results.Count > 0;

    /// Leeres Ergebnis trotz Suche: nichts gefunden (Daten wachsen noch).
    public bool NoResults => HasQuery && Results.Count == 0;

    private void Search()
    {
        Results.Clear();
        var q = _query.Trim();
        if (q.Length > 0)
        {
            foreach (var hit in LootFilterCatalog.AllItems)
            {
                if (hit.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                    Results.Add(ToResult(hit));
                if (Results.Count >= MaxResults) break;
            }
        }
        Raise(nameof(HasResults));
        Raise(nameof(NoResults));
    }

    private LookupResultViewModel ToResult(LootHit hit)
    {
        var crumb = string.Join(" › ", hit.Path);
        var top = hit.Path.Length > 0 ? hit.Path[0] : "";
        var hint = hit.Path.Length > 1
            ? Loc.T("lootfilter.hintSub", top, hit.Path[1])
            : Loc.T("lootfilter.hintTop", top);
        var subName = hit.Path.Length > 1 ? hit.Path[1] : null;
        return new LookupResultViewModel(
            hit.Name, LootFilterCatalog.Image(hit.File), crumb, hint,
            hit.TopKey, subName, _store, AfterFilterChanged);
    }

    /* ---------- Blaettern ---------- */

    public ObservableCollection<CatChip> TopCats { get; } = new();
    public ObservableCollection<CatChip> SubCats { get; } = new();
    public ObservableCollection<LookupTile> Items { get; } = new();

    public bool HasSubCats => SubCats.Count > 0;
    public bool HasItems => Items.Count > 0;

    /// Hinweis, wenn eine gewaehlte Kategorie noch keine Items hat.
    public string? BrowseHint { get; private set; }
    public bool HasBrowseHint => !string.IsNullOrEmpty(BrowseHint);

    public RelayCommand PickTopCommand { get; }
    public RelayCommand PickSubCommand { get; }

    private void BuildTopCats()
    {
        TopCats.Clear();
        foreach (var c in LootFilterCatalog.Categories)
            TopCats.Add(new CatChip(c, CountItems(c)));
    }

    private void PickTop(CatChip chip)
    {
        _top = chip.Node;
        _sub = null;
        foreach (var t in TopCats) t.IsActive = ReferenceEquals(t.Node, _top);
        SubCats.Clear();
        Items.Clear();
        foreach (var s in _top.Subs) SubCats.Add(new CatChip(s, CountItems(s)));
        SetBrowseHint(SubCats.Count == 0 && CountItems(_top) == 0
            ? Loc.T("lootfilter.emptyCat", _top.Name) : null);
        Raise(nameof(HasSubCats));
        Raise(nameof(HasItems));
    }

    private void PickSub(CatChip chip)
    {
        _sub = chip.Node;
        foreach (var s in SubCats) s.IsActive = ReferenceEquals(s.Node, _sub);
        Items.Clear();
        foreach (var hit in FlattenNode(_sub))
            Items.Add(new LookupTile(hit.Name, LootFilterCatalog.Image(hit.File)));
        SetBrowseHint(Items.Count == 0 ? Loc.T("lootfilter.emptyCat", _sub.Name) : null);
        Raise(nameof(HasItems));
    }

    private void SetBrowseHint(string? hint)
    {
        BrowseHint = hint;
        Raise(nameof(BrowseHint));
        Raise(nameof(HasBrowseHint));
    }

    private static int CountItems(LootCategory node) =>
        node.Items.Count + node.Subs.Sum(CountItems);

    private static IEnumerable<LootItem> FlattenNode(LootCategory node)
    {
        foreach (var it in node.Items) yield return it;
        foreach (var sub in node.Subs)
            foreach (var it in FlattenNode(sub)) yield return it;
    }

    public void RelabelAfterLanguageChange()
    {
        // Die Treffer-Hinweise und Leer-Texte werden beim naechsten Tippen/Klick
        // neu gebaut; der Baum selbst traegt deutsche Namen aus den Ordnern.
        Search();
    }
}

/// Ein Suchtreffer in der Oberflaeche - zugleich setzbar: `Active` spiegelt und
/// schaltet, ob diese Kategorie/Unterkategorie im Filter-Editor an ist.
public sealed class LookupResultViewModel : ViewModelBase
{
    private readonly string _topKey;
    private readonly string? _subName;
    private readonly LocalStore _store;
    private readonly Action _onChanged;

    public LookupResultViewModel(string name, Bitmap? image, string crumb, string hint,
        string topKey, string? subName, LocalStore store, Action onChanged)
    {
        Name = name;
        Image = image;
        Crumb = crumb;
        Hint = hint;
        _topKey = topKey;
        _subName = subName;
        _store = store;
        _onChanged = onChanged;
        SoloCommand = new RelayCommand(_ => Solo());
    }

    public string Name { get; }
    public Bitmap? Image { get; }
    public string Crumb { get; }
    public string Hint { get; }

    /// „Nur dieses": den Filter auf diese Kategorie/Unterkategorie stellen und
    /// alles andere ausschalten.
    public RelayCommand SoloCommand { get; }

    /// Ist diese Kategorie (und, falls vorhanden, die Unterkategorie) im Filter
    /// aktiv? Fehlt ein gespeicherter Stand, gilt die Vorgabe „alles an".
    public bool Active
    {
        get
        {
            if (!_store.Settings.LootFilter.Categories.TryGetValue(_topKey, out var state)) return true;
            if (!state.On) return false;
            return _subName is null || !state.Subs.TryGetValue(_subName, out var v) || v;
        }
        set
        {
            var cats = _store.Settings.LootFilter.Categories;
            if (!cats.TryGetValue(_topKey, out var state)) { state = new LootCatState(); cats[_topKey] = state; }

            if (value)
            {
                state.On = true;
                if (_subName is not null) state.Subs[_subName] = true;
            }
            else if (_subName is not null)
            {
                state.Subs[_subName] = false;
            }
            else
            {
                state.On = false;
            }

            _store.SaveSettings();
            Raise(nameof(Active));
            _onChanged();
        }
    }

    private void Solo()
    {
        var cats = _store.Settings.LootFilter.Categories;
        foreach (var c in LootFilterCatalog.Categories)
        {
            if (!cats.TryGetValue(c.Key, out var st)) { st = new LootCatState(); cats[c.Key] = st; }
            if (c.Key == _topKey)
            {
                st.On = true;
                if (_subName is not null)
                    foreach (var s in c.Subs) st.Subs[s.Name] = s.Name == _subName;
            }
            else
            {
                st.On = false;
            }
        }
        _store.SaveSettings();
        Raise(nameof(Active));
        _onChanged();
    }
}

/// Ein aktiver Posten in der Uebersicht - entfernbar.
public sealed class ActiveEntry : ViewModelBase
{
    private readonly string _topKey;
    private readonly string? _subName;
    private readonly LocalStore _store;
    private readonly Action _after;

    public ActiveEntry(string label, string topKey, string? subName, LocalStore store, Action after)
    {
        Label = label;
        _topKey = topKey;
        _subName = subName;
        _store = store;
        _after = after;
        RemoveCommand = new RelayCommand(_ => Remove());
    }

    public string Label { get; }
    public RelayCommand RemoveCommand { get; }

    private void Remove()
    {
        var cats = _store.Settings.LootFilter.Categories;
        if (!cats.TryGetValue(_topKey, out var st)) { st = new LootCatState(); cats[_topKey] = st; }
        if (_subName is not null) st.Subs[_subName] = false;
        else st.On = false;
        _store.SaveSettings();
        _after();
    }
}

/// Eine Kachel beim Blaettern.
public sealed record LookupTile(string Name, Bitmap? Image);

/// Ein Kategorie-Knopf (Ober- oder Unterkategorie) mit Anzahl-Badge.
public sealed class CatChip(LootCategory node, int count) : ViewModelBase
{
    private bool _active;

    public LootCategory Node { get; } = node;
    public string Name => Node.Name;
    public int Count { get; } = count;
    public bool HasItems => Count > 0;

    public bool IsActive
    {
        get => _active;
        set => Set(ref _active, value);
    }
}
