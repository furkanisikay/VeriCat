using Microsoft.Win32;

namespace VeriCat.Desktop.Theming;

/// <summary>Uygulamanın renk paleti. Windows'un açık/koyu uygulama temasını izler.</summary>
internal sealed record Palette(
    Color Background, Color Surface, Color Border, Color Separator,
    Color Text, Color Subtle, Color Accent, Color AccentText, Color AccentSoft, Color Hover, Color Danger, Color Track)
{
    /// <summary>Krem zemin, sıcak turuncu vurgu (Sarman kedisinin rengi).</summary>
    public static readonly Palette Light = new(
        Background: C(0xFBF8F3), Surface: C(0xFFFFFF), Border: C(0xE4DCD0), Separator: C(0xEDE6DC),
        Text: C(0x2B2230), Subtle: C(0x8A7F76), Accent: C(0xE9823A), AccentText: C(0xFFFFFF),
        AccentSoft: C(0xFCE8D6), Hover: C(0xF4EDE4), Danger: C(0xD9534F), Track: C(0xDCD3C7));

    public static readonly Palette Dark = new(
        Background: C(0x221E25), Surface: C(0x2C2730), Border: C(0x3D3642), Separator: C(0x39323E),
        Text: C(0xF3EDE6), Subtle: C(0xA69C94), Accent: C(0xF6A15B), AccentText: C(0x221E25),
        AccentSoft: C(0x43342A), Hover: C(0x35303A), Danger: C(0xFF7B72), Track: C(0x4A424F));

    static Color C(uint v) => Color.FromArgb(255, (int)(v >> 16 & 0xFF), (int)(v >> 8 & 0xFF), (int)(v & 0xFF));
}

internal static class Theme
{
    static Palette? current;

    public static Palette Current => current ??= Detect();

    public static bool IsDark => Current == Palette.Dark;

    /// <summary>Tema değişince (Windows ayarlarından) tetiklenir.</summary>
    public static event EventHandler? Changed;

    static Theme()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category != UserPreferenceCategory.General) return;
            var next = Detect();
            if (next == current) return;
            current = next;
            Changed?.Invoke(null, EventArgs.Empty);
        };
    }

    static Palette Detect()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0 ? Palette.Dark : Palette.Light;
        }
        catch (System.Security.SecurityException) { return Palette.Light; }
    }

    static Font? ui, uiBold, uiSmall, title;

    public static Font UiFont => ui ??= MakeFont(9.5f, FontStyle.Regular);
    public static Font UiBold => uiBold ??= MakeFont(9.5f, FontStyle.Bold);
    public static Font UiSmall => uiSmall ??= MakeFont(8.25f, FontStyle.Regular);
    public static Font Title => title ??= MakeFont(12f, FontStyle.Bold);

    static Font MakeFont(float size, FontStyle style)
    {
        foreach (var family in new[] { "Segoe UI Variable Text", "Segoe UI" })
        {
            var f = new Font(family, size, style);
            if (f.Name == family) return f;
            f.Dispose();
        }
        return new Font(FontFamily.GenericSansSerif, size, style);
    }
}
