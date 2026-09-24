using VeriCat.Core.Appearance;

namespace VeriCat.Core.Configuration;

/// <summary>Bir kedinin kalıcı ayarları.</summary>
public sealed class CatConfig
{
    public const int MaxNameLength = 16;

    public string Name { get; set; } = "Kedi";
    public CoatSpec Spec { get; set; } = CoatSpec.Presets[0].Spec;
    public Markings Markings { get; set; }
    public uint Collar { get; set; } = CoatSpec.CollarPresets[0];
    public Accessory Accessory { get; set; }
    public Personality Personality { get; set; } = new();
    public double Scale { get; set; } = 1;
    public double Speed { get; set; } = 1;
    public double Pitch { get; set; } = 640;

    /// <summary>i. hazır kedi; renkler ve tasma sırayla döner, karakteri biraz rastgele.</summary>
    public static CatConfig Preset(int i, double scale, Random random)
    {
        var p = CoatSpec.Presets[i % CoatSpec.Presets.Count];
        return new CatConfig
        {
            Name = p.Name,
            Spec = p.Spec,
            Collar = CoatSpec.CollarPresets[i % CoatSpec.CollarPresets.Count],
            Personality = Personality.Random(random, spread: 0.3),
            Scale = scale,
            Pitch = 560 + random.NextDouble() * 160,
        };
    }

    /// <summary>Tamamen rastgele bir kedi: isim, renk, desen, tasma, aksesuar ve karakter.</summary>
    public static CatConfig Random(Random random, double scale)
    {
        uint fur = FurPalette[random.Next(FurPalette.Length)];
        uint eye = CoatSpec.EyePresets[random.Next(CoatSpec.EyePresets.Count)];
        var markings = Markings.None;
        if (random.NextDouble() < 0.35) markings |= Markings.Socks;
        if (random.NextDouble() < 0.35) markings |= Markings.Bib;
        if (random.NextDouble() < 0.2) markings |= Markings.Spots;
        return new CatConfig
        {
            Name = Names[random.Next(Names.Length)],
            Spec = CoatSpec.Derived(fur, eye, striped: random.NextDouble() < 0.4),
            Markings = markings,
            Collar = CoatSpec.CollarPresets[random.Next(CoatSpec.CollarPresets.Count)],
            Accessory = random.NextDouble() < 0.5 ? Accessory.None : (Accessory)random.Next(1, Enum.GetValues<Accessory>().Length),
            Personality = Personality.Random(random, spread: 0.5),
            Scale = scale,
            Speed = 0.8 + random.NextDouble() * 0.5,
            Pitch = 480 + random.NextDouble() * 380,
        };
    }

    /// <summary>Kullanıcı girdisini tasmaya sığacak bir isme çevirir; boşsa null.</summary>
    public static string? SanitizeName(string? input)
    {
        var v = new string((input ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (v.Length == 0) return null;
        return v.Length > MaxNameLength ? v[..MaxNameLength] : v;
    }

    public CatConfig Clone() => new()
    {
        Name = Name, Spec = Spec, Markings = Markings, Collar = Collar, Accessory = Accessory,
        Personality = Personality with { }, Scale = Scale, Speed = Speed, Pitch = Pitch,
    };

    static readonly string[] Names =
    {
        "Minnoş", "Boncuk", "Pamuk", "Duman", "Tarçın", "Fındık", "Zeytin", "Karamel", "Mırmır", "Paşa",
        "Şeker", "Limon", "Bulut", "Gece", "Maviş", "Cimcime", "Pofuduk", "Leblebi", "Kömür", "Latte",
    };

    static readonly uint[] FurPalette =
    {
        0xF6B26B, 0xB3AAA2, 0x3A3540, 0xFFFDF9, 0x8D99AB, 0xE9CFA8, 0xC47A45, 0x6B5B53, 0xD9C3A5, 0xA0A7B4, 0x2B2B2B, 0xF3D9B1,
    };
}

/// <summary>Kürk desenleri (birleştirilebilir).</summary>
[Flags]
public enum Markings
{
    None = 0,
    /// <summary>Beyaz patiler (çorap).</summary>
    Socks = 1,
    /// <summary>Beyaz göğüs ve ağız çevresi.</summary>
    Bib = 2,
    /// <summary>Koyu benekler.</summary>
    Spots = 4,
}

/// <summary>Kafaya ya da tasmaya takılan süs.</summary>
public enum Accessory
{
    None,
    Bell,
    Bow,
    Crown,
    Flower,
    Glasses,
}

/// <summary>Davranışı etkileyen karakter; her değer 0...1, 0.5 "ortalama kedi".</summary>
public sealed record Personality
{
    /// <summary>Fareyle oynama, kovalama, pusu sıklığı.</summary>
    public double Playfulness { get; init; } = 0.5;

    /// <summary>Kavga etme ve bıkınca pati atma eğilimi.</summary>
    public double Temper { get; init; } = 0.5;

    /// <summary>Okşanmayı sevme, diğer kedilerle dostluk.</summary>
    public double Affection { get; init; } = 0.5;

    /// <summary>Hareketlilik; düşükse daha çok uyur.</summary>
    public double Energy { get; init; } = 0.5;

    public static Personality Random(Random random, double spread)
    {
        double Pick() => Math.Clamp(0.5 + (random.NextDouble() * 2 - 1) * spread, 0, 1);
        return new Personality { Playfulness = Pick(), Temper = Pick(), Affection = Pick(), Energy = Pick() };
    }

    public Personality Clamped() => new()
    {
        Playfulness = Math.Clamp(Playfulness, 0, 1), Temper = Math.Clamp(Temper, 0, 1),
        Affection = Math.Clamp(Affection, 0, 1), Energy = Math.Clamp(Energy, 0, 1),
    };
}
