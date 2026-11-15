using System.Numerics;
using FishUI;
using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class CursorDataInspectorTests
{
    private static CursorDataInspector CreateInspector()
    {
        var channel = SampleTelemetry.Create()[0];
        return new CursorDataInspector(new GraphTimeline(40), Enumerable.Repeat(channel, 300).ToArray())
        {
            Position = new FishUIPosition(PositionMode.Absolute, Vector2.Zero),
            Size = new Vector2(440, 500)
        };
    }

    [Fact]
    public void ThumbDragPreservesGrabPositionAndClampsAtBothEnds()
    {
        var inspector = CreateInspector();
        inspector.HandleMouseWheel(null!, default, -30);
        Assert.Equal(90, inspector.ScrollOffset);
        // 350px track, 17.5px thumb, 285 scrollable rows: thumb begins at y=233.
        inspector.HandleMousePress(null!, default, FishMouseButton.Left, new Vector2(432, 241));
        Assert.Equal(90, inspector.ScrollOffset);
        inspector.HandleMouseMove(null!, default, new Vector2(432, 1000));
        Assert.Equal(285, inspector.ScrollOffset);
        inspector.HandleMouseMove(null!, default, new Vector2(432, -100));
        Assert.Equal(0, inspector.ScrollOffset);
    }

    [Fact]
    public void RowDragScrollsAndReleaseStopsFurtherMovement()
    {
        var inspector = CreateInspector();
        inspector.HandleMousePress(null!, default, FishMouseButton.Left, new Vector2(200, 300));
        inspector.HandleMouseMove(null!, default, new Vector2(200, 234));
        Assert.Equal(3, inspector.ScrollOffset);
        inspector.HandleMouseRelease(null!, default, FishMouseButton.Left, new Vector2(200, 234));
        inspector.HandleMouseMove(null!, default, new Vector2(200, 100));
        Assert.Equal(3, inspector.ScrollOffset);
    }

    [Fact]
    public void HeaderAndRightClicksDoNotStartScrolling()
    {
        var inspector = CreateInspector();
        inspector.HandleMousePress(null!, default, FishMouseButton.Left, new Vector2(200, 60));
        inspector.HandleMouseMove(null!, default, new Vector2(200, -100));
        inspector.HandleMousePress(null!, default, FishMouseButton.Right, new Vector2(200, 300));
        inspector.HandleMouseMove(null!, default, new Vector2(200, 100));
        Assert.Equal(0, inspector.ScrollOffset);
    }
}
