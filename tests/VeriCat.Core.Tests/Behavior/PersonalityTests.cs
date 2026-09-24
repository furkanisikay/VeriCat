using VeriCat.Core.Behavior;
using VeriCat.Core.Configuration;
using VeriCat.Core.Tests.Support;

namespace VeriCat.Core.Tests.Behavior;

public class PersonalityTests
{
    static (Sim Sim, Cat A, Cat B) Meeting(Personality a, Personality b, double roll)
    {
        var sim = new Sim();
        sim.Random.DoubleValue = roll;
        var ca = sim.AddCat(500);
        var cb = sim.AddCat(700);
        ca.Config.Personality = a;
        cb.Config.Personality = b;
        ca.ForceState(CatState.Walk, 1000); ca.Face(right: true);
        cb.ForceState(CatState.Walk, 1000); cb.Face(right: false);
        return (sim, ca, cb);
    }

    [Fact]
    public void Gentle_cats_never_fight()
    {
        var calm = new Personality { Temper = 0 };
        var (sim, a, _) = Meeting(calm, calm, roll: 0.01);

        Assert.False(sim.RunUntil(() => a.State == CatState.Fight, 3));
    }

    [Fact]
    public void Hot_tempered_cats_fight_more_easily()
    {
        var hot = new Personality { Temper = 1 };
        var (sim, a, _) = Meeting(hot, hot, roll: 0.7);   // ortalama kediler için kavga eşiği 0.5, huysuzlar için 0.75 (tavan)

        Assert.True(sim.RunUntil(() => a.State == CatState.Fight, 3));
    }

    [Fact]
    public void Affectionate_cats_greet_each_other()
    {
        var sweet = new Personality { Temper = 0, Affection = 1 };
        var (sim, a, b) = Meeting(sweet, sweet, roll: 0.9);

        Assert.True(sim.RunUntil(() => a.State == CatState.Happy, 3));
        Assert.Equal(CatState.Happy, b.State);
        Assert.True(a.FacingRight);
        Assert.False(b.FacingRight);
    }

    [Fact]
    public void Aloof_cat_runs_from_petting_more_often()
    {
        var sim = new Sim();
        sim.Random.DoubleValue = 0.2;   // ortalama kedi için kaçma eşiği 0.15; mesafeli kedi için 0.3
        var cat = sim.AddCat(500);
        cat.Config.Personality = new Personality { Affection = 0 };

        cat.Pet(150);

        Assert.Equal(CatState.Flee, cat.State);
    }

    [Fact]
    public void Random_cat_is_valid_and_saves_cleanly()
    {
        var rng = new Random(42);
        for (int i = 0; i < 50; i++)
        {
            var c = CatConfig.Random(rng, 1);
            Assert.NotNull(CatConfig.SanitizeName(c.Name));
            Assert.InRange(c.Personality.Playfulness, 0, 1);
            Assert.True(Enum.IsDefined(c.Accessory));
        }
    }

    [Fact]
    public void Clone_is_independent()
    {
        var a = new CatConfig { Name = "A", Personality = new Personality { Energy = 0.9 } };
        var b = a.Clone();
        b.Name = "B";
        b.Personality = b.Personality with { Energy = 0.1 };

        Assert.Equal("A", a.Name);
        Assert.Equal(0.9, a.Personality.Energy);
    }
}

public class ExtrasTests
{
    [Fact]
    public void Summoned_cat_comes_to_the_cursor_and_is_happy_instead_of_punching()
    {
        var sim = new Sim();
        var cat = sim.AddCat(300);
        sim.Pointer.Position = (900, Sim.FloorY + 40);

        cat.Summon();

        Assert.True(sim.RunUntil(() => cat.State == CatState.Happy, 6));
        Assert.InRange(cat.X, 830, 900);
        Assert.Empty(sim.Pointer.Nudges);
        Assert.Equal(0, sim.Voice.Swats);
    }

    [Fact]
    public void Hop_jumps_onto_a_window_right_away_when_enabled()
    {
        var sim = new Sim(new VeriCat.Core.World.WindowSnapshot(9, new VeriCat.Core.Geometry.RectU(300, -900, 1100, -800)));
        var cat = sim.AddCat(700);

        Assert.True(cat.Hop());
        Assert.True(sim.RunUntil(() => cat.State == CatState.Air, 0.5));
        Assert.True(sim.RunUntil(() => cat.State != CatState.Air, 3));
        Assert.Equal(9, cat.Support?.Owner);
    }

    [Fact]
    public void Hop_does_nothing_when_windows_are_off_or_cat_is_busy()
    {
        var sim = new Sim(new VeriCat.Core.World.WindowSnapshot(9, new VeriCat.Core.Geometry.RectU(300, -900, 1100, -800)));
        var cat = sim.AddCat(700);

        sim.Settings.Windows = false;
        Assert.False(cat.Hop());

        sim.Settings.Windows = true;
        cat.ForceState(CatState.Fight, 2);
        Assert.False(cat.Hop());
    }

    [Fact]
    public void Turning_chase_on_starts_play_and_off_stops_it()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);

        cat.PlayWithPointer();
        Assert.Equal(CatState.Chase, cat.State);

        cat.StopHunting();
        Assert.Equal(CatState.Sit, cat.State);

        sim.Settings.Chase = false;
        cat.PlayWithPointer();
        Assert.Equal(CatState.Sit, cat.State);
    }

    [Fact]
    public void Sitting_cat_is_redrawn_at_a_reduced_frame_rate()
    {
        var sim = new Sim();
        var view = new CountingView();
        var cat = new Cat(new CatConfig(), sim.Env, view, 500, 0);
        cat.PlaceOn(sim.Floor, 500);
        cat.ForceState(CatState.Sit, 1000);
        sim.Colony.Add(cat);

        sim.Run(1);   // 60 kare

        Assert.InRange(view.Frames, Cat.CalmFrameRate - 2, Cat.CalmFrameRate + 2);
    }

    [Fact]
    public void Moving_cat_is_drawn_every_frame()
    {
        var sim = new Sim();
        var view = new CountingView();
        var cat = new Cat(new CatConfig(), sim.Env, view, 500, 0);
        cat.PlaceOn(sim.Floor, 500);
        cat.ForceState(CatState.Walk, 1000);
        sim.Colony.Add(cat);

        sim.Run(1);

        Assert.InRange(view.Frames, 58, 62);
    }

    sealed class CountingView : VeriCat.Core.Abstractions.ICatView
    {
        public int Frames;
        public void Present(double feetX, double feetY, double scale, VeriCat.Core.Rendering.Sprite sprite) => Frames++;
        public void Close() { }
    }
}
