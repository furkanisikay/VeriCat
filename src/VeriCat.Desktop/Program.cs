namespace VeriCat.Desktop;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var single = new Mutex(true, "VeriCat.SingleInstance", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplication());
    }
}
