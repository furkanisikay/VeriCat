using VeriCat.Core.Behavior;
using VeriCat.Core.Rendering;
using VeriCat.Core.Tests.Support;

namespace VeriCat.Core.Tests.Behavior;

public class HuntingTests
{
    static (Sim Sim, Cat Cat) Chasing(double pointerDx, double pointerDy)
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);
        cat.ForceState(CatState.Chase, 10);
        sim.Pointer.Position = (500 + pointerDx, Sim.FloorY + pointerDy);
        return (sim, cat);
    }

    [Fact]
    public void Cursor_within_reach_gets_punched_and_pushed()
    {
        var (sim, cat) = Chasing(20, 60);

        sim.Run(1 / 60.0);
        Assert.Equal(CatState.Swat, cat.State);
        Assert.Equal(Pose.Swat, cat.MakeSprite().Pose);

        sim.Run(Cat.PunchTime);
        Assert.True(sim.Voice.Swats >= 1);
        var nudge = Assert.Single(sim.Pointer.Nudges);
        Assert.True(nudge.Dx > 0, "yumruk yönünde (sağa) itmeli");
    }

    [Fact]
    public void Punch_does_not_move_the_cursor_when_disabled()
    {
        var (sim, _) = Chasing(20, 60);
        sim.Settings.PunchCursor = false;

        sim.Run(1);

        Assert.True(sim.Voice.Swats >= 1);
        Assert.Empty(sim.Pointer.Nudges);
    }

    [Fact]
    public void Punch_never_moves_the_cursor_while_a_button_is_held()
    {
        var (sim, _) = Chasing(20, 60);
        sim.Pointer.AnyButtonDown = true;

        sim.Run(1);

        Assert.Empty(sim.Pointer.Nudges);
    }

    [Fact]
    public void Cursor_high_above_gets_pounced()
    {
        var (sim, cat) = Chasing(10, 220);

        sim.Run(1 / 60.0);
        Assert.Equal(CatState.Crouch, cat.State);

        Assert.True(sim.RunUntil(() => cat.State == CatState.Air, 0.5));
        Assert.Equal(Pose.Pounce, cat.MakeSprite().Pose);
        Assert.True(sim.RunUntil(() => cat.State != CatState.Air, 3), "yere inmeli");
    }

    [Fact]
    public void Chasing_cat_runs_toward_a_distant_cursor()
    {
        var (sim, cat) = Chasing(600, 0);

        sim.Run(0.5);

        Assert.True(cat.X > 550);
        Assert.True(cat.FacingRight);
    }

    [Fact]
    public void Stalking_cat_crouches_and_wiggles_before_pouncing()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);
        sim.Pointer.Position = (620, Sim.FloorY + 40);
        cat.ForceState(CatState.Stalk, 0.8);

        var sprite = cat.MakeSprite();
        Assert.Equal(Pose.Walk, sprite.Pose);
        Assert.True(sprite.Crouch > 0.5);

        Assert.True(sim.RunUntil(() => cat.State == CatState.Crouch, 1.5));
    }
}
