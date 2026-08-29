using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace M2Hub.Desktop.Services;

/// Markup-Erweiterung fuer Oberflaechentexte:
///
///     Text="{loc:T events.subtitle}"
///
/// Sie bindet an den Indexer von Loc statt den Text einzusetzen - deshalb
/// wechselt die Sprache im laufenden Programm, ohne Neustart.
public sealed class TExtension : MarkupExtension
{
    public TExtension(string key) => Key = key;

    public string Key { get; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]")
        {
            Source = Loc.I,
            Mode = BindingMode.OneWay,
        };
}
