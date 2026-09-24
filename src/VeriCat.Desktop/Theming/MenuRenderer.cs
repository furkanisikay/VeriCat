using System.Drawing.Drawing2D;

namespace VeriCat.Desktop.Theming;

/// <summary>Tepsi ve kedi menülerinin çizimi: temaya uygun zemin, yuvarlak seçim vurgusu, ince ayraçlar.</summary>
internal sealed class MenuRenderer : ToolStripRenderer
{
    static Palette P => Theme.Current;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(P.Background);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(P.Border);
        var r = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        bool highlight = item.Tag is MenuTag.Highlight;
        bool hot = item.Enabled && (item.Selected || item.Pressed);
        if (!hot && !highlight) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = item.Owner?.DeviceDpi / 96f ?? 1;
        var r = new RectangleF(4 * k, 1, item.Width - 8 * k, item.Height - 2);
        using var path = RoundRect(r, 5 * k);
        using var b = new SolidBrush(highlight && !hot ? P.AccentSoft : hot && highlight ? Blend(P.AccentSoft, P.Accent, 0.15f) : P.Hover);
        g.FillPath(b, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item is ToggleMenuItem toggle && ToggleMenuItem.IsReserve(e.Text))
        {
            DrawToggleState(e.Graphics, e.TextRectangle, toggle.IsOn, e.Item.Owner?.DeviceDpi / 96f ?? 1);
            return;
        }
        e.TextColor = !e.Item.Enabled ? P.Subtle
            : e.Item.Tag is MenuTag.Danger ? P.Danger
            : e.Item.Tag is MenuTag.Highlight ? P.Accent
            : P.Text;
        base.OnRenderItemText(e);
    }

    /// <summary>Kısayol sütununa "Açık/Kapalı" yazısı ve anahtar: açıkken vurgu renginde dolu, kapalıyken gri.</summary>
    static void DrawToggleState(Graphics g, Rectangle area, bool on, float k)
    {
        var p = P;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float w = 30 * k, h = 17 * k;
        var r = new RectangleF(area.Right - w, area.Y + (area.Height - h) / 2, w, h);
        using (var track = RoundRect(r, h / 2))
        {
            using var b = new SolidBrush(on ? p.Accent : p.Track);
            g.FillPath(b, track);
        }
        float knob = h - 5 * k;
        float x = on ? r.Right - knob - 2.5f * k : r.X + 2.5f * k;
        using (var kb = new SolidBrush(on ? p.AccentText : p.Surface)) g.FillEllipse(kb, x, r.Y + 2.5f * k, knob, knob);

        var label = new Rectangle(area.X, area.Y, (int)(r.X - area.X - 6 * k), area.Height);
        TextRenderer.DrawText(g, on ? "Açık" : "Kapalı", on ? Theme.UiBold : Theme.UiFont, label, on ? p.Accent : p.Subtle,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        float k = e.Item.Owner?.DeviceDpi / 96f ?? 1;
        int y = e.Item.Height / 2;
        using var pen = new Pen(P.Separator);
        e.Graphics.DrawLine(pen, 12 * k, y, e.Item.Width - 12 * k, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? P.Subtle : P.Text;
        base.OnRenderArrow(e);
    }

    /// <summary>Seçili seçenek (ör. boyut): vurgu renginde nokta.</summary>
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = e.ImageRectangle;
        float d = Math.Min(r.Width, r.Height) * 0.45f;
        using var b = new SolidBrush(P.Accent);
        g.FillEllipse(b, r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}

/// <summary>Menü öğelerinin özel görünüm işaretleri (Tag olarak).</summary>
internal enum MenuTag
{
    /// <summary>Vurgulu (ör. "Güncelleme var").</summary>
    Highlight,
    /// <summary>Yıkıcı işlem (ör. "Bu kediyi kapat").</summary>
    Danger,
}
