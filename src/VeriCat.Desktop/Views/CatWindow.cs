using VeriCat.Core.Abstractions;
using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Core.Rendering;
using VeriCat.Desktop.Rendering;

namespace VeriCat.Desktop.Views;

/// <summary>Bir kediyi gösteren saydam pencere. Tutup fırlatma, tıklama, okşama ve sağ tık menüsü buradan gelir.</summary>
internal sealed class CatWindow : LayeredWindow, ICatView
{
    readonly ICatHost host;
    readonly AppSettings settings;
    readonly double dpi;
    Cat? cat;
    Coat? coat;
    int coatVersion = -1;
    Point lastMove;

    public CatWindow(ICatHost host, AppSettings settings, double dpi)
    {
        this.host = host;
        this.settings = settings;
        this.dpi = dpi;
    }

    public void Attach(Cat c) => cat = c;

    // MARK: ICatView

    public void Present(double feetX, double feetY, double scale, Sprite sprite)
    {
        if (IsDisposed || cat == null) return;
        if (coat == null || coatVersion != cat.AppearanceVersion)
        {
            coat = Coat.From(cat.Config);
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
