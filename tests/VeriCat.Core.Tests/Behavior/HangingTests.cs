using VeriCat.Core.Behavior;
using VeriCat.Core.Geometry;
using VeriCat.Core.Props;
using VeriCat.Core.Rendering;
using VeriCat.Core.Tests.Support;
using VeriCat.Core.World;

namespace VeriCat.Core.Tests.Behavior;

public class HangingTests
{
    // Ekranın tepesine 60 px yakın, üstünde durulamayan bir pencere (1) ve basamak olarak alçak bir pencere (2).
    static readonly WindowSnapshot High = new(1, new RectU(900, -300, 1500, -60));
    static readonly WindowSnapshot Low = new(2, new RectU(200, -800, 900, -500));

    [Fact]
    public void Cat_jumps_up_and_hangs_from_an_edge_with_no_room_above()
    {
        var sim = new Sim(High, Low);
        var step = sim.World.Platforms.Single(p => p.Owner == 2);
        var cat = sim.AddCat(800, on: step);

        Assert.True(cat.TryHangJump());
        Assert.True(sim.RunUntil(() => cat.State == CatState.Hang, 2), "kenara tutunmalı");
        Assert.Equal(1, cat.Support!.Value.Owner);
        Assert.Equal(-60 - 122, cat.Y, 1);
        Assert.Equal(Pose.Hang, cat.MakeSprite().Pose);
        Assert.True(sim.Voice.Scratches >= 1, "tırmalamalı");

        Assert.True(sim.RunUntil(() => cat.State != CatState.Hang, 12), "bir süre sonra bırakmalı");
        Assert.True(sim.RunUntil(() => cat.Support != null && cat.State != CatState.Air, 4), "aşağı inmeli");
    }

    [Fact]
    public void Window_pushed_to_the_screen_top_leaves_the_cat_hanging_instead_of_falling()
    {
        var sim = new Sim(new WindowSnapshot(3, new RectU(400, -700, 1200, -300)));
        var cat = sim.AddCat(800, on: sim.World.Platforms.Single(p => p.Owner == 3));
        sim.Run(1);

        sim.SetWindows(new WindowSnapshot(3, new RectU(400, -440, 1200, -40)));
        sim.Run(1 / 60.0);

        Assert.Equal(CatState.Hang, cat.State);
        Assert.Equal(-40 - 122, cat.Y, 1);
    }

    [Fact]
    public void Hanging_cat_follows_the_window_and_drops_when_it_closes()
    {
        var sim = new Sim(new WindowSnapshot(3, new RectU(400, -440, 1200, -40)));
        var cat = sim.AddCat(800, on: sim.World.Platforms.Single(p => p.Owner == 3));
        sim.Run(1 / 60.0);
        Assert.Equal(CatState.Hang, cat.State);

        sim.Run(1);
        sim.SetWindows(new WindowSnapshot(3, new RectU(500, -440, 1300, -40)));
        sim.Run(1 / 60.0);
        Assert.Equal(900, cat.X, 1);

        sim.SetWindows();
        sim.Run(1 / 60.0);
        Assert.Equal(CatState.Air, cat.State);
    }

    [Fact]
    public void Walking_cat_hops_over_a_bowl_in_its_way()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);
        var bowl = new Prop(PropKind.Bowl, sim.Env, new NullPropView(), 620, Sim.FloorY);
        sim.Colony.AddProp(bowl);
        sim.Run(0.5);
        cat.ForceState(CatState.Walk, 1000); cat.Face(right: true);
        bool flew = false;

        Assert.True(sim.RunUntil(() => { flew |= cat.State == CatState.Air; return cat.X > 700 && cat.Support != null; }, 5));
        Assert.True(flew, "kabın üstünden atlamalı");
        Assert.Equal(620, bowl.X, 1);
    }

    // Tam ekran (ekranın çalışma alanını kaplayan) pencere: üst kenarı yok, perde gibi.
    static readonly WindowSnapshot Maximized = new(5, new RectU(0, -1040, 1920, 0));

    [Fact]
    public void Cat_climbs_a_fullscreen_window_like_a_curtain_and_hangs_at_the_top()
    {
        var sim = new Sim(Maximized);
        var cat = sim.AddCat(960);

        Assert.True(cat.TryCurtainClimb());
        Assert.Equal(Pose.Hang, cat.MakeSprite().Pose);
        Assert.True(sim.RunUntil(() => cat.Y >= -122 - 1, 6), $"tepeye tırmanmalı (y={cat.Y:F0})");
        Assert.Equal(CatState.Hang, cat.State);
        Assert.True(sim.Voice.Scratches >= 2, "tırmalamalı");

        Assert.True(sim.RunUntil(() => cat.State != CatState.Hang, 10), "yorulunca bırakmalı");
        Assert.True(sim.RunUntil(() => cat.Support != null && cat.State != CatState.Air, 4));
    }

    [Fact]
    public void High_cursor_in_the_middle_is_reached_via_the_curtain()
    {
        var sim = new Sim(Maximized);
        var cat = sim.AddCat(960);
        sim.Pointer.Position = (960, -200);
        cat.ForceState(CatState.Chase, 10);

        Assert.True(sim.RunUntil(() => cat.State == CatState.Hang, 1), "perdeye tırmanmaya başlamalı");
        Assert.True(sim.RunUntil(() => cat.State == CatState.Air, 6), "imlece atlamalı");
        Assert.Equal(Pose.Pounce, cat.MakeSprite().Pose);
    }
}
