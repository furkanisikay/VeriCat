using VeriCat.Core.Geometry;
using VeriCat.Core.Rendering;

namespace VeriCat.Core.Behavior;

// Yürüme, zıplama, düşme ve iniş.
public sealed partial class Cat
{
    Platform? platform;
    RectU? riding;                                    // üstünde durduğu pencerenin son çerçevesi
    double rideChangedAt;                             // pencerenin en son yer değiştirdiği an (hız için)
    double jx, jy;                                    // çömelme bitince uygulanacak zıplama hızı
    bool flung;                                       // sallanan pencereden savruldu (şaşkın yüz)

    /// <summary>Hızla sallanan pencereden savrulur: şaşırır, bağırır.</summary>
    void Fling(double vxIn, double vyIn)
    {
        if (state == CatState.Fight) opponent = null;
        state = CatState.Air; stateTime = 0;
        vx = vxIn; vy = vyIn;
        platform = null; riding = null;
        flung = true;
        pouncing = false;
        Voice.Meow(Config.Pitch, scared: true);
        ShowEmote(Emote.Surprised, 1.2);
    }

    /// <summary>Şu an bastığı platform (havadaysa null).</summary>
    public Platform? Support => platform;

    /// <summary>Ayağının altında hâlâ bir şey var mı? Pencere taşındıysa onunla birlikte kayar.</summary>
    bool OnSupport()
    {
        if (platform is not Platform p) return false;
        if (p.IsFloor)
        {
            foreach (var f in World.Platforms)
            {
                if (f.Owner != p.Owner || px < f.MinX || px > f.MaxX) continue;
                if (f.Y < py - 1) return false;
                py = f.Y; platform = f;
                return true;
            }
            return false;
        }
        if (!World.Frames.TryGetValue(p.Owner, out var now)) return false;
        if (riding is RectU old && (old.MinX != now.MinX || old.MaxY != now.MaxY))
        {
            // Pencere taşındı: kedi de kayar. Çok hızlı sallandıysa eylemsizlikle savrulur.
            double dx = now.MinX - old.MinX, dy = now.MaxY - old.MaxY;
            double span = Math.Max(1 / 60.0, Now - rideChangedAt);
            px += dx;
            rideChangedAt = Now;
            riding = now;
            double wvx = dx / span, wvy = dy / span;
            if (Math.Abs(wvx) > Props.Physics.FlingSpeed * D || wvy > Props.Physics.FlingSpeed * 0.8 * D)
            {
                Fling(wvx * 0.9, Math.Max(wvy * 0.9, 350 * D));
                return false;
            }
        }
        else if (riding == null) { riding = now; rideChangedAt = Now; }
        // Üst kenar ya da iç bölüm: aynı yüzeyin güncel hâlini bul (pencere boyutlanınca iç bölüm de kayabilir).
        foreach (var seg in World.Platforms)
        {
            if (seg.SameSurface(p) && seg.Covers(px, 2)) { py = seg.Y; platform = seg; return true; }
        }
        return false;
    }

    /// <summary>Yürür. Duvara ya da kenara takılıp döndüyse false. <paramref name="forceDrop"/>: kenardan kesin atlar.</summary>
    bool Stride(double dt, double speed, bool mayDrop, bool forceDrop = false)
    {
        double s = S, dir = Dir;
        px += dir * speed * dt;
        phase += dt * speed / (9 * s);
        double lo = World.MinX + 35 * s, hi = World.MaxX - 35 * s;
        if (px < lo || px > hi) { px = Math.Clamp(px, lo, hi); facingRight = !facingRight; return false; }
        if (platform is not Platform p || (px >= p.MinX && px <= p.MaxX)) return true;
        if (p.IsFloor)
        {
            // Ekran kenarı: yanda başka ekran varsa geçer, yoksa döner.
            if (World.FloorAt(px, py + 1) != null) return true;
            px = Math.Clamp(px, p.MinX, p.MaxX); facingRight = !facingRight;
            return false;
        }
        if (forceDrop || (mayDrop && Rng.Next(3) == 0)) { Drop(dir * speed); return true; }
        px = Math.Clamp(px, p.MinX, p.MaxX); facingRight = !facingRight;
        return false;
    }

    /// <summary>Yakındaki bir pencereye, pencere içindeki bir bölüme ya da zemine balistik bir zıplama planlar.</summary>
    internal bool TryJump()
    {
        double s = S, maxUp = MaxJumpHeight, reach = 450 * D;
        var cur = platform;
        // "Ara ara": zıplamaların bir kısmında pencere içlerindeki raflar da hedef olur.
        bool inner = Settings.InnerWindows && Rng.NextDouble() < 0.4;
        var targets = World.Platforms.Where(p =>
        {
            if (cur is Platform c && c.SameSurface(p) && c.MinX == p.MinX) return false;
            if (p.IsInner && !inner) return false;
            if (p.IsFloor && p.Y >= py - 5) return false;
            if (!FitsUnderScreenTop(p)) return false;
            double dy = p.Y - py, nearest = Math.Clamp(px, p.MinX, p.MaxX);
            return dy < maxUp && dy > -700 * D && p.Width > 50 * s && Math.Abs(nearest - px) < reach;
        }).ToList();
        if (inner && targets.Any(p => p.IsInner)) targets.RemoveAll(p => !p.IsInner);
        if (targets.Count == 0) return false;

        var t = targets[Rng.Next(targets.Count)];
        double lo = Math.Max(t.MinX + 20 * s, px - reach), hi = Math.Min(t.MaxX - 20 * s, px + reach);
        if (lo >= hi) return false;
        double tx = R(lo, hi);
        double apex = Math.Max(py, t.Y) + 40 * D + 30 * s;
        double v = Math.Sqrt(2 * Gravity * (apex - py));
        double time = v / Gravity + Math.Sqrt(2 * (apex - t.Y) / Gravity);
        jx = (tx - px) / time; jy = v;
        facingRight = jx >= 0;
        pouncing = false;
        Set(CatState.Crouch, 99, 99);
        return true;
    }

    double MaxJumpHeight => (260 + 160 * Config.Scale) * D;

    /// <summary>Belirli bir platformdaki belirli bir noktaya zıplamayı dener (hedefe giderken). Erişilemiyorsa false.</summary>
    bool TryJumpTo(Platform t, double x)
    {
        double s = S, reach = 450 * D;
        double dy = t.Y - py;
        if (dy > MaxJumpHeight || dy < -700 * D || !FitsUnderScreenTop(t)) return false;
        double tx = Math.Clamp(x, t.MinX + 20 * s, t.MaxX - 20 * s);
        if (Math.Abs(tx - px) > reach) return false;
        double apex = Math.Max(py, t.Y) + 40 * D + 30 * s;
        double v = Math.Sqrt(2 * Gravity * (apex - py));
        double time = v / Gravity + Math.Sqrt(2 * (apex - t.Y) / Gravity);
        jx = (tx - px) / time; jy = v;
        facingRight = jx >= 0;
        pouncing = false;
        var keep = goal;                               // Crouch'a geçerken hedef unutulmasın
        Set(CatState.Crouch, 99, 99);
        goal = keep;
        return true;
    }

    void Launch()
    {
        state = CatState.Air; stateTime = 0;
        vx = jx; vy = jy;
        platform = null; riding = null;
    }

    void Drop(double speedX)
    {
        if (state == CatState.Fight) opponent = null;
        state = CatState.Air; stateTime = 0;
        vx = speedX; vy = 0;
        platform = null; riding = null;
    }

    void Fly(double dt)
    {
        vy -= Gravity * dt;
        double nx = px + vx * dt, ny = py + vy * dt;
        if (vy <= 0)
        {
            Platform? best = null;
            foreach (var p in World.Platforms)
            {
                if (p.Y <= py + 1 && p.Y >= ny && nx >= p.MinX && nx <= p.MaxX && (best == null || p.Y > best.Value.Y)) best = p;
            }
            if (best is Platform b) { px = nx; py = b.Y; Land(b, -vy); return; }
        }
        px = nx; py = ny;
        if (pouncing) PounceStrike();

        double s = S, lo = World.MinX + 35 * s, hi = World.MaxX - 35 * s;
        if (px < lo) { px = lo; vx = Math.Abs(vx) * 0.4; }
        if (px > hi) { px = hi; vx = -Math.Abs(vx) * 0.4; }
        if (Math.Abs(vx) > 30 * D) facingRight = vx > 0;
        // Görev çubuğu bölgesine bırakıldıysa zemine çıkar.
        if (vy <= 0 && World.FloorAt(px, py) is Platform f && py < f.Y) { py = f.Y; Land(f, -vy); }
        else if (py < World.BottomY - 400 * D)
        {   // güvenlik ağı: hiçbir ekranın içinde değil, en yakın ekranın ortasından yeniden düşer
            var (screen, _) = World.ScreenNear(px, py);
            px = screen.Bounds.MidX; py = screen.WorkTop - Headroom; vx = vy = 0;
        }
    }

    void Land(Platform p, double impactSpeed)
    {
        platform = p;
        riding = World.Frames.TryGetValue(p.Owner, out var r) ? r : null;
        vx = vy = 0;
        rideChangedAt = Now;
        bool wasPouncing = pouncing, wasFlung = flung;
        pouncing = false; flung = false;
        if (goal != Goal.None) { var keep = goal; Set(CatState.Seek, 12, 12); goal = keep; }   // hedefe yürümeye devam
        else if (wasFlung) Set(CatState.Sit, 1.5, 2.5);
        else if (wasPouncing && Settings.Chase && Rng.NextDouble() < 0.5) Set(CatState.Chase, 2, 4);
        else if (impactSpeed > 1500 * D) Set(CatState.Sit, 1.2, 2.2);
        else Set(CatState.Sit, 0.3, 1.0);
    }

    /// <summary>Çizim kutusunun temel birimdeki boyutları (Painter ile aynı).</summary>
    public const double SpriteWidth = 150, SpriteHeight = 135, SpriteGround = 6;
}
