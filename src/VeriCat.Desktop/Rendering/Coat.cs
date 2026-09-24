using VeriCat.Core.Appearance;
using VeriCat.Core.Configuration;

namespace VeriCat.Desktop.Rendering;

/// <summary>Çizimde kullanılan renkler ve süsler (kürk + desen + tasma + aksesuar).</summary>
internal sealed class Coat
{
    public Color Fur, Shade, Belly, Eye, Line, Collar, Spot;
    public Color? Stripe;
    public Markings Markings;
    public Accessory Accessory;

    public Color Whisker => ColorMath.Brightness(Hex.From(Fur)) < 0.35 ? Color.FromArgb(128, 255, 255, 255) : Color.FromArgb(128, Line);

    public bool Has(Markings m) => (Markings & m) != 0;

    public static Coat From(CatConfig c) => From(c.Spec, c.Collar, c.Markings, c.Accessory);

    public static Coat From(CoatSpec spec, uint collar, Markings markings = Markings.None, Accessory accessory = Accessory.None) => new()
    {
        Fur = Hex.ToColor(spec.Fur), Shade = Hex.ToColor(spec.Shade),
        Belly = (markings & Markings.Bib) != 0 ? Hex.ToColor(0xFFFCF7) : Hex.ToColor(spec.Belly),
        Eye = Hex.ToColor(spec.Eye), Line = Hex.ToColor(spec.Line), Collar = Hex.ToColor(collar),
        Spot = Hex.ToColor(ColorMath.Mix(spec.Fur, spec.Line, 0.55)),
        Stripe = spec.Stripe is uint s ? Hex.ToColor(s) : null,
        Markings = markings, Accessory = accessory,
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
