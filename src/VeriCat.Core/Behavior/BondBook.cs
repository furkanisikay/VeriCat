namespace VeriCat.Core.Behavior;

/// <summary>
/// Kediler arası ilişki hafızası: her çift için -1 (can düşmanı) … +1 (can dostu). Kavga düşürür, selamlaşma ve
/// yan yana uyumak yükseltir. Arkadaşlar daha az kavga eder ve birbirine sokulup uyur; rakipler karşılaşınca tıslar.
/// Değerler ayarlarla birlikte saklanır.
/// </summary>
public sealed class BondBook
{
    public const double Friend = 0.4, Rival = -0.4;

    readonly Dictionary<string, double> map;

    public BondBook(Dictionary<string, double>? storage = null) => map = storage ?? new();

    static string Key(Guid a, Guid b) => a.CompareTo(b) < 0 ? $"{a:N}|{b:N}" : $"{b:N}|{a:N}";

    public double Get(Cat a, Cat b) => map.TryGetValue(Key(a.Config.Id, b.Config.Id), out var v) ? v : 0;

    public void Adjust(Cat a, Cat b, double delta)
    {
        if (a == b) return;
        var k = Key(a.Config.Id, b.Config.Id);
        map[k] = Math.Clamp((map.TryGetValue(k, out var v) ? v : 0) + delta, -1, 1);
    }

    /// <summary>Kapatılan kedinin bütün ilişkilerini unutur.</summary>
    public void Forget(Guid id)
    {
        var n = id.ToString("N");
        foreach (var k in map.Keys.Where(k => k.Contains(n, StringComparison.Ordinal)).ToList()) map.Remove(k);
    }

    /// <summary>En iyi arkadaşı ve en büyük rakibi (eşiği geçenler).</summary>
    public (Cat? Friend, Cat? Rival) Closest(Cat cat, IEnumerable<Cat> others)
    {
        Cat? friend = null, rival = null;
        double best = Friend, worst = Rival;
        foreach (var o in others)
        {
            if (o == cat) continue;
            double v = Get(cat, o);
            if (v >= best) { best = v; friend = o; }
            if (v <= worst) { worst = v; rival = o; }
        }
        return (friend, rival);
    }
}
