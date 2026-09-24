using System.ComponentModel;
using System.Drawing.Drawing2D;
using VeriCat.Core.Rendering;
using VeriCat.Desktop.Rendering;
using VeriCat.Desktop.Theming;

namespace VeriCat.Desktop.Customization;

/// <summary>Pencerenin üstündeki canlı önizleme: tasmalı, isim etiketli oturan kedi; tıklayınca sevinir.</summary>
internal sealed class PreviewBox : Control
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
    double clock, blink = 2, happyUntil = -1;

    public PreviewBox()
    {
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        timer.Tick += (_, _) =>
        {
            clock += 1.0 / 30;
            blink -= 1.0 / 30;
            if (blink < -0.14) blink = 2 + Random.Shared.NextDouble() * 3;
            Invalidate();
        };
        timer.Start();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Coat? Coat { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? CatName { get; set; }

    public void Cheer() => happyUntil = clock + 2;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var p = Theme.Current;
        g.Clear(Parent?.BackColor ?? p.Background);
        using (var card = CatPainter.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 12))
        {
            using var bg = new SolidBrush(p.Surface);
            g.FillPath(bg, card);
            using var border = new Pen(p.Border);
            g.DrawPath(border, card);
        }
        if (Coat == null) return;
        var f = new Sprite { Pose = Pose.Sit, Clock = (float)clock };
        if (clock < happyUntil) { f.Eyes = EyeKind.Happy; f.Hearts = (float)(2 - (happyUntil - clock)); }
        else f.EyeOpen = blink < 0 ? 0.12f : 1;
        float s = Math.Min(Height / CatPainter.BaseH, Width / CatPainter.BaseW) * 0.95f;
        NameTag? name = string.IsNullOrWhiteSpace(CatName) ? null : new NameTag(CatName, Math.Max(11f, 9 * s));
        CatPainter.Draw(g, f, Coat, s, (Width - CatPainter.BaseW * s) / 2, Height - CatPainter.BaseH * s - 4, name);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}
