using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace VeriCat.Desktop.Theming;

internal enum Glyph
{
    Paw, Moon, Plus, Palette, Resize, Window, Layers, Target, Fist, Tag, Sound, Power,
    Update, Info, Exit, Heart, Dice, EyeOff, Close, Sparkle,
    Bowl, Yarn, Bug, Bulb, Broom,
}

/// <summary>Menüler için çizgi tarzı vektör ikonlar. 16x16'lık kutuda tanımlı, istenen boyda çizilir ve önbelleğe alınır.</summary>
internal static class Icons
{
    static readonly Dictionary<(Glyph, int, int), Bitmap> Cache = new();

    public static Bitmap Get(Glyph glyph, Color color, int size)
    {
        var key = (glyph, color.ToArgb(), size);
        if (Cache.TryGetValue(key, out var bmp)) return bmp;
        bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(size / 16f, size / 16f);
            using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(color);
            Draw(g, glyph, pen, brush);
        }
        Cache[key] = bmp;
        return bmp;
    }

    static void Draw(Graphics g, Glyph glyph, Pen p, Brush b)
    {
        switch (glyph)
        {
            case Glyph.Paw:
                g.FillEllipse(b, 4.5f, 7.5f, 7, 6);
                foreach (var (x, y) in new[] { (2.2f, 5f), (5f, 2.5f), (8.6f, 2.5f), (11.4f, 5f) }) g.FillEllipse(b, x, y, 2.6f, 3.2f);
                break;
            case Glyph.Moon:
                using (var path = new GraphicsPath())
                {
                    path.AddArc(2.5f, 2.5f, 11, 11, 300, 280);
                    path.AddBezier(9.3f, 12.9f, 5.5f, 11.5f, 5f, 5f, 10.5f, 3f);
                    g.DrawPath(p, path);
                }
                break;
            case Glyph.Plus:
                g.DrawLine(p, 8, 3, 8, 13); g.DrawLine(p, 3, 8, 13, 8);
                break;
            case Glyph.Palette:
                g.DrawEllipse(p, 2, 2, 12, 12);
                foreach (var (x, y) in new[] { (5f, 5f), (8.5f, 4f), (11f, 7f) }) g.FillEllipse(b, x - 1, y - 1, 2, 2);
                g.FillEllipse(b, 5, 9, 3, 3);
                break;
            case Glyph.Resize:
                g.DrawLine(p, 3, 13, 13, 3); g.DrawLine(p, 8, 3, 13, 3); g.DrawLine(p, 13, 3, 13, 8);
                g.DrawLine(p, 3, 8, 3, 13); g.DrawLine(p, 3, 13, 8, 13);
                break;
            case Glyph.Window:
                g.DrawRectangle(p, 2, 3, 12, 10); g.DrawLine(p, 2, 6, 14, 6);
                break;
            case Glyph.Layers:
                g.DrawRectangle(p, 2, 2, 12, 12); g.DrawLine(p, 2, 6, 14, 6); g.DrawLine(p, 5, 10, 11, 10);
                break;
            case Glyph.Target:
                g.DrawEllipse(p, 2, 2, 12, 12); g.DrawEllipse(p, 5.5f, 5.5f, 5, 5); g.FillEllipse(b, 7, 7, 2, 2);
                break;
            case Glyph.Fist:
                using (var path = new GraphicsPath())
                {
                    path.AddArc(3, 4, 10, 8, 180, 180);
                    path.AddLine(13, 8, 13, 11);
                    path.AddArc(9, 9, 4, 4, 0, 90);
                    path.AddLine(11, 13, 5, 13);
                    path.AddArc(3, 9, 4, 4, 90, 90);
                    path.CloseFigure();
                    g.DrawPath(p, path);
                }
                g.DrawLine(p, 6.3f, 4.5f, 6.3f, 7.5f); g.DrawLine(p, 9.6f, 4.5f, 9.6f, 7.5f);
                break;
            case Glyph.Tag:
                g.DrawPolygon(p, new PointF[] { new(2, 8), new(7, 3), new(14, 3), new(14, 13), new(7, 13) });
                g.FillEllipse(b, 9.5f, 7, 2, 2);
                break;
            case Glyph.Sound:
                g.DrawPolygon(p, new PointF[] { new(2, 6), new(5, 6), new(9, 3), new(9, 13), new(5, 10), new(2, 10) });
                g.DrawArc(p, 8, 5, 5, 6, -60, 120);
                g.DrawArc(p, 8, 3, 7, 10, -60, 120);
                break;
            case Glyph.Power:
                g.DrawArc(p, 2.5f, 3, 11, 11, -60, 300); g.DrawLine(p, 8, 1.5f, 8, 7.5f);
                break;
            case Glyph.Update:
                g.DrawArc(p, 2.5f, 2.5f, 11, 11, 20, 290);
                g.DrawLine(p, 13.3f, 3, 13.3f, 7); g.DrawLine(p, 13.3f, 7, 9.5f, 7);
                break;
            case Glyph.Info:
                g.DrawEllipse(p, 2, 2, 12, 12); g.DrawLine(p, 8, 7.5f, 8, 11); g.FillEllipse(b, 7.1f, 4.3f, 1.8f, 1.8f);
                break;
            case Glyph.Exit:
                g.DrawLines(p, new PointF[] { new(9, 3), new(3, 3), new(3, 13), new(9, 13) });
                g.DrawLine(p, 7, 8, 14, 8); g.DrawLine(p, 11.5f, 5.5f, 14, 8); g.DrawLine(p, 11.5f, 10.5f, 14, 8);
                break;
            case Glyph.Heart:
                using (var path = new GraphicsPath())
                {
                    path.AddBezier(8, 13.5f, 1, 9, 1.5f, 3, 5, 3);
                    path.AddBezier(5, 3, 6.8f, 3, 8, 4.5f, 8, 5.5f);
                    path.AddBezier(8, 5.5f, 8, 4.5f, 9.2f, 3, 11, 3);
                    path.AddBezier(11, 3, 14.5f, 3, 15, 9, 8, 13.5f);
                    g.DrawPath(p, path);
                }
                break;
            case Glyph.Dice:
                g.DrawRectangle(p, 2.5f, 2.5f, 11, 11);
                foreach (var (x, y) in new[] { (5.5f, 5.5f), (10.5f, 10.5f), (8f, 8f), (10.5f, 5.5f), (5.5f, 10.5f) }) g.FillEllipse(b, x - 1, y - 1, 2, 2);
                break;
            case Glyph.EyeOff:
                using (var path = new GraphicsPath())
                {
                    path.AddBezier(1.5f, 8, 4, 3.5f, 12, 3.5f, 14.5f, 8);
                    path.AddBezier(14.5f, 8, 12, 12.5f, 4, 12.5f, 1.5f, 8);
                    g.DrawPath(p, path);
                }
                g.DrawEllipse(p, 6, 6, 4, 4); g.DrawLine(p, 2.5f, 2.5f, 13.5f, 13.5f);
                break;
            case Glyph.Close:
                g.DrawLine(p, 4, 4, 12, 12); g.DrawLine(p, 12, 4, 4, 12);
                break;
            case Glyph.Bowl:
                g.DrawLine(p, 2, 8, 14, 8);
                using (var path = new GraphicsPath())
                {
                    path.AddBezier(2.5f, 8, 3, 12.5f, 13, 12.5f, 13.5f, 8);
                    g.DrawPath(p, path);
                }
                foreach (var (x, y) in new[] { (5.5f, 6.2f), (8f, 5.4f), (10.5f, 6.2f) }) g.FillEllipse(b, x - 1.2f, y - 1.2f, 2.4f, 2.4f);
                break;
            case Glyph.Yarn:
                g.DrawEllipse(p, 2.5f, 2.5f, 10, 10);
                g.DrawArc(p, 0.5f, 4.5f, 12, 9, 290, 120);
                g.DrawArc(p, 4.5f, 0.5f, 9, 12, 160, 120);
                g.DrawBezier(p, 12, 10.5f, 13.5f, 12, 14, 13.5f, 15, 14.5f);
                break;
            case Glyph.Bug:
                g.DrawEllipse(p, 4.5f, 5, 7, 9);
                g.DrawLine(p, 8, 7, 8, 13.5f);
                g.DrawArc(p, 5.5f, 2, 5, 5, 200, 140);
                foreach (var y in new[] { 7f, 10f, 12.5f })
                {
                    g.DrawLine(p, 4.5f, y, 2, y - 1);
                    g.DrawLine(p, 11.5f, y, 14, y - 1);
                }
                break;
            case Glyph.Bulb:
                g.DrawArc(p, 3.5f, 1.5f, 9, 9, 150, 240);
                g.DrawLine(p, 5.3f, 9.5f, 6, 11.5f);
                g.DrawLine(p, 10.7f, 9.5f, 10, 11.5f);
                g.DrawLine(p, 6, 11.5f, 10, 11.5f);
                g.DrawLine(p, 6.5f, 14, 9.5f, 14);
                break;
            case Glyph.Broom:
                g.DrawLine(p, 12.5f, 2, 7.5f, 9);
                g.DrawPolygon(p, new PointF[] { new(6, 8), new(9.5f, 10.5f), new(7, 14.5f), new(2, 13) });
                g.DrawLine(p, 4.5f, 11.5f, 3.5f, 13.3f);
                break;
            case Glyph.Sparkle:
                using (var path = new GraphicsPath())
                {
                    path.AddBezier(8, 1.5f, 8.6f, 6, 10, 7.4f, 14.5f, 8);
                    path.AddBezier(14.5f, 8, 10, 8.6f, 8.6f, 10, 8, 14.5f);
                    path.AddBezier(8, 14.5f, 7.4f, 10, 6, 8.6f, 1.5f, 8);
                    path.AddBezier(1.5f, 8, 6, 7.4f, 7.4f, 6, 8, 1.5f);
                    g.FillPath(b, path);
                }
                break;
        }
    }
}
