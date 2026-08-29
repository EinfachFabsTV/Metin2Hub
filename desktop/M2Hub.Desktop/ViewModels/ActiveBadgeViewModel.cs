using System.Windows.Input;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Ein Hinweis in der Kopfzeile auf etwas, das gerade laeuft - ein globales
/// Event oder eine Happy Hour. Ein Klick fuehrt zur passenden Seite.
public sealed class ActiveBadgeViewModel
{
    public ActiveBadgeViewModel(
        string title,
        DateTime? endsAt,
        bool happyHour,
        ICommand open,
        string? note = null,
        Bitmap? icon = null)
    {
        Title = title;
        IsHappyHour = happyHour;
        OpenCommand = open;
        Target = happyHour ? "itemshop" : "events";
        Icon = icon;

        // Bei Serverkalendern steht hier das Zeitfenster ("Jetzt (16-20)"),
        // sonst die Restlaufzeit.
        Remaining = note ?? Format(endsAt);
        HasRemaining = Remaining.Length > 0;
    }

    public Bitmap? Icon { get; }
    public bool HasIcon => Icon is not null;

    public string Title { get; }
    public string Remaining { get; }
    public bool HasRemaining { get; }
    public bool IsHappyHour { get; }
    public bool IsEvent => !IsHappyHour;
    public string Target { get; }
    public ICommand OpenCommand { get; }

    /// Restlaufzeit, solange sie ueberschaubar ist - bei mehreren Tagen sagt
    /// die Zahl nichts mehr, dann steht das Enddatum da.
    private static string Format(DateTime? endsAt)
    {
        if (endsAt is not { } end) return "";

        var left = end.ToLocalTime() - DateTime.Now;
        if (left <= TimeSpan.Zero) return "";
        if (left.TotalHours < 1) return $"noch {(int)left.TotalMinutes} min";
        if (left.TotalHours < 24) return $"noch {(int)left.TotalHours} h";
        return Loc.T("events.until", $"{end.ToLocalTime():dd.MM.}");
    }
}
