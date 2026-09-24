using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace VeriCat.Core.Updates;

/// <summary>Semantik sürüm (MAJOR.MINOR.PATCH[-prerelease][+build]). Karşılaştırma semver.org 2.0 kurallarına uyar.</summary>
public sealed record SemVersion(int Major, int Minor, int Patch, string Prerelease = "") : IComparable<SemVersion>
{
    public static readonly SemVersion Zero = new(0, 0, 0);

    /// <summary>Yerel/geliştirici derlemesi (0.0.0): otomatik güncelleme yapmaz.</summary>
    public bool IsDevelopment => Major == 0 && Minor == 0 && Patch == 0;

    public static bool TryParse(string? text, [NotNullWhen(true)] out SemVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        int plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        string pre = "";
        int dash = s.IndexOf('-');
        if (dash >= 0) { pre = s[(dash + 1)..]; s = s[..dash]; }
        var parts = s.Split('.');
        if (parts.Length != 3) return false;
        var nums = new int[3];
        for (int i = 0; i < 3; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out nums[i])) return false;
        version = new SemVersion(nums[0], nums[1], nums[2], pre);
        return true;
    }

    public static SemVersion Parse(string text) =>
        TryParse(text, out var v) ? v : throw new FormatException($"Geçersiz sürüm: {text}");

    public int CompareTo(SemVersion? other)
    {
        if (other is null) return 1;
        int c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        // Ön sürümü olmayan, ön sürümlüden büyüktür.
        if (Prerelease.Length == 0 || other.Prerelease.Length == 0) return other.Prerelease.Length.CompareTo(Prerelease.Length) switch
        {
            < 0 => -1, > 0 => 1, _ => 0,
        };
        var a = Prerelease.Split('.');
        var b = other.Prerelease.Split('.');
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            bool an = int.TryParse(a[i], out int ai), bn = int.TryParse(b[i], out int bi);
            c = (an, bn) switch
            {
                (true, true) => ai.CompareTo(bi),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(a[i], b[i]),
            };
            if (c != 0) return Math.Sign(c);
        }
        return a.Length.CompareTo(b.Length);
    }

    public static bool operator >(SemVersion a, SemVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(SemVersion a, SemVersion b) => a.CompareTo(b) < 0;
    public static bool operator >=(SemVersion a, SemVersion b) => a.CompareTo(b) >= 0;
    public static bool operator <=(SemVersion a, SemVersion b) => a.CompareTo(b) <= 0;

    public override string ToString() => Prerelease.Length == 0 ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Prerelease}";
}
