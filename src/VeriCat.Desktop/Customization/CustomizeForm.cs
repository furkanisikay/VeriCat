using VeriCat.Core.Appearance;
using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Desktop.Rendering;
using VeriCat.Desktop.Views;

namespace VeriCat.Desktop.Customization;

/// <summary>Bir kedinin ismini, renklerini, tasmasını, boyunu, hızını ve sesini değiştiren pencere.</summary>
internal sealed class CustomizeForm : Form
{
    readonly ICatHost host;
    readonly AppSettings settings;
    Cat? cat;
    bool loading;

    readonly PreviewBox preview = new() { Size = new Size(360, 170), Margin = new Padding(0) };
    readonly TextBox name = new() { Width = 230, MaxLength = CatConfig.MaxNameLength };
    readonly List<SwatchBox> swatches = new();
    readonly Button fur = ColorButton("Özel kürk rengi"), eye = ColorButton("Göz rengi"), collar = ColorButton("Tasma rengi");
    readonly CheckBox striped = new() { Text = "Çizgili", AutoSize = true, Margin = new Padding(16, 4, 0, 0) };
    readonly CheckBox showNames = new() { Text = "İsim etiketi görünsün (tüm kediler)", AutoSize = true, Margin = new Padding(16, 4, 0, 0) };
    readonly TrackBar size = Track(50, 250), speed = Track(40, 220), pitch = Track(420, 950);
    readonly ToolTip tips = new();

    public CustomizeForm(ICatHost host, AppSettings settings)
    {
        this.host = host;
        this.settings = settings;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        Text = "Özelleştir";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        preview.Click += (_, _) => Listen();

        var colorRow = Row();
        for (int i = 0; i < CoatSpec.Presets.Count; i++)
        {
            var sw = new SwatchBox(i);
            int idx = i;
            sw.Click += (_, _) => Pick(idx);
            tips.SetToolTip(sw, CoatSpec.Presets[i].Name);
            swatches.Add(sw);
            colorRow.Controls.Add(sw);
        }
        fur.Margin = new Padding(6, 3, 0, 0);
        colorRow.Controls.Add(fur);

        var eyeRow = Row();
        eyeRow.Controls.Add(eye);
        eyeRow.Controls.Add(striped);

        var collarRow = Row();
        collarRow.Controls.Add(collar);
        collarRow.Controls.Add(showNames);

        var listen = new Button { Text = "Dinle", AutoSize = true };
        listen.Click += (_, _) => Listen();
        var pitchRow = Row();
        pitch.Width = 160;
        pitchRow.Controls.Add(pitch);
        pitchRow.Controls.Add(listen);

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 10, 0, 10) };
        void Add(string label, Control c)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 6, 8, 6) });
            c.Anchor = AnchorStyles.Left;
            grid.Controls.Add(c);
        }
        Add("İsim", name);
        Add("Renk", colorRow);
        Add("Göz rengi", eyeRow);
        Add("Tasma", collarRow);
        Add("Boyut", size);
        Add("Hız", speed);
        Add("Ses tonu", pitchRow);

        var hint = new Label
        {
            Text = "Önizlemeye tıklarsan miyavlar", AutoSize = false, Width = 360, Height = 20,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText,
        };

        var close = new Button { Text = "Kediyi kapat", AutoSize = true };
        close.Click += (_, _) =>
        {
            if (cat != null) host.CloseCat(cat);   // bu pencereyi de kapatır
            if (!IsDisposed) Close();
        };
        var done = new Button { Text = "Tamam", AutoSize = true };
        done.Click += (_, _) => Close();
        AcceptButton = done;
        var buttons = new TableLayoutPanel { ColumnCount = 2, Width = 360, Height = 34, Margin = new Padding(0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        close.Anchor = AnchorStyles.Left;
        done.Anchor = AnchorStyles.Right;
        buttons.Controls.Add(close);
        buttons.Controls.Add(done);

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        root.Controls.AddRange(new Control[] { preview, hint, grid, buttons });
        Controls.Add(root);

        name.TextChanged += (_, _) =>
        {
            if (CatConfig.SanitizeName(name.Text) is string v) Change(c => c.Name = v);
        };
        fur.Click += (_, _) =>
        {
            if (PickColor(fur.BackColor) is Color col)
                Change(c => c.Spec = CoatSpec.Derived(Hex.From(col), c.Spec.Eye, c.Spec.Stripe != null));
        };
        eye.Click += (_, _) =>
        {
            if (PickColor(eye.BackColor) is Color col) Change(c => c.Spec = c.Spec with { Eye = Hex.From(col) });
        };
        collar.Click += (_, _) =>
        {
            if (PickColor(collar.BackColor) is Color col) Change(c => c.Collar = Hex.From(col));
        };
        showNames.CheckedChanged += (_, _) =>
        {
            if (loading) return;
            settings.ShowNames = showNames.Checked;
            Change(_ => { });   // kaydetsin
        };
        striped.CheckedChanged += (_, _) =>
            Change(c => c.Spec = c.Spec with { Stripe = striped.Checked ? ColorMath.Mix(c.Spec.Fur, 0x000000, 0.2) : null });
        size.Scroll += (_, _) => Change(c => c.Scale = Math.Round(size.Value / 5.0) * 5 / 100);
        speed.Scroll += (_, _) => Change(c => c.Speed = speed.Value / 100.0);
        pitch.Scroll += (_, _) => Change(c => c.Pitch = pitch.Value);
        pitch.MouseUp += (_, _) => Listen();
    }

    static FlowLayoutPanel Row() => new() { AutoSize = true, WrapContents = false, Margin = new Padding(0) };

    static Button ColorButton(string tip) => new()
    {
        Width = 44, Height = 24, FlatStyle = FlatStyle.Flat, Text = "", AccessibleName = tip,
        FlatAppearance = { BorderColor = Color.Gray },
    };

    static TrackBar Track(int min, int max) => new()
    {
        Minimum = min, Maximum = max, Width = 230, TickStyle = TickStyle.None, AutoSize = false, Height = 28,
    };

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
        fur.BackColor = Hex.ToColor(c.Spec.Fur);
        eye.BackColor = Hex.ToColor(c.Spec.Eye);
        collar.BackColor = Hex.ToColor(c.Collar);
        striped.Checked = c.Spec.Stripe != null;
        showNames.Checked = settings.ShowNames;
        size.Value = Math.Clamp((int)Math.Round(c.Scale * 100), size.Minimum, size.Maximum);
        speed.Value = Math.Clamp((int)Math.Round(c.Speed * 100), speed.Minimum, speed.Maximum);
        pitch.Value = Math.Clamp((int)Math.Round(c.Pitch), pitch.Minimum, pitch.Maximum);
        foreach (var sw in swatches) sw.Selected = CoatSpec.Presets[sw.Preset].Spec == c.Spec;
        preview.Coat = Coat.From(c.Spec, c.Collar);
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

    void Listen()
    {
        cat?.Meow();
        preview.Cheer();
    }
}
