namespace VeriCat.Core.Abstractions;

/// <summary>Fare imleci. Koordinatlar y yukarı.</summary>
public interface IPointer
{
    (double X, double Y) Position { get; }

    /// <summary>Herhangi bir fare tuşu basılı mı? (Sürükleme sırasında imleç itilmez.)</summary>
    bool AnyButtonDown { get; }

    /// <summary>İmleci kaydırır (y yukarı).</summary>
    void Nudge(double dx, double dy);
}
