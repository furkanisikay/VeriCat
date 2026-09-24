using System.Diagnostics;
using System.Runtime.InteropServices;
using VeriCat.Core.Configuration;
using VeriCat.Core.Diagnostics;

namespace VeriCat.Desktop.Diagnostics;

/// <summary>"Sorun bildir" / "Öneri gönder": tarayıcıda önceden doldurulmuş bir GitHub issue sayfası açar.</summary>
internal static class IssueReporter
{
    public static void Open(IssueKind kind, AppSettings? settings, int catCount, string? error = null)
    {
        var env = new Dictionary<string, string>
        {
            ["Windows"] = RuntimeInformation.OSDescription,
            ["Mimari"] = RuntimeInformation.OSArchitecture.ToString(),
            [".NET"] = RuntimeInformation.FrameworkDescription,
            ["Ekranlar"] = string.Join(", ", Screen.AllScreens.Select(s => $"{s.Bounds.Width}×{s.Bounds.Height}")),
            ["Ölçek"] = $"%{Math.Round(DpiScale() * 100)}",
            ["Kedi sayısı"] = catCount.ToString(),
        };
        if (settings != null)
        {
            env["Ayarlar"] = string.Join(" ", new[]
            {
                Flag("pencere", settings.Windows), Flag("içler", settings.InnerWindows), Flag("kovala", settings.Chase),
                Flag("yumruk", settings.PunchCursor), Flag("ses", settings.Sound), Flag("tamekran", settings.HideInFullscreen),
                "güncelleme:" + settings.Updates,
            });
        }

        var url = IssueReport.Build(
            AppInfo.RepoUrl, kind, AppInfo.DisplayVersion, env, error,
            kind == IssueKind.Bug ? Log.Tail(3000) : null,
            Environment.UserName, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Log.Info($"Sorun bildirimi açıldı ({kind})");
        try { Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception e) { Log.Error("Tarayıcı açılamadı", e); }
    }

    static string Flag(string name, bool on) => (on ? "+" : "-") + name;

    static double DpiScale()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96.0;
    }
}
