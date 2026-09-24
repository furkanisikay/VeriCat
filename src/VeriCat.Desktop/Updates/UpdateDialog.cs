using System.Diagnostics;
using System.Globalization;
using VeriCat.Core.Configuration;
using VeriCat.Core.Updates;
using VeriCat.Desktop.Theming;

namespace VeriCat.Desktop.Updates;

/// <summary>
/// Yenilikler penceresi. İki kipi var: yeni sürüm bulunduğunda notları gösterip kurmayı önerir;
/// güncellemeden sonraki ilk açılışta ise yalnızca gelen yenilikleri gösterir.
/// </summary>
internal sealed class UpdateDialog : Form
{
    readonly UpdateService? service;
    readonly ReleaseInfo release;
    readonly AppSettings settings;
    readonly Action save;
    readonly Label status = new() { AutoSize = true };
    readonly ProgressLine progress = new() { Visible = false };
    readonly ThemedButton install = new("Güncelle ve yeniden başlat", ButtonKind.Accent);
    readonly ThemedButton later = new("Sonra");
    readonly ThemedButton skip = new("Bu sürümü atla");
    CancellationTokenSource? cts;

    /// <param name="service">Null ise "yenilikler" kipi (kurulum düğmeleri yok).</param>
    public UpdateDialog(ReleaseInfo release, UpdateService? service, AppSettings settings, Action save)
    {
        this.release = release;
        this.service = service;
        this.settings = settings;
        this.save = save;
        bool whatsNew = service == null;

        ThemedWindow.Apply(this);
        var p = Theme.Current;
        Text = whatsNew ? "VeriCat — Yenilikler" : "VeriCat — Güncelleme";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(540, 470);
        Padding = new Padding(22, 18, 22, 16);

        var title = new Label
        {
            Text = whatsNew ? $"v{release.Version} ile gelenler" : $"Yeni sürüm hazır: v{release.Version}",
            Font = Theme.Title, AutoSize = true, ForeColor = p.Text, Margin = new Padding(0),
        };
        var date = release.PublishedAt?.ToLocalTime().ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR"));
        var subtitle = new Label
        {
            Text = whatsNew
                ? $"VeriCat güncellendi{(date != null ? " · " + date : "")}"
                : $"Şu an {AppInfo.DisplayVersion}{(date != null ? " · yayın: " + date : "")}",
            AutoSize = true, ForeColor = p.Subtle, Margin = new Padding(0, 4, 0, 12),
        };

        var notes = new RichTextBox
        {
            ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = p.Surface, ForeColor = p.Text,
            Dock = DockStyle.Fill, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.Vertical, TabStop = false,
        };
        var notesCard = new Panel { Dock = DockStyle.Fill, BackColor = p.Surface, Padding = new Padding(14, 10, 8, 10) };
        notesCard.Paint += (_, e) =>
        {
            using var pen = new Pen(p.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, notesCard.Width - 1, notesCard.Height - 1);
        };
        notesCard.Controls.Add(notes);
        FillNotes(notes, release.Notes);

        var auto = new CheckBox
        {
            Text = "Güncellemeleri bundan sonra otomatik yükle", AutoSize = true, ForeColor = p.Subtle,
            Checked = settings.Updates == UpdateMode.Automatic, Visible = !whatsNew, Margin = new Padding(0, 10, 0, 0),
        };
        auto.CheckedChanged += (_, _) =>
        {
            settings.Updates = auto.Checked ? UpdateMode.Automatic : UpdateMode.Notify;
            save();
        };

        var link = new LinkLabel
        {
            Text = "GitHub'da görüntüle", AutoSize = true, LinkColor = p.Accent, ActiveLinkColor = p.Accent,
            Margin = new Padding(0, 10, 0, 0), Visible = release.PageUrl != null,
        };
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(release.PageUrl!.ToString()) { UseShellExecute = true });

        status.ForeColor = p.Subtle;
        status.Margin = new Padding(0, 8, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, WrapContents = false,
            Margin = new Padding(0, 12, 0, 0), Padding = new Padding(0, 12, 0, 0), BackColor = p.Background,
        };
        if (whatsNew)
        {
            var ok = new ThemedButton("Harika!", ButtonKind.Accent);
            ok.Click += (_, _) => Close();
            ok.FitText();
            buttons.Controls.Add(ok);
            AcceptButton = ok;
        }
        else
        {
            foreach (var b in new[] { install, later, skip }) { b.FitText(); b.Margin = new Padding(8, 0, 0, 0); buttons.Controls.Add(b); }
            install.Click += async (_, _) => await InstallAsync();
            later.Click += (_, _) => Close();
            skip.Click += (_, _) => { service!.Skip(release); Close(); };
            AcceptButton = install;
        }
        CancelButton = null;

        var footer = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Bottom, AutoSize = true, WrapContents = false };
        footer.Controls.AddRange(new Control[] { progress, status, auto, link });
        progress.Width = 490;

        var header = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        header.Controls.AddRange(new Control[] { title, subtitle });

        Controls.Add(notesCard);
        Controls.Add(header);
        Controls.Add(footer);
        Controls.Add(buttons);

        FormClosing += (_, e) =>
        {
            if (service?.Installing == true) e.Cancel = true;   // yarıda bırakılmasın
            cts?.Cancel();
        };
    }

    async Task InstallAsync()
    {
        foreach (var b in new[] { install, later, skip }) b.Enabled = false;
        progress.Visible = true;
        status.Text = "İndiriliyor…";
        cts = new CancellationTokenSource();
        var report = new Progress<double>(v =>
        {
            progress.Value = (float)v;
            status.Text = v < 1 ? $"İndiriliyor… %{v * 100:0}" : "Doğrulandı, yeniden başlatılıyor…";
        });
        try
        {
            await service!.InstallAsync(release, report, cts.Token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            status.Text = "Güncellenemedi: " + e.Message;
            status.ForeColor = Theme.Current.Danger;
            progress.Visible = false;
            foreach (var b in new[] { install, later, skip }) b.Enabled = true;
        }
    }

    /// <summary>Sürüm notlarını başlık, madde ve kalın yazılarla RichTextBox'a yazar.</summary>
    static void FillNotes(RichTextBox box, string markdown)
    {
        var p = Theme.Current;
        var lines = ReleaseNotes.Parse(markdown);
        using var heading = new Font(Theme.UiBold.FontFamily, 10.5f, FontStyle.Bold);
        if (lines.Count == 0)
        {
            box.SelectionColor = p.Subtle;
            box.AppendText("Bu sürüm için not bulunmuyor.");
            return;
        }
        bool first = true;
        foreach (var line in lines)
        {
            if (line.Kind == NoteKind.Heading)
            {
                if (!first) box.AppendText("\n");
                box.SelectionFont = heading;
                box.SelectionColor = p.Accent;
                box.SelectionIndent = 0;
                box.AppendText(line.PlainText + "\n");
            }
            else
            {
                box.SelectionIndent = line.Kind == NoteKind.Bullet ? 14 : 0;
                box.SelectionHangingIndent = line.Kind == NoteKind.Bullet ? 12 : 0;
                if (line.Kind == NoteKind.Bullet)
                {
                    box.SelectionFont = Theme.UiFont;
                    box.SelectionColor = p.Accent;
                    box.AppendText("•  ");
                }
                foreach (var (text, bold) in line.Spans)
                {
                    box.SelectionFont = bold ? Theme.UiBold : Theme.UiFont;
                    box.SelectionColor = p.Text;
                    box.AppendText(text);
                }
                box.AppendText("\n");
            }
            first = false;
        }
        box.SelectionStart = 0;
    }

    /// <summary>İnce, yuvarlak uçlu ilerleme çubuğu.</summary>
    sealed class ProgressLine : Control
    {
        float value;

        public ProgressLine()
        {
            Height = 6;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Margin = new Padding(0, 6, 0, 0);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public float Value
        {
            get => value;
            set { this.value = Math.Clamp(value, 0, 1); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? p.Background);
            using (var track = MenuRenderer.RoundRect(new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f))
            using (var b = new SolidBrush(p.Track)) g.FillPath(b, track);
            if (value <= 0) return;
            using var fill = MenuRenderer.RoundRect(new RectangleF(0, 0, Math.Max(Height, (Width - 1) * value), Height - 1), Height / 2f);
            using var fb = new SolidBrush(p.Accent);
            g.FillPath(fb, fill);
        }
    }
}
