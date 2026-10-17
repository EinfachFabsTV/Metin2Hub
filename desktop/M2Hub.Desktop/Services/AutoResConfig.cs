namespace M2Hub.Desktop.Services;

/// Auto-Res: liest und schreibt die Aufloesung in der metin2.cfg des Clients.
/// Es wird **nur** WIDTH/HEIGHT geaendert; alle anderen Zeilen bleiben, wie sie
/// sind. Pro Preset liegt eine vollstaendige Config im Profil; beim Setzen
/// ersetzt sie die Ziel-cfg.
public static class AutoResConfig
{
    /// Ordner mit den Preset-Configs im Profil.
    public static string PresetDir => Path.Combine(LocalStore.Directory, "autores");

    /// Aktuelle Aufloesung aus einer cfg, oder null.
    public static (int Width, int Height)? Read(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            int? w = null, h = null;
            foreach (var line in File.ReadAllLines(path))
            {
                var key = FirstToken(line);
                if (key == "WIDTH") w = Value(line);
                else if (key == "HEIGHT") h = Value(line);
            }
            return w is { } ww && h is { } hh ? (ww, hh) : null;
        }
        catch
        {
            return null;
        }
    }

    /// Legt (oder aktualisiert) die Preset-Config an: die Ziel-cfg wird
    /// vollstaendig geladen, nur WIDTH/HEIGHT ersetzt, und als `fileName` im
    /// Profil gespeichert. Gibt true bei Erfolg.
    public static bool SaveSnapshot(string? sourcePath, int width, int height, string fileName)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return false;
        try
        {
            var lines = File.ReadAllLines(sourcePath).ToList();
            ApplyWidthHeight(lines, width, height);
            Directory.CreateDirectory(PresetDir);
            File.WriteAllLines(Path.Combine(PresetDir, fileName), lines);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// Setzt ein Preset: die gespeicherte Config ersetzt die Ziel-cfg.
    public static bool Apply(string fileName, string? targetPath)
    {
        if (string.IsNullOrEmpty(targetPath)) return false;
        try
        {
            var src = Path.Combine(PresetDir, fileName);
            if (!File.Exists(src)) return false;
            File.Copy(src, targetPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Delete(string fileName)
    {
        try
        {
            var f = Path.Combine(PresetDir, fileName);
            if (File.Exists(f)) File.Delete(f);
        }
        catch { /* nicht kritisch */ }
    }

    private static void ApplyWidthHeight(List<string> lines, int width, int height)
    {
        bool w = false, h = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var key = FirstToken(lines[i]);
            if (key == "WIDTH") { lines[i] = $"WIDTH\t{width}"; w = true; }
            else if (key == "HEIGHT") { lines[i] = $"HEIGHT\t{height}"; h = true; }
        }
        if (!w) lines.Add($"WIDTH\t{width}");
        if (!h) lines.Add($"HEIGHT\t{height}");
    }

    private static string FirstToken(string line)
    {
        var t = line.TrimStart();
        var i = 0;
        while (i < t.Length && !char.IsWhiteSpace(t[i])) i++;
        return t[..i];
    }

    private static int? Value(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[1], out var v) ? v : null;
    }
}
