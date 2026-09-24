using VeriCat.Core.Behavior;
using VeriCat.Core.Rendering;
using VeriCat.Core.Tests.Support;

namespace VeriCat.Core.Tests.Behavior;

public class PettingTests
{
    static void Stroke(Cat cat, double pixels = 150)
    {
        for (int i = 0; i < 3; i++) cat.Pet(pixels / 3);
    }

    [Fact]
    public void Enough_stroking_makes_the_cat_purr_with_hearts()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);

        Stroke(cat);

        Assert.Equal(CatState.Petted, cat.State);
        Assert.True(sim.Voice.Purrs > 0);
        var sprite = cat.MakeSprite();
        Assert.Equal(EyeKind.Happy, sprite.Eyes);
        Assert.NotNull(sprite.Hearts);
    }

    [Fact]
    public void A_little_movement_is_not_petting()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);

        cat.Pet(20);

        Assert.Equal(CatState.Sit, cat.State);
    }

    [Fact]
    public void Sometimes_the_cat_is_not_in_the_mood_and_runs_away()
    {
        var sim = new Sim();
        sim.Random.DoubleValue = Cat.MoodyChance / 2;
        var cat = sim.AddCat(500);
        sim.Pointer.Position = (480, Sim.FloorY + 40);

        Stroke(cat);

        Assert.Equal(CatState.Flee, cat.State);
        Assert.True(cat.FacingRight, "imleçten uzağa kaçmalı");
    }

    [Fact]
    public void When_petting_stops_the_cat_stays_happy_for_a_bit()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);
        Stroke(cat);

        Assert.True(sim.RunUntil(() => cat.State == CatState.Happy, 3));
    }

    [Fact]
    public void Too_much_petting_ends_with_the_cat_leaving()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);
        sim.Pointer.Position = (500, Sim.FloorY + 50);
        Stroke(cat);

        var seen = new HashSet<CatState>();
        sim.Run(14, () => { cat.Pet(6); seen.Add(cat.State); });

        Assert.Contains(CatState.Flee, seen);
    }

    [Fact]
    public void Overstimulated_cat_may_swat_the_hand_before_leaving()
    {
        var sim = new Sim();
        sim.Random.DoubleValue = 0.3;   // kaprisli değil (≥ %15) ama bıkınca pati atar (< %50)
        var cat = sim.AddCat(500);
        sim.Pointer.Position = (520, Sim.FloorY + 60);
        Stroke(cat);

        var seen = new List<CatState>();
        sim.Run(14, () => { cat.Pet(6); if (seen.Count == 0 || seen[^1] != cat.State) seen.Add(cat.State); });

        int swat = seen.IndexOf(CatState.Swat);
        Assert.True(swat >= 0, string.Join(" → ", seen));
        Assert.Contains(CatState.Flee, seen.Skip(swat));
    }

    [Fact]
    public void Sleeping_cat_wakes_up_slowly_when_petted()
    {
        var sim = new Sim();
        var cat = sim.AddCat(500);
        cat.Sleep();

        cat.Pet(60);
        Assert.Equal(CatState.Sleep, cat.State);
        cat.Pet(120);
        Assert.Equal(CatState.Sit, cat.State);
    }
}
