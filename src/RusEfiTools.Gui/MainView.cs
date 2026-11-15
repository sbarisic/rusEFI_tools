using System.Numerics;
using FishUI;

namespace RusEfiTools.Gui;

internal sealed class MainView
{
    private readonly TelemetryGraph graph;

    public MainView(FishUI.FishUI ui)
    {
        graph = new TelemetryGraph(SampleTelemetry.Create())
        {
            ID = "telemetryGraph",
            Position = new FishUIPosition(PositionMode.Absolute, new Vector2(24, 24))
        };
        ui.AddControl(graph);
    }

    public void Resize(int width, int height)
    {
        graph.Size = new Vector2(Math.Max(1, width - 48), Math.Max(1, height - 48));
    }
}
