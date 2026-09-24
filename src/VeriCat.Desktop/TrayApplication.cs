using System.Diagnostics;
using System.Drawing.Imaging;
using VeriCat.Core.Appearance;
using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Core.Updates;
using VeriCat.Core.World;
using VeriCat.Desktop.Audio;
using VeriCat.Desktop.Customization;
using VeriCat.Desktop.Native;
using VeriCat.Desktop.Platform;
using VeriCat.Desktop.Rendering;
using VeriCat.Desktop.Theming;
using VeriCat.Desktop.Updates;
using VeriCat.Desktop.Views;

namespace VeriCat.Desktop;

/// <summary>Görev çubuğu tepsisindeki uygulama: kedileri, dünya taramasını, güncellemeleri ve ana döngüyü yönetir.</summary>
internal sealed class TrayApplication : ApplicationContext, ICatHost
{
    const double ScanInterval = 1.0 / 15, FullscreenInterval = 0.5;

    readonly SettingsStore store = SettingsStore.ForCurrentUser();
    readonly AppSettings settings;
    readonly Stopwatch stopwatch = Stopwatch.StartNew();
    readonly double dpi;
    readonly WorldModel world = new();
    readonly Win32WorldScanner scanner;
    readonly FullscreenDetector fullscreen = new();
    readonly SoundPlayerVoice voice;
    readonly CatEnvironment env;
    readonly Colony colony;
    readonly Dictionary<Cat, CatWindow> windows = new();
    readonly List<Image> menuImages = new();
    readonly UpdateService updates;
    readonly NativeWindow menuHost = new();
    readonly NotifyIcon tray;
    readonly ContextMenuStrip trayMenu;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    CustomizeForm? customizer;
    UpdateDialog? updateDialog;
    Cat? customizing;
    Action? balloonAction;
    double last, scanClock, fullscreenClock;
    bool hidden;

    public TrayApplication()
    {
        settings = store.Load();
        using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX / 96.0;
        scanner = new Win32WorldScanner(dpi);
        voice = new SoundPlayerVoice(settings, Now);
        env = new CatEnvironment
        {
            World = world, Voice = voice, Pointer = new CursorPointer(), Settings = settings, Clock = Now, Dpi = dpi,
        };
        colony = new Colony(env);
        updates = new UpdateService(settings, Save);
        updates.AvailableChanged += (_, _) => OnUpdateAvailable();

        menuHost.CreateHandle(new CreateParams());
        RefreshWorld();

        trayMenu = MenuStyle.Apply(new ContextMenuStrip());
        trayMenu.Opening += (_, _) => PopulateTrayMenu();
        tray = new NotifyIcon { Icon = MakeIcon(), Text = "VeriCat", Visible = true, ContextMenuStrip = trayMenu };
        tray.MouseClick += (_, e) =>
        {
            // Sol tık da menüyü açsın.
            if (e.Button == MouseButtons.Left)
                typeof(NotifyIcon).GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(tray, null);
        };
        tray.BalloonTipClicked += (_, _) => { var a = balloonAction; balloonAction = null; a?.Invoke(); };

        var saved = settings.Cats.ToList();
        if (saved.Count == 0) AddCat(); else saved.ForEach(Spawn);

        Application.ApplicationExit += (_, _) => { tray.Visible = false; Save(); };
        last = Now();
        timer.Tick += (_, _) => Tick();
        timer.Start();

        updates.Start();
        _ = ShowWhatsNewIfUpdatedAsync();
    }

    double Now() => stopwatch.Elapsed.TotalSeconds;

    void Tick()
    {
        double now = Now(), dt = Math.Min(0.05, now - last);
        last = now;

        fullscreenClock += dt;
        if (fullscreenClock > FullscreenInterval)
        {
            fullscreenClock = 0;
            SetHidden(settings.HideInFullscreen && fullscreen.IsForegroundFullscreen());
        }
        if (hidden) return;   // tam ekran: ne tarama ne çizim, işlemci boşta

        scanClock += dt;
        if (scanClock > ScanInterval) { scanClock = 0; RefreshWorld(); }
        colony.Step(dt);
    }

    void RefreshWorld()
    {
        var found = settings.Windows ? scanner.Windows(settings.InnerWindows) : new List<WindowSnapshot>();
        world.Update(scanner.Screens(), found, dpi, settings.InnerWindows);
    }

    /// <summary>Tam ekran uygulama öndeyken kedileri saklar; çıkınca geri getirir.</summary>
    void SetHidden(bool value)
    {
        if (value == hidden) return;
        hidden = value;
        foreach (var w in windows.Values)
        {
            if (hidden) w.Hide();
            else w.Show();
        }
        if (!hidden) { RefreshWorld(); last = Now(); }
    }

    /// <summary>
    /// Kedinin penceresi odak çalmadığı için menü, gizli bir pencereyi öne alarak açılır; böylece dışarı tıklayınca kapanır.
    /// </summary>
    void ShowMenu(ContextMenuStrip m)
    {
        NativeMethods.SetForegroundWindow(menuHost.Handle);
        m.Closed += (_, _) => Task.Delay(500).ContinueWith(_ => m.Dispose(), TaskScheduler.FromCurrentSynchronizationContext());
        m.Show(Cursor.Position);
    }

    void Balloon(string title, string text, Action? onClick = null)
    {
        balloonAction = onClick;
        tray.ShowBalloonTip(6000, title, text, ToolTipIcon.None);
    }

    // MARK: Kediler

    void Spawn(CatConfig config)
    {
        var (screen, _) = world.ScreenNear(Cursor.Position.X, -Cursor.Position.Y);
        double x = screen.Bounds.MinX + 100 + Random.Shared.NextDouble() * Math.Max(1, screen.Bounds.Width - 200);
        var window = new CatWindow(this, settings, dpi);
        var cat = new Cat(config, env, window, x, screen.WorkTop - 160 * dpi);
        window.Attach(cat);
        cat.ConfigChanged += (_, _) => Save();
        colony.Add(cat);
        windows[cat] = window;
        cat.Present(force: true);
        if (!hidden) window.Show();
    }

    void Save()
    {
        settings.Cats = colony.Cats.Select(c => c.Config).ToList();
        store.Save(settings);
    }

    public void AddCat()
    {
        Spawn(CatConfig.Preset(colony.Cats.Count, settings.Scale, Random.Shared));
        Save();
    }

    void AddRandomCat()
    {
        Spawn(CatConfig.Random(Random.Shared, settings.Scale));
        Save();
    }

    void Duplicate(Cat cat)
    {
        var copy = cat.Config.Clone();
        copy.Name = CatConfig.SanitizeName(copy.Name + " 2") ?? copy.Name;
        Spawn(copy);
        Save();
    }

    public void CloseCat(Cat cat)
    {
        colony.Remove(cat);
        windows.Remove(cat);
        cat.Dismiss();
        if (customizing == cat) customizer?.Close();
        Save();
    }

    public void Customize(Cat cat)
    {
        if (customizer == null || customizer.IsDisposed)
        {
            customizer = new CustomizeForm(this, settings);
            customizer.FormClosed += (_, _) => customizing = null;
        }
        customizing = cat;
        customizer.Open(cat);
    }

    // MARK: Kedi menüsü

    public void ShowCatMenu(Cat cat)
    {
        cat.CancelGrab();
        var m = MenuStyle.Apply(new ContextMenuStrip());
        int icon = MenuStyle.IconSize(m);
        var coat = Coat.From(cat.Config);
        m.Items.Add(new MenuHeader(Avatar(coat, (int)(30 * dpi)), cat.Config.Name, Describe(cat), coat.Collar));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(MenuStyle.Item("Miyavla", Glyph.Paw, icon, cat.Meow));
        m.Items.Add(MenuStyle.Item(cat.IsAsleep ? "Uyandır" : "Uyut", Glyph.Moon, icon, () => { if (cat.IsAsleep) cat.Wake(); else cat.Sleep(); }));
        m.Items.Add(MenuStyle.Item("Yanıma gel", Glyph.Target, icon, cat.Summon));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(MenuStyle.Item("Özelleştir…", Glyph.Palette, icon, () => Customize(cat)));
        m.Items.Add(MenuStyle.Item("Kopyasını oluştur", Glyph.Plus, icon, () => Duplicate(cat)));
        m.Items.Add(MenuStyle.Item("Rastgele yeni kedi", Glyph.Dice, icon, AddRandomCat));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(MenuStyle.Item("Bu kediyi kapat", Glyph.Close, icon, () => CloseCat(cat), MenuTag.Danger));
        m.Closed += (_, _) => cat.Paused = false;
        cat.Paused = true;
        ShowMenu(m);
    }

    static string Describe(Cat cat)
    {
        string state = cat.State switch
        {
            CatState.Walk => "Geziniyor",
            CatState.Chase => "Fareyi kovalıyor",
            CatState.Sit => "Oturuyor",
            CatState.Happy => "Mutlu",
            CatState.Sleep => "Uyuyor",
            CatState.Crouch or CatState.Air => "Zıplıyor",
            CatState.Dragged => "Havada",
            CatState.Petted => "Mırlıyor",
            CatState.Flee => "Kaçıyor",
            CatState.Fight => "Kavga ediyor!",
            CatState.Stalk => "Pusuda",
            CatState.Swat => "Yumruk atıyor",
            _ => "",
        };
        var t = cat.Config.Personality;
        var (value, word) = new[]
        {
            (t.Playfulness, "oyuncu"), (t.Temper, "kavgacı"), (t.Affection, "sevecen"), (t.Energy, "enerjik"), (1 - t.Energy, "uykucu"),
        }.MaxBy(x => x.Item1);
        return value > 0.65 ? $"{state} · {word}" : state;
    }

    static Bitmap Avatar(Coat coat, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        CatPainter.DrawHead(g, coat, size);
        return bmp;
    }

    // MARK: Tepsi menüsü

    void PopulateTrayMenu()
    {
        var m = trayMenu;
        foreach (ToolStripItem old in m.Items.Cast<ToolStripItem>().ToList()) old.Dispose();
        m.Items.Clear();
        foreach (var img in menuImages) img.Dispose();
        menuImages.Clear();
        int icon = MenuStyle.IconSize(m);
        var P = Theme.Current;

        m.Items.Add(new MenuHeader(Avatar(Coat.From(CoatSpec.Presets[0].Spec, CoatSpec.CollarPresets[0]), (int)(30 * dpi)),
            "VeriCat", $"{colony.Cats.Count} kedi · {AppInfo.DisplayVersion}", P.Accent));
        m.Items.Add(new ToolStripSeparator());

        if (updates.Available is ReleaseInfo r)
            m.Items.Add(MenuStyle.Item($"Güncelleme var: v{r.Version}", Glyph.Sparkle, icon, () => OpenUpdateDialog(r), MenuTag.Highlight));

        var add = MenuStyle.Submenu("Kedi ekle", Glyph.Plus, icon);
        add.DropDownItems.Add(MenuStyle.Item("Sıradaki hazır kedi", Glyph.Paw, icon, AddCat));
        add.DropDownItems.Add(MenuStyle.Item("Rastgele kedi", Glyph.Dice, icon, AddRandomCat));
        m.Items.Add(add);

        var customize = MenuStyle.Submenu("Özelleştir", Glyph.Palette, icon);
        foreach (var c in colony.Cats)
        {
            var cat = c;
            var avatar = Avatar(Coat.From(cat.Config), icon);
            menuImages.Add(avatar);
            customize.DropDownItems.Add(new ToolStripMenuItem(cat.Config.Name, avatar, (_, _) => Customize(cat))
            { Padding = new Padding(2, 3, 2, 3) });
        }
        if (colony.Cats.Count == 0) customize.DropDownItems.Add(new ToolStripMenuItem("Hiç kedi yok") { Enabled = false });
        m.Items.Add(customize);

        m.Items.Add(MenuStyle.Item("Kedileri çağır", Glyph.Target, icon, () => { foreach (var c in colony.Cats) c.Summon(); }));
        m.Items.Add(MenuStyle.Item("Hepsi miyavlasın", Glyph.Paw, icon, MeowAll));
        m.Items.Add(MenuStyle.Item(colony.Cats.Any(c => c.IsAsleep) ? "Hepsini uyandır" : "Hepsini uyut", Glyph.Moon, icon, () =>
        {
            if (colony.Cats.Any(c => c.IsAsleep)) foreach (var c in colony.Cats) c.Wake();
            else foreach (var c in colony.Cats) c.Sleep();
        }));

        var size = MenuStyle.Submenu("Boyut (hepsi)", Glyph.Resize, icon);
        foreach (var (name, v) in new[] { ("Küçük", 0.6), ("Orta", 1.0), ("Büyük", 1.5), ("Dev", 2.2) })
        {
            var item = new ToolStripMenuItem(name) { Checked = Math.Abs(v - settings.Scale) < 0.01, Padding = new Padding(2, 3, 2, 3) };
            item.Click += (_, _) =>
            {
                settings.Scale = v;
                foreach (var c in colony.Cats) c.Update(cfg => cfg.Scale = v);
                Save();
            };
            size.DropDownItems.Add(item);
        }
        m.Items.Add(size);

        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(MenuStyle.Caption("DAVRANIŞ"));
        // Her ayar açılınca etkisi hemen görünür (menü açık kalır), üstüne gelince ne yaptığı yazar.
        m.Items.Add(Toggle("Pencerelerin üstüne çıksın", Glyph.Window, icon, () => settings.Windows,
            v => { settings.Windows = v; RefreshWorld(); if (v) HopOne(); },
            "Kediler açık pencerelerin üst kenarında yürür ve aralarında zıplar. Kapatınca pencerelerdeki kediler aşağı iner."));
        m.Items.Add(Toggle("Pencere içlerine de zıplasın", Glyph.Layers, icon, () => settings.InnerWindows,
            v => { settings.InnerWindows = v; RefreshWorld(); if (v) HopOne(); },
            "Ara ara pencerelerin içindeki bölümlere (araç çubuğu, panel, liste) de çıkarlar. Klasik Windows uygulamalarında belirgindir; Chrome gibi tarayıcılarda çalışmaz."));
        m.Items.Add(Toggle("Fareyi kovalasın", Glyph.Target, icon, () => settings.Chase,
            v => { settings.Chase = v; foreach (var c in colony.Cats) { if (v) c.PlayWithPointer(); else c.StopHunting(); } },
            "Kediler imleci kovalar, pusuya yatar, üstüne atlar ve yumruklar."));
        m.Items.Add(Toggle("Yumruk imleci itsin", Glyph.Fist, icon, () => settings.PunchCursor,
            v => { settings.PunchCursor = v; if (v && settings.Chase) foreach (var c in colony.Cats) c.PlayWithPointer(); },
            "Kedinin yumruğu imlece isabet edince imleç biraz itilir. Fare tuşu basılıyken hiçbir zaman itmez."));
        m.Items.Add(Toggle("Tasmada isim görünsün", Glyph.Tag, icon, () => settings.ShowNames, v => settings.ShowNames = v,
            "Her kedinin tasmasında adının yazdığı etiket."));
        m.Items.Add(Toggle("Tam ekranda saklansın", Glyph.EyeOff, icon, () => settings.HideInFullscreen, v => settings.HideInFullscreen = v,
            "Oyun, video ya da sunum tam ekrandayken kediler gizlenir ve arka planda hiçbir şey çalışmaz; çıkınca geri gelirler."));
        m.Items.Add(Toggle("Ses", Glyph.Sound, icon, () => settings.Sound, v => { settings.Sound = v; if (v) colony.Cats.FirstOrDefault()?.Meow(); },
            "Miyavlama, mırlama, tıslama ve pati sesleri."));

        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(MenuStyle.Caption("UYGULAMA"));
        m.Items.Add(Toggle("Windows ile başlat", Glyph.Power, icon, () => AutoStart.IsEnabled, AutoStart.Set,
            "Bilgisayar açılınca VeriCat da açılır."));

        var upd = MenuStyle.Submenu("Güncellemeler", Glyph.Update, icon);
        foreach (var (name, mode) in new[] { ("Otomatik yükle", UpdateMode.Automatic), ("Sorarak yükle", UpdateMode.Notify), ("Kapalı", UpdateMode.Off) })
        {
            var item = new ToolStripMenuItem(name) { Checked = settings.Updates == mode, Padding = new Padding(2, 3, 2, 3) };
            item.Click += (_, _) => { settings.Updates = mode; Save(); };
            upd.DropDownItems.Add(item);
        }
        upd.DropDownItems.Add(new ToolStripSeparator());
        upd.DropDownItems.Add(MenuStyle.Item("Şimdi denetle", Glyph.Update, icon, () => _ = CheckForUpdatesAsync()));
        m.Items.Add(upd);

        m.Items.Add(MenuStyle.Item($"Yenilikler ({AppInfo.DisplayVersion})", Glyph.Info, icon, () => _ = ShowWhatsNewAsync(AppInfo.Version)));
        m.Items.Add(MenuStyle.Item("Çıkış", Glyph.Exit, icon, Application.Exit));

        MenuStyle.StyleSubmenus(m.Items);
    }

    ToggleMenuItem Toggle(string title, Glyph glyph, int icon, Func<bool> get, Action<bool> set, string tip) =>
        MenuStyle.Toggle(title, glyph, icon, get, v => { set(v); Save(); }, tip);

    /// <summary>Ayar açılınca etkisi görünsün: boştaki kedilerden biri hemen bir pencereye zıplar.</summary>
    void HopOne()
    {
        foreach (var c in colony.Cats.OrderBy(_ => Random.Shared.Next()))
            if (c.Hop()) return;
    }

    void MeowAll()
    {
        var ui = TaskScheduler.FromCurrentSynchronizationContext();
        for (int i = 0; i < colony.Cats.Count; i++)
        {
            var c = colony.Cats[i];
            Task.Delay(i * 350).ContinueWith(_ => c.Meow(), ui);
        }
    }

    // MARK: Güncellemeler

    void OnUpdateAvailable()
    {
        if (updates.Available is not ReleaseInfo r || settings.Updates != UpdateMode.Notify) return;
        Balloon($"VeriCat v{r.Version} çıktı", "Yenilikleri görmek ve güncellemek için tıkla.", () => OpenUpdateDialog(r));
    }

    void OpenUpdateDialog(ReleaseInfo release)
    {
        if (updateDialog is { IsDisposed: false }) { updateDialog.Activate(); return; }
        updateDialog = new UpdateDialog(release, updates, settings, Save);
        updateDialog.Show();
        updateDialog.Activate();
    }

    async Task CheckForUpdatesAsync()
    {
        try
        {
            if (await updates.CheckAsync(manual: true) is ReleaseInfo r) OpenUpdateDialog(r);
            else Balloon("VeriCat güncel", $"En son sürümü kullanıyorsun ({AppInfo.DisplayVersion}).");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Balloon("Güncelleme denetlenemedi", "GitHub'a ulaşılamadı. İnternet bağlantını kontrol et.");
        }
    }

    /// <summary>Güncellemeden sonraki ilk açılışta yenilikleri duyurur.</summary>
    async Task ShowWhatsNewIfUpdatedAsync()
    {
        bool updated = UpdatePolicy.IsFirstRunAfterUpdate(AppInfo.Version, settings.LastSeenVersion);
        if (!AppInfo.Version.IsDevelopment && settings.LastSeenVersion != AppInfo.Version.ToString())
        {
            settings.LastSeenVersion = AppInfo.Version.ToString();
            Save();
        }
        if (!updated) return;
        await Task.Delay(1500);
        Balloon($"VeriCat v{AppInfo.Version} sürümüne güncellendi", "Neler yeni? Görmek için tıkla.", () => _ = ShowWhatsNewAsync(AppInfo.Version));
    }

    async Task ShowWhatsNewAsync(SemVersion version)
    {
        var notes = version.IsDevelopment ? null : await updates.NotesForAsync(version);
        if (notes == null)
        {
            Process.Start(new ProcessStartInfo(AppInfo.RepoUrl + "/releases") { UseShellExecute = true });
            return;
        }
        using var dlg = new UpdateDialog(notes, null, settings, Save);
        dlg.ShowDialog();
    }

    Icon MakeIcon()
    {
        int size = (int)Math.Round(16 * dpi) * 2;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) CatPainter.DrawHead(g, Coat.From(CoatSpec.Presets[0].Spec, 0), size);
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            tray.Dispose();
            trayMenu.Dispose();
            voice.Dispose();
            updates.Dispose();
            menuHost.DestroyHandle();
        }
        base.Dispose(disposing);
    }
}
