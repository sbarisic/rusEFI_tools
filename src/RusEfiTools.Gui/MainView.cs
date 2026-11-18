using System.Numerics;
using FishUI;
using FishUI.Controls;

namespace RusEfiTools.Gui;

internal sealed class MainView
{
    private const int MaximumGraphs = 4;
    private readonly FishUI.FishUI ui;
    private readonly GuiConfiguration configuration;
    private readonly List<GraphConfiguration> graphSettings = [];

    public string[] RequestedChannels => graphSettings.SelectMany(graph => graph.Channels)
        .Select(channel => channel.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private TelemetryChannel[] channels = SampleTelemetry.Create();
    private GraphTimeline timeline;
    private readonly List<TelemetryGraph> rows = [];
    private readonly Button addButton;
    private readonly Button removeButton;
    private readonly Label status;
    private readonly CursorDataInspector sensors;
    private int width = 1920;
    private int height = 1080;

    public MainView(FishUI.FishUI ui, GuiConfiguration? configuration = null)
    {
        this.ui = ui;
        this.configuration = configuration ?? new GuiConfiguration();
        timeline = new GraphTimeline(channels.SelectMany(channel => channel.Series.Points).Max(point => point.X));
        addButton = CreateButton("Add graph", 24, () => AddGraph(this.configuration.Graphs[0]));
        removeButton = CreateButton("Remove last graph", 160, RemoveGraph);
        status = new Label("Drop an .mlg log or .zip here to load configured channels")
        {
            Position = Absolute(340, 18),
            Size = new Vector2(1400, 24),
            Alignment = Align.Left
        };
        status.SetColorOverride("Text", new FishColor(224, 230, 237));
        ui.AddControl(status);
        sensors = new CursorDataInspector(timeline, channels);
        if (this.configuration.Inspector.Visible)
        {
            ui.AddControl(sensors);
        }
        foreach (var graph in this.configuration.Graphs)
        {
            AddGraph(graph);
        }
    }

    public void SetStatus(string message)
    {
        status.Text = message;
    }

    public void Load(LoadedLog log, string name)
    {
        var layouts = graphSettings.ToArray();
        foreach (var row in rows)
        {
            ui.RemoveControl(row);
        }
        rows.Clear();
        graphSettings.Clear();
        channels = log.Channels;
        timeline = new GraphTimeline(log.Sensors.Duration);
        timeline.Cursor = 0;
        sensors.Bind(timeline, channels, log.Sensors);
        foreach (var layout in layouts)
        {
            AddGraph(layout);
        }
        string[] missing = RequestedChannels.Where(name => !channels.Any(channel =>
            channel.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).ToArray();
        string warning = missing.Length == 0 ? "" : $" - Missing channels: {string.Join(", ", missing)}";
        SetStatus($"{name} - {log.Samples:N0} samples, {timeline.Duration:0.0} s - {log.Fields:N0} sensor fields{warning}");
    }

    public void Resize(int width, int height)
    {
        this.width = width;
        this.height = height;
        status.Size = new Vector2(Math.Max(1, width - 364), 24);
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

    private void AddGraph(GraphConfiguration settings)
    {
        if (rows.Count >= MaximumGraphs)
        {
            return;
        }

        var selected = settings.Channels.Select(setting =>
        {
            var source = channels.FirstOrDefault(channel => channel.Name.Equals(setting.Name, StringComparison.OrdinalIgnoreCase));
            return source == null ? null : setting.Apply(source);
        }).Where(channel => channel != null).Cast<TelemetryChannel>().ToArray();
        var preview = channels.FirstOrDefault(channel => channel.RightAxis) ?? channels.FirstOrDefault();        var graph = new TelemetryGraph(selected, timeline, preview)
        {
            ID = $"graph{rows.Count + 1}"
        };
        ui.AddControl(graph);
        rows.Add(graph);
        graphSettings.Add(settings);
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
        graphSettings.RemoveAt(graphSettings.Count - 1);
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
        float panelWidth = configuration.Inspector.Visible ? Math.Min(configuration.Inspector.Width, width * 0.35f) : 0;
        sensors.Position = Absolute(width - panelWidth - 24, 56);
        sensors.Size = new Vector2(panelWidth, Math.Max(1, height - 80));
        float rowHeight = Math.Max(120, (height - 80 - 112) / (float)rows.Count);
        float top = 56;
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            bool last = index == rows.Count - 1;
            row.ShowOverview = last;
            row.Position = Absolute(24, top);
            row.Size = new Vector2(Math.Max(1, width - panelWidth - (configuration.Inspector.Visible ? 60 : 48)), rowHeight + (last ? 112 : 0));
            top += rowHeight;
        }
    }

    private static FishUIPosition Absolute(float x, float y)
    {
        return new FishUIPosition(PositionMode.Absolute, new Vector2(x, y));
    }

}
