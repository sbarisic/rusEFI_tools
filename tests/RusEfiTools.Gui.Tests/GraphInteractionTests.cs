using System.Numerics;
using FishUI;
using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class GraphInteractionTests
{
    // Headless control input checks: base mouse handlers only log events.
    // A 1280px-wide control provides a 1000px-wide plot starting at x=78.
    private static TelemetryGraph CreateGraph(GraphTimeline timeline, bool overview = false)
    {
        return new TelemetryGraph(SampleTelemetry.Create(), timeline)
        {
            Position = new FishUIPosition(PositionMode.Absolute, Vector2.Zero),
            Size = new Vector2(1280, 500),
            ShowOverview = overview
        };
    }

    private static void Press(TelemetryGraph graph, float x, float y)
    {
        graph.HandleMousePress(null!, default, FishMouseButton.Left, new Vector2(x, y));
    }

    private static void Move(TelemetryGraph graph, float x, float y)
    {
        graph.HandleMouseMove(null!, default, new Vector2(x, y));
    }

    [Fact]
    public void EitherGraphCanMoveTheSharedCursor()
    {
        var timeline = new GraphTimeline(40) { Start = 10, End = 30 };
        var first = CreateGraph(timeline);
        var second = CreateGraph(timeline);

        Press(first, 328, 100);
        Assert.Equal(15, timeline.Cursor);
        Press(second, 828, 100);
        Assert.Equal(25, timeline.Cursor);
    }

    [Fact]
    public void OverviewResizeChangesOtherGraphsTimeMapping()
    {
        var timeline = new GraphTimeline(40);
        var first = CreateGraph(timeline);
        var last = CreateGraph(timeline, overview: true);

        Press(last, 78, 440);
        Move(last, 328, 440);
        Assert.Equal(10, timeline.Start);
        Assert.Equal(40, timeline.End);

        Press(first, 578, 100);
        Assert.Equal(25, timeline.Cursor);
    }

    [Fact]
    public void MiddleDragPreservesWidthAndStopsAtDataBoundary()
    {
        var timeline = new GraphTimeline(40) { Start = 10, End = 20 };
        var last = CreateGraph(timeline, overview: true);

        Press(last, 453, 440);
        Move(last, 578, 440);
        Assert.Equal(15, timeline.Start);
        Assert.Equal(25, timeline.End);

        Move(last, 1578, 440);
        Assert.Equal(30, timeline.Start);
        Assert.Equal(40, timeline.End);
    }

    [Fact]
    public void ChannelSelectionIsIndependentAndDoesNotResetTimeline()
    {
        var channels = SampleTelemetry.Create();
        var timeline = new GraphTimeline(40) { Start = 10, End = 25, Cursor = 18 };
        var first = new TelemetryGraph(channels, timeline);
        var second = new TelemetryGraph(channels, timeline);

        first.SetChannelVisible(channels[0], false);
        Assert.False(first.IsChannelVisible(channels[0]));
        Assert.True(second.IsChannelVisible(channels[0]));
        Assert.Equal(10, timeline.Start);
        Assert.Equal(25, timeline.End);
        Assert.Equal(18, timeline.Cursor);
    }

    [Fact]
    public void GraphWithoutOverviewDoesNotResizeRangeAtFormerOverviewLocation()
    {
        var timeline = new GraphTimeline(40) { Start = 10, End = 30 };
        var first = CreateGraph(timeline);
        Press(first, 328, 440);
        Move(first, 578, 440);
        Assert.Equal(10, timeline.Start);
        Assert.Equal(30, timeline.End);
    }
}
