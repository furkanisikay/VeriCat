using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Core.Geometry;
using VeriCat.Core.World;

namespace VeriCat.Core.Tests.Support;

/// <summary>Tek ekranlı (1920x1080, görev çubuğu 40 px) deterministik bir masaüstü simülasyonu.</summary>
internal sealed class Sim
{
    public const double FloorY = -1040;
    public static readonly ScreenSnapshot Screen = new(new RectU(0, -1080, 1920, 0), 0, FloorY);

    public double Time;
    public FakeVoice Voice { get; } = new();
    public FakePointer Pointer { get; } = new() { Position = (-5000, 5000) };
    public WorldModel World { get; } = new();
    public AppSettings Settings { get; } = new();
    public ScriptedRandom Random { get; } = new();
    public CatEnvironment Env { get; }
    public Colony Colony { get; }

    public Sim(params WindowSnapshot[] windows)
    {
        SetWindows(windows);
        Env = new CatEnvironment
        {
            World = World, Voice = Voice, Pointer = Pointer, Settings = Settings, Clock = () => Time, Random = Random,
        };
        Colony = new Colony(Env);
    }

    public void SetWindows(params WindowSnapshot[] windows) => World.Update(new[] { Screen }, windows, 1, Settings.InnerWindows);

    public Platform Floor => World.FloorAt(100, FloorY + 1)!.Value;

    /// <summary>Zemine oturmuş bir kedi ekler.</summary>
    public Cat AddCat(double x, double scale = 1, Platform? on = null)
    {
        var cat = new Cat(new CatConfig { Name = "Test", Scale = scale }, Env, new NullView(), x, 0);
        cat.PlaceOn(on ?? Floor, x);
        cat.ForceState(CatState.Sit, 1000);
        Colony.Add(cat);
        return cat;
    }

    public void Run(double seconds, Action? eachFrame = null, double dt = 1 / 60.0)
    {
        for (double t = 0; t < seconds; t += dt)
        {
            Time += dt;
            Colony.Step(dt);
            eachFrame?.Invoke();
        }
    }

    /// <summary>Koşul sağlanana kadar ilerletir; sağlandıysa true.</summary>
    public bool RunUntil(Func<bool> condition, double maxSeconds, double dt = 1 / 60.0)
    {
        for (double t = 0; t < maxSeconds; t += dt)
        {
            Time += dt;
            Colony.Step(dt);
            if (condition()) return true;
        }
        return false;
    }
}
