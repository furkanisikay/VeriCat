using VeriCat.Core.Appearance;

namespace VeriCat.Core.Configuration;

/// <summary>Bir kedinin kalıcı ayarları.</summary>
public sealed class CatConfig
{
    public const int MaxNameLength = 16;

    public string Name { get; set; } = "Kedi";
    public CoatSpec Spec { get; set; } = CoatSpec.Presets[0].Spec;
    public uint Collar { get; set; } = CoatSpec.CollarPresets[0];
    public double Scale { get; set; } = 1;
    public double Speed { get; set; } = 1;
    public double Pitch { get; set; } = 640;

    /// <summary>i. hazır kedi; renkler ve tasma sırayla döner.</summary>
    public static CatConfig Preset(int i, double scale, Random random)
    {
        var p = CoatSpec.Presets[i % CoatSpec.Presets.Count];
        return new CatConfig
        {
            Name = p.Name,
            Spec = p.Spec,
            Collar = CoatSpec.CollarPresets[i % CoatSpec.CollarPresets.Count],
            Scale = scale,
            Pitch = 560 + random.NextDouble() * 160,
        };
    }

    /// <summary>Kullanıcı girdisini tasmaya sığacak bir isme çevirir; boşsa null.</summary>
    public static string? SanitizeName(string? input)
    {
        var v = new string((input ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (v.Length == 0) return null;
        return v.Length > MaxNameLength ? v[..MaxNameLength] : v;
    }
}
