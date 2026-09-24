using VeriCat.Core.Abstractions;

namespace VeriCat.Desktop.Platform;

/// <summary>Sistem imleci (y yukarı koordinatlara çevrilir).</summary>
internal sealed class CursorPointer : IPointer
{
    public (double X, double Y) Position
    {
        get
        {
            var p = Cursor.Position;
            return (p.X, -p.Y);
        }
    }

    public bool AnyButtonDown => Control.MouseButtons != MouseButtons.None;

    public void Nudge(double dx, double dy)
    {
        var p = Cursor.Position;
        var vs = SystemInformation.VirtualScreen;
        int x = Math.Clamp(p.X + (int)Math.Round(dx), vs.Left, vs.Right - 1);
        int y = Math.Clamp(p.Y - (int)Math.Round(dy), vs.Top, vs.Bottom - 1);
        Cursor.Position = new Point(x, y);
    }
}
