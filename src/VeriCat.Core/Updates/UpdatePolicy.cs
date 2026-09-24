namespace VeriCat.Core.Updates;

/// <summary>Bulunan bir sürümün kullanıcıya sunulup sunulmayacağına karar verir.</summary>
public static class UpdatePolicy
{
    /// <param name="current">Çalışan sürüm.</param>
    /// <param name="latest">GitHub'daki en son sürüm.</param>
    /// <param name="skipped">Kullanıcının atladığı sürüm (varsa).</param>
    /// <param name="manual">Kullanıcı elle denetledi mi? (Atlanan sürüm yine gösterilir.)</param>
    public static bool ShouldOffer(SemVersion current, ReleaseInfo? latest, string? skipped, bool manual)
    {
        if (latest == null || !latest.IsInstallable || latest.Version <= current) return false;
        if (manual) return true;
        return !(SemVersion.TryParse(skipped, out var s) && s == latest.Version);
    }

    /// <summary>Uygulama güncellendikten sonraki ilk açılış mı? (Yenilikler gösterilir.)</summary>
    public static bool IsFirstRunAfterUpdate(SemVersion current, string? lastSeen) =>
        !current.IsDevelopment && SemVersion.TryParse(lastSeen, out var seen) && current > seen;
}
