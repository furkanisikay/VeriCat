using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Core.Geometry;
using VeriCat.Core.Props;
using VeriCat.Core.Rendering;
using VeriCat.Core.Tests.Support;
using VeriCat.Core.World;

namespace VeriCat.Core.Tests.Behavior;

internal sealed class NullPropView : IPropView
{
    public int Frames;
    public void Present(Prop prop) => Frames++;
    public void Close() { }
}

public class WindowFlingTests
{
    static WindowSnapshot Win(double dx) => new(7, new RectU(300 + dx, -900, 1100 + dx, -700));

    [Fact]
    public void Shaking_a_window_fast_throws_the_cat_off()
    {
        var sim = new Sim(Win(0));
        var cat = sim.AddCat(700, on: sim.World.Platforms.Single(p => p.Owner == 7));
        sim.Run(1);

        // İlk hareket (uzun duruştan sonra) savurmaz; hemen ardından gelen hızlı sallama savurur.
        sim.SetWindows(Win(40)); sim.Run(1 / 15.0);
        Assert.NotEqual(CatState.Air, cat.State);
        sim.SetWindows(Win(300)); sim.Run(1 / 60.0);   // 260 px / (1/15 sn) ≈ 3900 px/sn

        Assert.Equal(CatState.Air, cat.State);
        Assert.Equal(Emote.Surprised, cat.CurrentEmote);
        Assert.True(sim.Voice.Meows > 0);
        Assert.True(sim.RunUntil(() => cat.State != CatState.Air, 3));
    }

    [Fact]
    public void Dragging_a_window_calmly_carries_the_cat()
    {
        var sim = new Sim(Win(0));
        var cat = sim.AddCat(700, on: sim.World.Platforms.Single(p => p.Owner == 7));
        sim.Run(1);

        for (int i = 1; i <= 15; i++)   // 1 saniyede 300 px: sakin sürükleme
        {
            sim.SetWindows(Win(i * 20));
            sim.Run(1 / 15.0);
            Assert.NotEqual(CatState.Air, cat.State);
        }
        Assert.Equal(1000, cat.X, 0);
    }

    [Fact]
    public void Yarn_on_a_shaken_window_flies_too()
    {
        var sim = new Sim(Win(0));
        var yarn = new Prop(PropKind.Yarn, sim.Env, new NullPropView(), 700, 0);
        yarn.PlaceOn(sim.World.Platforms.Single(p => p.Owner == 7), 700);
        sim.Colony.AddProp(yarn);
        sim.Run(1);

        sim.SetWindows(Win(40)); sim.Run(1 / 15.0);
        sim.SetWindows(Win(300)); sim.Run(1 / 60.0);

        Assert.False(yarn.IsGrounded);
        Assert.True(yarn.VX > 0);
    }
}

public class VitalsTests
{
    [Fact]
    public void Needs_drain_over_time_and_sleep_restores_energy()
    {
        var v = new Vitals { Hunger = 1, Love = 1, Fun = 1, Energy = 0.5 };

        v.Decay(3600, sleeping: false);
        Assert.Equal(0.75, v.Hunger, 3);
        Assert.True(v.Energy < 0.5);

        v.Decay(600, sleeping: true);
        Assert.True(v.Energy > 0.5);
    }

    [Fact]
    public void Most_urgent_need_is_the_lowest_under_the_threshold()
    {
        Assert.Null(new Vitals().MostUrgent());
        Assert.Equal(Need.Fun, new Vitals { Hunger = 0.2, Fun = 0.1 }.MostUrgent());
    }

    [Fact]
    public void Time_away_is_gentle()
    {
        var saved = new DateTimeOffset(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);
        var v = new Vitals { Hunger = 0.9, Love = 0.9, Fun = 0.9, Energy = 0.2, SavedAt = saved };

        v.CatchUp(saved.AddDays(3));   // 3 gün kapalı

        Assert.True(v.Hunger >= Vitals.OfflineFloor);
        Assert.True(v.Love >= Vitals.OfflineFloor);
        Assert.True(v.Energy > 0.2, "kapalıyken dinlenmiş sayılır");
    }

    [Fact]
    public void Petting_fills_love()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);
        cat.Vitals.Love = 0.2;
        sim.Pointer.Position = (500, Sim.FloorY + 50);

        cat.Pet(150);
        sim.Run(5, () => cat.Pet(8));

        Assert.True(cat.Vitals.Love > 0.4);
    }
}

public class FeedingTests
{
    static (Sim Sim, Cat Cat, Prop Bowl) Setup(double food)
    {
        var sim = new Sim();
        var cat = sim.AddCat(400);
        var bowl = new Prop(PropKind.Bowl, sim.Env, new NullPropView(), 900, 0) { Food = food };
        bowl.PlaceOn(sim.Floor, 900);
        sim.Colony.AddProp(bowl);
        cat.Vitals.Hunger = 0.1;
        cat.ForceState(CatState.Sit, 0.1);
        return (sim, cat, bowl);
    }

    [Fact]
    public void Hungry_cat_walks_to_the_bowl_and_eats()
    {
        var (sim, cat, bowl) = Setup(food: 1);

        Assert.True(sim.RunUntil(() => cat.State == CatState.Eat, 8));
        Assert.InRange(Math.Abs(cat.X - bowl.X), 0, 36 + bowl.Radius + 2);
        Assert.Equal(Pose.Eat, cat.MakeSprite().Pose);

        Assert.True(sim.RunUntil(() => cat.State != CatState.Eat, 10));
        Assert.True(cat.Vitals.Hunger > 0.6);
        Assert.True(bowl.Food < 1);
    }

    [Fact]
    public void Empty_bowl_is_ignored_and_the_cat_asks_you_instead()
    {
        var (sim, cat, _) = Setup(food: 0);
        sim.Random.DoubleValue = 0.1;   // "sana gelip miyavla" dalı
        sim.Pointer.Position = (150, Sim.FloorY + 40);

        Assert.True(sim.RunUntil(() => cat.CurrentEmote == Emote.Hungry, 10));
        Assert.NotEqual(CatState.Eat, cat.State);
    }

    [Fact]
    public void Bowl_on_a_window_is_reached_by_jumping()
    {
        var sim = new Sim(new WindowSnapshot(3, new RectU(600, -1000, 1300, -850)));
        var cat = sim.AddCat(500);
        var bowl = new Prop(PropKind.Bowl, sim.Env, new NullPropView(), 950, 0) { Food = 1 };
        bowl.PlaceOn(sim.World.Platforms.Single(p => p.Owner == 3), 950);
        sim.Colony.AddProp(bowl);
        cat.Vitals.Hunger = 0.1;
        cat.ForceState(CatState.Sit, 0.1);

        Assert.True(sim.RunUntil(() => cat.State == CatState.Eat, 12));
        Assert.Equal(3, cat.Support?.Owner);
    }
}

public class YarnTests
{
    [Fact]
    public void Thrown_yarn_bounces_rolls_and_stops_on_screen()
    {
        var sim = new Sim();
        var yarn = new Prop(PropKind.Yarn, sim.Env, new NullPropView(), 900, -400);
        sim.Colony.AddProp(yarn);
        yarn.Kick(1500, 800);

        bool bounced = false;
        double lastVy = 0;
        sim.Run(8, () =>
        {
            if (lastVy < 0 && yarn.VY > 0) bounced = true;
            lastVy = yarn.VY;
            Assert.InRange(yarn.X, yarn.Radius - 1e-6, 1920 - yarn.Radius + 1e-6);
        });

        Assert.True(bounced, "yere çarpınca sekmeli");
        Assert.True(yarn.IsGrounded);
        Assert.Equal(0, yarn.VX);
        Assert.Equal(Sim.FloorY, yarn.Y, 3);
    }

    [Fact]
    public void Resting_props_cost_nothing_to_draw()
    {
        var sim = new Sim();
        var view = new NullPropView();
        var bowl = new Prop(PropKind.Bowl, sim.Env, view, 900, 0);
        bowl.PlaceOn(sim.Floor, 900);
        sim.Colony.AddProp(bowl);

        sim.Run(2);

        Assert.True(view.Frames <= 1, $"duran kap {view.Frames} kez çizildi");
    }

    [Fact]
    public void Bored_playful_cat_goes_after_the_yarn_and_bats_it()
    {
        var sim = new Sim();
        var cat = sim.AddCat(400);
        cat.Config.Personality = new Personality { Playfulness = 1 };
        var yarn = new Prop(PropKind.Yarn, sim.Env, new NullPropView(), 800, 0);
        yarn.PlaceOn(sim.Floor, 800);
        sim.Colony.AddProp(yarn);
        cat.Vitals.Fun = 0.1;
        cat.ForceState(CatState.Sit, 0.1);

        Assert.True(sim.RunUntil(() => !yarn.IsGrounded || yarn.VX != 0, 10), "yumağa vurmalı");
        Assert.True(sim.Voice.Swats > 0);
        Assert.True(cat.Vitals.Fun > 0.1);
    }

    [Fact]
    public void Rolling_yarn_does_not_pass_through_cats()
    {
        var sim = new Sim();
        var cat = sim.AddCat(800);
        cat.Config.Personality = new Personality { Playfulness = 0 };
        sim.Random.DoubleValue = 0.99;   // refleks pati yok, sadece çarpışma
        var yarn = new Prop(PropKind.Yarn, sim.Env, new NullPropView(), 500, 0);
        yarn.PlaceOn(sim.Floor, 500);
        sim.Colony.AddProp(yarn);
        yarn.Kick(900, 0);

        sim.Run(2);

        Assert.True(yarn.X < cat.X, "kediden geri sekmeli");
    }
}

public class BondTests
{
    static (Sim Sim, Cat A, Cat B) Meeting(double bond, double roll)
    {
        var sim = new Sim();
        sim.Random.DoubleValue = roll;
        var a = sim.AddCat(500);
        var b = sim.AddCat(700);
        sim.Colony.Bonds.Adjust(a, b, bond);
        a.ForceState(CatState.Walk, 1000); a.Face(right: true);
        b.ForceState(CatState.Walk, 1000); b.Face(right: false);
        return (sim, a, b);
    }

    [Fact]
    public void Fighting_makes_rivals_and_greeting_makes_friends()
    {
        var (sim, a, b) = Meeting(0, roll: 0.1);
        Assert.True(sim.RunUntil(() => a.State == CatState.Fight, 3));
        Assert.True(sim.Colony.Bonds.Get(a, b) < 0);

        var (sim2, c, d) = Meeting(0, roll: 0.9);
        Assert.True(sim2.RunUntil(() => c.State == CatState.Happy, 3));
        Assert.True(sim2.Colony.Bonds.Get(c, d) > 0);
    }

    [Fact]
    public void Rivals_hiss_instead_of_greeting()
    {
        var (sim, a, b) = Meeting(-0.8, roll: 0.95);

        Assert.True(sim.RunUntil(() => a.CurrentEmote == Emote.Grumpy || b.CurrentEmote == Emote.Grumpy, 3));
        Assert.NotEqual(CatState.Happy, a.State);
        Assert.NotEqual(CatState.Fight, a.State);
        Assert.True(sim.Voice.Hisses > 0);
    }

    [Fact]
    public void Tired_cat_curls_up_next_to_a_sleeping_friend()
    {
        var sim = new Sim();
        var a = sim.AddCat(400);
        var friend = sim.AddCat(1200);
        sim.Colony.Bonds.Adjust(a, friend, 0.9);
        friend.Sleep();
        a.Vitals.Energy = 0.05;
        sim.Random.DoubleValue = 0.1;
        a.ForceState(CatState.Sit, 0.1);

        Assert.True(sim.RunUntil(() => a.IsAsleep, 15));
        Assert.InRange(Math.Abs(a.X - friend.X), a.ContactRadius * 2 - 8, a.ContactRadius * 2 + 12);
        Assert.True(friend.IsAsleep, "arkadaşı uyanmamalı");
    }

    [Fact]
    public void Forgetting_a_cat_removes_its_bonds()
    {
        var sim = new Sim();
        var a = sim.AddCat(400);
        var b = sim.AddCat(800);
        sim.Colony.Bonds.Adjust(a, b, 0.5);

        sim.Colony.Bonds.Forget(a.Config.Id);

        Assert.Equal(0, sim.Colony.Bonds.Get(a, b));
    }
}

public class NightTests
{
    [Fact]
    public void Cats_sleep_more_at_night()
    {
        int Sleeps(int hour)
        {
            int count = 0;
            var rng = new Random(7);
            for (int i = 0; i < 400; i++)
            {
                var sim = new Sim { LocalNow = new DateTime(2026, 9, 24, hour, 0, 0) };
                var cat = new Cat(new CatConfig(), new CatEnvironment
                {
                    World = sim.World, Voice = sim.Voice, Pointer = sim.Pointer, Settings = sim.Settings,
                    Clock = () => sim.Time, Random = rng, LocalTime = () => sim.LocalNow,
                }, new NullView(), 500, 0);
                cat.PlaceOn(sim.Floor, 500);
                cat.ForceState(CatState.Sit, 0);
                cat.Step(0.01);
                if (cat.IsAsleep) count++;
            }
            return count;
        }

        int night = Sleeps(2), day = Sleeps(14);
        Assert.True(night > day * 2, $"gece {night}, gündüz {day}");
    }
}
