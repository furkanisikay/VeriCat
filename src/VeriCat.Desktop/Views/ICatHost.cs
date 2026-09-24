using VeriCat.Core.Behavior;
using VeriCat.Core.Props;

namespace VeriCat.Desktop.Views;

/// <summary>Kedi/eşya pencerelerinin ve özelleştirme penceresinin uygulamadan istedikleri.</summary>
internal interface ICatHost
{
    void AddCat();
    void CloseCat(Cat cat);
    void Customize(Cat cat);
    void ShowCatMenu(Cat cat);
    void ShowPropMenu(Prop prop);
    void FillBowl(Prop bowl);
}
