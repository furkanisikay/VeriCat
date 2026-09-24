using VeriCat.Core.Geometry;

namespace VeriCat.Core.World;

/// <summary>Kedilerin içinde yaşadığı masaüstü: ekranlar, pencereler, platformlar.</summary>
public interface IWorld
{
    IReadOnlyList<Platform> Platforms { get; }

    /// <summary>Pencere tutamacı → o anki çerçevesi. Üstünde duran kedi pencereyle birlikte kaysın diye.</summary>
    IReadOnlyDictionary<long, RectU> Frames { get; }

    IReadOnlyList<ScreenSnapshot> Screens { get; }

    double MinX { get; }
    double MaxX { get; }
    double TopY { get; }
    double BottomY { get; }

    /// <summary>Noktanın bulunduğu ekranın zemini. Ekranlar üst üste de dizilebildiği için sadece x yetmez.</summary>
    Platform? FloorAt(double x, double y);

    /// <summary>Noktayı içeren ekran; hiçbiri içermiyorsa en yakını. Zemin platformuyla birlikte döner.</summary>
    (ScreenSnapshot Screen, Platform Floor) ScreenNear(double x, double y);

    /// <summary>Noktayı içeren bir ekran var mı?</summary>
    bool IsOnAnyScreen(double x, double y);
}
