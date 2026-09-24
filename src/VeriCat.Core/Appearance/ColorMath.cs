namespace VeriCat.Core.Appearance;

/// <summary>0xRRGGBB biçimindeki renkler üzerinde işlemler.</summary>
public static class ColorMath
{
    public static uint Mix(uint a, uint b, double t)
    {
        uint o = 0;
        foreach (var shift in new[] { 16, 8, 0 })
        {
            double x = a >> shift & 0xFF, y = b >> shift & 0xFF;
            o |= (uint)Math.Round(x + (y - x) * t) << shift;
        }
        return o;
    }

    public static double Brightness(uint v) => Math.Max(v >> 16 & 0xFF, Math.Max(v >> 8 & 0xFF, v & 0xFF)) / 255.0;
}
