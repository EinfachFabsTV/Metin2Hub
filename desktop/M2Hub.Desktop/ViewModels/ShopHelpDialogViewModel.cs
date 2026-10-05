using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using M2Hub.Desktop;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// „Wie sehe ich meinen Shop-Wert?" - die vier Schritte als Bilder.
///
/// Die Bilder liegen als `Assets/Help/shop1..4.png` bei und sind **selbst
/// gezeichnet** (`desktop/tools/help-images.py`), keine Bildschirmfotos: die
/// echten Seiten sind voller Zahlen, die mit dem Weg nichts zu tun haben.
///
/// Nur lesen und wegklicken - hier wird nichts eingestellt.
public sealed class ShopHelpDialogViewModel : DialogViewModelBase
{
    /// Die Seite, die der erste Schritt meint. Sie steht hier und im ersten
    /// Schritttext - der Knopf spart das Abtippen.
    public const string Market = "https://metin2alerts.com/store/";

    public ShopHelpDialogViewModel() : base(Loc.T("goals.shop.help.title"))
    {
        OpenMarketCommand = new RelayCommand(_ => Platform.OpenUrl(Market));

        for (var i = 1; i <= 4; i++)
        {
            var image = Load(i);
            if (image is null) continue;

            Steps.Add(new ShopHelpStep(image, Loc.T("goals.shop.help.step" + i)));
        }
    }

    public ObservableCollection<ShopHelpStep> Steps { get; } = new();

    /// Oeffnet die Markt-Seite im Browser.
    public RelayCommand OpenMarketCommand { get; }

    /// Die Adresse zum Mitlesen - wer den Knopf nicht nutzen will oder die App
    /// auf einem anderen Rechner hat, tippt sie ab.
    public string MarketUrl => Market;

    /// Faellt ein Bild aus, bleibt der Schritt weg - eine Hilfe ist nichts,
    /// wofuer die App stehenbleiben darf.
    private static Bitmap? Load(int step)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://M2Hub/Assets/Help/shop{step}.png"));
            return new Bitmap(stream);
        }
        catch (FileNotFoundException) { return null; }
        catch (IOException) { return null; }
    }
}

/// Ein Schritt: Bild und die Zeile darunter.
public sealed class ShopHelpStep(Bitmap image, string text)
{
    public Bitmap Image { get; } = image;
    public string Text { get; } = text;
}
