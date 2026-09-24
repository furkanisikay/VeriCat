using System.Text;

namespace VeriCat.Core.Diagnostics;

public enum IssueKind
{
    /// <summary>Hata bildirimi.</summary>
    Bug,
    /// <summary>Öneri / istek.</summary>
    Idea,
}

/// <summary>
/// GitHub'da önceden doldurulmuş bir "yeni issue" sayfasının adresini üretir: ortam bilgisi, (varsa) hata ve
/// günlüğün son satırları. Kullanıcı sayfada okuyup düzenler, isterse gönderir; hiçbir şey kendiliğinden gönderilmez.
/// Kullanıcı adı ve ev klasörü metinden çıkarılır; adres GitHub'ın uzunluk sınırını aşmayacak şekilde kısaltılır.
/// </summary>
public static class IssueReport
{
    /// <summary>GitHub ~8 KB üstü adresleri reddediyor; kodlama payı bırakılır.</summary>
    public const int MaxUrlLength = 7500;

    public static Uri Build(
        string repoUrl, IssueKind kind, string version, IReadOnlyDictionary<string, string> environment,
        string? error = null, string? logTail = null, string? userName = null, string? homeFolder = null)
    {
        string title = kind == IssueKind.Bug
            ? (error != null ? "Hata: " + FirstLine(Redact(error, userName, homeFolder), 80) : "Hata: ")
            : "Öneri: ";
        string label = kind == IssueKind.Bug ? "bug" : "enhancement";

        // Öneriye tanılama verisi eklenmez. Hatada önce tam günlükle dene; sığmazsa kısalt, en son hiç koyma.
        string? log = logTail == null || kind != IssueKind.Bug ? null : Redact(logTail, userName, homeFolder);
        string? err = error == null ? null : Redact(error, userName, homeFolder);
        while (true)
        {
            var body = Body(kind, version, environment, Clip(err, 2500), log);
            var url = $"{repoUrl.TrimEnd('/')}/issues/new?labels={label}&title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
            if (url.Length <= MaxUrlLength || (log == null && (err == null || err.Length < 200))) return new Uri(url);
            if (log is { Length: > 400 }) log = log[(log.Length / 2)..];
            else if (log != null) log = null;
            else err = Clip(err, err!.Length / 2);
        }
    }

    static string Body(IssueKind kind, string version, IReadOnlyDictionary<string, string> env, string? error, string? log)
    {
        var sb = new StringBuilder();
        if (kind == IssueKind.Bug)
        {
            sb.AppendLine("### Ne oldu?").AppendLine().AppendLine("<!-- Ne yapıyordun, ne oldu? Mümkünse ekran görüntüsü ekle. -->").AppendLine();
            sb.AppendLine("### Ne olmasını bekliyordun?").AppendLine().AppendLine().AppendLine();
        }
        else
        {
            sb.AppendLine("### Fikir").AppendLine().AppendLine("<!-- Kedilerin ne yapmasını isterdin? -->").AppendLine().AppendLine();
        }

        sb.AppendLine("### Ortam").AppendLine().AppendLine("| | |").AppendLine("|---|---|");
        sb.AppendLine($"| Sürüm | {version} |");
        foreach (var (k, v) in env) sb.AppendLine($"| {k} | {v} |");

        if (error != null)
        {
            sb.AppendLine().AppendLine("### Hata").AppendLine().AppendLine("```").AppendLine(error.TrimEnd()).AppendLine("```");
        }
        if (!string.IsNullOrWhiteSpace(log))
        {
            sb.AppendLine().AppendLine("<details><summary>Günlük (son satırlar)</summary>").AppendLine()
                .AppendLine("```").AppendLine(log.TrimEnd()).AppendLine("```").AppendLine().AppendLine("</details>");
        }
        return sb.ToString();
    }

    /// <summary>Kullanıcı adını ve ev klasörünü gizler.</summary>
    public static string Redact(string text, string? userName, string? homeFolder)
    {
        if (!string.IsNullOrEmpty(homeFolder)) text = text.Replace(homeFolder, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(userName) && userName.Length >= 2) text = text.Replace(userName, "<kullanıcı>", StringComparison.OrdinalIgnoreCase);
        return text;
    }

    static string FirstLine(string s, int max)
    {
        var line = s.Split('\n')[0].Trim();
        return line.Length > max ? line[..max] + "…" : line;
    }

    static string? Clip(string? s, int max) => s == null || s.Length <= max ? s : s[..max] + "\n…";
}
