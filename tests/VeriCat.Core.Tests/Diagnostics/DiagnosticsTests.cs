using VeriCat.Core.Configuration;
using VeriCat.Core.Diagnostics;
using VeriCat.Core.Props;

namespace VeriCat.Core.Tests.Diagnostics;

public class IssueReportTests
{
    static readonly Dictionary<string, string> Env = new() { ["Windows"] = "Windows 11", ["Kedi sayısı"] = "3" };

    static string Decode(Uri u) => Uri.UnescapeDataString(u.Query);

    [Fact]
    public void Bug_report_is_prefilled_with_version_environment_and_log()
    {
        var url = IssueReport.Build("https://github.com/furkanisikay/VeriCat", IssueKind.Bug, "v1.2.0", Env,
            error: "System.InvalidOperationException: bozuldu", logTail: "12:00 [INFO] başladı");
        var q = Decode(url);

        Assert.StartsWith("https://github.com/furkanisikay/VeriCat/issues/new?", url.ToString());
        Assert.Contains("labels=bug", q);
        Assert.Contains("title=Hata: System.InvalidOperationException: bozuldu", q);
        Assert.Contains("| Sürüm | v1.2.0 |", q);
        Assert.Contains("| Windows | Windows 11 |", q);
        Assert.Contains("12:00 [INFO] başladı", q);
    }

    [Fact]
    public void Idea_has_no_log_and_the_enhancement_label()
    {
        var q = Decode(IssueReport.Build("https://github.com/o/r", IssueKind.Idea, "v1.0.0", Env, logTail: "gizli"));

        Assert.Contains("labels=enhancement", q);
        Assert.DoesNotContain("Günlük", q);
    }

    [Fact]
    public void Personal_paths_and_user_name_are_removed()
    {
        var q = Decode(IssueReport.Build("https://github.com/o/r", IssueKind.Bug, "v1", Env,
            error: @"C:\Users\furkan\AppData\x.json bulunamadı", logTail: "furkan kullanıcısı", userName: "furkan", homeFolder: @"C:\Users\furkan"));

        Assert.DoesNotContain("furkan", q, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"%USERPROFILE%\AppData", q);
    }

    [Fact]
    public void Huge_logs_are_trimmed_to_fit_github_url_limit()
    {
        var log = string.Join("\n", Enumerable.Range(0, 5000).Select(i => $"satır {i} ğüşiöç"));
        var url = IssueReport.Build("https://github.com/o/r", IssueKind.Bug, "v1", Env, error: new string('x', 9000), logTail: log);

        Assert.True(url.ToString().Length <= IssueReport.MaxUrlLength, $"{url.ToString().Length} karakter");
        Assert.Contains("| Sürüm | v1 |", Decode(url));   // ortam bilgisi hep kalır
    }
}

public sealed class LogTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "vericat-log-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Writes_rolls_over_and_returns_whole_line_tail()
    {
        var file = Path.Combine(dir, "vericat.log");
        Log.Configure(file);
        for (int i = 0; i < 20; i++) Log.Info($"ileti {i}");

        var tail = Log.Tail(60);
        Assert.Contains("ileti 19", tail);
        Assert.True(tail.Length <= 60);
        Assert.StartsWith(DateTime.Now.Year.ToString(), tail);   // tam satırdan başlar

        File.WriteAllText(file, new string('a', (int)Log.MaxBytes + 10));
        Log.Warn("yeni dosya");
        Assert.True(File.Exists(file + ".1"));
        Assert.True(new FileInfo(file).Length < 200);
    }

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}

public class PropSettingsTests
{
    [Fact]
    public void Props_bonds_and_vitals_survive_a_save()
    {
        var path = Path.Combine(Path.GetTempPath(), "vericat-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(path);
            var s = new AppSettings();
            var cat = new CatConfig { Name = "Tarçın" };
            cat.Vitals.Hunger = 0.25;
            s.Cats.Add(cat);
            s.Bonds["a|b"] = 0.7;
            s.Props.Add(new PropState { Kind = PropKind.Bowl, X = 800, Food = 0.5 });

            store.Save(s);
            var loaded = store.Load();

            Assert.Equal(cat.Id, loaded.Cats[0].Id);
            Assert.Equal(0.25, loaded.Cats[0].Vitals.Hunger);
            Assert.Equal(0.7, loaded.Bonds["a|b"]);
            Assert.Equal(PropKind.Bowl, Assert.Single(loaded.Props).Kind);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Duplicate_cat_ids_are_repaired_on_load()
    {
        var path = Path.Combine(Path.GetTempPath(), "vericat-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var id = Guid.NewGuid();
            File.WriteAllText(path, $$"""{"Cats":[{"Id":"{{id}}","Name":"A"},{"Id":"{{id}}","Name":"B"}]}""");

            var loaded = new SettingsStore(path).Load();

            Assert.NotEqual(loaded.Cats[0].Id, loaded.Cats[1].Id);
        }
        finally { File.Delete(path); }
    }
}
