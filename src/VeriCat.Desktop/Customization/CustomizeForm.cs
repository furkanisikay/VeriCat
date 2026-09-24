using VeriCat.Core.Appearance;
using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Desktop.Rendering;
using VeriCat.Desktop.Theming;
using VeriCat.Desktop.Views;

namespace VeriCat.Desktop.Customization;

/// <summary>
/// Kedi özelleştirme penceresi: canlı önizleme, isim, rastgele kedi ve üç bölüm —
/// Görünüm (kürk, göz, desen), Tasma &amp; aksesuar, Karakter (davranışı etkileyen özellikler + boy/hız/ses).
/// </summary>
internal sealed class CustomizeForm : Form
{
    const int W = 440;

    readonly ICatHost host;
    readonly AppSettings settings;
    Cat? cat;
    bool loading;

    readonly PreviewBox preview = new() { Size = new Size(W, 170), Margin = new Padding(0) };
    readonly TextBox name = new() { Width = 300, MaxLength = CatConfig.MaxNameLength, BorderStyle = BorderStyle.FixedSingle };
    readonly List<SwatchBox> swatches = new();
    readonly List<(ColorDot Dot, uint Value)> eyeDots = new(), collarDots = new();
    readonly ColorDot customFur = new(Color.Gray), customEye = new(Color.Gray), customCollar = new(Color.Gray);
    readonly Dictionary<Markings, Chip> markingChips = new();
    readonly Chip striped = new("Çizgili");
    readonly Dictionary<Accessory, Chip> accessoryChips = new();
    readonly Chip showNames = new("İsim etiketi görünsün");
    readonly Slider playful = new(), temper = new(), affection = new(), energy = new();
    readonly Slider size = new() { Minimum = 50, Maximum = 250 }, speed = new() { Minimum = 40, Maximum = 220 }, pitch = new() { Minimum = 420, Maximum = 950 };
    readonly Dictionary<Slider, (Label Word, string[] Words)> sliderWords = new();
    readonly List<(Chip Tab, Control Page)> tabs = new();
    readonly ToolTip tips = new();

    public CustomizeForm(ICatHost host, AppSettings settings)
    {
        this.host = host;
        this.settings = settings;
        ThemedWindow.Apply(this);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Özelleştir";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(18, 16, 18, 14);
        var p = Theme.Current;

        preview.Click += (_, _) => Listen();
        name.BackColor = p.Surface;
        name.ForeColor = p.Text;
        name.Font = Theme.UiBold;

        var dice = new ThemedButton("Rastgele") { Margin = new Padding(8, 0, 0, 0) };
        dice.FitText();
        dice.Click += (_, _) => Randomize();
        tips.SetToolTip(dice, "İsim hariç her şeyi rastgele seç");
        var nameRow = Row(name, dice);
        nameRow.Margin = new Padding(0, 12, 0, 8);

        var tabRow = Row();
        tabRow.Margin = new Padding(0, 4, 0, 10);
        var pages = new Panel { Width = W, Height = 250, Margin = new Padding(0), BackColor = p.Background };
        foreach (var (title, page) in new[] { ("Görünüm", LookPage()), ("Tasma & aksesuar", CollarPage()), ("Karakter", CharacterPage()) })
        {
            var tab = new Chip(title) { Margin = new Padding(0, 0, 6, 0) };
            tab.FitText();
            page.Dock = DockStyle.Fill;
            page.Visible = tabs.Count == 0;
            tab.Selected = tabs.Count == 0;
            tab.Click += (_, _) => ShowTab(tab);
            tabs.Add((tab, page));
            tabRow.Controls.Add(tab);
            pages.Controls.Add(page);
        }

        var close = new ThemedButton("Kediyi kapat", ButtonKind.Danger);
        close.FitText();
        close.Click += (_, _) =>
        {
            if (cat != null) host.CloseCat(cat);   // bu pencereyi de kapatır
            if (!IsDisposed) Close();
        };
        var done = new ThemedButton("Tamam", ButtonKind.Accent);
        done.FitText();
        done.Click += (_, _) => Close();
        AcceptButton = done;
        var buttons = new TableLayoutPanel { ColumnCount = 2, Width = W, Height = 40, Margin = new Padding(0, 6, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        close.Anchor = AnchorStyles.Left;
        done.Anchor = AnchorStyles.Right;
        buttons.Controls.Add(close);
        buttons.Controls.Add(done);

        var hint = new Label
        {
            Text = "Önizlemeye tıklarsan miyavlar", AutoSize = false, Width = W, Height = 20,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = p.Subtle, Font = Theme.UiSmall,
        };

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        root.Controls.AddRange(new Control[] { preview, hint, nameRow, tabRow, pages, buttons });
        Controls.Add(root);

        name.TextChanged += (_, _) =>
        {
            if (CatConfig.SanitizeName(name.Text) is string v) Change(c => c.Name = v);
        };
    }

    // MARK: Bölümler

    Control LookPage()
    {
        var page = Page();
        page.Controls.Add(Caption("Kürk"));
        var furRow = Row();
        for (int i = 0; i < CoatSpec.Presets.Count; i++)
        {
            var sw = new SwatchBox(i);
            int idx = i;
            sw.Click += (_, _) => Pick(idx);
            tips.SetToolTip(sw, CoatSpec.Presets[i].Name);
            swatches.Add(sw);
            furRow.Controls.Add(sw);
        }
        customFur.Size = new Size(30, 30);
        customFur.Click += (_, _) =>
        {
            if (PickColor(customFur.Color) is Color col)
                Change(c => c.Spec = CoatSpec.Derived(Hex.From(col), c.Spec.Eye, c.Spec.Stripe != null));
        };
        tips.SetToolTip(customFur, "Özel kürk rengi…");
        furRow.Controls.Add(Plus(customFur));
        page.Controls.Add(furRow);

        page.Controls.Add(Caption("Gözler"));
        var eyeRow = Row();
        foreach (var v in CoatSpec.EyePresets)
        {
            var dot = new ColorDot(Hex.ToColor(v)) { Size = new Size(28, 28) };
            uint value = v;
            dot.Click += (_, _) => Change(c => c.Spec = c.Spec with { Eye = value });
            eyeDots.Add((dot, v));
            eyeRow.Controls.Add(dot);
        }
        customEye.Size = new Size(28, 28);
        customEye.Click += (_, _) =>
        {
            if (PickColor(customEye.Color) is Color col) Change(c => c.Spec = c.Spec with { Eye = Hex.From(col) });
        };
        tips.SetToolTip(customEye, "Özel göz rengi…");
        eyeRow.Controls.Add(Plus(customEye));
        page.Controls.Add(eyeRow);

        page.Controls.Add(Caption("Desen"));
        var patternRow = Row();
        striped.Click += (_, _) =>
            Change(c => c.Spec = c.Spec with { Stripe = c.Spec.Stripe == null ? ColorMath.Mix(c.Spec.Fur, 0x000000, 0.2) : null });
        patternRow.Controls.Add(Fit(striped));
        foreach (var (m, label) in new[] { (Markings.Spots, "Benekli"), (Markings.Socks, "Beyaz patiler"), (Markings.Bib, "Beyaz göğüs") })
        {
            var chip = new Chip(label);
            chip.Click += (_, _) => Change(c => c.Markings ^= m);
            markingChips[m] = chip;
            patternRow.Controls.Add(Fit(chip));
        }
        page.Controls.Add(patternRow);
        return page;
    }

    Control CollarPage()
    {
        var page = Page();
        page.Controls.Add(Caption("Tasma rengi"));
        var row = Row();
        foreach (var v in CoatSpec.CollarPresets)
        {
            var dot = new ColorDot(Hex.ToColor(v)) { Size = new Size(30, 30) };
            uint value = v;
            dot.Click += (_, _) => Change(c => c.Collar = value);
            collarDots.Add((dot, v));
            row.Controls.Add(dot);
        }
        customCollar.Size = new Size(30, 30);
        customCollar.Click += (_, _) =>
        {
            if (PickColor(customCollar.Color) is Color col) Change(c => c.Collar = Hex.From(col));
        };
        tips.SetToolTip(customCollar, "Özel tasma rengi…");
        row.Controls.Add(Plus(customCollar));
        page.Controls.Add(row);

        page.Controls.Add(Caption("Aksesuar"));
        var acc = Row();
        acc.WrapContents = true;
        acc.MaximumSize = new Size(W, 0);
        foreach (var (a, label) in new[]
                 {
                     (Accessory.None, "Yok"), (Accessory.Bell, "Zil"), (Accessory.Bow, "Fiyonk"),
                     (Accessory.Crown, "Taç"), (Accessory.Flower, "Çiçek"), (Accessory.Glasses, "Gözlük"),
                 })
        {
            var chip = new Chip(label) { Margin = new Padding(0, 0, 6, 6) };
            chip.Click += (_, _) => Change(c => c.Accessory = a);
            accessoryChips[a] = chip;
            acc.Controls.Add(Fit(chip));
        }
        page.Controls.Add(acc);

        page.Controls.Add(Caption("İsim etiketi"));
        showNames.Click += (_, _) =>
        {
            settings.ShowNames = !settings.ShowNames;
            Change(_ => { });   // kaydet ve önizlemeyi yenile
        };
        page.Controls.Add(Fit(showNames));
        page.Controls.Add(new Label
        {
            Text = "Tüm kedilerin tasmasındaki isim etiketini açar/kapatır.", AutoSize = true,
            ForeColor = Theme.Current.Subtle, Font = Theme.UiSmall, Margin = new Padding(0, 4, 0, 0),
        });
        return page;
    }

    Control CharacterPage()
    {
        var page = Page();
        var grid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Margin = new Padding(0), BackColor = Theme.Current.Background };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        void Add(string label, Slider s, string[] words, Action<CatConfig, int> apply, string tip)
        {
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 4, 6) };
            tips.SetToolTip(l, tip);
            s.Size = new Size(216, 26);
            s.Anchor = AnchorStyles.Left;
            var word = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Theme.Current.Subtle, Margin = new Padding(4, 6, 0, 6) };
            sliderWords[s] = (word, words);
            s.ValueChanged += (_, _) => { UpdateWord(s); Change(c => apply(c, s.Value)); };
            grid.Controls.Add(l);
            grid.Controls.Add(s);
            grid.Controls.Add(word);
        }

        Add("Oyunculuk", playful, new[] { "Sakin", "Oyuncu", "Çok oyuncu" },
            (c, v) => c.Personality = c.Personality with { Playfulness = v / 100.0 }, "Fareyi kovalama, pusu kurma ve yumruklama sıklığı");
        Add("Huysuzluk", temper, new[] { "Uysal", "Dengeli", "Kavgacı" },
            (c, v) => c.Personality = c.Personality with { Temper = v / 100.0 }, "Diğer kedilerle kavga ve fazla okşanınca pati atma eğilimi");
        Add("Sevecenlik", affection, new[] { "Mesafeli", "Dost", "Sevgi pıtırcığı" },
            (c, v) => c.Personality = c.Personality with { Affection = v / 100.0 }, "Okşanmaya sabır, kaçmama, diğer kedilerle selamlaşma");
        Add("Enerji", energy, new[] { "Uykucu", "Dengeli", "Enerjik" },
            (c, v) => c.Personality = c.Personality with { Energy = v / 100.0 }, "Az uyur, çok gezer ve zıplar");
        Add("Boyut", size, new[] { "Minik", "Orta", "İri" }, (c, v) => c.Scale = Math.Round(v / 5.0) * 5 / 100, "Kedinin boyu");
        Add("Hız", speed, new[] { "Ağır", "Normal", "Çevik" }, (c, v) => c.Speed = v / 100.0, "Yürüme ve koşma hızı");
        Add("Ses tonu", pitch, new[] { "Kalın", "Normal", "İnce" }, (c, v) => c.Pitch = v, "Miyavlama tonu (bırakınca dinletir)");
        pitch.Committed += (_, _) => Listen();

        page.Controls.Add(grid);
        return page;
    }

    // MARK: Yardımcılar

    static FlowLayoutPanel Page() => new()
    {
        FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = false, Padding = new Padding(0),
        BackColor = Theme.Current.Background,
    };

    static FlowLayoutPanel Row(params Control[] items)
    {
        var r = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4), BackColor = Theme.Current.Background };
        r.Controls.AddRange(items);
        return r;
    }

    static Label Caption(string text) => new()
    {
        Text = text.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")), AutoSize = true,
        Font = Theme.UiSmall, ForeColor = Theme.Current.Subtle, Margin = new Padding(0, 8, 0, 4),
    };

    static Chip Fit(Chip c)
    {
        c.FitText();
        if (c.Margin == new Padding(3)) c.Margin = new Padding(0, 0, 6, 0);
        return c;
    }

    /// <summary>Özel renk noktası: üstünde "+" işareti.</summary>
    static Control Plus(ColorDot dot)
    {
        dot.Margin = new Padding(8, 0, 0, 0);
        dot.Paint += (_, e) =>
        {
            var p = Theme.Current;
            float w = Math.Min(dot.Width, dot.Height), k = w / 30f;
            using var pen = new Pen(ColorMath.Brightness(Hex.From(dot.Color)) > 0.6 ? p.Text : Color.White, 1.8f * k);
            e.Graphics.DrawLine(pen, w / 2, w / 2 - 5 * k, w / 2, w / 2 + 5 * k);
            e.Graphics.DrawLine(pen, w / 2 - 5 * k, w / 2, w / 2 + 5 * k, w / 2);
        };
        return dot;
    }

    void ShowTab(Chip tab)
    {
        foreach (var (t, page) in tabs)
        {
            t.Selected = t == tab;
            page.Visible = t == tab;
        }
    }

    void UpdateWord(Slider s)
    {
        var (word, words) = sliderWords[s];
        double t = (double)(s.Value - s.Minimum) / Math.Max(1, s.Maximum - s.Minimum);
        word.Text = words[t < 0.34 ? 0 : t < 0.67 ? 1 : 2];
    }

    Color? PickColor(Color current)
    {
        using var dlg = new ColorDialog { Color = current, FullOpen = true, AnyColor = true };
        return dlg.ShowDialog(this) == DialogResult.OK ? dlg.Color : null;
    }

    public void Open(Cat c)
    {
        cat = c;
        Sync();
        if (!Visible) Show();
        Activate();
    }

    /// <summary>Kontrolleri kedinin güncel ayarlarıyla doldurur.</summary>
    void Sync()
    {
        if (cat == null) return;
        loading = true;
        var c = cat.Config;
        Text = "Özelleştir: " + c.Name;
        if (!name.Focused) name.Text = c.Name;
        foreach (var sw in swatches) sw.Selected = CoatSpec.Presets[sw.Preset].Spec == c.Spec;
        customFur.Color = Hex.ToColor(c.Spec.Fur);
        customFur.Selected = !swatches.Any(s => s.Selected);
        foreach (var (dot, v) in eyeDots) dot.Selected = v == c.Spec.Eye;
        customEye.Color = Hex.ToColor(c.Spec.Eye);
        customEye.Selected = !eyeDots.Any(d => d.Dot.Selected);
        foreach (var (dot, v) in collarDots) dot.Selected = v == c.Collar;
        customCollar.Color = Hex.ToColor(c.Collar);
        customCollar.Selected = !collarDots.Any(d => d.Dot.Selected);
        foreach (var d in new Control[] { customFur, customEye, customCollar }) d.Invalidate();

        striped.Selected = c.Spec.Stripe != null;
        foreach (var (m, chip) in markingChips) chip.Selected = (c.Markings & m) != 0;
        foreach (var (a, chip) in accessoryChips) chip.Selected = c.Accessory == a;
        showNames.Selected = settings.ShowNames;

        playful.Value = (int)Math.Round(c.Personality.Playfulness * 100);
        temper.Value = (int)Math.Round(c.Personality.Temper * 100);
        affection.Value = (int)Math.Round(c.Personality.Affection * 100);
        energy.Value = (int)Math.Round(c.Personality.Energy * 100);
        size.Value = (int)Math.Round(c.Scale * 100);
        speed.Value = (int)Math.Round(c.Speed * 100);
        pitch.Value = (int)Math.Round(c.Pitch);
        foreach (var s in sliderWords.Keys) UpdateWord(s);

        preview.Coat = Coat.From(c);
        preview.CatName = settings.ShowNames ? c.Name : null;
        loading = false;
    }

    void Change(Action<CatConfig> change)
    {
        if (loading || cat == null) return;
        cat.Update(change);
        Sync();
    }

    void Pick(int i)
    {
        Change(c =>
        {
            // İsim hâlâ hazır isimlerden biriyse yeni rengin ismini al.
            if (CoatSpec.Presets.Any(p => p.Name == c.Name)) c.Name = CoatSpec.Presets[i].Name;
            c.Spec = CoatSpec.Presets[i].Spec;
        });
    }

    void Randomize()
    {
        var r = CatConfig.Random(Random.Shared, cat?.Config.Scale ?? 1);
        Change(c =>
        {
            c.Spec = r.Spec; c.Markings = r.Markings; c.Collar = r.Collar; c.Accessory = r.Accessory;
            c.Personality = r.Personality; c.Speed = r.Speed; c.Pitch = r.Pitch;
        });
        preview.Cheer();
    }

    void Listen()
    {
        cat?.Meow();
        preview.Cheer();
    }
}
