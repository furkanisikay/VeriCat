using VeriCat.Core.Props;
using VeriCat.Desktop.Rendering;

namespace VeriCat.Desktop.Views;

/// <summary>Bir eşyayı (yumak, mama kabı) gösteren saydam pencere. Tutup fırlatılabilir; sağ tıkla menüsü açılır.</summary>
internal sealed class PropWindow : LayeredWindow, IPropView
{
    readonly ICatHost host;
    readonly double dpi;
    Prop? prop;

    public PropWindow(ICatHost host, double dpi)
    {
        this.host = host;
        this.dpi = dpi;
    }

    public void Attach(Prop p) => prop = p;

    public void Present(Prop p)
    {
        var (w, h) = PropPainter.BoxSize(p, dpi);
        int left = (int)Math.Round(p.X - w / 2.0);
        int top = (int)Math.Round(-p.Y) - h;
        Blit(left, top, w, h, g => PropPainter.Draw(g, p, w, h, dpi));
    }

    void IPropView.Close()
    {
        Close();
        Dispose();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (prop == null) return;
        if (e.Button == MouseButtons.Right) { host.ShowPropMenu(prop); return; }
        if (e.Button != MouseButtons.Left) return;
        if (e.Clicks == 2 && prop.Kind == PropKind.Bowl) { host.FillBowl(prop); return; }   // çift tık: mamayı doldur
        Capture = true;
        var (x, y) = PointerUp();
        prop.Grab(x, y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (prop == null || e.Button != MouseButtons.Left) return;
        var (x, y) = PointerUp();
        prop.Drag(x, y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (prop == null || e.Button != MouseButtons.Left) return;
        Capture = false;
        prop.Release();
    }
}
