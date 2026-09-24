using VeriCat.Core.Rendering;

namespace VeriCat.Core.Abstractions;

/// <summary>Bir kedinin ekrandaki görünümü (Windows'ta saydam katmanlı pencere).</summary>
public interface ICatView
{
    /// <param name="feetX">Ayakların ortası, x.</param>
    /// <param name="feetY">Ayakların ortası, y (yukarı).</param>
    /// <param name="scale">Çizim ölçeği (piksel / temel birim).</param>
    /// <param name="sprite">Çizilecek kare.</param>
    void Present(double feetX, double feetY, double scale, Sprite sprite);

    void Close();
}
