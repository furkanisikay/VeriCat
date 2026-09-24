using System.Text.RegularExpressions;

namespace VeriCat.Core.Updates;

public enum NoteKind { Heading, Bullet, Paragraph }

/// <summary>Sürüm notunun bir satırı. <see cref="Spans"/>: (metin, kalın mı).</summary>
public sealed record NoteLine(NoteKind Kind, IReadOnlyList<(string Text, bool Bold)> Spans)
{
    public string PlainText => string.Concat(Spans.Select(s => s.Text));
}

/// <summary>
/// GitHub sürüm notlarındaki (conventional-changelog çıktısı) Markdown'ı uygulama içinde gösterilecek
/// basit satırlara çevirir: başlıklar, maddeler, kalın yazı. Bağlantılar ve commit özetleri atılır.
/// </summary>
public static partial class ReleaseNotes
{
    public static IReadOnlyList<NoteLine> Parse(string? markdown)
    {
        var lines = new List<NoteLine>();
        foreach (var raw in (markdown ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0) continue;
            var t = line.TrimStart();
            NoteKind kind;
            if (t.StartsWith('#')) { kind = NoteKind.Heading; t = t.TrimStart('#').Trim(); }
            else if (t.StartsWith("* ") || t.StartsWith("- ")) { kind = NoteKind.Bullet; t = t[2..].Trim(); }
            else kind = NoteKind.Paragraph;

            t = CommitRef().Replace(t, "");                     // " ([abc1234](…))"
            t = Link().Replace(t, "$1");                        // [metin](adres) → metin
            t = t.Replace("`", "").Trim();
            // "## [1.2.0](…) (2026-09-24)" gibi sürüm başlıkları diyalog başlığında zaten var.
            if (kind == NoteKind.Heading && VersionHeading().IsMatch(t)) continue;
            if (t.Length == 0) continue;
            lines.Add(new NoteLine(kind, Bold(t)));
        }
        return lines;
    }

    static List<(string, bool)> Bold(string text)
    {
        var spans = new List<(string, bool)>();
        var parts = text.Split("**");
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) spans.Add((parts[i], i % 2 == 1));
        return spans;
    }

    [GeneratedRegex(@"\s*\(\[[0-9a-f]{7,40}\]\([^)]*\)\)", RegexOptions.IgnoreCase)]
    private static partial Regex CommitRef();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^v?\d+\.\d+\.\d+\S*(\s+\(\d{4}-\d{2}-\d{2}\))?$")]
    private static partial Regex VersionHeading();
}
