namespace VeriCat.Core.Behavior;

// Diğer kedilerle ilişkiler: karşılaşma, üstünden atlama, yol verme, kovalamaca ve kavga. Eşleştirmeyi Colony yapar.
public sealed partial class Cat
{
    Cat? opponent;
    bool fightLeader;
    double fightAnchor;
    double fightCooldownUntil;

    internal double ContactCooldownUntil { get; set; }

    /// <summary>Kavga ettiği kedi.</summary>
    public Cat? Opponent => state == CatState.Fight ? opponent : null;

    /// <summary>Bir zeminde duruyor (çarpışma sadece aynı zemindeki kediler arasında olur).</summary>
    internal bool IsGrounded => platform != null && state is not (CatState.Air or CatState.Dragged or CatState.Hang);

    internal bool IsFighting => state == CatState.Fight;

    /// <summary>Gövdenin yarı genişliği; iki kedinin merkezleri bundan fazla yaklaşamaz.</summary>
    internal double ContactRadius => 34 * S;

    internal bool CanFight =>
        !Paused && Now >= fightCooldownUntil &&
        state is CatState.Walk or CatState.Chase or CatState.Sit or CatState.Stalk;

    /// <summary>Diğer kediye doğru mu ilerliyor?</summary>
    internal bool IsMovingToward(Cat other) => IsMovingToward(other.px);

    internal bool IsMovingToward(double x) =>
        state is CatState.Walk or CatState.Chase or CatState.Flee or CatState.Stalk or CatState.Seek &&
        (x > px) == facingRight;

    internal void BeginFight(Cat other, bool leader, double duration)
    {
        opponent = other;
        fightLeader = leader;
        fightAnchor = px;
        facingRight = other.px > px;
        petting = 0;
        Set(CatState.Fight, duration, duration);
        opponent = other;                              // Set() sıfırlamasın
        if (leader) Voice.Hiss();
    }

    void FightStep()
    {
        if (opponent is not Cat o || o.state != CatState.Fight || o.opponent != this)
        {
            Set(CatState.Sit, 0.8, 1.5);               // rakip kaçırıldı (ör. fareyle tutuldu)
            return;
        }
        facingRight = o.px > px;
        // İleri geri hamle: iki kedi ters fazda, sanki birbirine saldırıyor.
        px = fightAnchor + Dir * Math.Sin(stateTime * 13 + (fightLeader ? 0 : 1.7)) * 5 * S;
        if (fightLeader && stateTime >= stateLength) ResolveFight(o);
    }

    /// <summary>Kazananı seçer (iri ve huysuz kedinin şansı fazla); kaybeden kaçar.</summary>
    void ResolveFight(Cat o)
    {
        double mine = S * (0.5 + Traits.Temper), theirs = o.S * (0.5 + o.Traits.Temper);
        bool iWin = Rng.NextDouble() < mine / (mine + theirs);
        var (winner, loser) = iWin ? (this, o) : (o, this);
        foreach (var c in new[] { this, o })
        {
            c.fightCooldownUntil = Now + R(10, 20);
            c.px = c.fightAnchor;
        }
        loser.RunFrom(winner.px, scared: true);
        winner.Set(CatState.Sit, 1.5, 3);
        if (Rng.NextDouble() < 0.4) Voice.Meow(winner.Config.Pitch);
    }

    internal bool CanGreet => !Paused && state is CatState.Walk or CatState.Sit or CatState.Chase;

    /// <summary>Dost selamı: birbirine dönüp sevinirler.</summary>
    internal void Greet(Cat other)
    {
        facingRight = other.px > px;
        Set(CatState.Happy, 1.5, 2.5);
        if (Rng.Next(2) == 0) Voice.Meow(Config.Pitch);
    }

    /// <summary>Üstünden atlanan uyuyan kedi bazen uyanıp söylenir.</summary>
    internal void Disturbed()
    {
        if (state == CatState.Sleep && Rng.NextDouble() < 0.4) { Set(CatState.Sit, 1, 2); Voice.Hiss(); }
    }

    // MARK: Üstünden atlama

    CatState? hopResume;                              // atlayış bitince sürdüreceği iş
    double hopCooldownUntil;

    /// <summary>Yolundaki kedinin ya da eşyanın üstünden atlayabilir mi? (Yürüyor, koşuyor, bir yere gidiyor.)</summary>
    internal bool CanHop => !Paused && platform != null && Now >= hopCooldownUntil &&
        state is CatState.Walk or CatState.Chase or CatState.Flee or CatState.Seek;

    /// <summary>Üstünden atlamak için çömelmiş.</summary>
    internal bool IsHopping => state == CatState.Crouch && hopResume != null;

    /// <summary>Üstünden atlanacak kedi için gereken yükseklik: uyuyan alçak, oturan yüksek.</summary>
    internal double HopClearance => (IsAsleep ? 50 : 80) * S;

    /// <summary>
    /// <paramref name="overX"/>'teki engelin (kedi, mama kabı, yumak) üstünden sekerek atlar ve öbür yanına iner;
    /// indikten sonra yaptığı işe (yürüme, kaçma, hedefe gitme) devam eder. İtmek yerine gerçek kedinin yaptığı bu.
    /// </summary>
    internal void HopOver(double overX, double clearance, double beyond)
    {
        double dir = overX >= px ? 1 : -1;
        double land = overX + dir * beyond, rise = clearance + 12 * S;
        double v = Math.Sqrt(2 * Gravity * rise), time = 2 * v / Gravity;
        var resume = state;
        var keepGoal = goal; bool keepHunt = hunting, keepPlay = playful;
        jx = (land - px) / time; jy = v;
        facingRight = dir > 0;
        pouncing = false;
        Set(CatState.Crouch, 99, 99);
        goal = keepGoal; hunting = keepHunt; playful = keepPlay;
        hopResume = resume;
        hopCooldownUntil = Now + 0.8;
    }

    /// <summary>Üstüne doğru gelen kediye yol verir: durup onu izler.</summary>
    internal void YieldTo(Cat other)
    {
        facingRight = other.px > px;
        Set(CatState.Sit, 0.5, 1.0);
    }

    // MARK: Kovalamaca

    bool playful;                                     // oyun kaçışı (korku değil)
    int rompRounds;
    double rompStartedAt;

    /// <summary>Kovalamacada kovalayan, kaçana önce bu kadar süre avantaj tanır (kıpırdanıp bekler).</summary>
    const double RompHeadStart = 0.5;

    /// <summary>Oyuna kaçar: kovalayandan uzağa koşar, kulakları dik, yüzü neşeli.</summary>
    internal void RunPlayfully(Cat chaser, int rounds)
    {
        facingRight = px >= chaser.px;
        Set(CatState.Flee, 2, 3.5);
        playful = true;
        rompRounds = rounds;
        if (Rng.Next(3) == 0) Voice.Meow(Config.Pitch);
    }

    /// <summary>Oyun arkadaşını kovalar; yakalayınca rolleri değiştirebilirler.</summary>
    internal void ChasePlayfully(Cat runner, int rounds)
    {
        Seek(Goal.Romp, cat: runner);
        rompRounds = rounds;
        rompStartedAt = Now;
    }

    /// <summary>Çarpışma çözümü: duran kedileri yatayda hafifçe ayırır (dünya sınırları içinde).</summary>
    internal void Shove(double dx)
    {
        px += dx;
        if (state == CatState.Fight) fightAnchor += dx;
        KeepOnScreen();
    }
}
