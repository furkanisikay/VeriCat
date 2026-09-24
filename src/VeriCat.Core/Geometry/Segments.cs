namespace VeriCat.Core.Geometry;

/// <summary>Yatay aralık listeleri üzerinde işlemler.</summary>
public static class Segments
{
    /// <summary>[a, b] aralığını listedeki her parçadan çıkarır.</summary>
    public static List<(double Lo, double Hi)> Subtract(IEnumerable<(double Lo, double Hi)> segments, double a, double b)
    {
        var result = new List<(double, double)>();
        foreach (var (lo, hi) in segments)
        {
            if (b <= lo || a >= hi) { result.Add((lo, hi)); continue; }
            if (a > lo) result.Add((lo, a));
            if (b < hi) result.Add((b, hi));
        }
        return result;
    }
}
