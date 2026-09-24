namespace VeriCat.Core.Behavior;

public enum Need { Hunger, Love, Fun, Energy }

/// <summary>
/// Kedinin ihtiyaçları; her biri 0 (çok ihtiyacı var) … 1 (tamamen doygun). Zamanla azalır, bakımla dolar.
/// Nazik tasarım: hiçbir şey kediye zarar vermez, sadece davranışını ve ruh hâlini değiştirir. Uygulama kapalıyken
/// geçen zaman da yavaşça işler ama kedi o yüzden hiçbir zaman "perişan" bir hâlde karşılamaz.
/// </summary>
public sealed class Vitals
{
    // Tamamen doludan boşa ne kadar sürede iner (saniye).
    internal const double HungerSpan = 4 * 3600, LoveSpan = 3 * 3600, FunSpan = 2 * 3600, AwakeSpan = 4 * 3600;
    /// <summary>Uyurken enerjinin boştan dolmaya süresi.</summary>
    internal const double RestSpan = 20 * 60;
    /// <summary>Bu değerin altındaki ihtiyaç "acil" sayılır (kedi bunu belli eder).</summary>
    public const double Low = 0.3;
    /// <summary>Uygulama kapalıyken geçen zaman bu değerin altına indiremez.</summary>
    internal const double OfflineFloor = 0.4;

    public double Hunger { get; set; } = 0.8;
    public double Love { get; set; } = 0.8;
    public double Fun { get; set; } = 0.8;
    public double Energy { get; set; } = 0.9;

    /// <summary>En son ne zaman kaydedildi (kapalıyken geçen süreyi hesaplamak için).</summary>
    public DateTimeOffset? SavedAt { get; set; }

    /// <summary>Genel ruh hâli, 0…1.</summary>
    public double Mood => (Hunger + Love + Fun + Energy) / 4;

    public double this[Need n] => n switch
    {
        Need.Hunger => Hunger, Need.Love => Love, Need.Fun => Fun, _ => Energy,
    };

    public void Decay(double seconds, bool sleeping)
    {
        Hunger = Clamp(Hunger - seconds / HungerSpan);
        Love = Clamp(Love - seconds / LoveSpan);
        Fun = Clamp(Fun - seconds / FunSpan);
        Energy = Clamp(sleeping ? Energy + seconds / RestSpan : Energy - seconds / AwakeSpan);
    }

    public void Feed(double amount) => Hunger = Clamp(Hunger + amount);
    public void Cuddle(double amount) => Love = Clamp(Love + amount);
    public void Play(double amount) => Fun = Clamp(Fun + amount);

    /// <summary>En acil ihtiyaç (eşiğin altındaysa).</summary>
    public Need? MostUrgent()
    {
        Need? best = null;
        double lowest = Low;
        foreach (var n in Enum.GetValues<Need>())
            if (this[n] < lowest) { lowest = this[n]; best = n; }
        return best;
    }

    /// <summary>
    /// Uygulama kapalıyken geçen zamanı uygular: üçte bir hızla, en fazla 12 saat ve hiçbir ihtiyaç
    /// <see cref="OfflineFloor"/>'un altına inmeden (zaten altındaysa olduğu gibi kalır).
    /// </summary>
    public void CatchUp(DateTimeOffset now)
    {
        if (SavedAt is not DateTimeOffset saved || now <= saved) return;
        double seconds = Math.Min((now - saved).TotalSeconds, 12 * 3600) / 3;
        double h = Hunger, l = Love, f = Fun;
        Decay(seconds, sleeping: true);   // kapalıyken dinlenmiş sayılır
        Hunger = Math.Max(Hunger, Math.Min(h, OfflineFloor));
        Love = Math.Max(Love, Math.Min(l, OfflineFloor));
        Fun = Math.Max(Fun, Math.Min(f, OfflineFloor));
        SavedAt = now;
    }

    public Vitals Clone() => new() { Hunger = Hunger, Love = Love, Fun = Fun, Energy = Energy, SavedAt = SavedAt };

    public Vitals Clamped() => new()
    {
        Hunger = Clamp(Hunger), Love = Clamp(Love), Fun = Clamp(Fun), Energy = Clamp(Energy), SavedAt = SavedAt,
    };

    static double Clamp(double v) => double.IsFinite(v) ? Math.Clamp(v, 0, 1) : 0.5;
}
