using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace VeriCat.Core.Updates;

/// <summary>GitHub Releases üzerinden güncelleme denetler ve indirir.</summary>
public sealed class GitHubReleaseClient
{
    readonly HttpClient http;
    readonly string owner, repo;

    public GitHubReleaseClient(HttpClient http, string owner, string repo, SemVersion current)
    {
        this.http = http;
        this.owner = owner;
        this.repo = repo;
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VeriCat", current.ToString()));
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// <summary>En son kararlı sürüm; yoksa ya da erişilemezse null.</summary>
    public Task<ReleaseInfo?> LatestAsync(CancellationToken ct = default) =>
        GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest", ct);

    /// <summary>Belirli bir sürüm (ör. güncellemeden sonra "yenilikler" için).</summary>
    public Task<ReleaseInfo?> ByVersionAsync(SemVersion version, CancellationToken ct = default) =>
        GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases/tags/v{version}", ct);

    async Task<ReleaseInfo?> GetAsync(string url, CancellationToken ct)
    {
        using var res = await http.GetAsync(url, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return null;
        await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return ReleaseInfo.FromGitHubJson(doc.RootElement);
    }

    /// <summary>İndirme adresi bu deponun sürüm dosyası mı? (Başka bir yerden exe indirilmez.)</summary>
    public bool IsTrustedAsset(ReleaseAsset asset) =>
        asset.Url.Scheme == Uri.UriSchemeHttps &&
        asset.Url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        asset.Url.AbsolutePath.StartsWith($"/{owner}/{repo}/releases/download/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sürümün exe'sini <paramref name="destination"/>'a indirir ve SHA-256 özetini doğrular.
    /// Doğrulama başarısızsa dosya silinir ve <see cref="InvalidDataException"/> fırlatılır.
    /// </summary>
    public async Task DownloadAsync(ReleaseInfo release, string destination, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var exe = release.Executable ?? throw new InvalidOperationException("Sürümde VeriCat.exe yok.");
        var sum = release.Checksum ?? throw new InvalidOperationException("Sürümde özet (sha256) dosyası yok.");
        if (!IsTrustedAsset(exe) || !IsTrustedAsset(sum)) throw new InvalidDataException("Güvenilmeyen indirme adresi.");

        var expected = Checksums.Parse(await http.GetStringAsync(sum.Url, ct).ConfigureAwait(false))
            ?? throw new InvalidDataException("Özet dosyası okunamadı.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        var partial = destination + ".partial";
        using (var res = await http.GetAsync(exe.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            res.EnsureSuccessStatusCode();
            long total = res.Content.Headers.ContentLength ?? exe.Size;
            await using var src = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var dst = File.Create(partial);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                sha.AppendData(buffer, 0, n);
                done += n;
                if (total > 0) progress?.Report(Math.Min(1, (double)done / total));
            }
            var actual = Convert.ToHexString(sha.GetHashAndReset());
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                dst.Close();
                File.Delete(partial);
                throw new InvalidDataException("İndirilen dosyanın özeti tutmuyor; güncelleme iptal edildi.");
            }
        }
        File.Move(partial, destination, overwrite: true);
        progress?.Report(1);
    }
}

public static class Checksums
{
    /// <summary>"hex  dosya" ya da yalnızca "hex" biçimindeki SHA-256 özetini okur.</summary>
    public static string? Parse(string text)
    {
        var token = text.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token is { Length: 64 } && token.All(Uri.IsHexDigit) ? token.ToUpperInvariant() : null;
    }
}
