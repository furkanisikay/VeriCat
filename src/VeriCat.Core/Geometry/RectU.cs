namespace VeriCat.Core.Geometry;

// Koordinat sistemi: x sağa, y YUKARI (y = -ekranY). Fizik bu sayede sezgisel kalır (yerçekimi -y yönünde).

/// <summary>Y ekseni yukarı bakan dikdörtgen.</summary>
public readonly record struct RectU(double MinX, double MinY, double MaxX, double MaxY)
{
    /// <summary>Ekran koordinatlarındaki (y aşağı) bir dikdörtgeni dönüştürür.</summary>
    public static RectU FromScreen(double left, double top, double right, double bottom) => new(left, -bottom, right, -top);

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double MidX => (MinX + MaxX) / 2;

    public bool Contains(double x, double y) => x >= MinX && x < MaxX && y >= MinY && y < MaxY;
}
