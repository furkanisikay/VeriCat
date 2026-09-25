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
            case CatState.Climb: ClimbStep(dt); break;
            case CatState.Hang: HangStep(dt); break;
            default:
                if (!OnSupport()) { if (state != CatState.Air) Drop(0); break; }   // Air: pencereden savruldu
                switch (state)
                {
                    case CatState.Walk: Stride(dt, Pace * S, true); NoticePointer(dt); NoticeToys(dt); break;
                    case CatState.Sit: NoticePointer(dt); NoticeToys(dt); break;
                    case CatState.Seek: SeekStep(dt); break;
                    case CatState.Eat: EatStep(dt); break;
                    case CatState.Chase: ChaseStep(dt); break;
                    case CatState.Stalk: StalkStep(); break;
                    case CatState.Swat: SwatStep(); break;
                    case CatState.Flee: Stride(dt, (playful ? 200 : 260) * S * Config.Speed, true); break;
                    case CatState.Petted: PettedStep(dt); break;
                    case CatState.Fight: FightStep(); break;
                    case CatState.Crouch when stateTime >= CrouchTime: Launch(); break;
                }
                if (state is not (CatState.Crouch or CatState.Air or CatState.Fight or CatState.Swat or CatState.Petted or CatState.Stalk
                        or CatState.Eat)
                    && stateTime >= stateLength)
                    Decide();
                break;
        }
        KeepOnScreen();
    }

    void TickTimers(double dt)
    {
        petting = Math.Max(0, petting - 160 * D * dt);
        impact = Math.Max(0, impact - dt);
        emoteTime += dt;
        Vitals.Decay(dt, sleeping: state == CatState.Sleep);
        if (state is CatState.Chase or CatState.Stalk) Vitals.Play(dt * 0.004);
        TrackPointer(dt);
    }

    void Set(CatState s, double min, double max)
    {
        if (state == CatState.Fight && s != CatState.Fight) opponent = null;
        if (s != CatState.Chase) summoned = false;
        if (s != CatState.Seek) goal = Goal.None;
        if (s != CatState.Swat) swatProp = null;
        if (s is not (CatState.Walk or CatState.Chase or CatState.Seek or CatState.Flee)) gait = 0;
        if (s != CatState.Crouch) { hopResume = null; hangTarget = null; }
        if (s != CatState.Hang) curtain = 0;
        if (s is not (CatState.Flee or CatState.Crouch)) playful = false;
        if (s is not (CatState.Crouch or CatState.Chase or CatState.Stalk or CatState.Climb)) hunting = false;
        state = s; stateTime = 0; stateLength = R(min, max);
    }

    internal Personality Traits => Config.Personality;

    /// <summary>İhtiyaçlar (açlık, sevgi, oyun, enerji).</summary>
    public Vitals Vitals => Config.Vitals;

    /// <summary>Gece (23:00–07:00) mi? Kediler gece daha çok uyur.</summary>
    bool IsNight => env.LocalTime().Hour is >= 23 or < 7;

    /// <summary>
    /// Sıradaki işi seçer. Önce acil bir ihtiyaç var mı bakar (acıkmışsa mamaya, yalnızsa sana, sıkılmışsa yumağa
    /// gider); yoksa karakterine, enerjisine ve saate göre ağırlıklı rastgele bir iş seçer.
    /// </summary>
    void Decide()
    {
        if (TryPursueNeed()) return;

        double energy = Traits.Energy;
        double tired = 1 + 2 * (1 - Vitals.Energy), bored = 1 + 1.5 * (1 - Vitals.Fun), night = IsNight ? 4 : 1;
        double walk = (30 + 10 * energy) / Math.Sqrt(night), sit = 20;
        double sleep = 12 * (1.2 - energy) * tired * night, jump = (16 + 12 * energy) / Math.Sqrt(night);
        double chase = Settings.Chase ? 24 * Traits.Playfulness * bored : 0;
        double r = Rng.NextDouble() * (walk + sit + sleep + jump + chase);

        if ((r -= walk) < 0) { facingRight = Rng.Next(2) == 0; Set(CatState.Walk, 2, 6); }
        else if ((r -= sit) < 0) Set(CatState.Sit, 2, 5);
        else if ((r -= sleep) < 0) GoToSleep();
        else if ((r -= jump) < 0) { if (!(Settings.Windows && TryJump())) Set(CatState.Walk, 2, 4); }
        else Set(CatState.Chase, 3, 6);
        if (Rng.Next(14) == 0) Voice.Meow(Config.Pitch);
    }

    // MARK: Çizim

    /// <summary>Sakin durumlarda (oturma, uyku) saniyede en fazla bu kadar kare çizilir.</summary>
    internal const double CalmFrameRate = 20;

    double lastPresentAt = double.MinValue, lastPresentX, lastPresentY;
    CatState lastPresentState;
    int lastPresentVersion = -1;

    /// <summary>
    /// Kareyi görünüme gönderir. Kedi yerinde oturuyor ya da uyuyorsa kare hızını düşürür
    /// (nefes ve kuyruk animasyonu 20 fps'de de akıcı; işlemci ve GDI yükü ~3 kat azalır).
    /// </summary>
    public void Present(bool force = false)
    {
        double now = Now;
        bool calm = state is CatState.Sit or CatState.Sleep && !Paused;
        if (!force && calm && lastPresentState == state && lastPresentX == px && lastPresentY == py &&
            lastPresentVersion == AppearanceVersion && now - lastPresentAt < 1 / CalmFrameRate)
            return;
        lastPresentAt = now; lastPresentX = px; lastPresentY = py; lastPresentState = state; lastPresentVersion = AppearanceVersion;
        view.Present(px, py, S, MakeSprite());
    }

    public void Dismiss() => view.Close();

    internal Sprite MakeSprite()
    {
        var f = new Sprite { Clock = (float)clock, FacingRight = facingRight };
        float eye = blink < 0 ? 0.12f : 1;
        f.EyeOpen = eye;
        switch (state)
        {
            case CatState.Walk or CatState.Chase or CatState.Seek:
                f.Pose = Pose.Walk; f.Phase = (float)phase;
                break;
            case CatState.Eat:
                f.Pose = Pose.Eat; f.Phase = (float)(stateTime * 2.2 % 1); f.Eyes = EyeKind.Happy;
                break;
            case CatState.Flee when playful:
                f.Pose = Pose.Walk; f.Phase = (float)phase; f.Eyes = EyeKind.Happy;
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
            case CatState.Hang:
                f.Pose = Pose.Hang; f.Phase = (float)(stateTime * 2.4 % 1);
                f.Eyes = stateTime < 0.6 ? EyeKind.Wide : EyeKind.Open;
                break;
            case CatState.Climb:
                f.Pose = Pose.Climb; f.Phase = (float)ClimbCycle; f.Eyes = EyeKind.Wide;
                f.EarsBack = stateTime >= stateLength;          // kayarken kulaklar geride
                break;
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
        // İniş: dört ayak üstüne düşüp dizlerini kırarak yaylanır.
        double sinceLand = clock - landClock;
        if (landSquash > 0.15 && sinceLand < LandTime && f.Pose is Pose.Sit or Pose.Walk)
        {
            f.Pose = Pose.Walk;
            f.Crouch = Math.Max(f.Crouch, (float)(landSquash * Math.Sin(Math.PI * Math.Min(1, sinceLand / LandTime + 0.35))));
        }
        if (impact > 0) f.Impact = (float)(ImpactLength - impact);
        if (flung && state == CatState.Air) { f.Eyes = EyeKind.Wide; f.EarsBack = true; }
        if (CurrentEmote is Emote e) { f.Emote = e; f.EmoteAge = (float)emoteTime; }
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
        rideChangedAt = Now;
        Set(CatState.Sit, 1, 1);
    }

    internal void Face(bool right) => facingRight = right;
}
