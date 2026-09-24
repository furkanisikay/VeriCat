using System.ComponentModel;
using System.Drawing.Drawing2D;
using VeriCat.Desktop.Native;

namespace VeriCat.Desktop.Theming;

/// <summary>Temalı pencereler için ortak ayarlar: renkler, yazı tipi, koyu başlık çubuğu, yuvarlak köşe.</summary>
internal static class ThemedWindow
{
    public static void Apply(Form form)
    {
        var p = Theme.Current;
        form.BackColor = p.Background;
        form.ForeColor = p.Text;
        form.Font = Theme.UiFont;
        form.HandleCreated += (_, _) =>
        {
            int dark = Theme.IsDark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            int round = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        };
    }

    public static float K(Control c) => c.DeviceDpi / 96f;
}

public enum ButtonKind { Accent, Ghost, Danger }

/// <summary>Yuvarlak köşeli, temalı düğme.</summary>
internal sealed class ThemedButton : Button
{
    bool hot, down;

    public ThemedButton(string text, ButtonKind kind = ButtonKind.Ghost)
    {
        Text = text;
        Kind = kind;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        AutoSize = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonKind Kind { get; set; }

    public void FitText()
    {
        float k = ThemedWindow.K(this);
        var s = TextRenderer.MeasureText(Text, Font);
        Size = new Size(s.Width + (int)(28 * k), (int)(32 * k));
    }

    protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot = down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? p.Background);
        float k = ThemedWindow.K(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var (fill, text, border) = Kind switch
        {
            ButtonKind.Accent => (p.Accent, p.AccentText, p.Accent),
            ButtonKind.Danger => (hot ? p.Hover : p.Surface, p.Danger, p.Border),
            _ => (hot ? p.Hover : p.Surface, p.Text, p.Border),
        };
        if (!Enabled) { fill = p.Hover; text = p.Subtle; border = p.Border; }
        else if (Kind == ButtonKind.Accent && (hot || down)) fill = MenuRenderer.Blend(p.Accent, Color.Black, down ? 0.15f : 0.07f);
        using (var path = MenuRenderer.RoundRect(r, 7 * k))
        {
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
            using var pen = new Pen(border);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues)
        {
            using var focus = MenuRenderer.RoundRect(RectangleF.Inflate(r, -2.5f * k, -2.5f * k), 5 * k);
            using var pen = new Pen(Color.FromArgb(120, p.Accent), 1.5f);
            g.DrawPath(pen, focus);
        }
    }
}

/// <summary>Seçilebilir hap düğme (aksesuar, desen, sekme seçimi).</summary>
internal sealed class Chip : Control
{
    bool selected, hot;

    public Chip(string text)
    {
        Text = text;
        Cursor = Cursors.Hand;
        Font = Theme.UiFont;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => selected;
        set { selected = value; Invalidate(); }
    }

    public void FitText()
    {
        float k = ThemedWindow.K(this);
        var s = TextRenderer.MeasureText(Text, Font);
        Size = new Size(s.Width + (int)(22 * k), (int)(28 * k));
    }

    protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? p.Background);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = MenuRenderer.RoundRect(r, r.Height / 2))
        {
            using var b = new SolidBrush(selected ? p.AccentSoft : hot ? p.Hover : p.Surface);
            g.FillPath(b, path);
            using var pen = new Pen(selected ? p.Accent : Focused ? MenuRenderer.Blend(p.Border, p.Accent, 0.5f) : p.Border, selected ? 1.6f : 1f);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), selected ? p.Accent : p.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>0–100 arası değer seçen ince kaydırıcı. Klavye okları ve fare tekerleğiyle de çalışır.</summary>
internal sealed class Slider : Control
{
    int value = 50;
    bool dragging;

    public Slider()
    {
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get; set; } = 100;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int v = Math.Clamp(value, Minimum, Maximum);
            if (v == this.value) return;
            this.value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Kullanıcı sürüklemeyi bıraktı (ör. ses tonunu dinletmek için).</summary>
    public event EventHandler? Committed;
    public event EventHandler? ValueChanged;

    float K => ThemedWindow.K(this);
    float Pad => 9 * K;

    void SetFromX(int x)
    {
        float t = Math.Clamp((x - Pad) / Math.Max(1, Width - 2 * Pad), 0, 1);
        Value = Minimum + (int)Math.Round(t * (Maximum - Minimum));
    }

    protected override void OnMouseDown(MouseEventArgs e) { Focus(); dragging = true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { if (dragging) Committed?.Invoke(this, EventArgs.Empty); dragging = false; base.OnMouseUp(e); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Value += Math.Sign(e.Delta) * Math.Max(1, (Maximum - Minimum) / 20);
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int step = Math.Max(1, (Maximum - Minimum) / 20);
        if (e.KeyCode == Keys.Left) { Value -= step; Committed?.Invoke(this, EventArgs.Empty); }
        if (e.KeyCode == Keys.Right) { Value += step; Committed?.Invoke(this, EventArgs.Empty); }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? p.Background);
        float k = K, h = 4 * k, y = Height / 2f;
        float t = (float)(value - Minimum) / Math.Max(1, Maximum - Minimum);
        float x0 = Pad, x1 = Width - Pad, x = x0 + (x1 - x0) * t;
        using (var track = MenuRenderer.RoundRect(new RectangleF(x0, y - h / 2, x1 - x0, h), h / 2))
        using (var b = new SolidBrush(p.Track)) g.FillPath(b, track);
        if (x > x0)
        {
            using var fill = MenuRenderer.RoundRect(new RectangleF(x0, y - h / 2, x - x0, h), h / 2);
            using var b = new SolidBrush(p.Accent);
            g.FillPath(b, fill);
        }
        float r = 8 * k;
        using (var knob = new SolidBrush(p.Surface)) g.FillEllipse(knob, x - r, y - r, 2 * r, 2 * r);
        using (var pen = new Pen(Focused ? p.Accent : p.Border, Focused ? 2 * k : 1.2f)) g.DrawEllipse(pen, x - r, y - r, 2 * r, 2 * r);
    }
}

/// <summary>Yuvarlak renk seçimi (tasma renkleri).</summary>
internal sealed class ColorDot : Control
{
    bool selected;

    public ColorDot(Color color)
    {
        Color = color;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Color { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => selected;
        set { selected = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? p.Background);
        float w = Math.Min(Width, Height), ring = w * 0.09f, inset = w * 0.2f;
        if (selected)
        {
            using var pen = new Pen(p.Accent, ring);
            g.DrawEllipse(pen, ring, ring, w - 2 * ring, w - 2 * ring);
        }
        using (var b = new SolidBrush(Color)) g.FillEllipse(b, inset, inset, w - 2 * inset, w - 2 * inset);
        using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0))) g.DrawEllipse(pen, inset, inset, w - 2 * inset, w - 2 * inset);
    }
}
