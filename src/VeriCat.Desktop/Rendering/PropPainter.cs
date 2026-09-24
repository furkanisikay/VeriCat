using System.Drawing.Drawing2D;
using VeriCat.Core.Props;

namespace VeriCat.Desktop.Rendering;

/// <summary>Eşyaların çizimi: dönen iplik çizgili yumak, içindeki mama miktarı görünen seramik kap.</summary>
internal static class PropPainter
{
    static readonly Color BowlBlue = Hex.ToColor(0x5AA7E8), BowlDark = Hex.ToColor(0x2F6FA8), BowlRim = Hex.ToColor(0x8CC4F2);
    static readonly Color Kibble = Hex.ToColor(0xB9793F), KibbleDark = Hex.ToColor(0x8A5427), Ink = Hex.ToColor(0x2B2230);

    /// <summary>Pencere kutusunun piksel boyutu.</summary>
    public static (int W, int H) BoxSize(Prop p, double dpi)
    {
        int pad = (int)Math.Ceiling(4 * dpi);
        int d = (int)Math.Ceiling(p.Radius * 2);
        return p.Kind == PropKind.Yarn
            ? (d + 2 * pad + (int)(10 * dpi), d + 2 * pad)          // sağda sarkan ip için pay
            : (d + 2 * pad, (int)Math.Ceiling(p.Radius * 1.7) + 2 * pad);   // üstte mama tepeciği için pay
    }

    public static void Draw(Graphics g, Prop p, int w, int h, double dpi)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        if (p.Kind == PropKind.Yarn) Yarn(g, p, w, h, (float)dpi);
        else Bowl(g, p, w, h, (float)dpi);
    }

    static void Yarn(Graphics g, Prop p, int w, int h, float k)
    {
        float r = (float)p.Radius, cx = w / 2f, cy = h - r - 4 * k;
        var color = Hex.ToColor(p.Color);
        var dark = Hex.ToColor(Core.Appearance.ColorMath.Mix(p.Color, 0x000000, 0.35));
        var light = Hex.ToColor(Core.Appearance.ColorMath.Mix(p.Color, 0xFFFFFF, 0.35));

        // Sarkan ip ucu (dönmeyle birlikte yer değiştirir).
        float ang = (float)p.Spin;
        var start = new PointF(cx + MathF.Cos(ang) * r * 0.9f, cy + MathF.Sin(ang) * r * 0.9f);
        using (var tail = new Pen(dark, 1.6f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawBezier(tail, start, new PointF(start.X + 6 * k, start.Y + 2 * k), new PointF(cx + r + 2 * k, h - 6 * k), new PointF(cx + r + 8 * k, h - 3 * k));

        using (var body = new SolidBrush(color)) g.FillEllipse(body, cx - r, cy - r, 2 * r, 2 * r);

        // İplik sargıları: yumağın dönüşüyle birlikte döner.
        var state = g.Save();
        using var clip = new GraphicsPath();
        clip.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
        g.SetClip(clip);
        g.TranslateTransform(cx, cy);
        g.RotateTransform(ang * 180 / MathF.PI);
        using (var thread = new Pen(dark, 1.3f * k))
        {
            for (int i = -2; i <= 2; i++) g.DrawArc(thread, -r * 1.6f, -r + i * r * 0.42f - r, r * 3.2f, r * 2, 20, 140);
            for (int i = -1; i <= 1; i++) g.DrawArc(thread, -r + i * r * 0.5f - r * 0.2f, -r * 1.5f, r * 1.4f, r * 3f, 110, 140);
        }
        using (var shine = new Pen(light, 1.2f * k)) g.DrawArc(shine, -r * 0.7f, -r * 0.7f, r * 1.4f, r * 1.4f, 200, 60);
        g.Restore(state);
        using (var outline = new Pen(dark, 1.4f * k)) g.DrawEllipse(outline, cx - r, cy - r, 2 * r, 2 * r);
    }

    static void Bowl(Graphics g, Prop p, int w, int h, float k)
    {
        float r = (float)p.Radius, cx = w / 2f, bottom = h - 3 * k, top = bottom - r * 0.95f;
        float rimW = r * 2 - 2 * k, baseW = r * 1.35f, rimH = r * 0.42f;

        // Mama (kabın içinde, ağzından taşmadan tepecik): miktar arttıkça yükselir.
        if (p.Food > 0.01)
        {
            float mound = rimH * 0.35f + r * 0.45f * (float)Math.Min(1, p.Food);
            var rnd = new Random(7);   // sabit tohum: taneler her karede aynı yerde
            int count = 6 + (int)(p.Food * 14);
            using var kb = new SolidBrush(Kibble);
            using var kd = new SolidBrush(KibbleDark);
            for (int i = 0; i < count; i++)
            {
                float t = (float)rnd.NextDouble() * 2 - 1;
                float x = cx + t * rimW * 0.42f, y = top - (1 - t * t) * mound + (float)rnd.NextDouble() * 3 * k;
                float s = (3.5f + (float)rnd.NextDouble() * 2) * k;
                g.FillEllipse(i % 3 == 0 ? kd : kb, x - s / 2, y - s / 2, s, s * 0.8f);
            }
        }

        // Kabın gövdesi: üstü geniş, altı dar, yuvarlak dipli.
        using (var body = new GraphicsPath())
        {
            body.AddLine(cx - rimW / 2, top, cx + rimW / 2, top);
            body.AddBezier(cx + rimW / 2, top, cx + rimW / 2, bottom - 4 * k, cx + baseW / 2 + 4 * k, bottom, cx + baseW / 2, bottom);
            body.AddLine(cx + baseW / 2, bottom, cx - baseW / 2, bottom);
            body.AddBezier(cx - baseW / 2, bottom, cx - baseW / 2 - 4 * k, bottom, cx - rimW / 2, bottom - 4 * k, cx - rimW / 2, top);
            body.CloseFigure();
            using var fill = new LinearGradientBrush(new RectangleF(cx - rimW / 2, top, rimW, bottom - top + 1), BowlBlue, BowlDark, 90f);
            g.FillPath(fill, body);
            using var pen = new Pen(Ink, 1.3f * k);
            g.DrawPath(pen, body);
        }
        // Ağız kenarı.
        using (var rim = new Pen(BowlRim, 2.4f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(rim, cx - rimW / 2 + 1.5f * k, top + 1.2f * k, cx + rimW / 2 - 1.5f * k, top + 1.2f * k);

        // Balık süsü.
        float fx = cx, fy = top + (bottom - top) * 0.55f, fs = r * 0.22f;
        using var fish = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
        g.FillEllipse(fish, fx - fs, fy - fs * 0.55f, fs * 1.6f, fs * 1.1f);
        g.FillPolygon(fish, new[] { new PointF(fx + fs * 0.5f, fy), new PointF(fx + fs * 1.2f, fy - fs * 0.5f), new PointF(fx + fs * 1.2f, fy + fs * 0.5f) });
    }
}
