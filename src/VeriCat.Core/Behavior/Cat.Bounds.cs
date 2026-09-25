using VeriCat.Core.Geometry;

namespace VeriCat.Core.Behavior;

// Ekranın içinde kalma: kedinin gövdesi hiçbir zaman ekranın kenarından, tepesinden ya da altından taşmaz.
public sealed partial class Cat
{
    /// <summary>Gövdenin (kuyruk ve kafa dahil) ayak ortasından yana uzanımı.</summary>
    internal double BodyHalfWidth => 44 * S;

    /// <summary>Kulak uçlarının ayaklardan yüksekliği.</summary>
    internal double Headroom => 112 * S;

    /// <summary>Bu platformda dururken kafası ekranın tepesine sığar mı?</summary>
    bool FitsUnderScreenTop(Platform p) =>
        p.Y + Headroom <= World.ScreenNear((p.MinX + p.MaxX) / 2, p.Y).Screen.WorkTop;

    /// <summary>
    /// Kediyi bulunduğu (ya da en yakın) ekranın içine çeker. Yanında başka ekran olan kenarlar serbesttir,
    /// böylece monitörler arasında yürüyebilir.
    /// </summary>
    void KeepOnScreen()
    {
        if (state == CatState.Dragged) return;
        double probeY = py + 20 * S;
        var (screen, floor) = World.ScreenNear(px, probeY);
        var b = screen.Bounds;
        double half = BodyHalfWidth;

        bool leftOpen = World.IsOnAnyScreen(b.MinX - 1, probeY);
        bool rightOpen = World.IsOnAnyScreen(b.MaxX + 1, probeY);
        double lo = leftOpen ? double.MinValue : b.MinX + half;
        double hi = rightOpen ? double.MaxValue : b.MaxX - half;
        if (px < lo) PushInside(lo, towardRight: true);
        else if (px > hi) PushInside(hi, towardRight: false);

        double ceiling = screen.WorkTop - Headroom;
        if (py > ceiling)
        {
            if (state == CatState.Air) { py = ceiling; vy = Math.Min(vy, 0); }
            else if (state == CatState.Climb) py = ceiling;
            else if (state == CatState.Hang) { }        // asılıyken kafa kenarın altında, sığar
            else if (platform is Platform { IsFloor: false } edge) StartHang(edge);   // pencere tepeye dayandı: kenara asılır
            else Drop(0);
        }

        if (py < floor.Y && floor.Covers(px))
        {
            py = floor.Y;
            if (state is CatState.Air or CatState.Climb) Land(floor, -vy);
            else platform ??= floor;
        }
    }

    void PushInside(double x, bool towardRight)
    {
        double dx = x - px;
        px = x;
        if (state == CatState.Fight) fightAnchor += dx;
        if (state == CatState.Air && !OnWallContact(wallOnRight: !towardRight))
            vx = towardRight ? Math.Abs(vx) * 0.4 : -Math.Abs(vx) * 0.4;
        else if (state is CatState.Walk or CatState.Flee) facingRight = towardRight;   // kenardan dön
    }
}
