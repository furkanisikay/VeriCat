using VeriCat.Core.Geometry;

namespace VeriCat.Core.Behavior;

// Yüksekteki imlece ulaşmak: pencereleri basamak yapmak, ekran kenarına sıçrayıp tırmanmak,
// duvardan sekip yükselmek ve tepede imlece atlamak.
public sealed partial class Cat
{
    /// <summary>Tırmanırken ekranın tepesiyle arasında kalan pay (kulaklar dik).</summary>
    internal double ClimbHeadroom => 130 * S;

    bool hunting;                                     // imleç için zıplıyor: inince kovalamaya devam
    bool climbRight;                                  // tutunduğu duvar sağda mı?
    double climbPhase, wallCheckAt;
    int wallKicks;

    /// <summary>Tırmanma adımının ilerlemesi (çizim için), 0…1.</summary>
    double ClimbCycle => climbPhase % 1;

    /// <summary>
    /// İmleç normal zıplamayla erişilemeyecek kadar yüksekte. Gerçekten istiyorsa bir yol bulur:
    /// önce imlecin altına doğru bir pencereye basamak gibi zıplar; yoksa en yakın ekran kenarına koşup
    /// duvara sıçrar ve tırmanır. İstemiyorsa ya da yol yoksa oturup izler. Bir şey yaptıysa true.
    /// </summary>
    bool ReachHighPointer(double dt, double mx, double my)
    {
        if (stateTime >= wallCheckAt)
        {
            wallCheckAt = stateTime + 0.5;
            if (Settings.Windows && TryHopToward(mx, my)) return true;
        }
        if (NearestWall(mx) is not (double wallX, bool right)) return false;

        double toWall = wallX - px;
        facingRight = right;
        if (Math.Abs(toWall) <= 8 * S) { StartClimb(right); return true; }
        if (Math.Abs(toWall) < 230 * S) { LeapAtWall(wallX, right); return true; }
        // Penceredeyse kenardan duvara doğru atlar; zemindeyse koşar.
        bool up = platform is { IsFloor: false };
        return Stride(dt, 190 * S * Config.Speed, mayDrop: up, forceDrop: up) || Math.Abs(wallX - px) <= 8 * S;
    }

    /// <summary>Kovalamaya devam edecek kadar istekli mi? Oyunculuk, sıkıntı ve enerjiyle artar.</summary>
    bool WantsHighPointer()
    {
        double want = 0.25 + 0.55 * Traits.Playfulness + 0.3 * (1 - Vitals.Fun);
        want *= 0.5 + 0.5 * Vitals.Energy + 0.25 * Traits.Energy;
        return Rng.NextDouble() < want;
    }

    /// <summary>İmlece yaklaştıran, şu an zıplanabilecek en yüksek pencere kenarına zıplar.</summary>
    bool TryHopToward(double mx, double my)
    {
        Platform? best = null;
        double reach = 450 * D;
        foreach (var p in World.Platforms)
        {
            if (p.IsFloor || p.Y <= py + 40 * S || p.Y > my - 20 * S || p.Y - py > MaxJumpHeight) continue;
            if (p.Width < 50 * S || !FitsUnderScreenTop(p)) continue;
            if (platform is Platform cur && cur.SameSurface(p)) continue;
            double landX = Math.Clamp(mx, p.MinX + 20 * S, p.MaxX - 20 * S);
            if (Math.Abs(landX - px) > reach) continue;
            if (Math.Abs(landX - mx) > 500 * D) continue;                 // imlece yaklaştırmıyor
            if (best == null || p.Y > best.Value.Y) best = p;
        }
        if (best is not Platform t || !TryJumpTo(t, mx)) return false;
        hunting = true;
        return true;
    }

    /// <summary>
    /// İmlece en yakın kapalı ekran kenarı (duvar) ve kedinin o duvara yaslanınca duracağı x.
    /// Yanında başka ekran olan kenar duvar sayılmaz. İmleç duvardan atlanamayacak kadar uzaktaysa null.
    /// </summary>
    (double X, bool Right)? NearestWall(double mx)
    {
        var (screen, _) = World.ScreenNear(px, py + 20 * S);
        var b = screen.Bounds;
        double probeY = py + 20 * S, half = BodyHalfWidth;
        (double X, bool Right)? best = null;
        double bestScore = double.MaxValue;
        if (!World.IsOnAnyScreen(b.MinX - 1, probeY)) Consider(b.MinX + half, false);
        if (!World.IsOnAnyScreen(b.MaxX + 1, probeY)) Consider(b.MaxX - half, true);
        return best;

        void Consider(double x, bool right)
        {
            double fromPointer = Math.Abs(mx - x);
            if (fromPointer > 900 * D) return;
            double score = fromPointer + 0.5 * Math.Abs(px - x);
            if (score < bestScore) { bestScore = score; best = (x, right); }
        }
    }

    /// <summary>Koşup duvara sıçrar: duvara değince tutunur (bkz. <see cref="OnWallContact"/>).</summary>
    void LeapAtWall(double wallX, bool right)
    {
        double rise = 170 * S;
        double v = Math.Sqrt(2 * Gravity * rise), t = v / Gravity;
        jx = (right ? 1 : -1) * Math.Max(Math.Abs(wallX - px) / t * 1.3, 260 * D);
        jy = v;
        facingRight = right;
        pouncing = false;
        hunting = true;
        Set(CatState.Crouch, 99, 99);
    }

    void StartClimb(bool right)
    {
        if (state == CatState.Fight) opponent = null;
        climbRight = right; facingRight = right;
        platform = null; riding = null; vx = vy = 0;
        pouncing = false; hunting = false; flung = false;
        // Tırmanma gücü: enerjik ve dinç kedi daha uzun tırmanır.
        double stamina = R(2.6, 4.0) * (0.6 + 0.8 * Traits.Energy) * (0.55 + 0.45 * Vitals.Energy);
        Set(CatState.Climb, stamina, stamina);
        climbPhase = 0;
    }

    /// <summary>
    /// Duvarda: pençeleriyle sıçraya sıçraya tırmanır (ritmik hız), imleç atlama menziline girince
    /// duvardan tepip üstüne atlar. Gücü bitince pençeleri kayarak aşağı iner ve bırakır.
    /// </summary>
    void ClimbStep(double dt)
    {
        var (mx, my) = Pointer.Position;
        var (screen, floor) = World.ScreenNear(px, py);
        double top = screen.WorkTop - ClimbHeadroom;
        double dy = my - py;
        bool tired = stateTime >= stateLength;

        if (!Settings.Chase) { LetGoOfWall(); return; }

        // Menzilde: duvardan tepip imlece atlar (imleç duvardan uzaklaşıyorsa da).
        bool atTop = py >= top - 1;
        if (!tired && dy > -200 * S && Math.Abs(mx - px) < 900 * D &&
            (dy < MaxJumpHeight * 0.5 || (atTop && dy < MaxJumpHeight * 0.9)))
        {
            WallPounce(mx, my);
            return;
        }
        if (dy < -350 * S) { LetGoOfWall(); return; }   // imleç aşağıda kaldı

        if (!tired)
        {
            // Sıçrayışlı tırmanış: her adımda arka ayaklarla itip yükselir, arada neredeyse durur.
            climbPhase += dt * 1.9 * Config.Speed;
            double push = Math.Pow(Math.Max(0, Math.Sin(ClimbCycle * 2 * Math.PI)), 1.5);
            py = Math.Min(top, py + (60 + 650 * push) * S * Config.Speed * dt);
            return;
        }

        // Yoruldu: pençeler kayar, yavaşça aşağı süzülür, sonra bırakır.
        climbPhase += dt * 0.6;
        py -= 220 * S * dt;
        if (py <= floor.Y) { py = floor.Y; Land(floor, 0); return; }
        if (stateTime >= stateLength + 0.7) LetGoOfWall();
    }

    /// <summary>Duvardan imlece doğru tepinerek atlar; yatay hız her zaman duvardan uzağa.</summary>
    void WallPounce(double tx, double ty)
    {
        double away = climbRight ? -1 : 1;
        double apex = Math.Max(py, ty) + 25 * S;
        double v = Math.Sqrt(2 * Gravity * Math.Max(apex - py, 30 * S));
        double t = v / Gravity;
        double speed = Math.Clamp((tx - px) / t, -1500 * D, 1500 * D);   // duvardan tepiş yerden daha güçlü
        if (speed * away < 220 * D) speed = away * 220 * D;
        vx = speed; vy = v;
        facingRight = vx > 0;
        state = CatState.Air; stateTime = 0;
        pouncing = true; pounceHit = false;
        wallKicks++;
        huntCooldownUntil = Now + R(1.5, 3);
        Voice.Swat();
    }

    void LetGoOfWall()
    {
        double away = climbRight ? -1 : 1;
        state = CatState.Air; stateTime = 0;
        vx = away * 120 * D; vy = 0;
        facingRight = away > 0;
        pouncing = false; hunting = false;
    }

    /// <summary>
    /// Havadaki kedi ekranın kenarına çarptı. İmleç yukarıdaysa ve av peşindeyse ya duvardan sekip daha yükseğe
    /// fırlar ya da duvara tutunup tırmanır. Tepki verdiyse true (sekme yerine).
    /// </summary>
    bool OnWallContact(bool wallOnRight)
    {
        if (state != CatState.Air || flung || !Settings.Chase || !(hunting || pouncing)) return false;
        var (mx, my) = Pointer.Position;
        double dy = my - py;
        if (dy < 60 * S) return false;

        double fromWall = Math.Abs(mx - px);
        if (wallKicks == 0 && vy > -250 * D && fromWall > 140 * S && dy < MaxJumpHeight * 1.3)
        {   // duvardan sek: yükselip imlece doğru döner
            climbRight = wallOnRight;
            double v = Math.Max(vy, 0) * 0.3 + Math.Sqrt(2 * Gravity * Math.Min(dy + 30 * S, MaxJumpHeight * 0.8));
            double t = v / Gravity;
            double away = wallOnRight ? -1 : 1;
            vx = away * Math.Clamp(fromWall / Math.Max(t, 0.2), 240 * D, 950 * D);
            vy = v;
            facingRight = vx > 0;
            pouncing = true; pounceHit = false;
            wallKicks++;
            Voice.Swat();
            return true;
        }
        if (vy > -900 * D) { StartClimb(wallOnRight); return true; }
        return false;
    }
}
