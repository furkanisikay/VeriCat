using VeriCat.Core.Behavior;

namespace VeriCat.Desktop.Views;

/// <summary>Kedi pencerelerinin ve özelleştirme penceresinin uygulamadan istedikleri.</summary>
internal interface ICatHost
{
    void AddCat();
    void CloseCat(Cat cat);
    void Customize(Cat cat);
    void ShowCatMenu(Cat cat);
}
