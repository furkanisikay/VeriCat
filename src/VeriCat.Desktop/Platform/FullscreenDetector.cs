using System.Text;
using static VeriCat.Desktop.Native.NativeMethods;

namespace VeriCat.Desktop.Platform;

/// <summary>Öndeki pencere bir ekranı tamamen kaplıyor mu? (Oyun, video, sunum.)</summary>
internal sealed class FullscreenDetector
{
    readonly uint pid = (uint)Environment.ProcessId;
    readonly StringBuilder className = new(64);

    public bool IsForegroundFullscreen()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero || h == GetShellWindow() || h == GetDesktopWindow()) return false;
        GetWindowThreadProcessId(h, out uint owner);
        if (owner == pid) return false;
        className.Clear();
        GetClassName(h, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!GetWindowRect(h, out RECT r)) return false;
        var screen = Screen.FromHandle(h).Bounds;
        return r.Left <= screen.Left && r.Top <= screen.Top && r.Right >= screen.Right && r.Bottom >= screen.Bottom;
    }
}
