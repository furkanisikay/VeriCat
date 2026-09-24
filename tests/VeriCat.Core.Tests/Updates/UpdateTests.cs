using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VeriCat.Core.Updates;

namespace VeriCat.Core.Tests.Updates;

public class SemVersionTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3, "")]
    [InlineData("1.2.3-beta.1+abc", 1, 2, 3, "beta.1")]
    [InlineData("10.0.0", 10, 0, 0, "")]
    public void Parses(string text, int major, int minor, int patch, string pre)
    {
        Assert.Equal(new SemVersion(major, minor, patch, pre), SemVersion.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.x")]
    [InlineData("latest")]
    public void Rejects_garbage(string text) => Assert.False(SemVersion.TryParse(text, out _));

    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.0.0-alpha", "1.0.0")]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.2", "1.0.0-alpha.10")]
    [InlineData("1.0.0-alpha.1", "1.0.0-beta")]
    [InlineData("0.0.0", "1.0.0")]
    public void Orders_by_semver_rules(string lower, string higher)
    {
        Assert.True(SemVersion.Parse(lower) < SemVersion.Parse(higher));
        Assert.True(SemVersion.Parse(higher) > SemVersion.Parse(lower));
    }

    [Fact]
    public void Build_metadata_is_ignored() => Assert.Equal(SemVersion.Parse("1.2.3+a"), SemVersion.Parse("1.2.3+b"));
}

public class ReleaseInfoTests
{
    internal const string Json = """
        {
          "tag_name": "v1.4.0", "name": "v1.4.0", "draft": false,
          "html_url": "https://github.com/furkanisikay/VeriCat/releases/tag/v1.4.0",
          "published_at": "2026-09-24T10:00:00Z",
          "body": "## [1.4.0](https://x) (2026-09-24)\n\n### Yenilikler\n\n* **kedi:** kavga eder ([abc1234](https://x/c))\n",
          "assets": [
            { "name": "VeriCat.exe", "size": 10, "browser_download_url": "https://github.com/furkanisikay/VeriCat/releases/download/v1.4.0/VeriCat.exe" },
            { "name": "VeriCat.exe.sha256", "size": 80, "browser_download_url": "https://github.com/furkanisikay/VeriCat/releases/download/v1.4.0/VeriCat.exe.sha256" }
          ]
        }
        """;

    internal static ReleaseInfo Sample() => ReleaseInfo.FromGitHubJson(JsonDocument.Parse(Json).RootElement)!;

    [Fact]
    public void Reads_github_release_json()
    {
        var r = Sample();

        Assert.Equal(SemVersion.Parse("1.4.0"), r.Version);
        Assert.True(r.IsInstallable);
        Assert.Equal("VeriCat.exe", r.Executable!.Name);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero), r.PublishedAt);
    }

    [Fact]
    public void Drafts_and_non_semver_tags_are_ignored()
    {
        Assert.Null(ReleaseInfo.FromGitHubJson(JsonDocument.Parse("""{"tag_name":"v1.0.0","draft":true}""").RootElement));
        Assert.Null(ReleaseInfo.FromGitHubJson(JsonDocument.Parse("""{"tag_name":"nightly"}""").RootElement));
    }

    [Fact]
    public void Release_without_checksum_is_not_installable()
    {
        var r = Sample() with { Assets = Sample().Assets.Where(a => a.Name == "VeriCat.exe").ToList() };
        Assert.False(r.IsInstallable);
    }
}

public class UpdatePolicyTests
{
    static readonly SemVersion Current = SemVersion.Parse("1.3.0");

    [Fact]
    public void Offers_newer_installable_release() =>
        Assert.True(UpdatePolicy.ShouldOffer(Current, ReleaseInfoTests.Sample(), null, manual: false));

    [Fact]
    public void Does_not_offer_same_or_older() =>
        Assert.False(UpdatePolicy.ShouldOffer(SemVersion.Parse("1.4.0"), ReleaseInfoTests.Sample(), null, manual: false));

    [Fact]
    public void Skipped_version_is_offered_only_on_manual_check()
    {
        Assert.False(UpdatePolicy.ShouldOffer(Current, ReleaseInfoTests.Sample(), "1.4.0", manual: false));
        Assert.True(UpdatePolicy.ShouldOffer(Current, ReleaseInfoTests.Sample(), "1.4.0", manual: true));
    }

    [Fact]
    public void Detects_first_run_after_update()
    {
        Assert.True(UpdatePolicy.IsFirstRunAfterUpdate(Current, "1.2.9"));
        Assert.False(UpdatePolicy.IsFirstRunAfterUpdate(Current, "1.3.0"));
        Assert.False(UpdatePolicy.IsFirstRunAfterUpdate(Current, null));           // ilk kurulum
        Assert.False(UpdatePolicy.IsFirstRunAfterUpdate(SemVersion.Zero, "1.0.0")); // geliştirici derlemesi
    }
}

public class ReleaseNotesTests
{
    [Fact]
    public void Turns_changelog_markdown_into_clean_lines()
    {
        var lines = ReleaseNotes.Parse(ReleaseInfoTests.Sample().Notes);

        Assert.Equal(2, lines.Count);   // sürüm başlığı atlanır
        Assert.Equal(NoteKind.Heading, lines[0].Kind);
        Assert.Equal("Yenilikler", lines[0].PlainText);
        Assert.Equal(NoteKind.Bullet, lines[1].Kind);
        Assert.Equal("kedi: kavga eder", lines[1].PlainText);   // commit bağlantısı atıldı
        Assert.Equal(("kedi:", true), lines[1].Spans[0]);
    }

    [Fact]
    public void Empty_notes_give_no_lines() => Assert.Empty(ReleaseNotes.Parse(null));
}

public class DownloadTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "vericat-dl-" + Guid.NewGuid().ToString("N"));
    static readonly byte[] Payload = Encoding.UTF8.GetBytes("MZ fake exe");

    sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    static HttpResponseMessage Ok(byte[] body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    static FakeHandler Server(string checksum) => new(req => req.RequestUri!.AbsolutePath.EndsWith(".sha256")
        ? Ok(Encoding.UTF8.GetBytes(checksum + "  VeriCat.exe\n"))
        : Ok(Payload));

    static GitHubReleaseClient Client(FakeHandler h) =>
        new(new HttpClient(h), "furkanisikay", "VeriCat", SemVersion.Parse("1.3.0"));

    [Fact]
    public async Task Downloads_and_verifies_checksum()
    {
        var target = Path.Combine(dir, "VeriCat-1.4.0.exe");
        var progress = new List<double>();

        await Client(Server(Convert.ToHexString(SHA256.HashData(Payload)))).DownloadAsync(
            ReleaseInfoTests.Sample(), target, new SyncProgress(progress.Add));

        Assert.Equal(Payload, await File.ReadAllBytesAsync(target));
        Assert.Equal(1, progress[^1]);
    }

    [Fact]
    public async Task Rejects_tampered_download_and_leaves_nothing_behind()
    {
        var target = Path.Combine(dir, "VeriCat-1.4.0.exe");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Client(Server(new string('0', 64))).DownloadAsync(ReleaseInfoTests.Sample(), target));

        Assert.False(File.Exists(target));
        Assert.False(File.Exists(target + ".partial"));
    }

    [Fact]
    public async Task Refuses_assets_hosted_elsewhere()
    {
        var evil = ReleaseInfoTests.Sample() with
        {
            Assets = new[]
            {
                new ReleaseAsset("VeriCat.exe", new Uri("https://evil.example/VeriCat.exe"), 1),
                new ReleaseAsset("VeriCat.exe.sha256", new Uri("https://evil.example/VeriCat.exe.sha256"), 1),
            },
        };
        var handler = Server("x");

        await Assert.ThrowsAsync<InvalidDataException>(() => Client(handler).DownloadAsync(evil, Path.Combine(dir, "x.exe")));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789  VeriCat.exe", true)]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", true)]
    [InlineData("not-a-hash", false)]
    [InlineData("", false)]
    public void Parses_checksum_files(string text, bool valid) => Assert.Equal(valid, Checksums.Parse(text) != null);

    sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
