using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using static VeriCat.Desktop.Native.NativeMethods;

namespace VeriCat.Desktop.Views;

/// <summary>
/// Kenarlıksız, her pikselinde saydamlık olan, odak çalmayan, her zaman üstte duran pencere.
/// Kediler ve eşyalar bununla çizilir: saydam pikseller fare tıklamalarını alttaki pencerelere geçirir.
/// </summary>
internal abstract class LayeredWindow : Form
{
    IntPtr dib, memDC;
    Bitmap? bmp;
    int bw, bh;

    protected LayeredWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        SetStyle(ControlStyles.Selectable, false);   // tıklayınca odak çalmasın
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE) { m.Result = MA_NOACTIVATE; return; }
        base.WndProc(ref m);
    }

    /// <summary>Resmi çizer ve pencereyi aynı anda yerine koyar (titremesiz).</summary>
    protected void Blit(int left, int top, int w, int h, Action<Graphics> draw)
    {
        if (IsDisposed || w <= 0 || h <= 0) return;
        EnsureBuffer(w, h);
        using (var g = Graphics.FromImage(bmp!))
        {
            g.Clear(Color.Transparent);
            draw(g);
        }
        var dst = new POINT { X = left, Y = top };
        var size = new SIZE { CX = w, CY = h };
        var src = new POINT();
        var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
        IntPtr screen = GetDC(IntPtr.Zero);
        UpdateLayeredWindow(Handle, screen, ref dst, ref size, memDC, ref src, 0, ref blend, ULW_ALPHA);
        ReleaseDC(IntPtr.Zero, screen);
    }

    void EnsureBuffer(int w, int h)
    {
        if (bmp != null && w == bw && h == bh) return;
        FreeBuffer();
        var bi = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = w, biHeight = -h,   // yukarıdan aşağı
            biPlanes = 1, biBitCount = 32,
        };
        dib = CreateDIBSection(IntPtr.Zero, ref bi, 0, out IntPtr bits, IntPtr.Zero, 0);
        memDC = CreateCompatibleDC(IntPtr.Zero);
        SelectObject(memDC, dib);
        bmp = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
        bw = w; bh = h;
    }

    void FreeBuffer()
    {
        bmp?.Dispose(); bmp = null;
        if (memDC != IntPtr.Zero) { DeleteDC(memDC); memDC = IntPtr.Zero; }
        if (dib != IntPtr.Zero) { DeleteObject(dib); dib = IntPtr.Zero; }
    }

    protected override void Dispose(bool disposing)
    {
        FreeBuffer();
        base.Dispose(disposing);
    }

    /// <summary>İmleç konumu, y yukarı koordinatlarda.</summary>
    protected static (double X, double Y) PointerUp()
    {
        var p = Cursor.Position;
        return (p.X, -p.Y);
    }
}
