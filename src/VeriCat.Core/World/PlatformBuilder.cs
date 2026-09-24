using VeriCat.Core.Geometry;

namespace VeriCat.Core.World;

/// <summary>Ekran ve pencere anlık görüntülerinden kedilerin basabileceği platformları üretir.</summary>
public static class PlatformBuilder
{
    /// <summary>Bir pencere için en fazla bu kadar iç platform üretilir.</summary>
    public const int MaxInnerPerWindow = 24;

    /// <param name="screens">Ekranlar.</param>
    /// <param name="windowsFrontToBack">Pencereler, öndeki önce gelecek şekilde (EnumWindows sırası).</param>
    /// <param name="dpi">1 mantıksal nokta kaç piksel.</param>
    /// <param name="includeInner">Pencerelerin iç bölümleri de platform olsun mu.</param>
    public static List<Platform> Build(
        IReadOnlyList<ScreenSnapshot> screens,
        IReadOnlyList<WindowSnapshot> windowsFrontToBack,
        double dpi,
        bool includeInner)
    {
        var result = new List<Platform>();
        for (int i = 0; i < screens.Count; i++)
        {
            var s = screens[i];
            result.Add(new Platform(s.WorkBottom, s.Bounds.MinX, s.Bounds.MaxX, -1 - i));
        }

        double topMost = screens.Count > 0 ? screens.Max(s => s.WorkTop) : double.MaxValue;
        var front = new List<RectU>();

        foreach (var w in windowsFrontToBack)
        {
            var rect = w.Frame;
            double top = rect.MaxY;
            double screenTop = ScreenTopAt(screens, rect.MidX, top - 1, topMost);

            // Üst kenar: ekranın tepesine yapışık (tam ekran/maksimize) pencerelerde anlamsız.
            if (top < screenTop - 12 * dpi)
                AddVisible(result, screens, front, top, rect.MinX + 10 * dpi, rect.MaxX - 10 * dpi, w.Handle, 0, 40 * dpi);

            if (includeInner) AddInner(result, screens, front, w, screenTop, dpi);

            front.Add(rect);
        }
        return result;
    }

    /// <summary>Pencerenin içindeki alt bölümlerin üst kenarları (raf gibi).</summary>
    static void AddInner(List<Platform> result, IReadOnlyList<ScreenSnapshot> screens, List<RectU> front, WindowSnapshot w, double screenTop, double dpi)
    {
        var rect = w.Frame;
        var added = new List<Platform>();
        foreach (var child in w.Children)
        {
            if (added.Count >= MaxInnerPerWindow) break;
            double y = child.Frame.MaxY;
            // Başlık çubuğuna ya da pencerenin dibine çok yakın kenarlar raf sayılmaz.
            if (y > rect.MaxY - 36 * dpi || y < rect.MinY + 30 * dpi || y >= screenTop - 12 * dpi) continue;

            double lo = Math.Max(child.Frame.MinX, rect.MinX) + 8 * dpi;
            double hi = Math.Min(child.Frame.MaxX, rect.MaxX) - 8 * dpi;
            if (hi - lo < 60 * dpi) continue;

            // Aynı yükseklikte zaten bir raf varsa (iç içe paneller) tekrar ekleme.
            if (added.Any(p => Math.Abs(p.Y - y) < 4 * dpi && Overlap(p.MinX, p.MaxX, lo, hi) > 0.5 * Math.Min(p.Width, hi - lo)))
                continue;

            int before = result.Count;
            AddVisible(result, screens, front, y, lo, hi, w.Handle, child.Handle, 60 * dpi);
            for (int i = before; i < result.Count; i++) added.Add(result[i]);
        }
    }

    /// <summary>Ekran dışında kalan ve öndeki pencerelerin örttüğü parçaları atıp kalanları ekler.</summary>
    static void AddVisible(List<Platform> result, IReadOnlyList<ScreenSnapshot> screens, List<RectU> front,
        double y, double lo, double hi, long owner, long part, double minWidth)
    {
        var segs = ClipToScreens(lo, hi, y, screens);
        foreach (var o in front)
            if (o.MinY < y && o.MaxY > y + 1) segs = Segments.Subtract(segs, o.MinX, o.MaxX);
        foreach (var (a, b) in segs)
            if (b - a > minWidth) result.Add(new Platform(y, a, b, owner, part));
    }

    /// <summary>
    /// [lo, hi] aralığının y yüksekliğinde bir ekranın içinde kalan kısımları. Yan yana ekranlar birleştirilir;
    /// pencerenin ekran dışına taşan kenarı kediye yol olmaz.
    /// </summary>
    internal static List<(double Lo, double Hi)> ClipToScreens(double lo, double hi, double y, IReadOnlyList<ScreenSnapshot> screens)
    {
        var parts = screens
            .Where(s => y >= s.Bounds.MinY && y < s.Bounds.MaxY)
            .Select(s => (Lo: Math.Max(lo, s.Bounds.MinX), Hi: Math.Min(hi, s.Bounds.MaxX)))
            .Where(p => p.Hi > p.Lo)
            .OrderBy(p => p.Lo)
            .ToList();
        var merged = new List<(double Lo, double Hi)>();
        foreach (var p in parts)
        {
            if (merged.Count > 0 && p.Lo <= merged[^1].Hi + 1) merged[^1] = (merged[^1].Lo, Math.Max(merged[^1].Hi, p.Hi));
            else merged.Add(p);
        }
        return merged;
    }

    static double ScreenTopAt(IReadOnlyList<ScreenSnapshot> screens, double x, double y, double fallback)
    {
        foreach (var s in screens) if (s.Bounds.Contains(x, y)) return s.WorkTop;
        return fallback;
    }

    static double Overlap(double a1, double b1, double a2, double b2) => Math.Max(0, Math.Min(b1, b2) - Math.Max(a1, a2));
}
