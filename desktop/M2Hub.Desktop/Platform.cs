using System.Diagnostics;
using System.Runtime.InteropServices;

namespace M2Hub.Desktop;

/// Ein Forum-Link soll im Standardbrowser aufgehen - die App selbst zeigt
/// bewusst kein HTML an.
public static class Platform
{
    /// Oeffnet einen Ordner im Dateimanager.
    public static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            System.IO.Directory.CreateDirectory(path);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", path);
        }
        catch
        {
            // Kein Dateimanager gefunden - dann passiert eben nichts.
        }
    }

    public static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", uri.ToString());
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", uri.ToString());
        }
        catch
        {
            // Kein Browser gefunden - dann passiert eben nichts.
        }
    }
}
