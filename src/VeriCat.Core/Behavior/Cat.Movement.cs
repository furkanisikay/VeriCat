using VeriCat.Core.Geometry;

namespace VeriCat.Core.Behavior;

// Yürüme, zıplama, düşme ve iniş.
public sealed partial class Cat
{
    Platform? platform;
    RectU? riding;                                    // üstünde durduğu pencerenin son çerçevesi
    double jx, jy;                                    // çömelme bitince uygulanacak zıplama hızı

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
        if (riding is RectU old) px += now.MinX - old.MinX;
        riding = now;
        // Üst kenar ya da iç bölüm: aynı yüzeyin güncel hâlini bul (pencere boyutlanınca iç bölüm de kayabilir).
        foreach (var seg in World.Platforms)
        {
            if (seg.SameSurface(p) && seg.Covers(px, 2)) { py = seg.Y; platform = seg; return true; }
        }
        return false;
    }

    /// <summary>Yürür. Duvara ya da kenara takılıp döndüyse false.</summary>
    bool Stride(double dt, double speed, bool mayDrop)
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
        if (mayDrop && Rng.Next(3) == 0) { Drop(dir * speed); return true; }
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
        double ceiling = World.TopY - (SpriteHeight - SpriteGround) * s;
        if (py > ceiling) { py = ceiling; vy = Math.Min(vy, 0); }
        // Görev çubuğu bölgesine bırakıldıysa zemine çıkar.
        if (vy <= 0 && World.FloorAt(px, py) is Platform f && py < f.Y) { py = f.Y; Land(f, -vy); }
        else if (py < World.BottomY - 400 * D) { px = (World.MinX + World.MaxX) / 2; py = ceiling; vx = vy = 0; }
    }

    void Land(Platform p, double impactSpeed)
    {
        platform = p;
        riding = World.Frames.TryGetValue(p.Owner, out var r) ? r : null;
        vx = vy = 0;
        bool wasPouncing = pouncing;
        pouncing = false;
        if (wasPouncing && Settings.Chase && Rng.NextDouble() < 0.5) Set(CatState.Chase, 2, 4);
        else if (impactSpeed > 1500 * D) Set(CatState.Sit, 1.2, 2.2);
        else Set(CatState.Sit, 0.3, 1.0);
    }

    /// <summary>Çizim kutusunun temel birimdeki boyutları (Painter ile aynı).</summary>
    public const double SpriteWidth = 150, SpriteHeight = 135, SpriteGround = 6;
}
