using VeriCat.Core.Geometry;
using VeriCat.Core.Tests.Support;
using VeriCat.Core.World;

namespace VeriCat.Core.Tests.World;

public class PlatformBuilderTests
{
    static readonly ScreenSnapshot[] Screens = { Sim.Screen };

    [Fact]
    public void Every_screen_gets_a_floor_on_top_of_the_taskbar()
    {
        var p = PlatformBuilder.Build(Screens, Array.Empty<WindowSnapshot>(), 1, true);

        var floor = Assert.Single(p);
        Assert.True(floor.IsFloor);
        Assert.Equal(Sim.FloorY, floor.Y);
    }

    [Fact]
    public void Window_top_edge_is_a_platform_inset_from_the_corners()
    {
        var w = new WindowSnapshot(7, new RectU(100, -800, 900, -300));

        var top = PlatformBuilder.Build(Screens, new[] { w }, 1, false).Single(p => p.Owner == 7);

        Assert.Equal(-300, top.Y);
        Assert.Equal(110, top.MinX);
        Assert.Equal(890, top.MaxX);
        Assert.False(top.IsInner);
    }

    [Fact]
    public void Front_window_hides_the_part_of_a_back_window_edge_it_covers()
    {
        var front = new WindowSnapshot(1, new RectU(400, -700, 600, -200));
        var back = new WindowSnapshot(2, new RectU(100, -800, 900, -300));

        var edges = PlatformBuilder.Build(Screens, new[] { front, back }, 1, false).Where(p => p.Owner == 2).ToList();

        Assert.Equal(2, edges.Count);
        Assert.Contains(edges, e => e.MinX == 110 && e.MaxX == 400);
        Assert.Contains(edges, e => e.MinX == 600 && e.MaxX == 890);
    }

    [Fact]
    public void Maximized_window_has_no_top_edge()
    {
        var w = new WindowSnapshot(3, new RectU(0, -1040, 1920, 0));

        Assert.DoesNotContain(PlatformBuilder.Build(Screens, new[] { w }, 1, false), p => p.Owner == 3);
    }

    [Fact]
    public void Inner_sections_become_platforms_only_when_enabled()
    {
        var w = new WindowSnapshot(5, new RectU(100, -900, 900, -200), new[]
        {
            new ChildSnapshot(51, new RectU(120, -800, 880, -400)),
        });

        var off = PlatformBuilder.Build(Screens, new[] { w }, 1, includeInner: false);
        var on = PlatformBuilder.Build(Screens, new[] { w }, 1, includeInner: true);

        Assert.DoesNotContain(off, p => p.IsInner);
        var shelf = Assert.Single(on, p => p.IsInner);
        Assert.Equal(-400, shelf.Y);
        Assert.Equal(5, shelf.Owner);
        Assert.Equal(51, shelf.Part);
        Assert.Equal(128, shelf.MinX);
        Assert.Equal(872, shelf.MaxX);
    }

    [Fact]
    public void Inner_sections_hugging_the_title_bar_or_bottom_are_ignored()
    {
        var w = new WindowSnapshot(5, new RectU(100, -900, 900, -200), new[]
        {
            new ChildSnapshot(1, new RectU(120, -880, 880, -220)),   // başlık çubuğunun hemen altı
            new ChildSnapshot(2, new RectU(120, -899, 880, -880)),   // pencerenin dibi
        });

        Assert.DoesNotContain(PlatformBuilder.Build(Screens, new[] { w }, 1, true), p => p.IsInner);
    }

    [Fact]
    public void Nested_sections_at_the_same_height_are_not_duplicated()
    {
        var w = new WindowSnapshot(5, new RectU(100, -900, 900, -200), new[]
        {
            new ChildSnapshot(1, new RectU(120, -800, 880, -400)),
            new ChildSnapshot(2, new RectU(125, -790, 870, -402)),
        });

        Assert.Single(PlatformBuilder.Build(Screens, new[] { w }, 1, true), p => p.IsInner);
    }

    [Fact]
    public void Inner_section_is_clipped_by_windows_in_front()
    {
        var front = new WindowSnapshot(1, new RectU(0, -700, 500, -300));
        var back = new WindowSnapshot(2, new RectU(100, -900, 900, -200), new[]
        {
            new ChildSnapshot(21, new RectU(120, -800, 880, -400)),
        });

        var shelf = Assert.Single(PlatformBuilder.Build(Screens, new[] { front, back }, 1, true), p => p.IsInner);
        Assert.Equal(500, shelf.MinX);
    }

    [Fact]
    public void Segments_subtract_splits_and_trims()
    {
        var r = Segments.Subtract(new[] { (0.0, 100.0) }, 40, 60);
        Assert.Equal(new[] { (0.0, 40.0), (60.0, 100.0) }, r);
        Assert.Empty(Segments.Subtract(new[] { (10.0, 20.0) }, 0, 30));
        Assert.Equal(new[] { (10.0, 20.0) }, Segments.Subtract(new[] { (10.0, 20.0) }, 30, 40));
    }
}
