using System.Numerics;
using FishUI;
using FishUI.Controls;

namespace RusEfiTools.Gui;

internal sealed class MainView
{
    private const int MaximumGraphs = 4;
    private readonly FishUI.FishUI ui;
    private readonly TelemetryChannel[] channels = SampleTelemetry.Create();
    private readonly GraphTimeline timeline;
    private readonly List<TelemetryGraph> rows = [];
    private readonly Button addButton;
    private readonly Button removeButton;
    private int width = 1920;
    private int height = 1080;

    public MainView(FishUI.FishUI ui)
    {
        this.ui = ui;
        timeline = new GraphTimeline(channels.SelectMany(channel => channel.Series.Points).Max(point => point.X));
        addButton = CreateButton("Add graph", 24, AddGraph);
        removeButton = CreateButton("Remove last graph", 160, RemoveGraph);
        AddGraph();
        AddGraph();
        rows[1].SetChannelVisible(channels[1], false);
    }

    public void Resize(int width, int height)
    {
        this.width = width;
        this.height = height;
        Layout();
    }

    private Button CreateButton(string text, float x, Action action)
    {
        var button = new Button
        {
            Text = text,
            Position = Absolute(x, 16),
            Size = new Vector2(text.Length > 12 ? 160 : 124, 28)
        };
        button.OnButtonPressed += (_, mouseButton, _) =>
        {
            if (mouseButton == FishMouseButton.Left)
            {
                action();
            }
        };
        ui.AddControl(button);
        return button;
    }

    private void AddGraph()
    {
        if (rows.Count >= MaximumGraphs)
        {
            return;
        }

        var graph = new TelemetryGraph(channels, timeline)
        {
            ID = $"graph{rows.Count + 1}"
        };
        ui.AddControl(graph);
        rows.Add(graph);
        Layout();
    }

    private void RemoveGraph()
    {
        if (rows.Count <= 1)
        {
            return;
        }
        ui.RemoveControl(rows[^1]);
        rows.RemoveAt(rows.Count - 1);
        Layout();
    }

    private void Layout()
    {
        addButton.Disabled = rows.Count >= MaximumGraphs;
        removeButton.Disabled = rows.Count <= 1;
        if (rows.Count == 0)
        {
            return;
        }

        // Reserve the overview once; all plot areas retain the same height.
        float rowHeight = Math.Max(120, (height - 80 - 112) / (float)rows.Count);
        float top = 56;
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            bool last = index == rows.Count - 1;
            row.ShowOverview = last;
            row.Position = Absolute(24, top);
            row.Size = new Vector2(Math.Max(1, width - 48), rowHeight + (last ? 112 : 0));
            top += rowHeight;
        }
    }

    private static FishUIPosition Absolute(float x, float y)
    {
        return new FishUIPosition(PositionMode.Absolute, new Vector2(x, y));
    }

}
