using VeriCat.Core.Diagnostics;
using VeriCat.Desktop.Diagnostics;

namespace VeriCat.Desktop;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Log.Configure(Log.DefaultPath);
        using var single = new Mutex(true, "VeriCat.SingleInstance", out bool first);
        if (!first)
        {
            bool afterUpdate = args.Contains(AppInfo.AfterUpdateArg);
            if (!afterUpdate) return;   // zaten açık
            if (!WaitForPrevious(single))
            {
                Log.Warn("Güncelleme sonrası: önceki sürüm 15 sn içinde kapanmadı, yeni sürüm başlatılamadı");
                return;
            }
        }

        Log.Info($"VeriCat {AppInfo.DisplayVersion} başladı");
        InstallCrashHandlers();

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplication());
        Log.Info("VeriCat kapandı");
    }

    /// <summary>Güncelleme sonrası: eski sürüm kapanıp kilidi bırakana kadar bekler.</summary>
    static bool WaitForPrevious(Mutex mutex)
    {
        try { return mutex.WaitOne(TimeSpan.FromSeconds(15)); }
        catch (AbandonedMutexException) { return true; }   // eski süreç kilidi bırakmadan kapandı: artık bizim
    }

    /// <summary>Beklenmeyen hatalar günlüğe yazılır; arayüz iş parçacığındakiler için bildirme teklif edilir.</summary>
    static void InstallCrashHandlers()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        bool asking = false;
        Application.ThreadException += (_, e) =>
        {
            Log.Error("Beklenmeyen hata", e.Exception);
            if (asking) return;
            asking = true;
            try
            {
                var answer = MessageBox.Show(
                    "VeriCat'te beklenmeyen bir hata oldu; kediler çalışmaya devam ediyor.\n\nGitHub'da bir sorun kaydı açmak ister misin? " +
                    "Sayfa hata ayrıntılarıyla dolu açılır, göndermeden önce okuyabilirsin.",
                    "VeriCat", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer == DialogResult.Yes) IssueReporter.Open(Core.Diagnostics.IssueKind.Bug, null, 0, e.Exception.ToString());
            }
            finally { asking = false; }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Ölümcül hata", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("Gözlemlenmeyen görev hatası", e.Exception); e.SetObserved(); };
    }
}
