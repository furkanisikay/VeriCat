using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using VeriCat.Core.Abstractions;
using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Core.Rendering;
using VeriCat.Desktop.Rendering;
using static VeriCat.Desktop.Native.NativeMethods;

namespace VeriCat.Desktop.Views;

/// <summary>Kenarlıksız, her pikselinde saydamlık olan, odak çalmayan pencere. Bir kediyi gösterir.</summary>
internal sealed class CatWindow : Form, ICatView
{
    readonly ICatHost host;
    readonly AppSettings settings;
    readonly double dpi;
    Cat? cat;
    Coat? coat;
    int coatVersion = -1;

    IntPtr dib, memDC;
    Bitmap? bmp;
    int bw, bh;
    Point lastMove;

    public CatWindow(ICatHost host, AppSettings settings, double dpi)
    {
        this.host = host;
        this.settings = settings;
        this.dpi = dpi;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        SetStyle(ControlStyles.Selectable, false);   // tıklayınca odak çalmasın
    }

    public void Attach(Cat c) => cat = c;

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

    // MARK: ICatView

    public void Present(double feetX, double feetY, double scale, Sprite sprite)
    {
        if (IsDisposed || cat == null) return;
        if (coat == null || coatVersion != cat.AppearanceVersion)
        {
            coat = Coat.From(cat.Config.Spec, cat.Config.Collar);
            coatVersion = cat.AppearanceVersion;
        }
        int w = (int)Math.Ceiling(CatPainter.BaseW * scale), h = (int)Math.Ceiling(CatPainter.BaseH * scale);
        int left = (int)Math.Round(feetX - CatPainter.BaseW * scale / 2);
        int top = (int)Math.Round(-(feetY - CatPainter.Ground * scale)) - h;
        NameTag? name = settings.ShowNames ? new NameTag(cat.Config.Name, LabelEm(scale)) : null;
        var c = coat;
        Blit(left, top, w, h, g => CatPainter.Draw(g, sprite, c, (float)scale, name: name));
    }

    /// <summary>İsim yazısının boyu: kediyle büyür ama okunaklılık için alt ve üst sınırı var.</summary>
    float LabelEm(double scale) => (float)Math.Clamp(8.5 * scale, 9.5 * dpi, 14 * dpi);

    void ICatView.Close()
    {
        Close();
        Dispose();
    }

    /// <summary>Resmi çizer ve pencereyi aynı anda yerine koyar.</summary>
    void Blit(int left, int top, int w, int h, Action<Graphics> draw)
    {
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

    // MARK: Fare

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (cat == null) return;
        if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Control))) host.ShowCatMenu(cat);
        else if (e.Button == MouseButtons.Left) { Capture = true; cat.Grab(); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (cat == null) return;
        var p = Cursor.Position;
        if (e.Button == MouseButtons.Left) cat.Drag();
        else if (e.Button == MouseButtons.None && lastMove != Point.Empty)
            cat.Pet(Math.Sqrt(Math.Pow(p.X - lastMove.X, 2) + Math.Pow(p.Y - lastMove.Y, 2)));   // okşama
        lastMove = p;
    }

    protected override void OnMouseLeave(EventArgs e) => lastMove = Point.Empty;

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || cat == null) return;
        Capture = false;
        cat.Release();
    }
}
