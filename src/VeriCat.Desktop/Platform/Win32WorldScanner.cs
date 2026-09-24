using System.Runtime.InteropServices;
using System.Text;
using VeriCat.Core.Geometry;
using VeriCat.Core.World;
using static VeriCat.Desktop.Native.NativeMethods;

namespace VeriCat.Desktop.Platform;

/// <summary>Ekranları ve görünür pencereleri (isteğe bağlı olarak iç bölümleriyle) Win32 üzerinden okur.</summary>
internal sealed class Win32WorldScanner
{
    /// <summary>İç bölümleri taranacak en öndeki pencere sayısı (maliyeti sınırlamak için).</summary>
    const int MaxWindowsWithChildren = 10;
    const int MaxChildVisits = 400, MaxChildren = 200;

    readonly uint pid = (uint)Environment.ProcessId;
    readonly double dpi;
    readonly StringBuilder className = new(64);

    public Win32WorldScanner(double dpi) => this.dpi = dpi;

    public List<ScreenSnapshot> Screens() => Screen.AllScreens.Select(s => new ScreenSnapshot(
        RectU.FromScreen(s.Bounds.Left, s.Bounds.Top, s.Bounds.Right, s.Bounds.Bottom),
        -(double)s.WorkingArea.Top,
        -(double)s.WorkingArea.Bottom)).ToList();

    /// <summary>Pencereler, öndeki önce (EnumWindows z-sırası).</summary>
    public List<WindowSnapshot> Windows(bool includeChildren)
    {
        var result = new List<WindowSnapshot>();
        EnumWindows((h, _) =>
        {
            if (!Candidate(h, out var rect)) return true;
            var children = includeChildren && result.Count < MaxWindowsWithChildren
                ? CachedChildren(h, rect, Now)
                : (IReadOnlyList<ChildSnapshot>)Array.Empty<ChildSnapshot>();
            result.Add(new WindowSnapshot((long)h, rect, children));
            return true;
        }, IntPtr.Zero);

        // Kapanan pencerelerin önbelleğini at.
        if (childCache.Count > result.Count * 2)
        {
            var alive = result.Select(w => (IntPtr)w.Handle).ToHashSet();
            foreach (var k in childCache.Keys.Where(k => !alive.Contains(k)).ToList()) childCache.Remove(k);
        }
        return result;
    }

    /// <summary>
    /// Alt pencere taraması en pahalı kısım. Pencere yerinden oynamadıysa sonuç 0.5 sn boyunca yeniden kullanılır;
    /// böylece durağan bir masaüstünde tarama maliyeti ~7 kat düşer.
    /// </summary>
    IReadOnlyList<ChildSnapshot> CachedChildren(IntPtr h, RectU frame, long now)
    {
        if (childCache.TryGetValue(h, out var c) && c.Frame == frame && now - c.At < ChildCacheMs) return c.Children;
        var kids = Children(h);
        childCache[h] = (frame, kids, now);
        return kids;
    }

    const long ChildCacheMs = 500;
    readonly Dictionary<IntPtr, (RectU Frame, List<ChildSnapshot> Children, long At)> childCache = new();
    static long Now => Environment.TickCount64;

    /// <summary>Kedinin üstüne çıkabileceği normal, görünür bir üst düzey pencere mi?</summary>
    bool Candidate(IntPtr h, out RectU rect)
    {
        rect = default;
        if (!IsWindowVisible(h) || IsIconic(h)) return false;
        GetWindowThreadProcessId(h, out uint owner);
        if (owner == pid) return false;
        long ex = (long)GetWindowLongPtr(h, GWL_EXSTYLE);
        if ((ex & WS_EX_TOOLWINDOW) != 0) return false;
        if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) return false;
        className.Clear();
        GetClassName(h, className, className.Capacity);
        if (className.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW") return false;
        // Görünen çerçeve (Windows 10/11'deki görünmez yeniden boyutlandırma kenarları hariç)
        if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rc, Marshal.SizeOf<RECT>()) != 0)
            GetWindowRect(h, out rc);
        rect = RectU.FromScreen(rc.Left, rc.Top, rc.Right, rc.Bottom);
        return rect.Width > 120 * dpi && rect.Height > 50 * dpi;
    }

    /// <summary>
    /// Pencerenin görünür alt pencereleri (araç çubukları, paneller, listeler…). Klasik Win32 uygulamalarında zengindir;
    /// Chromium/UWP gibi kendi çizen uygulamalarda boş dönebilir, o durumda sadece üst kenar kullanılır.
    /// </summary>
    List<ChildSnapshot> Children(IntPtr parent)
    {
        var list = new List<ChildSnapshot>();
        int visited = 0;
        EnumChildWindows(parent, (h, _) =>
        {
            if (++visited > MaxChildVisits) return false;
            if (!IsWindowVisible(h) || !GetWindowRect(h, out RECT rc)) return true;
            var r = RectU.FromScreen(rc.Left, rc.Top, rc.Right, rc.Bottom);
            if (r.Width >= 90 * dpi && r.Height >= 16 * dpi) list.Add(new ChildSnapshot((long)h, r));
            return list.Count < MaxChildren;
        }, IntPtr.Zero);
        return list;
    }
}
