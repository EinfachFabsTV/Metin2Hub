using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;

namespace M2Hub.Desktop.Services;

/// Ankuendigungsbilder werden einmal geladen, neben den Daten abgelegt und
/// danach aus dem Speicher bedient.
public sealed class ImageCache
{
    private readonly HttpClient _http;
    private readonly string _dir;
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _inFlight = new();

    public ImageCache()
    {
        _dir = Path.Combine(LocalStore.Directory, "images");
        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("M2Hub Desktop (+https://m2hub.orfabs.de)");
    }

    public Task<Bitmap?> GetAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Task.FromResult<Bitmap?>(null);
        return _inFlight.GetOrAdd(url!, LoadAsync);
    }

    private async Task<Bitmap?> LoadAsync(string url)
    {
        var file = Path.Combine(_dir, Hash(url));
        try
        {
            if (File.Exists(file)) return new Bitmap(file);
        }
        catch
        {
            // Beschaedigte Datei - neu laden.
        }

        byte[] bytes;
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            bytes = await _http.GetByteArrayAsync(uri).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
        if (bytes.Length == 0) return null;

        try
        {
            Directory.CreateDirectory(_dir);
            await File.WriteAllBytesAsync(file, bytes).ConfigureAwait(false);
        }
        catch
        {
            // Ohne Plattencache geht es auch.
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant() + ".img";
}
