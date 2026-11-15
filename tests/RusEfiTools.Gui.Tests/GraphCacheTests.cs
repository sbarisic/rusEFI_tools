using System.Numerics;
using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class GraphCacheTests
{
    [Fact]
    public void GeometryIsReusedUntilViewportChanges()
    {
        var channel = SampleTelemetry.Create()[0];
        var cache = new GraphPolylineCache();
        var size = new Vector2(1000, 200);
        var first = cache.Get(channel, false, Vector2.Zero, size, 0, 40);
        Assert.Same(first, cache.Get(channel, false, Vector2.Zero, size, 0, 40));
        var zoomed = cache.Get(channel, false, Vector2.Zero, size, 10, 20);
        Assert.NotSame(first, zoomed);
        Assert.Equal(0, zoomed[0].X);
        Assert.Equal(1000, zoomed[^1].X);
        var moved = cache.Get(channel, false, new Vector2(10, 20), size, 10, 20);
        Assert.Equal(zoomed[0] + new Vector2(10, 20), moved[0]);
    }

    [Fact]
    public void ViewsShareReductionAndOverviewSurvivesZooming()
    {
        var points = SampleTelemetry.Create()[0].Series.Points;
        var overview = GraphSampleCache.Get(points, 0, 40, 1000, true);
        var view = GraphSampleCache.Get(points, 5, 10, 1000, false);
        Assert.Same(view, GraphSampleCache.Get(points, 5, 10, 1000, false));
        Assert.Same(overview, GraphSampleCache.Get(points, 0, 40, 1000, true));
        Assert.NotSame(view, GraphSampleCache.Get(points, 6, 11, 1000, false));
    }
}
