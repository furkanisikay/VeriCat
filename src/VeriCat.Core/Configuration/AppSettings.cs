namespace VeriCat.Core.Configuration;

/// <summary>Uygulama genelindeki ayarlar; settings.json olarak saklanır.</summary>
public sealed class AppSettings
{
    /// <summary>Yeni kedilerin boyu. 0.6 küçük, 1.0 orta, 1.5 büyük, 2.2 dev.</summary>
    public double Scale { get; set; } = 1;

    /// <summary>Pencerelerin üst kenarında yürür ve aralarında zıplar.</summary>
    public bool Windows { get; set; } = true;

    /// <summary>Ara ara pencerelerin içindeki bölümlerin (araç çubuğu, panel…) üstüne de zıplar.</summary>
    public bool InnerWindows { get; set; } = true;

    /// <summary>Fare imlecini kovalar.</summary>
    public bool Chase { get; set; } = true;

    /// <summary>İmlece yumruk attığında imleç biraz itilir.</summary>
    public bool PunchCursor { get; set; } = true;

    /// <summary>Tasmadaki isim etiketi görünsün.</summary>
    public bool ShowNames { get; set; } = true;

    public bool Sound { get; set; } = true;

    /// <summary>Tam ekran bir uygulama (oyun, video, sunum) öndeyken kediler saklanır ve simülasyon durur.</summary>
    public bool HideInFullscreen { get; set; } = true;

    public UpdateMode Updates { get; set; } = UpdateMode.Notify;

    /// <summary>"Bu sürümü atla" denen sürüm; bu sürüm için tekrar bildirim yapılmaz.</summary>
    public string? SkippedVersion { get; set; }

    /// <summary>En son çalışan sürüm. Değiştiyse "yenilikler" gösterilir.</summary>
    public string? LastSeenVersion { get; set; }

    public List<CatConfig> Cats { get; set; } = new();
}

public enum UpdateMode
{
    /// <summary>Arka planda indirir, kurar ve yeniden başlar.</summary>
    Automatic,
    /// <summary>Yeni sürümü bildirir; kullanıcı yenilikleri görüp onaylayınca kurar.</summary>
    Notify,
    /// <summary>Kendiliğinden denetlemez (elle denetleme yine çalışır).</summary>
    Off,
}
