namespace VeriCat.Desktop;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var single = new Mutex(true, "VeriCat.SingleInstance", out bool first);
        if (!first && !(args.Contains(AppInfo.AfterUpdateArg) && WaitForPrevious(single))) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplication());
    }

    /// <summary>Güncelleme sonrası: eski sürüm kapanıp kilidi bırakana kadar bekler.</summary>
    static bool WaitForPrevious(Mutex mutex)
    {
        try { return mutex.WaitOne(TimeSpan.FromSeconds(15)); }
        catch (AbandonedMutexException) { return true; }   // eski süreç kilidi bırakmadan kapandı: artık bizim
    }
}
