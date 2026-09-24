using VeriCat.Core.Behavior;
using VeriCat.Core.Geometry;
using VeriCat.Core.Tests.Support;
using VeriCat.Core.World;

namespace VeriCat.Core.Tests.Behavior;

public class MovementTests
{
    static WindowSnapshot Window(double dx = 0, double dy = 0) =>
        new(42, new RectU(300 + dx, -1000 + dy, 1100 + dx, -600 + dy), new[]
        {
            new ChildSnapshot(43, new RectU(320 + dx, -950 + dy, 1080 + dx, -800 + dy)),
        });

    [Fact]
    public void Cat_on_an_inner_section_moves_with_its_window()
    {
        var sim = new Sim(Window());
        var shelf = sim.World.Platforms.Single(p => p.IsInner);
        var cat = sim.AddCat(600, on: shelf);

        sim.SetWindows(Window(dx: 100, dy: -50));
        sim.Run(1 / 60.0);

        Assert.Equal(700, cat.X, 3);
        Assert.Equal(-850, cat.Y, 3);
        Assert.True(cat.Support?.IsInner);
    }

    [Fact]
    public void Cat_falls_when_the_inner_section_disappears()
    {
        var sim = new Sim(Window());
        var cat = sim.AddCat(600, on: sim.World.Platforms.Single(p => p.IsInner));

        sim.SetWindows(new WindowSnapshot(42, new RectU(300, -1000, 1100, -600)));
        sim.Run(1 / 60.0);

        Assert.Equal(CatState.Air, cat.State);
    }

    [Fact]
    public void Now_and_then_a_jump_targets_an_inner_section()
    {
        var sim = new Sim(Window());
        sim.Random.DoubleValue = 0.1;   // < %40: bu zıplama pencere içlerini hedefler
        var cat = sim.AddCat(600);

        Assert.True(cat.TryJump());
        Assert.True(sim.RunUntil(() => cat.State == CatState.Air, 0.5));
        Assert.True(sim.RunUntil(() => cat.State != CatState.Air, 3));

        Assert.True(cat.Support?.IsInner);
        Assert.Equal(-800, cat.Y, 3);
    }

    [Fact]
    public void Inner_sections_are_skipped_when_the_setting_is_off()
    {
        var sim = new Sim(Window());
        sim.Settings.InnerWindows = false;
        sim.Random.DoubleValue = 0.1;
        var cat = sim.AddCat(600);

        Assert.False(cat.TryJump());   // tek aday iç bölümdü
    }

    [Fact]
    public void Dropped_cat_lands_on_the_floor()
    {
        var sim = new Sim();
        var cat = new Cat(new VeriCat.Core.Configuration.CatConfig(), sim.Env, new NullView(), 800, -200);
        sim.Colony.Add(cat);

        Assert.True(sim.RunUntil(() => cat.State != CatState.Air, 3));
        Assert.Equal(Sim.FloorY, cat.Y, 3);
    }
}
