using VeriCat.Core.Behavior;
using VeriCat.Core.Geometry;
using VeriCat.Core.Tests.Support;
using VeriCat.Core.World;

namespace VeriCat.Core.Tests.Behavior;

public class ScreenBoundsTests
{
    static void AssertInside(Sim sim, Cat cat)
    {
        var (screen, _) = sim.World.ScreenNear(cat.X, cat.Y + 1);
        Assert.InRange(cat.X, screen.Bounds.MinX + cat.BodyHalfWidth - 1e-6, screen.Bounds.MaxX - cat.BodyHalfWidth + 1e-6);
        Assert.True(cat.Y + cat.Headroom <= screen.WorkTop + 1e-6, $"tepeden taştı: {cat.Y}");
        Assert.True(cat.Y >= screen.WorkBottom - 1e-6, $"alttan taştı: {cat.Y}");
    }

    [Fact]
    public void Walking_cat_turns_before_its_body_leaves_the_screen()
    {
        var sim = new Sim();
        var cat = sim.AddCat(1850);
        cat.ForceState(CatState.Walk, 1000); cat.Face(right: true);

        sim.Run(3, () => AssertInside(sim, cat));

        Assert.False(cat.FacingRight);
    }

    [Fact]
    public void Window_edge_hanging_off_screen_is_not_a_walkway()
    {
        var w = new WindowSnapshot(1, new RectU(1500, -800, 2600, -400));

        var edge = PlatformBuilder.Build(new[] { Sim.Screen }, new[] { w }, 1, false).Single(p => p.Owner == 1);

        Assert.Equal(1920, edge.MaxX);
    }

    [Fact]
    public void Thrown_cat_never_flies_above_the_screen()
    {
        var sim = new Sim();
        var cat = sim.AddCat(900);
        sim.Pointer.Position = (900, Sim.FloorY + 30);
        cat.Grab();
        for (int i = 1; i <= 5; i++)
        {
            sim.Time += 0.01;
            sim.Pointer.Position = (900 + i * 30, Sim.FloorY + 30 + i * 60);
            cat.Drag();
        }
        cat.Release();

        sim.Run(3, () => AssertInside(sim, cat));
        Assert.NotEqual(CatState.Air, cat.State);
    }

    [Fact]
    public void Cat_in_the_gap_of_uneven_monitors_is_pulled_back_onto_a_screen()
    {
        var sim = new Sim();
        // Sağdaki ekran daha kısa ve aşağıda: aradaki boşluk hiçbir ekranda değil.
        var tall = Sim.Screen;
        var shortRight = new ScreenSnapshot(new RectU(1920, -1080, 3200, -360), -360, -1040);
        sim.World.Update(new[] { tall, shortRight }, Array.Empty<WindowSnapshot>(), 1, true);
        var cat = new Cat(new VeriCat.Core.Configuration.CatConfig(), sim.Env, new NullView(), 2500, -100);
        sim.Colony.Add(cat);

        sim.Run(3, () => Assert.True(sim.World.IsOnAnyScreen(cat.X, cat.Y + 1), $"ekran dışında: {cat.X}, {cat.Y}"));

        Assert.Equal(Sim.FloorY, cat.Y, 3);
    }

    [Fact]
    public void Cats_pushed_together_at_the_edge_stay_on_screen()
    {
        var sim = new Sim();
        sim.Colony.FightChance = 0;
        var a = sim.AddCat(1860);
        var b = sim.AddCat(1870);

        sim.Run(1, () => { AssertInside(sim, a); AssertInside(sim, b); });
    }

    [Fact]
    public void Cat_does_not_jump_onto_a_window_too_close_to_the_screen_top()
    {
        var sim = new Sim(new WindowSnapshot(1, new RectU(400, -800, 1200, -60)));
        var cat = sim.AddCat(800);

        Assert.False(cat.TryJump());
    }
}
