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
    public void Walking_cat_hops_over_the_other_instead_of_pushing()
    {
        var (sim, a, b) = WalkingTowardEachOther(fightChance: 0);
        sim.Colony.RompChance = 0;
        bool flew = false;

        Assert.True(sim.RunUntil(() => { flew |= a.State == CatState.Air; return a.X > b.X + a.ContactRadius; }, 4),
            "a, b'nin öbür yanına geçmeli");
        Assert.True(flew, "üstünden atlamalı");
        Assert.NotEqual(CatState.Fight, a.State);
    }

    [Fact]
    public void Walking_into_a_sitting_cat_hops_over_without_moving_it()
    {
        var sim = new Sim();
        sim.Colony.RompChance = 0;
        sim.Colony.GreetChance = 0;
        sim.Colony.FightChance = 0;
        var a = sim.AddCat(500);
        var b = sim.AddCat(700);
        a.ForceState(CatState.Walk, 1000); a.Face(right: true);

        Assert.True(sim.RunUntil(() => a.X > 780 && a.Support != null, 5), "üstünden atlayıp inmeli");
        Assert.Equal(700, b.X, 3);
    }

    [Fact]
    public void Playful_meeting_turns_into_a_chase()
    {
        var (sim, a, b) = WalkingTowardEachOther(fightChance: 0);
        sim.Colony.RompChance = 1;

        Assert.True(sim.RunUntil(() => a.State == CatState.Flee || b.State == CatState.Flee, 3));
        var (runner, chaser) = a.State == CatState.Flee ? (a, b) : (b, a);
        Assert.Equal(CatState.Seek, chaser.State);
        Assert.Equal(runner.X > chaser.X, runner.FacingRight);   // kovalayandan uzağa
        Assert.NotEqual(VeriCat.Core.Rendering.EyeKind.Wide, runner.MakeSprite().Eyes);   // korku değil, oyun
    }

    [Fact]
    public void Chaser_gives_a_head_start_and_then_catches_the_runner()
    {
        var (sim, a, b) = WalkingTowardEachOther(fightChance: 0);
        sim.Colony.RompChance = 1;
        Assert.True(sim.RunUntil(() => a.State == CatState.Flee || b.State == CatState.Flee, 3));
        var (runner, chaser) = a.State == CatState.Flee ? (a, b) : (b, a);
        int swats = sim.Voice.Swats;

        sim.Run(0.3);
        Assert.Equal(CatState.Seek, chaser.State);   // hemen yakalamaz
        Assert.True(sim.RunUntil(() => sim.Voice.Swats > swats, 4), "yakalayıp pati değmeli");
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
