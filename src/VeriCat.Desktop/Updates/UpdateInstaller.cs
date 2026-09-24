using System.Diagnostics;

namespace VeriCat.Desktop.Updates;

/// <summary>
/// Tek dosyalık exe'yi yerinde günceller. Windows çalışan bir exe'nin silinmesine izin vermez ama yeniden
/// adlandırılmasına izin verir: çalışan dosya ".old" yapılır, yenisi yerine konur, uygulama yeniden başlar.
/// Bir sonraki açılışta ".old" silinir.
/// </summary>
internal static class UpdateInstaller
{
    static string Target => Environment.ProcessPath ?? throw new InvalidOperationException("Çalışan dosya bulunamadı.");
    static string Old => Target + ".old";

    public static string DownloadFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VeriCat", "updates");

    /// <summary>Exe'nin bulunduğu klasöre yazılabiliyor mu? (Program Files'a kurulduysa yönetici izni gerekir.)</summary>
    public static bool CanInstall()
    {
        if (!OperatingSystem.IsWindows() || !Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var probe = Path.Combine(Path.GetDirectoryName(Target)!, $".vericat-{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(probe, Array.Empty<byte>());
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>İndirilmiş ve doğrulanmış exe'yi yerine koyup yeni sürümü başlatır. Başarılıysa çağıran uygulamayı kapatmalıdır.</summary>
    public static void InstallAndRestart(string downloadedExe)
    {
        var target = Target;
        TryDelete(Old);
        File.Move(target, Old);
        try
        {
            File.Move(downloadedExe, target);
        }
        catch
        {
            File.Move(Old, target);   // geri al
            throw;
        }
        Process.Start(new ProcessStartInfo(target, AppInfo.AfterUpdateArg) { UseShellExecute = false });
    }

    /// <summary>Önceki güncellemeden kalan dosyaları temizler (eski süreç kapanana dek birkaç kez dener).</summary>
    public static void CleanupInBackground() => Task.Run(async () =>
    {
        for (int i = 0; i < 20 && File.Exists(Old); i++)
        {
            TryDelete(Old);
            await Task.Delay(500).ConfigureAwait(false);
        }
        try
        {
            if (Directory.Exists(DownloadFolder))
                foreach (var f in Directory.EnumerateFiles(DownloadFolder)) TryDelete(f);
        }
        catch (IOException) { }
    });

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
