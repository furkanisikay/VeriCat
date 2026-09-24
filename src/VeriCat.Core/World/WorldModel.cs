using VeriCat.Core.Geometry;

namespace VeriCat.Core.World;

/// <summary>Anlık görüntülerden beslenen <see cref="IWorld"/> uygulaması.</summary>
public sealed class WorldModel : IWorld
{
    readonly Dictionary<long, RectU> frames = new();
    List<(ScreenSnapshot Screen, Platform Floor)> screens = new() { (new ScreenSnapshot(new RectU(0, -1080, 1920, 0), 0, -1040), new Platform(-1040, 0, 1920, -1)) };

    public IReadOnlyList<Platform> Platforms { get; private set; } = Array.Empty<Platform>();
    public IReadOnlyDictionary<long, RectU> Frames => frames;
    public IReadOnlyList<ScreenSnapshot> Screens { get; private set; } = Array.Empty<ScreenSnapshot>();
    public double MinX { get; private set; }
    public double MaxX { get; private set; } = 1920;
    public double TopY { get; private set; }
    public double BottomY { get; private set; } = -1080;

    public Platform? FloorAt(double x, double y)
    {
        foreach (var s in screens) if (s.Screen.Bounds.Contains(x, y)) return s.Floor;
        return null;
    }

    public bool IsOnAnyScreen(double x, double y)
    {
        foreach (var s in screens) if (s.Screen.Bounds.Contains(x, y)) return true;
        return false;
    }

    public (ScreenSnapshot Screen, Platform Floor) ScreenNear(double x, double y)
    {
        (ScreenSnapshot, Platform) best = screens[0];
        double bestD = double.MaxValue;
        foreach (var s in screens)
        {
            var b = s.Screen.Bounds;
            if (b.Contains(x, y)) return s;
            double dx = Math.Max(0, Math.Max(b.MinX - x, x - b.MaxX)), dy = Math.Max(0, Math.Max(b.MinY - y, y - b.MaxY));
            double d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    /// <param name="screens">En az bir ekran.</param>
    /// <param name="windowsFrontToBack">Pencereler (öndeki önce). Pencere modu kapalıysa boş liste verilir.</param>
    /// <param name="dpi">1 mantıksal nokta kaç piksel.</param>
    /// <param name="includeInner">Pencere içlerindeki bölümler de platform olsun mu.</param>
    public void Update(IReadOnlyList<ScreenSnapshot> screens, IReadOnlyList<WindowSnapshot> windowsFrontToBack, double dpi, bool includeInner)
    {
        if (screens.Count == 0) throw new ArgumentException("En az bir ekran gerekli.", nameof(screens));

        MinX = screens.Min(s => s.Bounds.MinX);
        MaxX = screens.Max(s => s.Bounds.MaxX);
        TopY = screens.Max(s => s.WorkTop);
        BottomY = screens.Min(s => s.WorkBottom);

        var platforms = PlatformBuilder.Build(screens, windowsFrontToBack, dpi, includeInner);
        this.screens = screens.Select((s, i) => (s, platforms[i])).ToList();
        Screens = screens;

        frames.Clear();
        foreach (var w in windowsFrontToBack) frames[w.Handle] = w.Frame;
        Platforms = platforms;
    }
}
