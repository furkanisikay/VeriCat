using VeriCat.Core.Behavior;
using VeriCat.Core.Geometry;
using VeriCat.Core.Rendering;
using VeriCat.Core.Tests.Support;
using VeriCat.Core.World;

namespace VeriCat.Core.Tests.Behavior;

public class ClimbingTests
{
    static (Sim Sim, Cat Cat) Hunting(double catX, double pointerX, double pointerY, params WindowSnapshot[] windows)
    {
        var sim = new Sim(windows);
        var cat = sim.AddCat(catX);
        sim.Pointer.Position = (pointerX, pointerY);
        cat.ForceState(CatState.Chase, 10);
        return (sim, cat);
    }

    [Fact]
    public void Cursor_high_near_the_screen_edge_is_reached_by_climbing_the_wall()
    {
        var (sim, cat) = Hunting(1500, 1800, -300);

        Assert.True(sim.RunUntil(() => cat.State == CatState.Climb, 4), "duvara tutunmalı");
        Assert.Equal(Pose.Climb, cat.MakeSprite().Pose);
        Assert.True(cat.X > 1850, "sağ kenara yaslanmalı");

        double startY = cat.Y;
        Assert.True(sim.RunUntil(() => cat.State == CatState.Air, 5), "tepede imlece atlamalı");
        Assert.True(cat.Y > startY + 150, "yukarı tırmanmış olmalı");
        Assert.Equal(Pose.Pounce, cat.MakeSprite().Pose);
        Assert.True(sim.RunUntil(() => sim.Voice.Swats >= 2, 2), "atlayışta imlece vurmalı");
    }

    [Fact]
    public void Cat_kicks_off_the_wall_to_get_higher()
    {
        var (sim, cat) = Hunting(1700, 1300, -600);
        double peak = double.MinValue;

        sim.RunUntil(() => { peak = Math.Max(peak, cat.Y); return cat.State == CatState.Sit; }, 6);

        Assert.True(sim.Voice.Swats >= 1, "duvardan sekmeli (pati sesi)");
        Assert.True(peak > Sim.FloorY + 420, $"normal zıplamadan daha yükseğe çıkmalı (tepe {peak - Sim.FloorY:F0})");
    }

    [Fact]
    public void Windows_are_used_as_steps_toward_a_high_cursor()
    {
        // Ortada, zeminden 340 px yüksekte bir pencere; imleç onun çok üstünde, kenarlardan uzakta.
        var win = new WindowSnapshot(7, new RectU(800, -900, 1200, -700), Array.Empty<ChildSnapshot>());
        var (sim, cat) = Hunting(700, 1000, -150, win);

        Assert.True(sim.RunUntil(() => cat.Support is { Owner: 7 }, 4), "pencereye basamak gibi zıplamalı");
    }

    [Fact]
    public void Uninterested_cat_just_watches_a_high_cursor()
    {
        var (sim, cat) = Hunting(1500, 1800, -300);
        sim.Random.DoubleValue = 0.99;   // istek zarı tutmaz

        sim.Run(3);

        Assert.NotEqual(CatState.Climb, cat.State);
        Assert.Equal(Sim.FloorY, cat.Y, 3);
    }

    [Fact]
    public void Tired_climber_slides_down_and_lets_go()
    {
        var (sim, cat) = Hunting(1500, 1800, -60);
        Assert.True(sim.RunUntil(() => cat.State == CatState.Climb, 4));
        sim.Pointer.Position = (1800, 60);   // ekranın dışı: hiç ulaşamaz, tepede bekler

        Assert.True(sim.RunUntil(() => cat.State != CatState.Climb, 12), "yorulunca bırakmalı");
        Assert.True(sim.RunUntil(() => cat.Support != null, 5), "yere inmeli");
    }
}
