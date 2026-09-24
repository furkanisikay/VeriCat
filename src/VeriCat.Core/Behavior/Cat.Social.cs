namespace VeriCat.Core.Behavior;

// Diğer kedilerle ilişkiler: çarpışma, itişme ve kavga. Eşleştirmeyi Colony yapar.
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
    internal bool IsGrounded => platform != null && state is not (CatState.Air or CatState.Dragged);

    internal bool IsFighting => state == CatState.Fight;

    /// <summary>Gövdenin yarı genişliği; iki kedinin merkezleri bundan fazla yaklaşamaz.</summary>
    internal double ContactRadius => 34 * S;

    internal bool CanFight =>
        !Paused && Now >= fightCooldownUntil &&
        state is CatState.Walk or CatState.Chase or CatState.Sit or CatState.Stalk;

    /// <summary>Diğer kediye doğru mu ilerliyor?</summary>
    internal bool IsMovingToward(Cat other) =>
        state is CatState.Walk or CatState.Chase or CatState.Flee or CatState.Stalk &&
        (other.px > px) == facingRight;

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

    /// <summary>Diğer kediye kavgasız çarptı.</summary>
    internal void Bump(Cat other)
    {
        if (state == CatState.Sleep)
        {
            // Uykusu bölünen kedi bazen söylenir.
            if (Rng.NextDouble() < 0.4) { Set(CatState.Sit, 1, 2); Voice.Hiss(); }
            return;
        }
        if (!IsMovingToward(other)) return;
        if (state is CatState.Chase or CatState.Stalk) Set(CatState.Sit, 0.6, 1.2);
        else facingRight = !facingRight;
    }

    /// <summary>Çarpışma çözümü: kediyi yatayda iter (dünya sınırları içinde).</summary>
    internal void Shove(double dx)
    {
        px += dx;
        if (state == CatState.Fight) fightAnchor += dx;
        KeepOnScreen();
    }
}
