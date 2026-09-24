using VeriCat.Core.Abstractions;
using VeriCat.Core.Configuration;
using VeriCat.Core.Geometry;
using VeriCat.Core.Rendering;
using VeriCat.Core.World;

namespace VeriCat.Core.Behavior;

/// <summary>
/// Bir kedinin durum makinesi ve fiziği. Davranış konularına göre kısmi dosyalara bölünmüştür:
/// <c>Cat.Movement</c> (yürüme, zıplama, düşme), <c>Cat.Interaction</c> (sürükleme, okşama),
/// <c>Cat.Hunting</c> (imleç kovalama, pusu, yumruk), <c>Cat.Social</c> (çarpışma, kavga).
/// </summary>
public sealed partial class Cat
{
    readonly CatEnvironment env;
    readonly ICatView view;

    CatState state = CatState.Air;
    double stateTime, stateLength = 1;
    double px, py, vx, vy;                            // ayakların ortası, y yukarı
    bool facingRight;
    double phase, clock, blink = 3;

    public Cat(CatConfig config, CatEnvironment env, ICatView view, double x, double y)
    {
        Config = config;
        this.env = env;
        this.view = view;
        px = x; py = y;
        facingRight = env.Random.Next(2) == 0;
    }

    public CatConfig Config { get; }
    public CatState State => state;
    public bool IsAsleep => state == CatState.Sleep;
    public double X => px;
    public double Y => py;
    public bool FacingRight => facingRight;

    /// <summary>Menü açıkken kedi olduğu yerde donar.</summary>
    public bool Paused { get; set; }

    /// <summary>Görünümü etkileyen ayar değiştikçe artar (çizim önbelleği için).</summary>
    public int AppearanceVersion { get; private set; }

    /// <summary>Ayar değişince (kaydetmek için).</summary>
    public event EventHandler? ConfigChanged;

    IWorld World => env.World;
    ICatVoice Voice => env.Voice;
    IPointer Pointer => env.Pointer;
    AppSettings Settings => env.Settings;
    Random Rng => env.Random;
    double Now => env.Clock();

    double D => env.Dpi;                              // 1 mantıksal nokta kaç piksel
    double Gravity => 2200 * D;
    double S => Config.Scale * D;                     // çizim ölçeği
    double Pace => 55 * Config.Speed;
    double Dir => facingRight ? 1 : -1;

    double R(double a, double b) => a + Rng.NextDouble() * (b - a);

    /// <summary>Ayarları değiştirir (özelleştirme penceresinden).</summary>
    public void Update(Action<CatConfig> change)
    {
        change(Config);
        AppearanceVersion++;
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    // MARK: Döngü

    public void Step(double dt)
    {
        clock += dt; stateTime += dt;
        blink -= dt;
        if (blink < -0.14) blink = R(2, 5);
        TickTimers(dt);
        if (Paused) return;

        switch (state)
        {
            case CatState.Dragged: break;
            case CatState.Air: Fly(dt); break;
            default:
                if (!OnSupport()) { Drop(0); break; }
                switch (state)
                {
                    case CatState.Walk: Stride(dt, Pace * S, true); NoticePointer(dt); break;
                    case CatState.Sit: NoticePointer(dt); break;
                    case CatState.Chase: ChaseStep(dt); break;
                    case CatState.Stalk: StalkStep(); break;
                    case CatState.Swat: SwatStep(); break;
                    case CatState.Flee: Stride(dt, 260 * S * Config.Speed, true); break;
                    case CatState.Petted: PettedStep(dt); break;
                    case CatState.Fight: FightStep(); break;
                    case CatState.Crouch when stateTime >= 0.22: Launch(); break;
                }
                if (state is not (CatState.Crouch or CatState.Air or CatState.Fight or CatState.Swat or CatState.Petted or CatState.Stalk)
                    && stateTime >= stateLength)
                    Decide();
                break;
        }
    }

    void TickTimers(double dt)
    {
        petting = Math.Max(0, petting - 160 * D * dt);
        impact = Math.Max(0, impact - dt);
        TrackPointer(dt);
    }

    void Set(CatState s, double min, double max)
    {
        if (state == CatState.Fight && s != CatState.Fight) opponent = null;
        state = s; stateTime = 0; stateLength = R(min, max);
    }

    void Decide()
    {
        int r = Rng.Next(100);
        if (r < 34) { facingRight = Rng.Next(2) == 0; Set(CatState.Walk, 2, 6); }
        else if (r < 54) Set(CatState.Sit, 2, 5);
        else if (r < 62) Set(CatState.Sleep, 10, 25);
        else if (r < 84) { if (!(Settings.Windows && TryJump())) Set(CatState.Walk, 2, 4); }
        else if (Settings.Chase) Set(CatState.Chase, 3, 6);
        else Set(CatState.Sit, 2, 4);
        if (Rng.Next(14) == 0) Voice.Meow(Config.Pitch);
    }

    // MARK: Çizim

    /// <summary>Kareyi görünüme gönderir.</summary>
    public void Present() => view.Present(px, py, S, MakeSprite());

    public void Dismiss() => view.Close();

    internal Sprite MakeSprite()
    {
        var f = new Sprite { Clock = (float)clock, FacingRight = facingRight };
        float eye = blink < 0 ? 0.12f : 1;
        f.EyeOpen = eye;
        switch (state)
        {
            case CatState.Walk or CatState.Chase:
                f.Pose = Pose.Walk; f.Phase = (float)phase;
                break;
            case CatState.Flee:
                f.Pose = Pose.Walk; f.Phase = (float)phase; f.Eyes = EyeKind.Wide; f.EarsBack = true;
                break;
            case CatState.Stalk:
                f.Pose = Pose.Walk; f.Crouch = 0.8f; f.Eyes = EyeKind.Wide;
                f.Wiggle = (float)Math.Sin(stateTime * 22);
                break;
            case CatState.Crouch:
                f.Pose = Pose.Walk; f.Crouch = (float)Math.Min(1, stateTime / 0.12);
                if (pouncing) f.Eyes = EyeKind.Wide;
                break;
            case CatState.Air:
                f.Pose = pouncing ? Pose.Pounce : Pose.Air;
                f.Tilt = (float)Math.Clamp(Math.Atan2(vy, Math.Max(Math.Abs(vx), 200 * D)) * 0.5, -0.5, 0.5);
                if (pouncing || vy < -900 * D) f.Eyes = EyeKind.Wide;
                break;
            case CatState.Sit: f.Pose = Pose.Sit; break;
            case CatState.Happy: f.Pose = Pose.Sit; f.Eyes = EyeKind.Happy; f.Hearts = (float)stateTime; break;
            case CatState.Petted:
                f.Pose = Pose.Sit; f.Eyes = EyeKind.Happy; f.Hearts = (float)stateTime;
                f.Lean = (float)(Math.Sin(clock * 2.4) * 0.12);
                break;
            case CatState.Sleep: f.Pose = Pose.Sleep; f.Eyes = EyeKind.Closed; break;
            case CatState.Dragged: f.Pose = Pose.Dangle; f.Eyes = EyeKind.Wide; break;
            case CatState.Fight:
                f.Pose = Pose.Fight; f.Eyes = EyeKind.Angry; f.EarsBack = true; f.Puffed = true;
                f.Phase = (float)(stateTime * 3.2 % 1); f.Hiss = stateTime % 0.9 < 0.5; f.Dust = (float)stateTime;
                break;
            case CatState.Swat:
                f.Pose = Pose.Swat; f.Eyes = EyeKind.Wide;
                f.Phase = (float)SwatProgress(); f.Arm = punchIndex % 2; f.Aim = (float)aim;
                break;
        }
        if (impact > 0) f.Impact = (float)(ImpactLength - impact);
        return f;
    }

    // MARK: Test kancaları

    internal void ForceState(CatState s, double length)
    {
        Set(s, length, length);
    }

    internal void PlaceOn(Platform p, double x)
    {
        px = x; py = p.Y; vx = vy = 0;
        platform = p;
        riding = World.Frames.TryGetValue(p.Owner, out var r) ? r : null;
        Set(CatState.Sit, 1, 1);
    }

    internal void Face(bool right) => facingRight = right;
}
