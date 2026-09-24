using System.Text.Json;

namespace VeriCat.Core.Updates;

public sealed record ReleaseAsset(string Name, Uri Url, long Size);

/// <summary>GitHub'daki bir sürüm (release).</summary>
public sealed record ReleaseInfo(
    SemVersion Version, string Tag, string Title, string Notes, Uri? PageUrl, DateTimeOffset? PublishedAt,
    IReadOnlyList<ReleaseAsset> Assets)
{
    /// <summary>Uygulamanın kendisi.</summary>
    public const string ExecutableName = "VeriCat.exe";

    /// <summary>Exe'nin SHA-256 özeti ("hex  VeriCat.exe").</summary>
    public const string ChecksumName = "VeriCat.exe.sha256";

    public ReleaseAsset? Executable => Assets.FirstOrDefault(a => a.Name.Equals(ExecutableName, StringComparison.OrdinalIgnoreCase));
    public ReleaseAsset? Checksum => Assets.FirstOrDefault(a => a.Name.Equals(ChecksumName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Kurulabilir mi: sürüm geçerli, exe ve özet dosyası var.</summary>
    public bool IsInstallable => Executable != null && Checksum != null;

    /// <summary>GitHub REST API'nin "release" nesnesini okur. Taslak ve geçersiz sürümler için null.</summary>
    public static ReleaseInfo? FromGitHubJson(JsonElement e)
    {
        if (e.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
        var tag = e.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        if (!SemVersion.TryParse(tag, out var version)) return null;

        var assets = new List<ReleaseAsset>();
        if (e.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in arr.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                long size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var v) ? v : 0;
                if (name != null && Uri.TryCreate(url, UriKind.Absolute, out var uri)) assets.Add(new ReleaseAsset(name, uri, size));
            }
        }

        string Str(string p) => e.TryGetProperty(p, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "";
        Uri.TryCreate(Str("html_url"), UriKind.Absolute, out var page);
        DateTimeOffset? published = DateTimeOffset.TryParse(Str("published_at"), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d : null;
        var title = Str("name");
        return new ReleaseInfo(version, tag!, title.Length > 0 ? title : tag!, Str("body"), page, published, assets);
    }
}
