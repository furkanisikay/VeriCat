namespace VeriCat.Core.Behavior;

/// <summary>
/// Tüm kedileri birlikte ilerletir ve aralarındaki etkileşimi çözer:
/// aynı zemindeki kediler birbirinin içinden geçmez; üstüne yürüyen kedi ya geri döner ya da kavga çıkar.
/// </summary>
public sealed class Colony
{
    readonly List<Cat> cats = new();
    readonly CatEnvironment env;

    public Colony(CatEnvironment env) => this.env = env;

    public IReadOnlyList<Cat> Cats => cats;

    /// <summary>İki kedi karşılaştığında kavga çıkma olasılığı.</summary>
    public double FightChance { get; set; } = 0.5;

    /// <summary>İki ortalama sevecenlikteki (0.5) kedi karşılaştığında selamlaşma olasılığı; sevecenlikle doğru orantılı.</summary>
    public double GreetChance { get; set; } = 0.25;

    /// <summary>Aynı kedi çifti için karar sonrası bekleme (saniye).</summary>
    public double ContactCooldown { get; set; } = 2.5;

    public void Add(Cat cat) => cats.Add(cat);

    public bool Remove(Cat cat) => cats.Remove(cat);

    public void Step(double dt)
    {
        foreach (var c in cats.ToArray()) c.Step(dt);
        ResolveContacts();
        foreach (var c in cats) c.Present();
    }

    internal void ResolveContacts()
    {
        double now = env.Clock(), d = env.Dpi;
        for (int i = 0; i < cats.Count; i++)
        {
            for (int j = i + 1; j < cats.Count; j++)
            {
                Cat a = cats[i], b = cats[j];
                if (!a.IsGrounded || !b.IsGrounded || a.Paused || b.Paused) continue;
                if (Math.Abs(a.Y - b.Y) > 4 * d) continue;
                if (a.IsFighting && b.IsFighting) continue;

                double dx = b.X - a.X, dist = Math.Abs(dx), min = a.ContactRadius + b.ContactRadius;
                if (dist >= min) continue;
                double sign = dx > 0 ? 1 : dx < 0 ? -1 : a.FacingRight ? -1 : 1;

                if (!a.IsFighting && !b.IsFighting && now >= a.ContactCooldownUntil && now >= b.ContactCooldownUntil)
                {
                    a.ContactCooldownUntil = b.ContactCooldownUntil = now + ContactCooldown;
                    bool approaching = a.IsMovingToward(b) || b.IsMovingToward(a);
                    // Tek zar: alt uç kavga (huysuzlukla artar), üst uç dost selamı (sevecenlikle artar).
                    double roll = env.Random.NextDouble();
                    double fight = FightChance * (a.Traits.Temper + b.Traits.Temper);
                    double greet = GreetChance * 2 * Math.Min(a.Traits.Affection, b.Traits.Affection);
                    if (approaching && a.CanGreet && b.CanGreet && roll >= 1 - greet && roll >= fight)
                    {
                        a.Greet(b);
                        b.Greet(a);
                    }
                    else if (approaching && a.CanFight && b.CanFight && roll < fight)
                    {
                        // Kavga mesafesi: hafif iç içe, ama üst üste değil.
                        double want = min * 0.8;
                        if (dist < want) { a.Shove(-sign * (want - dist) / 2); b.Shove(sign * (want - dist) / 2); }
                        double duration = 1.6 + env.Random.NextDouble() * 1.2;
                        a.BeginFight(b, leader: true, duration);
                        b.BeginFight(a, leader: false, duration);
                        continue;
                    }
                    else
                    {
                        a.Bump(b);
                        b.Bump(a);
                    }
                }

                double overlap = min - dist;
                if (a.IsFighting) b.Shove(sign * overlap);
                else if (b.IsFighting) a.Shove(-sign * overlap);
                else { a.Shove(-sign * overlap / 2); b.Shove(sign * overlap / 2); }
            }
        }
    }
}
