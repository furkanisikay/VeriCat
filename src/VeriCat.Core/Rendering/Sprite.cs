namespace VeriCat.Core.Rendering;

public enum Pose { Walk, Air, Sit, Sleep, Dangle, Fight, Swat, Pounce }

public enum EyeKind { Open, Closed, Happy, Wide, Angry }

/// <summary>Bir karede kedinin nasıl görüneceği. Çizimden bağımsız, saf veri.</summary>
public sealed class Sprite
{
    public Pose Pose = Pose.Sit;
    public EyeKind Eyes = EyeKind.Open;
    public float EyeOpen = 1;     // göz kırpma, 0...1
    public float Phase;           // yürüme döngüsü; Fight/Swat'ta vuruş ilerlemesi (0...1)
    public float Crouch;          // zıplamadan / pusudan önce çömelme, 0...1
    public float Wiggle;          // pusuda kıç sallama, -1...1
    public float Tilt;            // havadayken gövde açısı (radyan)
    public float Lean;            // okşanırken başın yana yatması (radyan)
    public float Clock;           // kuyruk, nefes, zzz animasyonu
    public float? Hearts;         // sevilince çıkan kalplerin zamanı
    public bool FacingRight = true;
    public bool EarsBack;         // kavga / korku: kulaklar geride
    public bool Hiss;             // ağız açık, dişler görünür
    public bool Puffed;           // tüyler kabarmış kuyruk
    public int Arm;               // Swat: hangi pati (0/1)
    public float Aim;             // Swat: vuruş açısı, yukarı pozitif (radyan)
    public float? Impact;         // Swat/Pounce: isabet efekti zamanı
    public float? Dust;           // Fight: toz bulutu zamanı
}
