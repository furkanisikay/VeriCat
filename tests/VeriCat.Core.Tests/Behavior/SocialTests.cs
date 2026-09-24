using VeriCat.Core.Behavior;
using VeriCat.Core.Tests.Support;

namespace VeriCat.Core.Tests.Behavior;

public class SocialTests
{
    static (Sim Sim, Cat A, Cat B) WalkingTowardEachOther(double fightChance)
    {
        var sim = new Sim();
        sim.Colony.FightChance = fightChance;
        var a = sim.AddCat(500);
        var b = sim.AddCat(700);
        a.ForceState(CatState.Walk, 1000); a.Face(right: true);
        b.ForceState(CatState.Walk, 1000); b.Face(right: false);
        return (sim, a, b);
    }

    [Fact]
    public void Cats_on_the_same_floor_never_pass_through_each_other()
    {
        var (sim, a, b) = WalkingTowardEachOther(fightChance: 0);
        double min = a.ContactRadius + b.ContactRadius;

        sim.Run(4, () => Assert.True(b.X - a.X >= min - 1e-6, $"iç içe geçtiler: {b.X - a.X:F1} < {min}"));
    }

    [Fact]
    public void Without_a_fight_they_bump_and_turn_back()
    {
        var (sim, a, b) = WalkingTowardEachOther(fightChance: 0);

        sim.Run(3);

        Assert.False(a.FacingRight);
        Assert.True(b.FacingRight);
        Assert.NotEqual(CatState.Fight, a.State);
    }

    [Fact]
    public void Meeting_can_start_a_fight_that_ends_with_one_cat_fleeing()
    {
        var (sim, a, b) = WalkingTowardEachOther(fightChance: 1);

        Assert.True(sim.RunUntil(() => a.State == CatState.Fight, 3));
        Assert.Equal(CatState.Fight, b.State);
        Assert.Same(b, a.Opponent);
        Assert.Same(a, b.Opponent);
        Assert.True(sim.Voice.Hisses > 0);

        Assert.True(sim.RunUntil(() => a.State != CatState.Fight, 5));
        var states = new[] { a.State, b.State };
        Assert.Contains(CatState.Flee, states);
        Assert.Contains(CatState.Sit, states);
        Assert.Null(a.Opponent);
        Assert.Null(b.Opponent);
    }

    [Fact]
    public void Fight_stops_when_one_cat_is_picked_up()
    {
        var (sim, a, b) = WalkingTowardEachOther(fightChance: 1);
        Assert.True(sim.RunUntil(() => a.State == CatState.Fight, 3));

        sim.Pointer.Position = (a.X, a.Y + 30);
        a.Grab();
        sim.Pointer.Position = (a.X + 50, a.Y + 80);
        a.Drag();
        sim.Run(0.1);

        Assert.Equal(CatState.Dragged, a.State);
        Assert.NotEqual(CatState.Fight, b.State);
    }

    [Fact]
    public void Recent_fighters_do_not_immediately_fight_again()
    {
        var (sim, a, b) = WalkingTowardEachOther(fightChance: 1);
        Assert.True(sim.RunUntil(() => a.State == CatState.Fight, 3));
        Assert.True(sim.RunUntil(() => a.State != CatState.Fight, 5));

        a.ForceState(CatState.Walk, 1000); a.Face(b.X > a.X);
        b.ForceState(CatState.Walk, 1000); b.Face(a.X > b.X);

        Assert.False(sim.RunUntil(() => a.State == CatState.Fight, 3));
    }

    [Fact]
    public void Cats_on_different_levels_do_not_collide()
    {
        var sim = new Sim(new VeriCat.Core.World.WindowSnapshot(9, new VeriCat.Core.Geometry.RectU(300, -900, 1000, -600)));
        var top = sim.World.Platforms.Single(p => p.Owner == 9);
        var a = sim.AddCat(600);
        var b = sim.AddCat(600, on: top);

        sim.Run(0.5);

        Assert.Equal(600, a.X, 3);
        Assert.Equal(600, b.X, 3);
    }
}
