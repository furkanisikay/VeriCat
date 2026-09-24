using System.Drawing.Drawing2D;
using VeriCat.Core.Configuration;

namespace VeriCat.Desktop.Rendering;

// Desenler (beyaz pati, benek) ve aksesuarlar (zil, fiyonk, taç, çiçek, gözlük).
internal sealed partial class CatPainter
{
    static readonly Color SockWhite = Hex.ToColor(0xFFFCF7), Gold = Hex.ToColor(0xF5C542), GoldDark = Hex.ToColor(0xB8871B);
    static readonly Color BowPink = Hex.ToColor(0xFF7AA8), PetalWhite = Hex.ToColor(0xFFF6FA), FlowerCore = Hex.ToColor(0xFFC93C);
    static readonly Color GlassRim = Hex.ToColor(0x2B2230), GlassTint = Color.FromArgb(55, 180, 220, 255);

    /// <summary>Bacak; "beyaz pati" deseni varsa ucu beyaz.</summary>
    void Leg(PointF a, PointF b, float w, Color col)
    {
        Limb(a, b, w, col);
        if (!c.Has(Markings.Socks)) return;
        // Bacağın son üçte biri beyaz: yuvarlak uçlu kısa bir çizgi.
        var from = Lerp(b, a, 0.3f);
        var p = new GraphicsPath();
        p.AddLine(from, b);
        using (p)
        using (var pen = RoundPen(SockWhite, w - 1)) g.DrawPath(pen, p);
    }

    /// <summary>Benek deseni: gövde üzerinde birkaç koyu leke. radius: yayılma, k: boyut çarpanı.</summary>
    void Spots(PointF center, float radius, float k)
    {
        if (!c.Has(Markings.Spots)) return;
        foreach (var (dx, dy, s) in new[] { (-0.7f, 0.2f, 7f), (-0.1f, 0.55f, 5.5f), (0.45f, 0.1f, 6.5f), (0.15f, -0.35f, 4.5f) })
            Fill(Oval(center.X + dx * radius, center.Y + dy * radius * 0.6f, s * k * 1.3f, s * k), c.Spot, false);
    }

    void DrawAccessory(PointF h, float look)
    {
        switch (c.Accessory)
        {
            case Accessory.Bell: Bell(P(h.X + look * 0.5f + 9, h.Y - 30)); break;
            case Accessory.Bow: Bow(P(h.X + 19 + look * 0.3f, h.Y + 22)); break;
            case Accessory.Crown: Crown(P(h.X + look * 0.2f, h.Y + 23)); break;
            case Accessory.Flower: Flower(P(h.X - 20 + look * 0.3f, h.Y + 22)); break;
            case Accessory.Glasses: Glasses(h.X + look, h.Y + 2); break;
        }
    }

    /// <summary>Tasmada madalyonun yanında küçük altın zil.</summary>
    void Bell(PointF at)
    {
        Fill(Oval(at.X, at.Y, 8, 8), Gold);
        var slit = new GraphicsPath();
        slit.AddLine(P(at.X, at.Y - 1), P(at.X, at.Y - 3.5f));
        Line(slit, 1.2f, GoldDark);
        Fill(Oval(at.X, at.Y - 2.5f, 2.2f, 2.2f), GoldDark, false);
    }

    /// <summary>Kulağın dibinde fiyonk.</summary>
    void Bow(PointF at)
    {
        Fill(RoundPoly(new[] { P(at.X, at.Y), P(at.X - 10, at.Y + 6), P(at.X - 10, at.Y - 6) }, 2), BowPink);
        Fill(RoundPoly(new[] { P(at.X, at.Y), P(at.X + 10, at.Y - 6), P(at.X + 10, at.Y + 6) }, 2), BowPink);
        Fill(Oval(at.X, at.Y, 5, 5), Hex.ToColor(0xE8538A));
    }

    /// <summary>Kulakların arasında minik taç.</summary>
    void Crown(PointF at)
    {
        var pts = new[]
        {
            P(at.X - 11, at.Y), P(at.X + 11, at.Y), P(at.X + 12, at.Y + 11), P(at.X + 6, at.Y + 5),
            P(at.X, at.Y + 13), P(at.X - 6, at.Y + 5), P(at.X - 12, at.Y + 11),
        };
        var path = new GraphicsPath();
        path.AddPolygon(pts);
        Fill(path, Gold);
        Fill(Oval(at.X, at.Y + 4, 3.5f, 3.5f), Hex.ToColor(0xE0413A), false);
        foreach (var x in new[] { -12f, 0f, 12f }) Fill(Oval(at.X + x, at.Y + (x == 0 ? 13 : 11), 3, 3), Gold, false);
    }

    /// <summary>Kulağa takılı papatya.</summary>
    void Flower(PointF at)
    {
        for (int i = 0; i < 6; i++)
        {
            var p = Polar(at, i * MathF.PI / 3, 4.5f);
            Fill(Oval(p.X, p.Y, 6, 6), PetalWhite);
        }
        Fill(Oval(at.X, at.Y, 5, 5), FlowerCore, false);
    }

    /// <summary>Yuvarlak gözlük.</summary>
    void Glasses(float fx, float ey)
    {
        foreach (var x in new[] { fx - 10, fx + 12 })
        {
            using var lens = Oval(x, ey, 17, 17);
            using (var tint = new SolidBrush(GlassTint)) g.FillPath(tint, lens);
            using (var rim = new Pen(GlassRim, 1.8f)) g.DrawPath(rim, lens);
        }
        var bridge = new GraphicsPath();
        bridge.AddBezier(P(fx - 1.5f, ey + 1), P(fx, ey + 3), P(fx + 2, ey + 3), P(fx + 3.5f, ey + 1));
        Line(bridge, 1.6f, GlassRim);
    }
}
