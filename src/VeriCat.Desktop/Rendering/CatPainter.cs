using System.Drawing.Drawing2D;
using VeriCat.Core.Rendering;

namespace VeriCat.Desktop.Rendering;

/// <summary>
/// Kediyi 150x135'lik bir kutuda, y yukarı bakacak şekilde çizer. Tarz: iri kafa, öne bakan yüz, kısa tombul bacaklar.
/// Parçalar bu dosyada; pozlar <c>CatPainter.Poses</c>, efektler ve yazılar <c>CatPainter.Effects</c> içinde.
/// </summary>
internal sealed partial class CatPainter
{
    public const float BaseW = 150, BaseH = 135, Ground = 6;

    static readonly Color EarPink = Hex.ToColor(0xFFB3C1), NosePink = Hex.ToColor(0xF28BA0);
    static readonly Color Blush = Hex.ToColor(0xFF8FA8, 115), Pupil = Hex.ToColor(0x221B2A), HeartRed = Hex.ToColor(0xFF5C7C);
    static readonly Color TagGold = Hex.ToColor(0xF5C542), MouthDark = Hex.ToColor(0x5A2A35);

    readonly Graphics g;
    readonly Coat c;
    readonly Sprite f;
    readonly float scale, ox, oy;
    PointF? tagAnchor;                                 // tasma madalyonunun alt noktası, cihaz pikseli

    CatPainter(Graphics g, Coat c, Sprite f, float scale, float ox, float oy)
    {
        this.g = g; this.c = c; this.f = f; this.scale = scale; this.ox = ox; this.oy = oy;
    }

    /// <summary>Kediyi çizer. (ox, oy): kutunun sol üst köşesi, cihaz pikseli.</summary>
    public static void Draw(Graphics g, Sprite f, Coat coat, float scale, float ox = 0, float oy = 0, NameTag? name = null)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TranslateTransform(ox, oy + BaseH * scale);
        g.ScaleTransform(scale, -scale);
        var p = new CatPainter(g, coat, f, scale, ox, oy);
        var inner = g.Save();
        if (!f.FacingRight) { g.TranslateTransform(BaseW, 0); g.ScaleTransform(-1, 1); }
        switch (f.Pose)
        {
            case Pose.Walk: p.Walk(); break;
            case Pose.Air: p.Air(); break;
            case Pose.Pounce: p.Pounce(); break;
            case Pose.Sit: p.Sit(); break;
            case Pose.Sleep: p.Sleep(); break;
            case Pose.Dangle: p.Dangle(); break;
            case Pose.Fight: p.Fight(); break;
            case Pose.Swat: p.Swat(); break;
        }
        g.Restore(inner);
        p.Overlays(name);
        g.Restore(state);
    }

    /// <summary>Tepsi ikonu için sadece kafa (tasmasız).</summary>
    public static void DrawHead(Graphics g, Coat coat, float size)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float s = size / 66f;
        g.TranslateTransform(size / 2, 38 * s + (size - 65 * s) / 2);
        g.ScaleTransform(s, -s);
        new CatPainter(g, coat, new Sprite(), s, 0, 0).Head(P(0, 0), 0, whiskers: false, collar: false);
        g.Restore(state);
    }

    // MARK: Yardımcılar

    static PointF P(float x, float y) => new(x, y);

    static GraphicsPath Oval(float cx, float cy, float w, float h)
    {
        var p = new GraphicsPath();
        p.AddEllipse(cx - w / 2, cy - h / 2, w, h);
        return p;
    }

    static GraphicsPath Bezier(PointF a, PointF c1, PointF c2, PointF b)
    {
        var p = new GraphicsPath();
        p.AddBezier(a, c1, c2, b);
        return p;
    }

    static PointF Toward(PointF from, PointF to, float d)
    {
        float dx = to.X - from.X, dy = to.Y - from.Y, len = MathF.Sqrt(dx * dx + dy * dy);
        float t = Math.Min(d, len / 2) / len;
        return P(from.X + dx * t, from.Y + dy * t);
    }

    static PointF Lerp(PointF a, PointF b, float t) => P(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    static PointF Polar(PointF from, float angle, float length) =>
        P(from.X + MathF.Cos(angle) * length, from.Y + MathF.Sin(angle) * length);

    /// <summary>Köşeleri yuvarlatılmış çokgen (kulaklar, burun).</summary>
    static GraphicsPath RoundPoly(PointF[] pts, float r)
    {
        var path = new GraphicsPath();
        int n = pts.Length;
        PointF first = default, last = default;
        for (int i = 0; i < n; i++)
        {
            PointF v = pts[i], a = pts[(i + n - 1) % n], b = pts[(i + 1) % n];
            PointF p1 = Toward(v, a, r * 2), p2 = Toward(v, b, r * 2);
            if (i == 0) first = p1; else path.AddLine(last, p1);
            path.AddBezier(p1, Lerp(p1, v, 0.55f), Lerp(p2, v, 0.55f), p2);
            last = p2;
        }
        path.AddLine(last, first);
        path.CloseFigure();
        return path;
    }

    static Pen RoundPen(Color col, float w) =>
        new(col, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

    void Fill(GraphicsPath p, Color col, bool outlined = true)
    {
        using (p)
        {
            using var b = new SolidBrush(col);
            g.FillPath(b, p);
            if (outlined)
            {
                using var pen = new Pen(c.Line, 2) { LineJoin = LineJoin.Round };
                g.DrawPath(pen, p);
            }
        }
    }

    void Stroke(GraphicsPath p, float w, Color col, bool stripes = false)
    {
        using (p)
        {
            using (var outline = RoundPen(c.Line, w + 4)) g.DrawPath(outline, p);
            using (var body = RoundPen(col, w)) g.DrawPath(body, p);
            if (stripes && c.Stripe is Color s)
            {
                using var dash = new Pen(s, w) { DashPattern = new[] { 4f / w, 6f / w }, DashCap = DashCap.Flat };
                g.DrawPath(dash, p);
            }
        }
    }

    void Line(GraphicsPath p, float w, Color col)
    {
        using (p)
        using (var pen = RoundPen(col, w)) g.DrawPath(pen, p);
    }

    void Limb(PointF a, PointF b, float w, Color col)
    {
        var p = new GraphicsPath();
        p.AddLine(a, b);
        Stroke(p, w, col);
    }

    /// <summary>Uzanan ön ayak: bacak + açık renkli pati.</summary>
    void Paw(PointF shoulder, PointF tip, Color col)
    {
        Limb(shoulder, tip, 11, col);
        Fill(Oval(tip.X, tip.Y, 11, 10), c.Belly);
    }

    void Stripe(PointF a, PointF b)
    {
        if (c.Stripe is not Color s) return;
        var cp = P((a.X + b.X) / 2 + 3, (a.Y + b.Y) / 2);
        Line(Bezier(a, cp, cp, b), 3.5f, s);
    }

    void Rotated(PointF center, float radians, Action body)
    {
        var st = g.Save();
        g.TranslateTransform(center.X, center.Y);
        g.RotateTransform(radians * 180 / MathF.PI);
        g.TranslateTransform(-center.X, -center.Y);
        body();
        g.Restore(st);
    }

    /// <summary>Temel birimdeki noktanın o anki dönüşümle cihaz pikseli karşılığı.</summary>
    PointF ToDevice(PointF p)
    {
        using var m = g.Transform;
        var pts = new[] { p };
        m.TransformPoints(pts);
        return pts[0];
    }

    // MARK: Parçalar

    /// <summary>look: yüzün bakış yönüne ne kadar kaydığı.</summary>
    void Head(PointF h, float look, bool whiskers = true, bool collar = true)
    {
        if (collar) CollarBand(h, look);

        void Ear(PointF[] pts)
        {
            Fill(RoundPoly(pts, 5), c.Fur);
            float mx = (pts[0].X + pts[1].X + pts[2].X) / 3, my = (pts[0].Y + pts[1].Y + pts[2].Y) / 3;
            var inner = pts.Select(p => P(mx + (p.X - mx) * 0.5f, my + (p.Y - my) * 0.5f + 1)).ToArray();
            Fill(RoundPoly(inner, 3), EarPink, false);
        }
        if (f.EarsBack)
        {   // kulaklar yana yatık: kızgın ya da korkmuş
            Ear(new[] { P(h.X - 27, h.Y + 4), P(h.X - 36 + look * 0.3f, h.Y + 22), P(h.X - 8, h.Y + 20) });
            Ear(new[] { P(h.X + 8, h.Y + 20), P(h.X + 36 + look * 0.3f, h.Y + 22), P(h.X + 27, h.Y + 4) });
        }
        else
        {
            Ear(new[] { P(h.X - 28, h.Y + 6), P(h.X - 21 + look * 0.4f, h.Y + 35), P(h.X - 4, h.Y + 21) });
            Ear(new[] { P(h.X + 4, h.Y + 21), P(h.X + 21 + look * 0.4f, h.Y + 35), P(h.X + 28, h.Y + 6) });
        }
        Fill(Oval(h.X, h.Y, 60, 50), c.Fur);

        float fx = h.X + look, mid = fx * 0.5f + h.X * 0.5f;
        foreach (var dx in new[] { -6f, 0f, 6f }) Stripe(P(mid + dx, h.Y + 23), P(mid + dx, h.Y + 16));
        Fill(Oval(fx + 1, h.Y - 9, 24, 14), c.Belly, false);
        Fill(Oval(fx - 17, h.Y - 7, 10, 6), Blush, false);
        Fill(Oval(fx + 19, h.Y - 7, 10, 6), Blush, false);

        var eyes = new[] { P(fx - 10, h.Y + 2), P(fx + 12, h.Y + 2) };
        for (int i = 0; i < eyes.Length; i++)
        {
            var e = eyes[i];
            switch (f.Eyes)
            {
                case EyeKind.Open when f.EyeOpen > 0.3f: Eye(e, 1, f.EyeOpen); break;
                case EyeKind.Wide: Eye(e, 1.15f, 1); break;
                case EyeKind.Angry: Eye(e, 1, 0.8f); Brow(e, i == 0); break;
                case EyeKind.Happy: Arc(e, true); break;
                default: Arc(e, false); break;
            }
        }

        Fill(RoundPoly(new[] { P(fx - 2, h.Y - 3.5f), P(fx + 4, h.Y - 3.5f), P(fx + 1, h.Y - 7) }, 1.2f), NosePink, false);
        if (f.Hiss) HissMouth(fx, h.Y);
        else
        {
            var mouth = new GraphicsPath();
            mouth.AddLine(P(fx + 1, h.Y - 7), P(fx + 1, h.Y - 8));
            mouth.StartFigure();
            mouth.AddBezier(P(fx - 4, h.Y - 8.5f), P(fx - 3.5f, h.Y - 12), P(fx + 0.5f, h.Y - 12), P(fx + 1, h.Y - 8));
            mouth.AddBezier(P(fx + 1, h.Y - 8), P(fx + 1.5f, h.Y - 12), P(fx + 5.5f, h.Y - 12), P(fx + 6, h.Y - 8.5f));
            Line(mouth, 1.4f, c.Line);
        }

        if (whiskers)
        {
            var w = new GraphicsPath();
            foreach (var (from, to) in new[]
                     {
                         (P(fx - 20, h.Y - 6), P(fx - 33, h.Y - 4)), (P(fx - 20, h.Y - 6), P(fx - 32, h.Y - 10)),
                         (P(fx + 22, h.Y - 6), P(fx + 35, h.Y - 4)), (P(fx + 22, h.Y - 6), P(fx + 34, h.Y - 10)),
                     })
            {
                w.StartFigure();
                w.AddLine(from, to);
            }
            Line(w, 1, c.Whisker);
        }

        if (collar) CollarTag(h, look);
    }

    /// <summary>Çenenin altında görünen tasma bandı (kafadan önce çizilir, uçları kafanın altında kalır).</summary>
    void CollarBand(PointF h, float look)
    {
        float m = h.X + look * 0.3f;
        Stroke(Bezier(P(h.X - 19, h.Y - 17), P(m - 12, h.Y - 29), P(m + 12, h.Y - 29), P(h.X + 19, h.Y - 17)), 5.5f, c.Collar);
    }

    /// <summary>Tasmadaki madalyon; isim etiketi buna asılır.</summary>
    void CollarTag(PointF h, float look)
    {
        var t = P(h.X + look * 0.5f, h.Y - 31);
        using (var ring = new Pen(c.Line, 1.4f)) g.DrawLine(ring, P(t.X, t.Y + 4), P(t.X, t.Y + 2));
        using var path = Oval(t.X, t.Y, 7.5f, 7.5f);
        using (var b = new SolidBrush(TagGold)) g.FillPath(b, path);
        using (var pen = new Pen(c.Line, 1.2f)) g.DrawPath(pen, path);
        tagAnchor = ToDevice(P(t.X, t.Y - 3.75f));
    }

    /// <summary>İri, parlak göz: renkli halka, büyük göz bebeği, iki ışık noktası.</summary>
    void Eye(PointF e, float size, float k)
    {
        float w = 11 * size, h = 13 * size * k;
        Fill(Oval(e.X, e.Y, w, h), c.Eye);
        Fill(Oval(e.X, e.Y - 0.3f, w * 0.72f, h * 0.8f), Pupil, false);
        Fill(Oval(e.X + w * 0.18f, e.Y + h * 0.2f, w * 0.38f, w * 0.38f * Math.Min(1, k * 1.2f)), Color.White, false);
        Fill(Oval(e.X - w * 0.2f, e.Y - h * 0.22f, w * 0.16f, w * 0.16f), Color.White, false);
    }

    /// <summary>Çatık kaş: burna doğru aşağı eğik.</summary>
    void Brow(PointF e, bool left)
    {
        var p = new GraphicsPath();
        if (left) p.AddLine(P(e.X - 6, e.Y + 10), P(e.X + 5, e.Y + 6));
        else p.AddLine(P(e.X - 5, e.Y + 6), P(e.X + 6, e.Y + 10));
        Line(p, 2.4f, c.Line);
    }

    /// <summary>Kapalı göz; up = mutlu "^", değilse uykulu "‿".</summary>
    void Arc(PointF e, bool up)
    {
        float d = up ? 1 : -1;
        Line(Bezier(P(e.X - 4.5f, e.Y - d * 1.5f), P(e.X - 2, e.Y + d * 3.5f), P(e.X + 2, e.Y + d * 3.5f), P(e.X + 4.5f, e.Y - d * 1.5f)), 2, c.Line);
    }

    /// <summary>Tıslayan ağız: açık, iki küçük diş.</summary>
    void HissMouth(float fx, float hy)
    {
        Fill(Oval(fx + 1, hy - 11, 9, 7), MouthDark);
        foreach (var dx in new[] { -2f, 4f })
            Fill(RoundPoly(new[] { P(fx + dx - 1.2f, hy - 7.8f), P(fx + dx + 1.2f, hy - 7.8f), P(fx + dx, hy - 10.5f) }, 0.4f), Color.White, false);
    }

    void Torso(PointF t)
    {
        Fill(Oval(t.X, t.Y, 64, 32), c.Fur);
        Fill(Oval(t.X + 2, t.Y - 8, 40, 13), c.Belly, false);
        foreach (var (dx, top) in new[] { (-16f, 12f), (-4f, 14f), (8f, 14f) })
            Stripe(P(t.X + dx, t.Y + top), P(t.X + dx + 2, t.Y + top - 9));
    }
}
