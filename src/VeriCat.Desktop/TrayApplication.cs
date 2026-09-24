using System.Diagnostics;
using System.Drawing.Imaging;
using VeriCat.Core.Appearance;
using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Core.World;
using VeriCat.Desktop.Audio;
using VeriCat.Desktop.Customization;
using VeriCat.Desktop.Native;
using VeriCat.Desktop.Platform;
using VeriCat.Desktop.Rendering;
using VeriCat.Desktop.Views;

namespace VeriCat.Desktop;

/// <summary>Görev çubuğu tepsisindeki uygulama: kedileri, dünya taramasını ve ana döngüyü yönetir.</summary>
internal sealed class TrayApplication : ApplicationContext, ICatHost
{
    const double ScanInterval = 1.0 / 15;

    readonly SettingsStore store = SettingsStore.ForCurrentUser();
    readonly AppSettings settings;
    readonly Stopwatch stopwatch = Stopwatch.StartNew();
    readonly double dpi;
    readonly WorldModel world = new();
    readonly Win32WorldScanner scanner;
    readonly SoundPlayerVoice voice;
    readonly CatEnvironment env;
    readonly Colony colony;
    readonly NativeWindow menuHost = new();
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    CustomizeForm? customizer;
    Cat? customizing;
    double last, scanClock;

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

        menuHost.CreateHandle(new CreateParams());
        RefreshWorld();

        tray = new NotifyIcon { Icon = MakeIcon(), Text = "VeriCat", Visible = true, ContextMenuStrip = BuildMenu() };
        tray.MouseClick += (_, e) =>
        {
            // Sol tık da menüyü açsın.
            if (e.Button == MouseButtons.Left)
                typeof(NotifyIcon).GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(tray, null);
        };

        var saved = settings.Cats.ToList();
        if (saved.Count == 0) AddCat(); else saved.ForEach(Spawn);

        Application.ApplicationExit += (_, _) => { tray.Visible = false; Save(); };
        last = Now();
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    double Now() => stopwatch.Elapsed.TotalSeconds;

    void Tick()
    {
        double now = Now(), dt = Math.Min(0.05, now - last);
        last = now;
        scanClock += dt;
        if (scanClock > ScanInterval) { scanClock = 0; RefreshWorld(); }
        colony.Step(dt);
    }

    void RefreshWorld()
    {
        var windows = settings.Windows ? scanner.Windows(settings.InnerWindows) : new List<WindowSnapshot>();
        world.Update(scanner.Screens(), windows, dpi, settings.InnerWindows);
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

    // MARK: Kediler

    void Spawn(CatConfig config)
    {
        double x = world.MinX + 100 + Random.Shared.NextDouble() * Math.Max(1, world.MaxX - world.MinX - 200);
        var window = new CatWindow(this, settings, dpi);
        var cat = new Cat(config, env, window, x, world.TopY - 160 * dpi);
        window.Attach(cat);
        cat.ConfigChanged += (_, _) => Save();
        colony.Add(cat);
        cat.Present();
        window.Show();
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

    public void CloseCat(Cat cat)
    {
        colony.Remove(cat);
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

    public void ShowCatMenu(Cat cat)
    {
        cat.CancelGrab();
        var m = new ContextMenuStrip();
        m.Items.Add(new ToolStripMenuItem(cat.Config.Name) { Enabled = false });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Miyavla", null, (_, _) => cat.Meow());
        m.Items.Add(cat.IsAsleep ? "Uyandır" : "Uyut", null, (_, _) => { if (cat.IsAsleep) cat.Wake(); else cat.Sleep(); });
        m.Items.Add("Özelleştir…", null, (_, _) => Customize(cat));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Yeni kedi ekle", null, (_, _) => AddCat());
        m.Items.Add("Bu kediyi kapat", null, (_, _) => CloseCat(cat));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Uygulamadan çık", null, (_, _) => Application.Exit());
        m.Closed += (_, _) => cat.Paused = false;
        cat.Paused = true;
        ShowMenu(m);
    }

    // MARK: Tepsi menüsü

    ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip();
        m.Items.Add("Miyavla", null, (_, _) => MeowAll());
        m.Items.Add("Uyut / Uyandır", null, (_, _) =>
        {
            if (colony.Cats.Any(c => c.IsAsleep)) foreach (var c in colony.Cats) c.Wake();
            else foreach (var c in colony.Cats) c.Sleep();
        });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Kedi ekle", null, (_, _) => AddCat());
        var customize = new ToolStripMenuItem("Özelleştir");
        customize.DropDownItems.Add("-");   // DropDownOpening tetiklensin diye yer tutucu
        customize.DropDownOpening += (_, _) =>
        {
            customize.DropDownItems.Clear();
            foreach (var c in colony.Cats) customize.DropDownItems.Add(c.Config.Name, null, (_, _) => Customize(c));
            if (colony.Cats.Count == 0) customize.DropDownItems.Add(new ToolStripMenuItem("Hiç kedi yok") { Enabled = false });
        };
        m.Items.Add(customize);
        m.Items.Add(new ToolStripSeparator());

        var size = new ToolStripMenuItem("Boyut (hepsi)");
        foreach (var (name, v) in new[] { ("Küçük", 0.6), ("Orta", 1.0), ("Büyük", 1.5), ("Dev", 2.2) })
        {
            var item = new ToolStripMenuItem(name) { Tag = v };
            item.Click += (_, _) =>
            {
                settings.Scale = v;
                foreach (var c in colony.Cats) c.Update(cfg => cfg.Scale = v);
                Save();
            };
            size.DropDownItems.Add(item);
        }
        m.Items.Add(size);

        var toggles = new[]
        {
            Toggle("Pencerelerin üstüne çıksın", () => settings.Windows, v => { settings.Windows = v; RefreshWorld(); }),
            Toggle("Pencere içlerine de zıplasın", () => settings.InnerWindows, v => { settings.InnerWindows = v; RefreshWorld(); }),
            Toggle("Fareyi kovalasın", () => settings.Chase, v => settings.Chase = v),
            Toggle("Fareye yumruk atınca imleci itsin", () => settings.PunchCursor, v => settings.PunchCursor = v),
            Toggle("Tasmada isim görünsün", () => settings.ShowNames, v => settings.ShowNames = v),
            Toggle("Ses", () => settings.Sound, v => settings.Sound = v),
            Toggle("Windows ile başlat", () => AutoStart.IsEnabled, AutoStart.Set),
        };
        m.Items.AddRange(toggles);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(new ToolStripMenuItem("İpucu: kediye sağ tıkla, üstünde fareyi gezdirerek okşa") { Enabled = false });
        m.Items.Add("Çıkış", null, (_, _) => Application.Exit());

        m.Opening += (_, _) =>
        {
            foreach (ToolStripMenuItem i in size.DropDownItems) i.Checked = Math.Abs((double)i.Tag! - settings.Scale) < 0.01;
            foreach (var t in toggles) ((Action)t.Tag!)();
        };
        return m;
    }

    ToolStripMenuItem Toggle(string title, Func<bool> get, Action<bool> set)
    {
        var item = new ToolStripMenuItem(title);
        item.Tag = (Action)(() => item.Checked = get());
        item.Click += (_, _) => { set(!get()); Save(); };
        return item;
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
            voice.Dispose();
            menuHost.DestroyHandle();
        }
        base.Dispose(disposing);
    }
}
