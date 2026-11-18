using System.Numerics;
using System.Text.Json;
using FishUI;
using FishUI.Controls;
using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class GuiConfigurationTests
{
    [Fact]
    public void MassFlowDisplayConversionPreservesRawDataAndGraphPositions()
    {
        var gui = AppConfiguration.Parse("""
            { "Gui": { "Graphs": [{ "Channels": [
                { "Name": "Air: Flow estimate", "Min": 0, "Max": 360, "ShowGramsPerSecond": true }
            ] }] } }
            """).Gui;
        var series = new LineChartSeries("Air: Flow estimate (kg/h)", new FishColor(255, 200, 87));
        series.AddPoint(0, 0);
        series.AddPoint(1, 360);
        var source = new TelemetryChannel(series, 360, false);
        var converted = gui.Graphs[0].Channels[0].Apply(source);
        Assert.Equal("Air: Flow estimate (g/s)", converted.DisplayName);
        Assert.Equal(100, converted.DisplayValue(converted.AxisMaximum), 4);
        Assert.Equal(50, converted.DisplayValue(GraphSamples.ValueAt(series.Points, 0.5f)!.Value), 4);
        Assert.Same(source.Series.Points, converted.Series.Points);
        Assert.Equal(source.Normalize(180), converted.Normalize(180));
        Assert.Equal(360, source.Series.Points[1].Y);
        Assert.Equal("Air: Flow estimate (kg/h)", source.DisplayName);
        Assert.Equal(360, source.DisplayValue(360));
    }

    [Theory]
    [InlineData("g/s")]
    [InlineData("rpm")]
    public void MassFlowOptionDoesNotConvertOtherSourceUnits(string unit)
    {
        var series = new LineChartSeries($"Sensor ({unit})", new FishColor(255, 0, 0));
        var channel = new TelemetryChannel(series, 100, false) { ShowGramsPerSecond = true };
        Assert.Equal(series.Name, channel.DisplayName);
        Assert.Equal(36, channel.DisplayValue(36));
    }

    [Fact]
    public void ParsesLayoutAndAppliesIndependentChannelStyles()
    {
        var gui = AppConfiguration.Parse("""
            { "gui": {
                "window": { "width": 1280, "height": 720 },
                "inspector": { "visible": false, "width": 300 },
                "graphs": [
                    { "channels": [{ "name": "RPM", "color": "#12AB34", "min": -100, "max": 100 }] },
                    { "channels": [{ "name": "RPM", "color": "#FF0000", "min": 0, "max": 8000 }] }
                ]
            } }
            """).Gui;
        Assert.Equal(1280, gui.Window.Width);
        Assert.Equal(720, gui.Window.Height);
        Assert.False(gui.Inspector.Visible);
        Assert.Equal(300, gui.Inspector.Width);
        var source = Source();
        var first = gui.Graphs[0].Channels[0].Apply(source);
        var second = gui.Graphs[1].Channels[0].Apply(source);
        Assert.Same(first.Series.Points, second.Series.Points);
        Assert.Equal(new FishColor(0x12, 0xAB, 0x34), first.Color);
        Assert.NotEqual(first.Color, second.Color);
        Assert.Equal(-100, first.AxisMinimum);
        Assert.Equal(8000, second.AxisMaximum);
        Assert.Equal(0.5f, first.Normalize(0));
        Assert.Equal(0, first.Normalize(-200));
        Assert.Equal(1, first.Normalize(200));

        var points = new GraphPolylineCache().Get(first, false, Vector2.Zero, new Vector2(100, 100), 0, 1);
        Assert.Equal(new Vector2(0, 75), points[0]);
        Assert.Equal(new Vector2(100, 25), points[^1]);
    }

    [Fact]
    public void AutomaticScalesUseFullDataAndHandleConstantSignals()
    {
        var source = Source();
        var automatic = new ChannelConfiguration { Name = "RPM" }.Apply(source);
        Assert.Equal(-50, automatic.AxisMinimum);
        Assert.Equal(50, automatic.AxisMaximum);
        var upperBound = new ChannelConfiguration { Name = "RPM", Max = -100 }.Apply(source);
        Assert.Equal(-100, upperBound.AxisMaximum);
        Assert.True(upperBound.AxisMinimum < upperBound.AxisMaximum);
        source.Series.Points[0] = new Vector2(0, 0);
        source.Series.Points[1] = new Vector2(1, 0);
        var constant = new ChannelConfiguration { Name = "RPM" }.Apply(source);
        Assert.True(constant.AxisMaximum > constant.AxisMinimum);
        Assert.True(float.IsFinite(constant.Normalize(0)));
    }

    [Theory]
    [InlineData("{\"Gui\":null}")]
    [InlineData("{\"Gui\":{\"Window\":null}}")]
    [InlineData("{\"Gui\":{\"Window\":{\"Width\":0}}}")]
    [InlineData("{\"Gui\":{\"Inspector\":{\"Width\":-1}}}")]
    [InlineData("{\"Gui\":{\"Graphs\":[]}}")]
    [InlineData("{\"Gui\":{\"Graphs\":[null]}}")]
    [InlineData("{\"Gui\":{\"Graphs\":[{\"Channels\":null}]}}")]
    [InlineData("{\"Gui\":{\"Graphs\":[{\"Channels\":[{\"Name\":\"\"}]}]}}")]
    [InlineData("{\"Gui\":{\"Graphs\":[{\"Channels\":[{\"Name\":\"RPM\",\"Color\":\"red\"}]}]}}")]
    [InlineData("{\"Gui\":{\"Graphs\":[{\"Channels\":[{\"Name\":\"RPM\",\"Min\":10,\"Max\":10}]}]}}")]
    public void RejectsInvalidGuiSettings(string json)
    {
        Assert.Throws<JsonException>(() => AppConfiguration.Parse(json));
    }

    [Fact]
    public void DefaultsMatchTheTwoGraphLayout()
    {
        var gui = AppConfiguration.Parse("{}").Gui;
        Assert.Equal(["MAP", "RPM", "TPS"], gui.Graphs[0].Channels.Select(channel => channel.Name));
        Assert.Equal(["MAP", "TPS"], gui.Graphs[1].Channels.Select(channel => channel.Name));
        Assert.Equal(8000, gui.Graphs[0].Channels[1].Max);
    }

    private static TelemetryChannel Source()
    {
        var series = new LineChartSeries("RPM (rpm)", new FishColor(255, 0, 0));
        series.AddPoint(0, -50);
        series.AddPoint(1, 50);
        return new TelemetryChannel(series, 8000, true);
    }
}
