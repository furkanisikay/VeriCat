using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using VeriCat.Core.Rendering;

namespace VeriCat.Desktop.Rendering;

// Efektler (kalpler, zzz, toz bulutu, isabet yıldızı) ve isim etiketi.
internal sealed partial class CatPainter
{
    static readonly FontFamily UiFont = PickFont("Segoe UI");
    static readonly FontFamily LabelFont = PickFont("Segoe UI Semibold");
    static readonly ConcurrentDictionary<int, Font> LabelFonts = new();
    static readonly Color LabelInk = Hex.ToColor(0x2B2230), StarYellow = Hex.ToColor(0xFFD84A), AngerRed = Hex.ToColor(0xE8364F);

    static FontFamily PickFont(string name)
    {
        try { return new FontFamily(name); }
        catch (ArgumentException) { return FontFamily.GenericSansSerif; }
    }

    /// <summary>Aynalanmadan çizilen süsler (yazı ters dönmesin diye).</summary>
    void Overlays(NameTag? name)
    {
        float Mirror(float x) => f.FacingRight ? x : BaseW - x;
        static float Frac(float x) => x - MathF.Floor(x);

        if (f.Pose == Pose.Sleep)
        {
            for (int i = 0; i < 3; i++)
            {
                float k = Frac(f.Clock * 0.5f + i / 3f), size = 9 + k * 9, a = MathF.Sin(k * MathF.PI);
                float bx = Mirror(122 + k * 14) - size / 3, by = 58 + k * 46;
                float em = size * scale, dx = ox + bx * scale, dy = oy + (BaseH - by) * scale - em * 1.25f;
                var st = g.Save();
                g.ResetTransform();
                using var path = new GraphicsPath();
                path.AddString("z", UiFont, (int)FontStyle.Bold, em, P(dx, dy), StringFormat.GenericTypographic);
                using (var pen = new Pen(Color.FromArgb((int)(a * 255), Color.White), em * 0.14f) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, path);
                using (var brush = new SolidBrush(Color.FromArgb((int)(a * 255), 115, 128, 217)))
                    g.FillPath(brush, path);
                g.Restore(st);
            }
        }
        if (f.Hearts is float hearts)
        {
            for (int i = 0; i < 2; i++)
            {
                float k = Frac((hearts + i * 0.6f) / 1.2f);
                using var p = Heart(P(Mirror(86 + i * 18 + MathF.Sin(k * 6) * 3), 110 + k * 16), 6 + k * 2);
                int a = (int)((1 - k) * 255);
                using (var b = new SolidBrush(Color.FromArgb(a, HeartRed))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(a, Color.White), 1.2f)) g.DrawPath(pen, p);
            }
        }
        if (f.Emote != Emote.None) EmoteBubble(P(Mirror(f.Pose == Pose.Sleep ? 106 : 116), f.Pose == Pose.Sleep ? 78 : 116), f.Emote, f.EmoteAge);
        if (name is NameTag n) NamePlate(n);
    }

    static readonly Color FishBlue = Hex.ToColor(0x5AA7E8), MoonGold = Hex.ToColor(0xF5CB4B), YarnRed = Hex.ToColor(0xE0413A);

    /// <summary>Düşünce baloncuğu: kafanın üstünde "pop" diye belirir, içinde ihtiyacın simgesi.</summary>
    void EmoteBubble(PointF at, Emote e, float age)
    {
        // Belirme: hafif taşan bir büyüme (0 → 1.12 → 1).
        float t = Math.Clamp(age / 0.22f, 0, 1), pop = t < 1 ? 1.12f * MathF.Sin(t * MathF.PI / 2 * 1.1f) : 1;
        if (pop <= 0.01f) return;
        var st = g.Save();
        g.TranslateTransform(at.X, at.Y);
        g.ScaleTransform(pop, pop);

        const float r = 12;
        // Düşünce kuyruğu: kafaya doğru iki küçük daire.
        float side = f.FacingRight ? -1 : 1;
        using (var tail = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
        using (var edge = new Pen(c.Line, 1.2f))
        {
            foreach (var (dx, dy, s) in new[] { (side * 7f, -14f, 4.5f), (side * 12f, -19f, 3f) })
            {
                g.FillEllipse(tail, dx - s / 2, dy - s / 2, s, s);
                g.DrawEllipse(edge, dx - s / 2, dy - s / 2, s, s);
            }
            g.FillEllipse(tail, -r, -r, 2 * r, 2 * r);
            g.DrawEllipse(edge, -r, -r, 2 * r, 2 * r);
        }

        switch (e)
        {
            case Emote.Hungry:   // balık
                using (var b = new SolidBrush(FishBlue))
                {
                    g.FillEllipse(b, -7, -3.5f, 10, 7);
                    g.FillPolygon(b, new[] { P(2, 0), P(7, 4), P(7, -4) });
                }
                using (var eye = new SolidBrush(Color.White)) g.FillEllipse(eye, -5, 0, 2, 2);
                break;
            case Emote.Lonely:   // boş kalp
                using (var h = Heart(P(0, 2), 5.5f))
                using (var pen = new Pen(HeartRed, 1.8f)) g.DrawPath(pen, h);
                break;
            case Emote.Love:     // dolu kalp
                using (var h = Heart(P(0, 2), 5.5f))
                using (var b = new SolidBrush(HeartRed)) g.FillPath(b, h);
                break;
            case Emote.Bored:    // yumak
                using (var b = new SolidBrush(YarnRed)) g.FillEllipse(b, -5.5f, -5.5f, 11, 11);
                using (var pen = new Pen(Color.FromArgb(160, 90, 20, 20), 1))
                {
                    g.DrawArc(pen, -8, -4, 16, 10, 200, 140);
                    g.DrawArc(pen, -4, -8, 10, 16, 110, 140);
                }
                break;
            case Emote.Sleepy:   // hilal
                using (var b = new SolidBrush(MoonGold))
                using (var moon = new GraphicsPath())
                {
                    moon.AddEllipse(-6, -6, 12, 12);
                    using var cut = new Region(moon);
                    using var bite = new GraphicsPath();
                    bite.AddEllipse(-2, -1, 12, 12);
                    cut.Exclude(bite);
                    g.FillRegion(b, cut);
                }
                break;
            case Emote.Grumpy:   // kızgınlık işareti
                foreach (var angle in new[] { 0.785f, 2.356f, 3.927f, 5.498f })
                {
                    var a = Polar(P(0, 0), angle, 2);
                    var b2 = Polar(P(0, 0), angle, 6.5f);
                    var cp = Polar(P(0, 0), angle + 0.5f, 5);
                    Line(Bezier(a, cp, cp, b2), 1.8f, AngerRed);
                }
                break;
            case Emote.Surprised:   // ünlem
                using (var pen = RoundPen(c.Line, 2.6f)) g.DrawLine(pen, 0, 6, 0, -1.5f);
                using (var b = new SolidBrush(c.Line)) g.FillEllipse(b, -1.5f, -6.5f, 3, 3);
                break;
        }
        g.Restore(st);
    }

    /// <summary>
    /// Tasmadaki madalyona asılı, okunaklı isim etiketi. Yazı boyu kedi ne kadar küçük olursa olsun
    /// alt sınırın altına inmez; etiket pencere kutusunun dışına taşmaz.
    /// </summary>
    void NamePlate(NameTag n)
    {
        if (tagAnchor is not PointF anchor || string.IsNullOrWhiteSpace(n.Text)) return;
        var st = g.Save();
        g.ResetTransform();
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var font = LabelFonts.GetOrAdd((int)MathF.Round(n.Em * 4), k => new Font(LabelFont, k / 4f, FontStyle.Regular, GraphicsUnit.Pixel));
        var size = g.MeasureString(n.Text, font, PointF.Empty, StringFormat.GenericTypographic);
        float padX = n.Em * 0.5f, padY = n.Em * 0.2f;
        float w = size.Width + 2 * padX, h = size.Height + 2 * padY;
        float boxW = BaseW * scale, boxH = BaseH * scale;
        float x = Math.Clamp(anchor.X - w / 2, ox + 1, ox + Math.Max(1, boxW - w - 1));
        float y = Math.Clamp(anchor.Y + 1, oy + 1, oy + Math.Max(1, boxH - h - 1));

        using (var plate = RoundRect(new RectangleF(x, y, w, h), h / 2))
        {
            using var bg = new SolidBrush(Color.FromArgb(238, 255, 255, 255));
            g.FillPath(bg, plate);
            using var border = new Pen(c.Collar, Math.Max(1.2f, n.Em * 0.12f));
            g.DrawPath(border, plate);
        }
        using (var ink = new SolidBrush(LabelInk))
            g.DrawString(n.Text, font, ink, x + padX, y + padY, StringFormat.GenericTypographic);
        g.Restore(st);
    }

    /// <summary>Kavga bulutu: öne doğru kabaran toz, uçuşan yıldızlar ve kızgınlık işareti.</summary>
    void FightDust(float t)
    {
        for (int i = 0; i < 5; i++)
        {
            float k = (t * 2.2f + i * 0.37f) % 1;
            float cx = 118 + i * 6 + MathF.Sin(t * 9 + i) * 3, cy = 16 + (i % 3) * 14 + k * 10;
            float r = 8 + k * 8;
            using var puff = Oval(cx, cy, r * 1.6f, r * 1.3f);
            using var b = new SolidBrush(Color.FromArgb((int)(150 * (1 - k)), 236, 230, 222));
            g.FillPath(b, puff);
            using var pen = new Pen(Color.FromArgb((int)(120 * (1 - k)), 170, 160, 150), 1.2f);
            g.DrawPath(pen, puff);
        }
        for (int i = 0; i < 2; i++)
        {
            float k = (t * 1.6f + i * 0.5f) % 1;
            Star(P(124 + i * 12, 58 + k * 26), 4 + 2 * MathF.Sin(k * MathF.PI), 1 - k);
        }
        // Kızgınlık işareti (dört kıvrık çizgi).
        float pulse = 1 + 0.12f * MathF.Sin(t * 16);
        var m = P(120, 104);
        var st = g.Save();
        g.TranslateTransform(m.X, m.Y);
        g.ScaleTransform(pulse, pulse);
        foreach (var angle in new[] { 0.785f, 2.356f, 3.927f, 5.498f })
        {
            var a = Polar(P(0, 0), angle, 3);
            var b = Polar(P(0, 0), angle, 8);
            var cp = Polar(P(0, 0), angle + 0.5f, 6);
            Line(Bezier(a, cp, cp, b), 2.2f, AngerRed);
        }
        g.Restore(st);
    }

    /// <summary>İsabet anı: büyüyüp sönen sarı yıldız ve çıkan çizgiler. k: saniye (0 → 0.2).</summary>
    void Burst(PointF at, float k)
    {
        float u = Math.Clamp(k / 0.2f, 0, 1);
        Star(at, 7 + 9 * u, 1 - u);
        using var pen = RoundPen(Color.FromArgb((int)(255 * (1 - u)), StarYellow), 2);
        for (int i = 0; i < 6; i++)
        {
            float a = i * MathF.PI / 3 + 0.3f;
            g.DrawLine(pen, Polar(at, a, 10 + 10 * u), Polar(at, a, 15 + 12 * u));
        }
    }

    void Star(PointF c0, float r, float alpha)
    {
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
            pts[i] = Polar(c0, MathF.PI / 2 + i * MathF.PI / 5, i % 2 == 0 ? r : r * 0.45f);
        using var path = new GraphicsPath();
        path.AddPolygon(pts);
        int a = (int)(255 * Math.Clamp(alpha, 0, 1));
        using (var b = new SolidBrush(Color.FromArgb(a, StarYellow))) g.FillPath(b, path);
        using (var pen = new Pen(Color.FromArgb(a, c.Line), 1.1f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
    }

    static GraphicsPath Heart(PointF c, float s)
    {
        const float k = 0.5523f;
        float r = s * 0.5f, ly = c.Y + s * 0.2f, lx = c.X - r, rx = c.X + r;
        var bottom = P(c.X, c.Y - s * 0.8f);
        var p = new GraphicsPath();
        p.AddBezier(bottom, P(c.X - s * 0.3f, c.Y - s * 0.4f), P(c.X - s, c.Y - s * 0.2f), P(c.X - s, ly));
        p.AddBezier(P(c.X - s, ly), P(lx - r, ly + k * r), P(lx - k * r, ly + r), P(lx, ly + r));
        p.AddBezier(P(lx, ly + r), P(lx + k * r, ly + r), P(lx + r, ly + k * r), P(c.X, ly));
        p.AddBezier(P(c.X, ly), P(rx - r, ly + k * r), P(rx - k * r, ly + r), P(rx, ly + r));
        p.AddBezier(P(rx, ly + r), P(rx + k * r, ly + r), P(rx + r, ly + k * r), P(c.X + s, ly));
        p.AddBezier(P(c.X + s, ly), P(c.X + s, c.Y - s * 0.2f), P(c.X + s * 0.3f, c.Y - s * 0.4f), bottom);
        p.CloseFigure();
        return p;
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
