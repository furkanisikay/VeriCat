using VeriCat.Core.Geometry;

namespace VeriCat.Core.World;

/// <summary>Bir ekranın o anki durumu.</summary>
/// <param name="Bounds">Ekranın tamamı.</param>
/// <param name="WorkTop">Çalışma alanının üst kenarı (y yukarı).</param>
/// <param name="WorkBottom">Çalışma alanının alt kenarı, yani görev çubuğunun üstü (y yukarı).</param>
public sealed record ScreenSnapshot(RectU Bounds, double WorkTop, double WorkBottom);

/// <summary>Pencerenin içindeki, üstüne basılabilecek bir alt bölüm (araç çubuğu, panel, liste…).</summary>
public sealed record ChildSnapshot(long Handle, RectU Frame);

/// <summary>Görünür bir üst düzey pencere.</summary>
public sealed record WindowSnapshot(long Handle, RectU Frame, IReadOnlyList<ChildSnapshot> Children)
{
    public WindowSnapshot(long handle, RectU frame) : this(handle, frame, Array.Empty<ChildSnapshot>()) { }
}
