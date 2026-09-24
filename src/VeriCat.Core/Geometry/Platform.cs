namespace VeriCat.Core.Geometry;

/// <summary>Kedinin basabileceği yatay çizgi.</summary>
/// <param name="Y">Çizginin yüksekliği (y yukarı).</param>
/// <param name="MinX">Sol uç.</param>
/// <param name="MaxX">Sağ uç.</param>
/// <param name="Owner">Üst düzey pencere tutamacı; ekran zemini için negatif.</param>
/// <param name="Part">0: pencerenin üst kenarı; aksi halde pencerenin içindeki alt bölümün (child window) tutamacı.</param>
public readonly record struct Platform(double Y, double MinX, double MaxX, long Owner, long Part = 0)
{
    public bool IsFloor => Owner < 0;
    public bool IsInner => Part != 0;
    public double Width => MaxX - MinX;

    public bool Covers(double x, double tolerance = 0) => x >= MinX - tolerance && x <= MaxX + tolerance;

    /// <summary>Aynı fiziksel yüzey mi (pencere taşınsa bile)?</summary>
    public bool SameSurface(Platform other) => Owner == other.Owner && Part == other.Part;
}
