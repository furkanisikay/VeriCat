using VeriCat.Core.Appearance;

namespace VeriCat.Desktop.Rendering;

/// <summary>Çizimde kullanılan renkler (CoatSpec + tasma rengi).</summary>
internal sealed class Coat
{
    public Color Fur, Shade, Belly, Eye, Line, Collar;
    public Color? Stripe;

    public Color Whisker => ColorMath.Brightness(Hex.From(Fur)) < 0.35 ? Color.FromArgb(128, 255, 255, 255) : Color.FromArgb(128, Line);

    public static Coat From(CoatSpec spec, uint collar) => new()
    {
        Fur = Hex.ToColor(spec.Fur), Shade = Hex.ToColor(spec.Shade), Belly = Hex.ToColor(spec.Belly),
        Eye = Hex.ToColor(spec.Eye), Line = Hex.ToColor(spec.Line), Collar = Hex.ToColor(collar),
        Stripe = spec.Stripe is uint s ? Hex.ToColor(s) : null,
    };
}

internal static class Hex
{
    public static Color ToColor(uint v, int a = 255) =>
        Color.FromArgb(a, (int)(v >> 16 & 0xFF), (int)(v >> 8 & 0xFF), (int)(v & 0xFF));

    public static uint From(Color c) => (uint)(c.R << 16 | c.G << 8 | c.B);
}

/// <summary>Tasmadaki isim etiketi. <see cref="Em"/>: yazı boyu, cihaz pikseli.</summary>
internal readonly record struct NameTag(string Text, float Em);
