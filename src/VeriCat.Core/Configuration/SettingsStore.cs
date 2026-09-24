using System.Text.Json;

namespace VeriCat.Core.Configuration;

/// <summary>Ayarları JSON dosyasında saklar. Eski sürümün dosyası varsa ilk açılışta oradan okur.</summary>
public sealed class SettingsStore
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    readonly string path;
    readonly string? legacyPath;

    public SettingsStore(string path, string? legacyPath = null)
    {
        this.path = path;
        this.legacyPath = legacyPath;
    }

    /// <summary>%APPDATA%\VeriCat\settings.json (eski sürüm: %APPDATA%\Kedi\settings.json).</summary>
    public static SettingsStore ForCurrentUser()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new SettingsStore(Path.Combine(root, "VeriCat", "settings.json"), Path.Combine(root, "Kedi", "settings.json"));
    }

    public AppSettings Load()
    {
        foreach (var p in new[] { path, legacyPath })
        {
            if (p == null || !File.Exists(p)) continue;
            try
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(p), Options);
                if (s != null) return Normalize(s);
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                // Bozuk ya da okunamayan dosya: varsayılanlarla devam et.
            }
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
            File.Move(tmp, path, overwrite: true);   // yarım yazılmış dosya kalmasın
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Kaydedilemezse uygulama çalışmaya devam eder.
        }
    }

    static AppSettings Normalize(AppSettings s)
    {
        s.Cats ??= new();
        s.Cats.RemoveAll(c => c == null);
        foreach (var c in s.Cats)
        {
            c.Name = CatConfig.SanitizeName(c.Name) ?? "Kedi";
            c.Spec ??= Appearance.CoatSpec.Presets[0].Spec;
            c.Scale = Math.Clamp(c.Scale, 0.3, 3);
            c.Speed = Math.Clamp(c.Speed, 0.2, 3);
        }
        return s;
    }
}
