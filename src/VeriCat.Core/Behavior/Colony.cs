using VeriCat.Core.Props;

namespace VeriCat.Core.Behavior;

/// <summary>
/// Tüm kedileri ve eşyaları birlikte ilerletir, aralarındaki etkileşimi çözer:
/// aynı zemindeki kediler birbirinin içinden geçmez; karşılaşınca geri döner, selamlaşır ya da kavga eder
/// (ilişki hafızasına göre); önlerinden yuvarlanan yumağa pati atar, yumak kedilere çarpıp seker.
/// </summary>
public sealed class Colony
{
    readonly List<Cat> cats = new();
    readonly List<Prop> props = new();
    readonly CatEnvironment env;

    public Colony(CatEnvironment env, BondBook? bonds = null)
    {
        this.env = env;
        Bonds = bonds ?? new BondBook();
    }

    public IReadOnlyList<Cat> Cats => cats;
    public IReadOnlyList<Prop> Props => props;

    /// <summary>Kediler arası ilişki hafızası.</summary>
    public BondBook Bonds { get; }

    /// <summary>İki kedi karşılaştığında kavga çıkma olasılığı (ortalama huysuzlukta).</summary>
    public double FightChance { get; set; } = 0.5;

    /// <summary>Kavga olasılığının üst sınırı (karakter ve rekabet ne olursa olsun).</summary>
    public double MaxFightChance { get; set; } = 0.75;

    /// <summary>İki ortalama sevecenlikteki (0.5) kedi karşılaştığında selamlaşma olasılığı; sevecenlikle doğru orantılı.</summary>
    public double GreetChance { get; set; } = 0.25;

    /// <summary>Aynı kedi çifti için karar sonrası bekleme (saniye).</summary>
    public double ContactCooldown { get; set; } = 2.5;

    public void Add(Cat cat)
    {
        cats.Add(cat);
        cat.Colony = this;
    }

    public bool Remove(Cat cat)
    {
        cat.Colony = null;
        return cats.Remove(cat);
    }

    public void AddProp(Prop prop) => props.Add(prop);

    public bool RemoveProp(Prop prop) => props.Remove(prop);

    public void Step(double dt)
    {
        foreach (var c in cats.ToArray()) c.Step(dt);
        foreach (var p in props.ToArray()) p.Step(dt);
        ResolveContacts();
        ResolveToys();
        foreach (var c in cats) c.Present();
    }

    // MARK: Hedef bulma (kediler sorar)

    /// <summary>İçinde mama olan en yakın kap.</summary>
    internal Prop? FindFood(Cat cat) => Nearest(cat, p => p.Kind == PropKind.Bowl && p.Food > 0.02);

    /// <summary>En yakın yumak.</summary>
    internal Prop? FindToy(Cat cat) => Nearest(cat, p => p.Kind == PropKind.Yarn && !p.IsHeld);

    Prop? Nearest(Cat cat, Func<Prop, bool> match) => props
        .Where(p => match(p) && p.IsGrounded)
        .OrderBy(p => Math.Abs(p.X - cat.X) + Math.Abs(p.Y - cat.Y) * 2)
        .FirstOrDefault();

    /// <summary>Uyuyan bir arkadaş (yanına sokulmak için); aynı katta olan tercih edilir.</summary>
    internal Cat? FindSleepingFriend(Cat cat) => cats
        .Where(o => o != cat && o.IsAsleep && o.Support != null && Bonds.Get(cat, o) >= BondBook.Friend)
        .OrderBy(o => Math.Abs(o.Y - cat.Y) > 4 * env.Dpi ? 1 : 0)
        .ThenBy(o => Math.Abs(o.X - cat.X))
        .FirstOrDefault();

    // MARK: Kedi ↔ kedi

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
                double bond = Bonds.Get(a, b);
                bool friends = bond >= BondBook.Friend;

                // Arkadaşlar yan yana uyuyabilir: birbirini itmeden ayrılırlar, kimse uyanmaz.
                bool cuddling = friends && (a.IsAsleep || b.IsAsleep);

                if (!cuddling && !a.IsFighting && !b.IsFighting && now >= a.ContactCooldownUntil && now >= b.ContactCooldownUntil)
                {
                    a.ContactCooldownUntil = b.ContactCooldownUntil = now + ContactCooldown;
                    bool approaching = a.IsMovingToward(b) || b.IsMovingToward(a);
                    // Tek zar: alt uç kavga (huysuzluk ve rekabetle artar), üst uç selam (sevecenlik ve dostlukla artar).
                    double roll = env.Random.NextDouble();
                    double fightMul = bond <= BondBook.Rival ? 2 : friends ? 0.25 : 1;
                    double greetMul = friends ? 2 : bond <= BondBook.Rival ? 0 : 1;
                    // Üst sınır: en azılı rakipler bile arada bir sadece tıslayıp geçer (yoksa kavga ↔ rekabet kısır döngüsü).
                    double fight = Math.Min(MaxFightChance, FightChance * (a.Traits.Temper + b.Traits.Temper) * fightMul);
                    double greet = GreetChance * 2 * Math.Min(a.Traits.Affection, b.Traits.Affection) * greetMul;
                    if (approaching && a.CanGreet && b.CanGreet && roll >= 1 - greet && roll >= fight)
                    {
                        a.Greet(b);
                        b.Greet(a);
                        Bonds.Adjust(a, b, 0.15);
                    }
                    else if (approaching && a.CanFight && b.CanFight && roll < fight)
                    {
                        // Kavga mesafesi: hafif iç içe, ama üst üste değil.
                        double want = min * 0.8;
                        if (dist < want) { a.Shove(-sign * (want - dist) / 2); b.Shove(sign * (want - dist) / 2); }
                        double duration = 1.6 + env.Random.NextDouble() * 1.2;
                        a.BeginFight(b, leader: true, duration);
                        b.BeginFight(a, leader: false, duration);
                        Bonds.Adjust(a, b, -0.25);
                        continue;
                    }
                    else
                    {
                        // Rakip: üstüne yürünen taraf tıslar (Bump yönleri çevirmeden önce belirlenir).
                        var grumbler = bond <= BondBook.Rival && approaching ? (a.IsMovingToward(b) ? b : a) : null;
                        a.Bump(b);
                        b.Bump(a);
                        grumbler?.Grumble();
                    }
                }

                double overlap = min - dist;
                if (a.IsFighting) b.Shove(sign * overlap);
                else if (b.IsFighting) a.Shove(-sign * overlap);
                else { a.Shove(-sign * overlap / 2); b.Shove(sign * overlap / 2); }
            }
        }
    }

    // MARK: Kedi ↔ yumak

    /// <summary>
    /// Yerde yuvarlanan yumak aynı kattaki bir kediye çarparsa seker; kedi boştaysa ve oyuncuysa refleksle pati atar.
    /// </summary>
    internal void ResolveToys()
    {
        double d = env.Dpi;
        foreach (var toy in props)
        {
            if (toy.Kind != PropKind.Yarn || !toy.IsGrounded || !toy.IsMoving) continue;
            foreach (var cat in cats)
            {
                if (!cat.IsGrounded || Math.Abs(cat.Y - toy.Y) > 4 * d) continue;
                double reach = cat.ContactRadius * 0.8 + toy.Radius, dx = toy.X - cat.X;
                if (Math.Abs(dx) > reach) continue;
                if (cat.CanBat && env.Random.NextDouble() < 0.35 + 0.5 * cat.Traits.Playfulness) cat.BatProp(toy);
                else toy.BounceOff(cat.X + Math.Sign(dx == 0 ? 1 : dx) * reach, Math.Sign(dx == 0 ? 1 : dx));
                break;
            }
        }
    }
}
