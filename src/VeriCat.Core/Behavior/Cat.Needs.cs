using VeriCat.Core.Geometry;
using VeriCat.Core.Props;
using VeriCat.Core.Rendering;

namespace VeriCat.Core.Behavior;

// İhtiyaçlar ve hedefler: acıkınca mama kabına, sıkılınca yumağa, yorulunca arkadaşının yanına gitmek;
// ihtiyacını düşünce baloncuğuyla belli etmek; kap boşsa gelip sana miyavlamak.
public sealed partial class Cat
{
    enum Goal { None, Eat, Play, Cuddle, AskFood, AskLove, Romp }

    Goal goal;
    Prop? goalProp, eatProp;
    Cat? goalCat;
    double nextJumpTry;

    Emote emote;
    double emoteTime, emoteLength;

    /// <summary>Kediyi yöneten koloni (eşyalar ve diğer kediler buradan bulunur).</summary>
    internal Colony? Colony { get; set; }

    /// <summary>Şu an gösterilen düşünce baloncuğu.</summary>
    public Emote? CurrentEmote => emote != Emote.None && emoteTime < emoteLength ? emote : null;

    internal void ShowEmote(Emote e, double seconds)
    {
        emote = e; emoteTime = 0; emoteLength = seconds;
    }

    static Emote EmoteFor(Need n) => n switch
    {
        Need.Hunger => Emote.Hungry, Need.Love => Emote.Lonely, Need.Fun => Emote.Bored, _ => Emote.Sleepy,
    };

    /// <summary>Acil bir ihtiyaç varsa ona göre bir hedef seçer. Seçtiyse true.</summary>
    bool TryPursueNeed()
    {
        if (Vitals.MostUrgent() is not Need need) return false;
        if (Rng.NextDouble() < 0.5) ShowEmote(EmoteFor(need), 2.5);   // belli eder
        switch (need)
        {
            case Need.Hunger:
                if (Colony?.FindFood(this) is Prop bowl) { Seek(Goal.Eat, prop: bowl); return true; }
                if (Rng.NextDouble() < 0.5) { Seek(Goal.AskFood); return true; }
                return false;
            case Need.Fun:
                if (Colony?.FindToy(this) is Prop toy) { Seek(Goal.Play, prop: toy); return true; }
                if (Settings.Chase && Rng.NextDouble() < 0.5) { Set(CatState.Chase, 3, 6); return true; }
                return false;
            case Need.Love:
                if (Rng.NextDouble() < 0.5) { Seek(Goal.AskLove); return true; }
                return false;
            default:
                GoToSleep();
                return true;
        }
    }

    /// <summary>Uyumaya karar verdi: uyuyan bir arkadaşı varsa yanına sokulur.</summary>
    void GoToSleep()
    {
        if (Colony?.FindSleepingFriend(this) is Cat friend && Rng.NextDouble() < 0.7) Seek(Goal.Cuddle, cat: friend);
        else Set(CatState.Sleep, 10, 25);
    }

    void Seek(Goal g, Prop? prop = null, Cat? cat = null)
    {
        Set(CatState.Seek, 12, 12);
        goal = g; goalProp = prop; goalCat = cat;
        nextJumpTry = 0.5;
    }

    /// <summary>Şu an bu eşyaya mı gidiyor (ya da ondan yiyor)?</summary>
    internal bool IsHeadingFor(Prop p) => (goal != Goal.None && goalProp == p) || eatProp == p;

    /// <summary>Şu an bu kediye mi gidiyor (sokulmak, kovalamak)?</summary>
    internal bool IsHeadingFor(Cat c) => goal != Goal.None && goalCat == c;

    /// <summary>Hareket eden yumak yakındaysa oyuncu kedi peşine düşer.</summary>
    void NoticeToys(double dt)
    {
        if (Colony?.FindToy(this) is not Prop toy || !toy.IsMoving) return;
        if (Math.Abs(toy.X - px) > 320 * S || Math.Abs(toy.Y - py) > 200 * S) return;
        if (Rng.NextDouble() < dt * 2.5 * Traits.Playfulness) Seek(Goal.Play, prop: toy);
    }

    /// <summary>Hedefin konumu ve (varsa) üstünde durduğu platform. Hedef ortadan kalktıysa null.</summary>
    (double X, double Y, Platform? Support)? GoalTarget()
    {
        switch (goal)
        {
            case Goal.Eat or Goal.Play:
                return goalProp != null && Colony?.Props.Contains(goalProp) == true && !goalProp.IsHeld
                    ? (goalProp.X, goalProp.Y, goalProp.Support) : null;
            case Goal.Cuddle:
                if (goalCat is not { IsAsleep: true } f || f.platform is not Platform fp) return null;
                double side = px < f.px ? -1 : 1;   // yakın olan tarafına
                return (f.px + side * (ContactRadius + f.ContactRadius + 1), f.py, fp);
            case Goal.AskFood or Goal.AskLove:
                return (Pointer.Position.X, py, null);   // imleç: yalnızca yatayda yanına gelir
            case Goal.Romp:
                if (goalCat is not { playful: true } r || r.state is not (CatState.Flee or CatState.Crouch or CatState.Air)) return null;
                return (r.px, r.py, r.platform);
            default:
                return null;
        }
    }

    double ArriveDistance => goal switch
    {
        Goal.Eat => 36 * S + (goalProp?.Radius ?? 0),
        Goal.Play => 34 * S + (goalProp?.Radius ?? 0),
        Goal.Cuddle => 5 * S,
        Goal.Romp => ContactRadius + (goalCat?.ContactRadius ?? 0) + 6 * S,
        _ => 60 * S,
    };

    void SeekStep(double dt)
    {
        if (GoalTarget() is not (double tx, _, var support)) { Set(CatState.Sit, 1, 2); return; }
        if (goal == Goal.Romp && Now - rompStartedAt < RompHeadStart) { facingRight = tx > px; return; }   // avantaj tanır
        double dx = tx - px, speed = (goal == Goal.Romp ? 280 : 150) * S * Config.Speed;
        bool sameLevel = support == null || Math.Abs(support.Value.Y - py) < 4 * D;

        if (sameLevel && Math.Abs(dx) <= ArriveDistance) { Arrive(); return; }
        if (Math.Abs(dx) > 2 * D) facingRight = dx > 0;

        if (sameLevel) { Stride(dt, speed, mayDrop: false); return; }
        if (support!.Value.Y < py) { Stride(dt, speed, mayDrop: true, forceDrop: true); return; }   // hedef aşağıda: kenardan atla

        // Hedef yukarıda: ara ara zıplamayı dener, olmazsa altına doğru yürür.
        if (stateTime >= nextJumpTry)
        {
            nextJumpTry = stateTime + 0.8;
            if (TryJumpTo(support.Value, tx)) return;
        }
        Stride(dt, speed, mayDrop: false);
    }

    void Arrive()
    {
        var g = goal;
        var prop = goalProp;
        var cat = goalCat;
        switch (g)
        {
            case Goal.Eat when prop != null:
                facingRight = prop.X > px;
                if (prop.Food <= 0.01)
                {   // kap boş: sana söyler
                    Set(CatState.Sit, 2, 3);
                    ShowEmote(Emote.Hungry, 3);
                    Voice.Meow(Config.Pitch);
                    return;
                }
                Set(CatState.Eat, 4, 6);
                eatProp = prop;
                return;
            case Goal.Play when prop != null:
                swatProp = prop;
                StartSwat(1, fleeAfter: false);
                return;
            case Goal.Cuddle when cat != null:
                facingRight = cat.px > px;
                Set(CatState.Sleep, 20, 40);
                Colony?.Bonds.Adjust(this, cat, 0.05);
                return;
            case Goal.Romp when cat != null:
                // Yakaladı: pati değer, roller değişir (tur kaldıysa) ya da ikisi de sevinip oyunu bitirir.
                facingRight = cat.px > px;
                Voice.Swat();
                Vitals.Play(0.08); cat.Vitals.Play(0.08);
                if (rompRounds > 0 && Rng.NextDouble() < 0.6)
                {
                    RunPlayfully(cat, rompRounds - 1);
                    cat.ChasePlayfully(this, rompRounds - 1);
                }
                else { Set(CatState.Happy, 1.2, 2); cat.Set(CatState.Happy, 1.2, 2); cat.facingRight = px > cat.px; }
                return;
            case Goal.AskFood:
                Set(CatState.Sit, 3, 5);
                ShowEmote(Emote.Hungry, 3.5);
                Voice.Meow(Config.Pitch);
                return;
            case Goal.AskLove:
                Set(CatState.Sit, 4, 6);
                ShowEmote(Emote.Lonely, 3.5);
                if (Rng.Next(2) == 0) Voice.Meow(Config.Pitch);
                return;
            default:
                Set(CatState.Sit, 1, 2);
                return;
        }
    }

    /// <summary>Kaptan yer: açlık ~6 sn'de dolar, dolu bir kap ~4 öğün yeter.</summary>
    void EatStep(double dt)
    {
        var bowl = eatProp;
        if (bowl == null || Colony?.Props.Contains(bowl) != true || bowl.IsHeld || Math.Abs(bowl.X - px) > 100 * S)
        {
            Set(CatState.Sit, 1, 2);
            return;
        }
        facingRight = bowl.X > px;
        bowl.Food = Math.Max(0, bowl.Food - dt / 24);
        Vitals.Feed(dt / 6);
        if (bowl.Food <= 0 && Vitals.Hunger < 0.9) { ShowEmote(Emote.Hungry, 3); Set(CatState.Sit, 2, 3); return; }
        if (stateTime >= stateLength || Vitals.Hunger >= 0.99)
        {
            eatProp = null;
            Vitals.Cuddle(0.03);
            Set(CatState.Happy, 1.5, 2.5);
        }
    }

    /// <summary>Yumağa pati vurur: yumak fırlar, kedi eğlenir.</summary>
    internal void BatProp(Prop prop)
    {
        double dir = prop.X >= px ? 1 : -1;
        facingRight = dir > 0;
        prop.Kick(dir * R(380, 720) * D, R(260, 560) * D);
        Vitals.Play(0.06);
        impact = ImpactLength;
        Voice.Swat();
        batCooldownUntil = Now + R(0.6, 1.2);
    }

    double batCooldownUntil;

    /// <summary>Anlık refleks: boşta ve yakındaysa önünden yuvarlanan yumağa vurabilir mi?</summary>
    internal bool CanBat => !Paused && Now >= batCooldownUntil &&
        platform != null && state is CatState.Walk or CatState.Sit or CatState.Seek or CatState.Chase or CatState.Happy;

    /// <summary>Rakibiyle burun buruna geldi: tıslar, kulaklarını yatırır.</summary>
    internal void Grumble()
    {
        ShowEmote(Emote.Grumpy, 1.6);
        Voice.Hiss();
    }
}
