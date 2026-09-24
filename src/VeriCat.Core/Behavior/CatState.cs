namespace VeriCat.Core.Behavior;

public enum CatState
{
    /// <summary>Rastgele yürüyüş.</summary>
    Walk,
    /// <summary>Fare imlecinin peşinde.</summary>
    Chase,
    Sit,
    /// <summary>Tıklanınca / okşama bitince sevinç.</summary>
    Happy,
    Sleep,
    /// <summary>Zıplamadan hemen önce çömelme.</summary>
    Crouch,
    Air,
    Dragged,
    /// <summary>Fareyle okşanıyor: mırlar, kalpler çıkar.</summary>
    Petted,
    /// <summary>Kaçıyor (kavgayı kaybetti ya da okşanmaktan sıkıldı).</summary>
    Flee,
    /// <summary>Başka bir kediyle kavga.</summary>
    Fight,
    /// <summary>İmlece pusu kurma: çömelip kıç sallama.</summary>
    Stalk,
    /// <summary>Arka ayaklar üstünde imlece yumruk atma.</summary>
    Swat,
    /// <summary>Bir hedefe gidiyor (mama kabı, yumak, arkadaşının yanı, imleç).</summary>
    Seek,
    /// <summary>Mama kabından yiyor.</summary>
    Eat,
}
