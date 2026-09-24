using System.Drawing.Drawing2D;
using VeriCat.Desktop.Native;

namespace VeriCat.Desktop.Theming;

/// <summary>Sağında açma/kapama anahtarı olan menü öğesi. Tıklanınca menü kapanmaz.</summary>
internal sealed class ToggleMenuItem : ToolStripMenuItem
{
    readonly Func<bool> get;
    readonly Action<bool> set;

    public ToggleMenuItem(string text, Image? image, Func<bool> get, Action<bool> set) : base(text, image)
    {
        this.get = get;
        this.set = set;
    }

    float K => Owner?.DeviceDpi / 96f ?? 1;

    public override Size GetPreferredSize(Size constrainingSize)
    {
        var s = base.GetPreferredSize(constrainingSize);
        return new Size(s.Width + (int)(46 * K), s.Height);
    }

    protected override void OnClick(EventArgs e)
    {
        set(!get());
        Invalidate();
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var p = Theme.Current;
        bool on = get();
        float k = K, w = 30 * k, h = 17 * k;
        var r = new RectangleF(Width - w - 14 * k, (Height - h) / 2, w, h);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var track = MenuRenderer.RoundRect(r, h / 2))
        using (var b = new SolidBrush(on ? p.Accent : p.Track))
            g.FillPath(b, track);
        float knob = h - 5 * k;
        float x = on ? r.Right - knob - 2.5f * k : r.X + 2.5f * k;
        using var kb = new SolidBrush(on ? p.AccentText : p.Surface);
        g.FillEllipse(kb, x, r.Y + 2.5f * k, knob, knob);
    }
}

/// <summary>Kedi menüsünün başlığı: avatar, isim ve o anki hâli.</summary>
internal sealed class MenuHeader : ToolStripItem
{
    readonly Image avatar;
    readonly string title, subtitle;
    readonly Color ring;

    public MenuHeader(Image avatar, string title, string subtitle, Color ring)
    {
        this.avatar = avatar;
        this.title = title;
        this.subtitle = subtitle;
        this.ring = ring;
    }

    public override bool CanSelect => false;

    float K => Owner?.DeviceDpi / 96f ?? 1;

    /// <summary>Avatar bu öğeye aittir; öğeyle birlikte atılır.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing) avatar.Dispose();
        base.Dispose(disposing);
    }

    public override Size GetPreferredSize(Size constrainingSize)
    {
        float k = K;
        int text = Math.Max(TextRenderer.MeasureText(title, Theme.UiBold).Width, TextRenderer.MeasureText(subtitle, Theme.UiSmall).Width);
        return new Size((int)(64 * k) + text, (int)(50 * k));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        float k = K, d = 36 * k, x = 12 * k, y = (Height - d) / 2;
        using (var bg = new SolidBrush(p.AccentSoft)) g.FillEllipse(bg, x, y, d, d);
        using (var pen = new Pen(ring, 2 * k)) g.DrawEllipse(pen, x, y, d, d);
        g.DrawImage(avatar, x + 3 * k, y + 3 * k, d - 6 * k, d - 6 * k);
        float tx = x + d + 10 * k;
        TextRenderer.DrawText(g, title, Theme.UiBold, new Point((int)tx, (int)(Height / 2 - 18 * k)), p.Text, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, subtitle, Theme.UiSmall, new Point((int)tx, (int)(Height / 2 + 1 * k)), p.Subtle, TextFormatFlags.NoPadding);
    }
}

/// <summary>Menüleri temaya bağlar ve öğe üretmeyi kolaylaştırır.</summary>
internal static class MenuStyle
{
    public static int IconSize(Control c) => (int)Math.Round(16 * c.DeviceDpi / 96f);

    /// <summary>Menüye (ve alt menülerine) tema, yazı tipi, yuvarlak köşe ve "anahtar tıklanınca kapanma" davranışı uygular.</summary>
    public static T Apply<T>(T menu) where T : ToolStripDropDownMenu
    {
        menu.Renderer = new MenuRenderer();
        menu.Font = Theme.UiFont;
        menu.ShowCheckMargin = false;
        menu.ShowImageMargin = true;
        menu.Padding = new Padding(2, 6, 2, 6);
        menu.ImageScalingSize = new Size(IconSize(menu), IconSize(menu));
        menu.HandleCreated += (_, _) => RoundCorners(menu.Handle);

        bool keepOpen = false;
        menu.ItemClicked += (_, e) => keepOpen = e.ClickedItem is ToggleMenuItem;
        menu.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && keepOpen) e.Cancel = true;
            keepOpen = false;
        };

        StyleSubmenus(menu.Items);
        return menu;
    }

    /// <summary>Sonradan eklenen öğelerin alt menülerini de temaya bağlar.</summary>
    public static void StyleSubmenus(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
            if (item is ToolStripMenuItem { HasDropDownItems: true } sub && sub.DropDown is ToolStripDropDownMenu dd && dd.Renderer is not MenuRenderer)
                Apply(dd);
    }

    static void RoundCorners(IntPtr handle)
    {
        int pref = NativeMethods.DWMWCP_ROUNDSMALL;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));   // Windows 10'da etkisiz
    }

    public static ToolStripMenuItem Item(string text, Glyph glyph, int size, Action onClick, MenuTag? tag = null)
    {
        var p = Theme.Current;
        var color = tag == MenuTag.Danger ? p.Danger : tag == MenuTag.Highlight ? p.Accent : p.Subtle;
        var item = new ToolStripMenuItem(text, Icons.Get(glyph, color, size), (_, _) => onClick());
        item.Padding = new Padding(2, 3, 2, 3);
        if (tag != null) item.Tag = tag;
        return item;
    }

    public static ToggleMenuItem Toggle(string text, Glyph glyph, int size, Func<bool> get, Action<bool> set) =>
        new(text, Icons.Get(glyph, Theme.Current.Subtle, size), get, set) { Padding = new Padding(2, 3, 2, 3) };

    public static ToolStripMenuItem Submenu(string text, Glyph glyph, int size)
    {
        var item = new ToolStripMenuItem(text, Icons.Get(glyph, Theme.Current.Subtle, size)) { Padding = new Padding(2, 3, 2, 3) };
        return item;
    }

    public static ToolStripLabel Caption(string text) => new(text)
    {
        Font = Theme.UiSmall, ForeColor = Theme.Current.Subtle, Padding = new Padding(10, 4, 0, 2), Enabled = false,
    };
}
