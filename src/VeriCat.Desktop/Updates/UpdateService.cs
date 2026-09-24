using VeriCat.Core.Configuration;
using VeriCat.Core.Diagnostics;
using VeriCat.Core.Updates;

namespace VeriCat.Desktop.Updates;

/// <summary>
/// Güncelleme döngüsü: açılıştan kısa süre sonra ve 6 saatte bir GitHub'ı denetler.
/// Otomatik modda sessizce indirip kurar; bildirim modunda yalnızca "güncelleme var" der.
/// </summary>
internal sealed class UpdateService : IDisposable
{
    static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(20), Interval = TimeSpan.FromHours(6);

    readonly AppSettings settings;
    readonly Action save;
    readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(5) };
    readonly GitHubReleaseClient client;
    readonly System.Windows.Forms.Timer timer = new();
    bool checking;

    public UpdateService(AppSettings settings, Action save)
    {
        this.settings = settings;
        this.save = save;
        client = new GitHubReleaseClient(http, AppInfo.Owner, AppInfo.Repo, AppInfo.Version);
        timer.Tick += async (_, _) =>
        {
            timer.Interval = (int)Interval.TotalMilliseconds;
            await CheckQuietlyAsync();
        };
    }

    /// <summary>Sunulmayı bekleyen yeni sürüm.</summary>
    public ReleaseInfo? Available { get; private set; }

    /// <summary>İndirme/kurulum sürüyor.</summary>
    public bool Installing { get; private set; }

    public event EventHandler? AvailableChanged;

    /// <summary>Otomatik denetimi başlatır. Geliştirici derlemesinde ve "kapalı" modda çalışmaz.</summary>
    public void Start()
    {
        UpdateInstaller.CleanupInBackground();
        if (AppInfo.Version.IsDevelopment) return;
        timer.Interval = (int)FirstCheck.TotalMilliseconds;
        timer.Start();
    }

    async Task CheckQuietlyAsync()
    {
        if (settings.Updates == UpdateMode.Off || checking || Installing) return;
        try
        {
            var release = await CheckAsync(manual: false);
            if (release != null && settings.Updates == UpdateMode.Automatic && UpdateInstaller.CanInstall())
                await InstallAsync(release, null, CancellationToken.None);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // Ağ yok, GitHub erişilemiyor ya da dosya doğrulanamadı: bir sonraki denetimde tekrar denenir.
            Log.Warn($"Güncelleme denetimi başarısız: {e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>GitHub'daki son sürüme bakar; sunulacak bir sürüm varsa döndürür. Elle denetimde hatalar fırlatılır.</summary>
    public async Task<ReleaseInfo?> CheckAsync(bool manual)
    {
        checking = true;
        try
        {
            var latest = await client.LatestAsync();
            var offer = UpdatePolicy.ShouldOffer(AppInfo.Version, latest, settings.SkippedVersion, manual) ? latest : null;
            Log.Info($"Güncelleme denetimi: en son {latest?.Version.ToString() ?? "yok"}, sunulan {offer?.Version.ToString() ?? "yok"}");
            if (offer != Available)
            {
                Available = offer;
                AvailableChanged?.Invoke(this, EventArgs.Empty);
            }
            return offer;
        }
        finally { checking = false; }
    }

    /// <summary>İndirir, doğrular, exe'yi değiştirir ve uygulamayı yeni sürümle yeniden başlatır.</summary>
    public async Task InstallAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken ct)
    {
        if (!UpdateInstaller.CanInstall())
            throw new UnauthorizedAccessException("VeriCat'in bulunduğu klasöre yazılamıyor. Exe'yi yazılabilir bir klasöre taşıyın ya da sürümü GitHub'dan elle indirin.");
        Installing = true;
        try
        {
            var path = Path.Combine(UpdateInstaller.DownloadFolder, $"VeriCat-{release.Version}.exe");
            Log.Info($"v{release.Version} indiriliyor");
            await client.DownloadAsync(release, path, progress, ct);
            settings.SkippedVersion = null;
            save();
            Log.Info($"v{release.Version} doğrulandı, kuruluyor ve yeniden başlatılıyor");
            UpdateInstaller.InstallAndRestart(path);
            Application.Exit();
        }
        finally { Installing = false; }
    }

    public void Skip(ReleaseInfo release)
    {
        settings.SkippedVersion = release.Version.ToString();
        save();
        Available = null;
        AvailableChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Belirli bir sürümün notları (güncellemeden sonra "yenilikler" için).</summary>
    public async Task<ReleaseInfo?> NotesForAsync(SemVersion version)
    {
        try { return await client.ByVersionAsync(version); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return null; }
    }

    public void Dispose()
    {
        timer.Dispose();
        http.Dispose();
    }
}
