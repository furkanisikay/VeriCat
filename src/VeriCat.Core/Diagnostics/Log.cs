using System.Globalization;
using System.Text;

namespace VeriCat.Core.Diagnostics;

/// <summary>
/// Küçük, dönen (rolling) dosya günlüğü. Sorun bildirirken son satırlar rapora eklenir.
/// Kişisel veri yazılmaz: pencere başlıkları, dosya içerikleri vb. asla günlüğe girmez.
/// </summary>
public static class Log
{
    /// <summary>Bu boyutu aşınca dosya ".1" olarak saklanır ve yenisine başlanır.</summary>
    public const long MaxBytes = 512 * 1024;

    static readonly object Gate = new();
    static string? path;

    public static string? FilePath => path;

    /// <summary>%LOCALAPPDATA%\VeriCat\logs\vericat.log</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VeriCat", "logs", "vericat.log");

    public static void Configure(string file)
    {
        lock (Gate)
        {
            path = file;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? e = null) => Write("ERROR", e == null ? message : $"{message}: {e}");

    static void Write(string level, string message)
    {
        lock (Gate)
        {
            if (path == null) return;
            try
            {
                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxBytes) File.Move(path, path + ".1", overwrite: true);
                var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }   // günlük yazılamazsa uygulama durmaz
        }
    }

    /// <summary>Günlüğün son <paramref name="maxChars"/> karakteri (tam satırlardan başlar).</summary>
    public static string Tail(int maxChars)
    {
        lock (Gate)
        {
            if (path == null || !File.Exists(path)) return "";
            try
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                if (text.Length <= maxChars) return text;
                var tail = text[^maxChars..];
                int nl = tail.IndexOf('\n');
                return nl >= 0 ? tail[(nl + 1)..] : tail;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
        }
    }
}
