namespace VeriCat.Core.Appearance;

/// <summary>Kalıcı olarak saklanan renk takımı (0xRRGGBB).</summary>
public sealed record CoatSpec
{
    public uint Fur { get; init; }
    public uint Shade { get; init; }
    public uint Belly { get; init; }
    public uint Eye { get; init; }
    public uint Line { get; init; }
    public uint? Stripe { get; init; }

    /// <summary>Tek bir kürk renginden gölge, karın, kenar ve çizgi tonlarını türetir.</summary>
    public static CoatSpec Derived(uint fur, uint eye, bool striped)
    {
        bool dark = ColorMath.Brightness(fur) < 0.35;
        return new CoatSpec
        {
            Fur = fur, Shade = ColorMath.Mix(fur, 0x000000, 0.12), Belly = ColorMath.Mix(fur, 0xFFFFFF, dark ? 0.12 : 0.72),
            Eye = eye, Line = ColorMath.Mix(fur, 0x1A1410, dark ? 0.7 : 0.55),
            Stripe = striped ? ColorMath.Mix(fur, 0x000000, 0.2) : null,
        };
    }

    public static readonly IReadOnlyList<(string Name, CoatSpec Spec)> Presets = new[]
    {
        ("Sarman", new CoatSpec { Fur = 0xF6B26B, Shade = 0xE0964F, Belly = 0xFFF1DF, Eye = 0x7FBF4D, Line = 0x8E5427, Stripe = 0xE08A3C }),
        ("Tekir", new CoatSpec { Fur = 0xB3AAA2, Shade = 0x958B82, Belly = 0xF1ECE6, Eye = 0xE9B949, Line = 0x5A524C, Stripe = 0x7A7068 }),
        ("Zeytin", new CoatSpec { Fur = 0x3A3540, Shade = 0x2A262F, Belly = 0x4C4653, Eye = 0xF5CB4B, Line = 0x16131A }),
        ("Pamuk", new CoatSpec { Fur = 0xFFFDF9, Shade = 0xECE4DA, Belly = 0xFFFFFF, Eye = 0x6FB6F0, Line = 0x9C9088 }),
        ("Duman", Derived(0x8D99AB, 0xF2C14E, false)),
        ("Karamel", Derived(0xE9CFA8, 0x5AA7E8, true)),
    };

    /// <summary>Özelleştirmede hızlı seçim için göz renkleri.</summary>
    public static readonly IReadOnlyList<uint> EyePresets = new uint[]
    {
        0x7FBF4D, 0xE9B949, 0xF5CB4B, 0x6FB6F0, 0x5AA7E8, 0xC78A3B, 0x8ED1A5,
    };

    /// <summary>Tasma renkleri; hazır kediler sırayla bunları alır.</summary>
    public static readonly IReadOnlyList<uint> CollarPresets = new uint[]
    {
        0xE0413A, 0x2F80ED, 0x27AE60, 0x9B51E0, 0xF2994A, 0xEB5A9C,
    };
}
