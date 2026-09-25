using VeriCat.Core.Geometry;

namespace VeriCat.Core.Behavior;

// Pencere kenarından sarkmak: üstünde duracak yer olmayan (ekranın tepesine çok yakın) kenara ön patileriyle tutunur,
// arka ayaklarıyla tırmalar; yer açılırsa üstüne çıkar, yoksa bir süre sonra bırakır. Ekranın tepesine dayanan (tam ekran)
// pencere perde gibidir: kedi yüzeyine pençelerini geçirip tırmanır, tepede asılı kalır, oradan imlece atlayabilir.
public sealed partial class Cat
{
    /// <summary>Asılıyken tutunduğu kenarın ayaklardan yüksekliği (ön pati uçları).</summary>
    internal double HangDrop => 122 * S;

    Platform? hangTarget;                             // havada: tutunmaya çalıştığı kenar
    double nextScratch;
    long curtain;                                     // tırmandığı "perde" pencere (0: yok)
    RectU curtainFrame;
    bool curtainHunt;                                 // perdeye imleç için tırmanıyor

    /// <summary>
    /// Üstünde durulamayan ama ön patilerle tutunulabilecek bir kenara (pencerenin tepeye yakın üst kenarı ya da
    /// iç bölümü) zıplar. Uygun kenar yoksa false.
    /// </summary>
    internal bool TryHangJump()
    {
        double s = S, reach = 450 * D;
        var cur = platform;
        var targets = World.Platforms.Where(p =>
        {
            if (p.IsFloor || FitsUnderScreenTop(p) || p.Width < 50 * s) return false;
            if (cur is Platform c && c.SameSurface(p)) return false;
            double rise = p.Y - HangDrop - py, nearest = Math.Clamp(px, p.MinX, p.MaxX);
            return rise > 20 * s && rise < MaxJumpHeight && Math.Abs(nearest - px) < reach;
        }).ToList();
        if (targets.Count == 0) return false;

        var t = targets[Rng.Next(targets.Count)];
        double lo = Math.Max(t.MinX + 25 * s, px - reach), hi = Math.Min(t.MaxX - 25 * s, px + reach);
        if (lo >= hi) return false;
        double tx = R(lo, hi);
        double v = Math.Sqrt(2 * Gravity * (t.Y - HangDrop + 10 * s - py)), time = v / Gravity;
        jx = (tx - px) / time; jy = v;
        facingRight = jx >= 0;
        pouncing = false;
        Set(CatState.Crouch, 99, 99);
        hangTarget = t;
        return true;
    }

    /// <summary>Havada: patileri hedef kenara yetişti mi? Yetiştiyse tutunur.</summary>
    bool CatchEdge()
    {
        if (hangTarget is not Platform h) return false;
        if (vy < -700 * D) { hangTarget = null; return false; }   // ıskaladı, düşüyor
        if (vy > 80 * D) return false;
        double paws = py + HangDrop;
        foreach (var seg in World.Platforms)
        {
            if (!seg.SameSurface(h) || !seg.Covers(px, 4 * D) || Math.Abs(paws - seg.Y) > 30 * S) continue;
            StartHang(seg);
            return true;
        }
        return false;
    }

    /// <summary>Ön patileriyle kenara asılır. Pencere kayarsa onunla birlikte kayar.</summary>
    void StartHang(Platform p)
    {
        if (state == CatState.Fight) opponent = null;
        platform = p;
        riding = World.Frames.TryGetValue(p.Owner, out var r) ? r : null;
        rideChangedAt = Now;
        px = Math.Clamp(px, p.MinX + 10 * S, p.MaxX - 10 * S);
        py = p.Y - HangDrop;
        vx = vy = 0;
        pouncing = false; hunting = false; flung = false; hangTarget = null; curtain = 0;
        Set(CatState.Hang, 2.5 + 3.5 * Traits.Energy, 4 + 4 * Traits.Energy);
        nextScratch = 0.15;
        Voice.Scratch();
    }

    void HangStep(double dt)
    {
        if (curtain != 0) { CurtainStep(dt); return; }
        if (!OnSupport())
        {   // pencere kapandı, taşındı ya da ekrandan çıktı
            if (state == CatState.Hang) Drop(0);
            return;
        }
        var edge = platform!.Value;
        py = edge.Y - HangDrop;

        if (stateTime >= nextScratch)
        {   // arka ayaklarla tırmalar
            nextScratch = stateTime + R(0.7, 1.4);
            Voice.Scratch();
        }
        if (stateTime < stateLength) return;
        if (FitsUnderScreenTop(edge))
        {   // yer açıldı (pencere aşağı indi): üstüne çıkar
            py = edge.Y;
            Set(CatState.Sit, 0.8, 1.6);
            return;
        }
        if (Rng.Next(3) == 0) Voice.Meow(Config.Pitch);
        platform = null; riding = null;
        Drop(0);
    }

    // MARK: Perde

    /// <summary>Kedinin arkasında, ekranın tepesine dayanan ve ayaklarına kadar inen bir pencere (tutunacak kenarı yok).</summary>
    long? CurtainBehind()
    {
        var (screen, _) = World.ScreenNear(px, py + 20 * S);
        foreach (var (h, f) in World.Frames)
        {
            if (px < f.MinX + 30 * S || px > f.MaxX - 30 * S) continue;
            if (f.MaxY >= screen.WorkTop - 12 * D && f.MinY <= py + 40 * S && f.MaxY > py + HangDrop + 60 * S) return h;
        }
        return null;
    }

    /// <summary>Arkasındaki tam ekran pencereye perdeye tırmanır gibi tırmanmayı dener.</summary>
    internal bool TryCurtainClimb(bool hunt = false)
    {
        if (!Settings.Windows || CurtainBehind() is not long h) return false;
        if (state == CatState.Fight) opponent = null;
        platform = null; riding = null; vx = vy = 0;
        pouncing = false; flung = false;
        double stamina = R(3.5, 6) * (0.6 + 0.8 * Traits.Energy) * (0.55 + 0.45 * Vitals.Energy);
        Set(CatState.Hang, stamina, stamina);
        curtain = h; curtainFrame = World.Frames[h]; curtainHunt = hunt;
        climbPhase = 0; nextScratch = 0.1;
        return true;
    }

    /// <summary>
    /// Perdede: pençeleriyle hamle hamle yukarı çıkar, tırmalar; tepede asılı kalır. İmleç menzile girince atlar;
    /// gücü bitince bırakır. Pencere kayarsa onunla kayar, kapanırsa düşer.
    /// </summary>
    void CurtainStep(double dt)
    {
        if (!World.Frames.TryGetValue(curtain, out var f) || px < f.MinX || px > f.MaxX) { LetGoOfCurtain(); return; }
        if (f.MinX != curtainFrame.MinX || f.MaxY != curtainFrame.MaxY)
        {
            px += f.MinX - curtainFrame.MinX; py += f.MaxY - curtainFrame.MaxY;
            curtainFrame = f;
        }
        var (screen, _) = World.ScreenNear(px, py);
        double top = Math.Min(screen.WorkTop, f.MaxY) - HangDrop;

        if (stateTime >= nextScratch) { nextScratch = stateTime + R(0.5, 1.1); Voice.Scratch(); }

        if (curtainHunt && Settings.Chase)
        {
            var (mx, my) = Pointer.Position;
            double dy = my - py;
            if (dy < MaxJumpHeight * 0.5 && dy > -200 * S && Math.Abs(mx - px) < 900 * D)
            {
                climbRight = mx < px;                     // "duvardan uzağa" = imlece doğru
                curtain = 0;
                WallPounce(mx, my);
                return;
            }
        }
        if (stateTime >= stateLength) { if (Rng.Next(3) == 0) Voice.Meow(Config.Pitch); LetGoOfCurtain(); return; }
        if (py >= top) return;                            // tepede asılı
        climbPhase += dt * 1.6 * Config.Speed;
        double push = Math.Pow(Math.Max(0, Math.Sin(ClimbCycle * 2 * Math.PI)), 1.5);
        py = Math.Min(top, py + (80 + 700 * push) * S * Config.Speed * dt);
    }

    void LetGoOfCurtain()
    {
        curtain = 0;
        Drop(0);
    }
}

