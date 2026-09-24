using Microsoft.Win32;

namespace VeriCat.Desktop.Platform;

/// <summary>Windows ile birlikte başlatma (HKCU\...\Run).</summary>
internal static class AutoStart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "VeriCat", LegacyValueName = "Kedi";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) != null || key?.GetValue(LegacyValueName) != null;
        }
    }

    public static void Set(bool on)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key == null) return;
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        if (on) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
