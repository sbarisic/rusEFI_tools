using System.Globalization;
using System.Text.Json;
using FishUI;

namespace RusEfiTools.Gui;

internal sealed class GuiConfiguration
{
    public WindowConfiguration Window { get; init; } = new();
    public InspectorConfiguration Inspector { get; init; } = new();
    public GraphConfiguration[] Graphs { get; init; } =
    [
        new() { Channels = [DefaultChannel("MAP", 200), DefaultChannel("RPM", 8000), DefaultChannel("TPS", 200)] },
        new() { Channels = [DefaultChannel("MAP", 200), DefaultChannel("TPS", 200)] }
    ];

    private static ChannelConfiguration DefaultChannel(string name, float maximum) =>
        new() { Name = name, Min = 0, Max = maximum };

    public void Validate()
    {
        if (Window == null || Window.Width < 640 || Window.Height < 480)
        {
            throw new JsonException("Gui.Window must specify a width of at least 640 and height of at least 480.");
        }
        if (Inspector == null || Inspector.Width < 200 || !float.IsFinite(Inspector.Width))
        {
            throw new JsonException("Gui.Inspector.Width must be at least 200.");
        }
        if (Graphs == null || Graphs.Length is < 1 or > 4)
        {
            throw new JsonException("Gui.Graphs must contain between one and four graph views.");
        }
        foreach (var graph in Graphs)
        {
            if (graph?.Channels == null)
            {
                throw new JsonException("Each graph must have a Channels array.");
            }
            foreach (var channel in graph.Channels)
            {
                if (channel == null || string.IsNullOrWhiteSpace(channel.Name)
                    || channel.Min is float min && !float.IsFinite(min)
                    || channel.Max is float max && !float.IsFinite(max)
                    || channel.Min.HasValue && channel.Max.HasValue && channel.Min >= channel.Max)
                {
                    throw new JsonException("Channels need a Name and finite scales with Min less than Max.");
                }
                _ = channel.ParseColor();
            }
        }
    }
}

internal sealed class WindowConfiguration
{
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
}

internal sealed class InspectorConfiguration
{
    public bool Visible { get; init; } = true;
    public float Width { get; init; } = 440;
}

internal sealed class GraphConfiguration
{
    public ChannelConfiguration[] Channels { get; init; } = [];
}

internal sealed class ChannelConfiguration
{
    public string Name { get; init; } = "";
    public string? Color { get; init; }
    public float? Min { get; init; }
    public float? Max { get; init; }
    public bool ShowGramsPerSecond { get; init; }

    public FishColor? ParseColor()
    {
        if (Color == null)
        {
            return null;
        }
        if (Color.Length != 7 || Color[0] != '#'
            || !uint.TryParse(Color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            throw new JsonException($"Invalid color '{Color}' for '{Name}'; use #RRGGBB.");
        }
        return new FishColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    public TelemetryChannel Apply(TelemetryChannel source)
    {
        float minimum = Min ?? source.Series.Points.Min(point => point.Y);
        float maximum = Max ?? source.Series.Points.Max(point => point.Y);
        if (maximum <= minimum)
        {
            float padding = Math.Max(1, Math.Abs(minimum) * 0.05f);
            if (!Min.HasValue)
            {
                minimum = maximum - padding;
            }
            else
            {
                maximum = minimum + padding;
            }
        }
        return source with
        {
            AxisMinimum = minimum,
            AxisMaximum = maximum,
            ColorOverride = ParseColor(),
            ShowGramsPerSecond = ShowGramsPerSecond
        };
    }
}
