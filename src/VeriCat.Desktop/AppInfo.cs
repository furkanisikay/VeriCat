using System.Reflection;
using VeriCat.Core.Updates;

namespace VeriCat.Desktop;

/// <summary>Uygulamanın kimliği: sürüm ve güncellemelerin geldiği GitHub deposu.</summary>
internal static class AppInfo
{
    public const string Owner = "furkanisikay";
    public const string Repo = "VeriCat";
    public const string RepoUrl = "https://github.com/" + Owner + "/" + Repo;

    /// <summary>Güncellemeden sonra yeni süreç, eskisinin tek-kopya kilidini bırakmasını bekler.</summary>
    public const string AfterUpdateArg = "--after-update";

    public static SemVersion Version { get; } =
        SemVersion.TryParse(typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, out var v)
            ? v : SemVersion.Zero;

    public static string DisplayVersion => Version.IsDevelopment ? "geliştirici sürümü" : "v" + Version;
}
