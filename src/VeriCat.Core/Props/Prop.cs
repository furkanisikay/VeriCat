using VeriCat.Core.Abstractions;
using VeriCat.Core.Behavior;
using VeriCat.Core.Geometry;

namespace VeriCat.Core.Props;

public enum PropKind
{
    /// <summary>Yumak: zıplar, yuvarlanır; kediler patiyle vurur.</summary>
    Yarn,
    /// <summary>Mama kabı: aç kediler gelip yer.</summary>
    Bowl,
}

/// <summary>Bir eşyanın ekrandaki görünümü.</summary>
public interface IPropView
{
    void Present(Prop prop);
    void Close();
}

/// <summary>
/// Masaüstündeki fiziksel eşya: yerçekimi, sekme, yuvarlanma sürtünmesi, pencerelerle birlikte kayma,
/// hızlı sallanan pencereden savrulma ve fareyle tutup fırlatma. Kediler gibi ekranın dışına taşmaz.
/// </summary>
public sealed class Prop
{
    readonly CatEnvironment env;
    readonly IPropView view;
    Platform? support;
    RectU? riding;
    double rideChangedAt;
    bool grabbed, dragging;
    double grabOffX, grabOffY, lastDragX, lastDragY, lastDragTime, dragVX, dragVY;
    double lastX = double.NaN, lastY, lastSpin, lastFood = -1;

    public Prop(PropKind kind, CatEnvironment env, IPropView view, double x, double y)
    {
        Kind = kind;
        this.env = env;
        this.view = view;
        X = x; Y = y;
        Color = 0xE0413A;
    }

    public PropKind Kind { get; }
    public double X { get; private set; }
    public double Y { get; private set; }            // alt kenarın ortası (y yukarı)
    public double VX { get; private set; }
    public double VY { get; private set; }
    /// <summary>Yumağın dönüş açısı (radyan), çizim için.</summary>
    public double Spin { get; private set; }
    /// <summary>Mama kabındaki mama, 0…1.</summary>
    public double Food { get; set; }
    /// <summary>Yumak rengi (0xRRGGBB).</summary>
    public uint Color { get; set; }

    public bool IsGrounded => support != null;
    public bool IsHeld => grabbed && dragging;
    public Platform? Support => support;

    double D => env.Dpi;
    double Gravity => 2200 * D;

    /// <summary>Çizim yarıçapı ve çarpışma boyutu (piksel).</summary>
    public double Radius => (Kind == PropKind.Yarn ? 13 : 22) * D;

    /// <summary>Hareket ediyor mu? (Kediler hareketli yumağa daha çok ilgi duyar.)</summary>
    public bool IsMoving => !IsGrounded || Math.Abs(VX) > 20 * D;

    public void Step(double dt)
    {
        if (!grabbed || !dragging)
        {
            if (support != null) Roll(dt);
            else Fall(dt);
            Contain();
        }
        Present();
    }

    void Roll(double dt)
    {
        if (!FollowSupport()) { support = null; riding = null; return; }
        if (VX == 0) return;
        X += VX * dt;
        Spin += VX * dt / Radius;
        // Yumak uzun yuvarlanır, kap hemen durur.
        VX *= Math.Exp(-(Kind == PropKind.Yarn ? 1.1 : 9) * dt);
        if (Math.Abs(VX) < 6 * D) VX = 0;
        if (support is Platform p && (X < p.MinX || X > p.MaxX))
        {
            if (p.IsFloor && env.World.FloorAt(X, Y + 1) is Platform next) { support = next; return; }
            support = null; riding = null;   // kenardan düştü
        }
    }

    /// <summary>Üstünde durduğu pencereyle birlikte kayar; pencere çok hızlı sallanırsa savrulur.</summary>
    bool FollowSupport()
    {
        if (support is not Platform p) return false;
        if (p.IsFloor) return true;
        if (!env.World.Frames.TryGetValue(p.Owner, out var now)) return false;
        double t = env.Clock();
        if (riding is RectU old && (old.MinX != now.MinX || old.MaxY != now.MaxY))
        {
            double dx = now.MinX - old.MinX, dy = now.MaxY - old.MaxY, span = Math.Max(1 / 60.0, t - rideChangedAt);
            X += dx; Y += dy;
            rideChangedAt = t;
            riding = now;
            double wvx = dx / span, wvy = dy / span;
            if (Math.Abs(wvx) > Physics.FlingSpeed * D || wvy > Physics.FlingSpeed * 0.8 * D)
            {
                support = null; riding = null;
                VX = wvx * 0.9; VY = Math.Max(wvy * 0.9, 300 * D);
                return true;
            }
        }
        else if (riding == null) { riding = now; rideChangedAt = t; }
        foreach (var seg in env.World.Platforms)
            if (seg.SameSurface(p) && seg.Covers(X, 2)) { Y = seg.Y; support = seg; return true; }
        return false;
    }

    void Fall(double dt)
    {
        VY -= Gravity * dt;
        double nx = X + VX * dt, ny = Y + VY * dt;
        Spin += VX * dt / Radius;
        if (VY <= 0)
        {
            Platform? best = null;
            foreach (var p in env.World.Platforms)
                if (p.Y <= Y + 1 && p.Y >= ny && nx >= p.MinX && nx <= p.MaxX && (best == null || p.Y > best.Value.Y)) best = p;
            if (best is Platform b) { X = nx; Y = b.Y; Bounce(b); return; }
        }
        X = nx; Y = ny;
        if (VY <= 0 && env.World.FloorAt(X, Y) is Platform f && Y < f.Y) { Y = f.Y; Bounce(f); }
    }

    /// <summary>Yere çarpınca: yumak hızlıysa seker, değilse konar. Kap hiç sekmez.</summary>
    void Bounce(Platform p)
    {
        if (Kind == PropKind.Yarn && -VY > 260 * D)
        {
            VY = -VY * 0.5;
            VX *= 0.85;
            return;
        }
        VY = 0;
        support = p;
        riding = env.World.Frames.TryGetValue(p.Owner, out var r) ? r : null;
        rideChangedAt = env.Clock();
    }

    /// <summary>Ekranın içinde kalır: kenarlardan seker, tepeden geri düşer, hiçbir ekranda değilse en yakınına çekilir.</summary>
    void Contain()
    {
        double r = Radius, probe = Y + r;
        var (screen, floor) = env.World.ScreenNear(X, probe);
        var b = screen.Bounds;
        bool leftOpen = env.World.IsOnAnyScreen(b.MinX - 1, probe), rightOpen = env.World.IsOnAnyScreen(b.MaxX + 1, probe);
        if (!leftOpen && X < b.MinX + r) { X = b.MinX + r; VX = Math.Abs(VX) * 0.6; }
        if (!rightOpen && X > b.MaxX - r) { X = b.MaxX - r; VX = -Math.Abs(VX) * 0.6; }
        double top = screen.WorkTop - 2 * r;
        if (Y > top) { Y = top; VY = Math.Min(VY, 0); }
        if (Y < floor.Y && floor.Covers(X)) { Y = floor.Y; if (support == null) Bounce(floor); }
    }

    /// <summary>Kedinin pati darbesi ya da çarpma.</summary>
    public void Kick(double vx, double vy)
    {
        VX = vx;
        if (vy > 0) { VY = vy; support = null; riding = null; }
    }

    /// <summary>Eşyayı bir noktaya taşır ve serbest bırakır (oradan düşer).</summary>
    public void MoveTo(double x, double y)
    {
        X = x; Y = y; VX = VY = 0;
        support = null; riding = null;
    }

    /// <summary>Bir engele (kediye) çarpıp geri seker: <paramref name="x"/> konumuna itilir, <paramref name="dir"/> yönünde yavaşlayarak döner.</summary>
    public void BounceOff(double x, int dir)
    {
        X = x;
        VX = dir * Math.Abs(VX) * 0.5;
    }

    // MARK: Fareyle tutma / fırlatma

    public void Grab(double mx, double my)
    {
        grabbed = true; dragging = false;
        grabOffX = mx - X; grabOffY = my - Y;
        lastDragX = mx; lastDragY = my; lastDragTime = env.Clock();
        dragVX = dragVY = 0;
    }

    public void Drag(double mx, double my)
    {
        if (!grabbed) return;
        if (!dragging && Math.Abs(mx - lastDragX) + Math.Abs(my - lastDragY) < 4 * D) return;
        dragging = true;
        support = null; riding = null;
        double now = env.Clock(), dt = Math.Max(0.001, now - lastDragTime);
        dragVX = dragVX * 0.5 + (mx - lastDragX) / dt * 0.5;
        dragVY = dragVY * 0.5 + (my - lastDragY) / dt * 0.5;
        lastDragX = mx; lastDragY = my; lastDragTime = now;
        X = mx - grabOffX; Y = my - grabOffY;
        VX = VY = 0;
        Present();
    }

    public void Release()
    {
        if (!grabbed) return;
        grabbed = false;
        if (!dragging) return;
        dragging = false;
        if (env.Clock() - lastDragTime > 0.08) dragVX = dragVY = 0;
        double v = Math.Sqrt(dragVX * dragVX + dragVY * dragVY), max = 2200 * D, k = v > max ? max / v : 1;
        VX = dragVX * k; VY = dragVY * k;
    }

    /// <summary>Yalnızca bir şey değiştiyse çizer: duran bir mama kabı hiç işlemci harcamaz.</summary>
    void Present()
    {
        if (X == lastX && Y == lastY && Spin == lastSpin && Food == lastFood) return;
        lastX = X; lastY = Y; lastSpin = Spin; lastFood = Food;
        view.Present(this);
    }

    public void Dismiss() => view.Close();

    /// <summary>Test kancası: eşyayı bir platforma koyar.</summary>
    internal void PlaceOn(Platform p, double x)
    {
        X = x; Y = p.Y; VX = VY = 0;
        support = p;
        riding = env.World.Frames.TryGetValue(p.Owner, out var r) ? r : null;
        rideChangedAt = env.Clock();
    }
}

/// <summary>Kedi ve eşyaların ortak fizik sabitleri.</summary>
public static class Physics
{
    /// <summary>Pencere bu hızdan (px/sn, 96 DPI'da) hızlı sallanırsa üstündekiler savrulur.</summary>
    public const double FlingSpeed = 2200;
}
