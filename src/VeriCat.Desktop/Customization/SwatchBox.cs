using System.ComponentModel;
using System.Drawing.Drawing2D;
using VeriCat.Core.Appearance;
using VeriCat.Desktop.Rendering;

namespace VeriCat.Desktop.Customization;

/// <summary>Hazır renk takımı düğmesi: kürk renginde yuvarlak, çizgiliyse çizgili.</summary>
internal sealed class SwatchBox : Control
{
    bool selected;

    public SwatchBox(int preset)
    {
        Preset = preset;
        Size = new Size(28, 28);
        Margin = new Padding(1);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
    }

    public int Preset { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => selected;
        set { selected = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var spec = CoatSpec.Presets[Preset].Spec;
        float w = Width, ring = w * 0.09f, inset = w * 0.18f;
        if (selected)
        {
            using var pen = new Pen(SystemColors.Highlight, ring);
            g.DrawEllipse(pen, ring, ring, w - 2 * ring, w - 2 * ring);
        }
        var dot = new RectangleF(inset, inset, w - 2 * inset, w - 2 * inset);
        using (var b = new SolidBrush(Hex.ToColor(spec.Fur))) g.FillEllipse(b, dot);
        if (spec.Stripe is uint st)
        {
            using var clip = new GraphicsPath();
            clip.AddEllipse(dot);
            var state = g.Save();
            g.SetClip(clip);
            using var pen = new Pen(Hex.ToColor(st), w * 0.09f);
            foreach (var x in new[] { 0.32f, 0.54f, 0.76f }) g.DrawLine(pen, (x - 0.1f) * w, w * 0.86f, (x + 0.1f) * w, w * 0.14f);
            g.Restore(state);
        }
        using (var pen = new Pen(Hex.ToColor(spec.Line, 150), 1)) g.DrawEllipse(pen, dot);
    }
}
