namespace VeriCat.Core.Behavior;

// Fare imleciyle oyun: kovalama, pusu kurma, üstüne atlama ve yumruk atma.
public sealed partial class Cat
{
    /// <summary>Bir yumruğun süresi (saniye).</summary>
    internal const double PunchTime = 0.28;
    const double ImpactLength = 0.2;

    double pointerX, pointerY, pointerSpeed;
    bool pointerKnown;
    double huntCooldownUntil;

    bool pouncing, pounceHit;
    int punchCount, punchIndex;
    bool punchLanded, fleeAfterSwat;
    double aim, impact;

    /// <summary>Son isabetten beri geçen süre efekti için; 0 ise efekt yok.</summary>
    public bool IsShowingImpact => impact > 0;

    void TrackPointer(double dt)
    {
        var (x, y) = Pointer.Position;
        if (pointerKnown && dt > 0)
        {
            double v = Math.Sqrt((x - pointerX) * (x - pointerX) + (y - pointerY) * (y - pointerY)) / dt;
            pointerSpeed = pointerSpeed * 0.8 + v * 0.2;
        }
        pointerX = x; pointerY = y; pointerKnown = true;
    }

    /// <summary>Yakında hızla oynayan imleç dikkatini çeker.</summary>
    void NoticePointer(double dt)
    {
        if (!Settings.Chase || pointerSpeed < 900 * D || Now < huntCooldownUntil) return;
        double dx = Math.Abs(pointerX - px), dy = pointerY - py;
        if (dx > 320 * S || dy < -60 * S || dy > MaxJumpHeight) return;
        if (Rng.NextDouble() < dt * 1.5) Set(CatState.Chase, 3, 6);
    }

    void ChaseStep(double dt)
    {
        var (mx, my) = Pointer.Position;
        double dx = mx - px, dy = my - py, adx = Math.Abs(dx);
        if (adx > 2 * D) facingRight = dx > 0;

        if (adx < 50 * S)
        {
            if (dy > -10 * S && dy < 115 * S) { StartSwat(Rng.Next(2, 4), fleeAfter: false); return; }
            if (dy >= 115 * S && dy < MaxJumpHeight) { Pounce(mx, my); return; }
            Set(CatState.Sit, 1, 2);   // imleç ulaşamayacağı yerde: oturup izler
            return;
        }
        if (adx < 190 * S && dy > -20 * S && dy < MaxJumpHeight && Now >= huntCooldownUntil && Rng.NextDouble() < dt * 1.2)
        {
            Set(CatState.Stalk, 0.6, 1.1);
            return;
        }
        if (!Stride(dt, 175 * S * Config.Speed, false)) Set(CatState.Sit, 1.5, 3);
    }

    /// <summary>Çömelmiş, kıçını sallıyor; süre dolunca imlece atlar.</summary>
    void StalkStep()
    {
        var (mx, my) = Pointer.Position;
        if (Math.Abs(mx - px) > 2 * D) facingRight = mx > px;
        if (stateTime < stateLength) return;
        double dy = my - py;
        if (Math.Abs(mx - px) < 320 * S && dy > -40 * S && dy < MaxJumpHeight) Pounce(mx, my);
        else Set(CatState.Chase, 2, 4);
    }

    /// <summary>Patileri önde, imlecin olduğu noktaya varacak şekilde zıplar.</summary>
    void Pounce(double tx, double ty)
    {
        double apex = Math.Max(py, ty) + 20 * S;
        double v = Math.Sqrt(2 * Gravity * Math.Max(apex - py, 10 * S));
        double t = v / Gravity;
        jx = Math.Clamp((tx - px) / t, -1100 * D, 1100 * D); jy = v;
        facingRight = tx >= px;
        pouncing = true; pounceHit = false;
        huntCooldownUntil = Now + R(2, 4);
        Set(CatState.Crouch, 99, 99);
    }

    /// <summary>Havadayken patiler imlece değerse vurur.</summary>
    void PounceStrike()
    {
        if (pounceHit) return;
        double pawX = px + Dir * 45 * S, pawY = py + 45 * S;
        var (mx, my) = Pointer.Position;
        if (Distance(pawX, pawY, mx, my) >= 45 * S) return;
        pounceHit = true;
        Hit(0.3);
    }

    /// <summary>Arka ayaklar üstünde kalkıp sırayla <paramref name="punches"/> yumruk atar.</summary>
    void StartSwat(int punches, bool fleeAfter)
    {
        punchCount = Math.Max(1, punches); punchIndex = 0; punchLanded = false;
        fleeAfterSwat = fleeAfter;
        huntCooldownUntil = Now + R(1.5, 3);
        double length = punchCount * PunchTime + 0.15;
        Set(CatState.Swat, length, length);
        UpdateAim();
    }

    void SwatStep()
    {
        var (mx, _) = Pointer.Position;
        if (Math.Abs(mx - px) > 6 * S) facingRight = mx > px;
        UpdateAim();

        int idx = (int)(stateTime / PunchTime);
        if (idx < punchCount)
        {
            if (idx != punchIndex) { punchIndex = idx; punchLanded = false; }
            if (!punchLanded && SwatProgress() >= 0.45) { punchLanded = true; TryPunch(); }
        }

        if (stateTime < stateLength) return;
        if (fleeAfterSwat) RunFrom(mx, scared: false);
        else if (Settings.Chase && Rng.NextDouble() < 0.6) Set(CatState.Chase, 2, 4);
        else Set(CatState.Sit, 0.8, 1.6);
    }

    /// <summary>Şu anki yumruğun ilerlemesi, 0 (geride) → 1 (geri çekildi).</summary>
    double SwatProgress()
    {
        if (state != CatState.Swat || (int)(stateTime / PunchTime) >= punchCount) return 0;
        return stateTime % PunchTime / PunchTime;
    }

    (double X, double Y) Shoulder => (px + Dir * 12 * S, py + 60 * S);

    void UpdateAim()
    {
        var (mx, my) = Pointer.Position;
        var (sx, sy) = Shoulder;
        aim = Math.Clamp(Math.Atan2(my - sy, Math.Max(Math.Abs(mx - sx), 1)), -0.4, 1.2);
    }

    void TryPunch()
    {
        var (sx, sy) = Shoulder;
        double tipX = sx + Dir * Math.Cos(aim) * 40 * S, tipY = sy + Math.Sin(aim) * 40 * S;
        var (mx, my) = Pointer.Position;
        if (Distance(tipX, tipY, mx, my) < 45 * S) Hit(aim);
    }

    /// <summary>İsabet: efekt, ses ve (açıksa) imleci vuruş yönünde iter.</summary>
    void Hit(double angle)
    {
        impact = ImpactLength;
        Voice.Swat();
        if (!Settings.PunchCursor || Pointer.AnyButtonDown) return;
        double push = R(14, 24) * D;
        Pointer.Nudge(Dir * Math.Cos(angle) * push, Math.Sin(angle) * push + 4 * D);
    }

    static double Distance(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
}
