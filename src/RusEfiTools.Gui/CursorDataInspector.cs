using System.Globalization;
using System.Numerics;
using FishUI;
using FishUI.Controls;

namespace RusEfiTools.Gui;

internal sealed class CursorDataInspector : Control
{
    private const int RowHeight = 22;
    private readonly Textbox search;
    private GraphTimeline timeline;
    private TelemetryChannel[] sampleChannels;
    private SensorLogData? data;
    private int[] visibleFields = [];
    private int scroll;
    private float inspectedTime = float.NaN;
    private double[] inspectedValues = [];
    private DragMode dragging;
    private float dragStartY;
    private int dragStartScroll;
    private float thumbGrabOffset;

    private enum DragMode { None, Thumb, Rows }
    internal int ScrollOffset => scroll;

    public CursorDataInspector(GraphTimeline timeline, TelemetryChannel[] sampleChannels)
    {
        this.timeline = timeline;
        this.sampleChannels = sampleChannels;
        search = new Textbox
        {
            Position = new FishUIPosition(PositionMode.Relative, new Vector2(12, 52)),
            Size = new Vector2(380, 26)
        };
        search.OnTextChanged += (_, _) => Filter();
        AddChild(search);
        Filter();
    }

    public void Bind(GraphTimeline timeline, TelemetryChannel[] channels, SensorLogData sensors)
    {
        this.timeline = timeline;
        sampleChannels = channels;
        data = sensors;
        inspectedTime = float.NaN;
        Filter();
    }

    private void Filter()
    {
        int count = data?.Fields.Count ?? sampleChannels.Length;
        string query = (search.Text ?? "").Trim();
        visibleFields = Enumerable.Range(0, count)
            .Select(index => (Index: index, Score: ChannelSearch.Score(Name(index), query)
                ?? (Unit(index).Contains(query, StringComparison.OrdinalIgnoreCase) ? (int?)10000 : null)))
            .Where(match => match.Score.HasValue)
            .OrderBy(match => match.Score)
            .ThenBy(match => Name(match.Index), StringComparer.OrdinalIgnoreCase)
            .Select(match => match.Index).ToArray();
        scroll = 0;
        dragging = DragMode.None;
    }

    private string Name(int field) => data?.Fields[field].Name ?? sampleChannels[field].Series.Name;
    private string Unit(int field) => data?.Fields[field].Units ?? "";
    private int VisibleRows => Math.Max(1, (int)(ScaledSize.Y - 150) / RowHeight);
    private int MaximumScroll => Math.Max(0, visibleFields.Length - VisibleRows);
    private float TrackLength => Math.Max(1, ScaledSize.Y - 150);
    private float ThumbLength => Math.Min(TrackLength,
        Math.Max(12, TrackLength * Math.Min(1, VisibleRows / (float)Math.Max(1, visibleFields.Length))));
    private float ThumbTop => MaximumScroll == 0 ? 0 : (TrackLength - ThumbLength) * scroll / MaximumScroll;

    public override void HandleMouseWheel(FishUI.FishUI ui, FishInputState state, float delta)
    {
        scroll = Math.Clamp(scroll - (int)(delta * 3), 0, MaximumScroll);
    }

    public override void HandleMousePress(FishUI.FishUI ui, FishInputState state, FishMouseButton button, Vector2 position)
    {
        var local = position - GetAbsolutePosition();
        if (button != FishMouseButton.Left || local.Y < 128 || local.Y > 128 + TrackLength
            || local.X < 0 || local.X > ScaledSize.X || MaximumScroll == 0)
        {
            return;
        }
        if (local.X >= ScaledSize.X - 18)
        {
            dragging = DragMode.Thumb;
            float y = local.Y - 128;
            thumbGrabOffset = y >= ThumbTop && y <= ThumbTop + ThumbLength ? y - ThumbTop : ThumbLength / 2;
        }
        else
        {
            dragging = DragMode.Rows;
            dragStartY = local.Y;
            dragStartScroll = scroll;
        }
        Drag(position);
    }

    public override void HandleMouseMove(FishUI.FishUI ui, FishInputState state, Vector2 position)
    {
        Drag(position);
    }

    public override void HandleMouseRelease(FishUI.FishUI ui, FishInputState state, FishMouseButton button, Vector2 position)
    {
        if (button == FishMouseButton.Left)
        {
            dragging = DragMode.None;
        }
    }

    protected override void OnFishUIPostInputUpdate(FishUI.FishUI ui, float deltaTime, float time)
    {
        if (dragging == DragMode.None)
        {
            return;
        }
        if (!ui.Input.IsMouseDown(FishMouseButton.Left))
        {
            dragging = DragMode.None;
            return;
        }
        Drag(ui.Input.GetMousePosition());
    }

    private void Drag(Vector2 position)
    {
        float y = position.Y - GetAbsolutePosition().Y;
        if (dragging == DragMode.Thumb)
        {
            float fraction = (y - 128 - thumbGrabOffset) / Math.Max(1, TrackLength - ThumbLength);
            scroll = (int)MathF.Round(Math.Clamp(fraction, 0, 1) * MaximumScroll);
        }
        else if (dragging == DragMode.Rows)
        {
            scroll = Math.Clamp(dragStartScroll + (int)MathF.Round((dragStartY - y) / RowHeight), 0, MaximumScroll);
        }
    }

    public override void DrawControl(FishUI.FishUI ui, float deltaTime, float time)
    {
        var position = GetAbsolutePosition();
        var size = ScaledSize;
        var gfx = ui.Graphics;
        var font = ui.Settings.FontDefault;
        if (font == null)
        {
            return;
        }
        search.Size = new Vector2(Math.Max(1, Size.X - 24), 26);
        if (data != null && inspectedTime != timeline.Cursor)
        {
            inspectedValues = data.ValuesAt(timeline.Cursor);
            inspectedTime = timeline.Cursor;
        }
        gfx.DrawRectangle(position, size, new FishColor(22, 29, 38));
        Write("CURSOR DATA INSPECTOR", 12, 12);
        Write($"Selected time: {timeline.Cursor:0.000} s · {visibleFields.Length:N0} fields", 12, 32);
        Write("Filter by name or unit", 12, 84);
        Write("Sensor", 12, 108);
        Write("Value", size.X - 150, 108);
        Write("Unit", size.X - 62, 108);
        scroll = Math.Clamp(scroll, 0, MaximumScroll);
        int end = Math.Min(visibleFields.Length, scroll + VisibleRows);
        gfx.BeginScissor(position + new Vector2(0, 128), new Vector2(size.X - 18, Math.Max(1, size.Y - 150)));
        try
        {
            for (int index = scroll; index < end; index++)
            {
                int field = visibleFields[index];
                float y = 128 + (index - scroll) * RowHeight;
                double value = data != null ? inspectedValues[field]
                    : GraphSamples.ValueAt(sampleChannels[field].Series.Points, timeline.Cursor) ?? double.NaN;
                string formatted = double.IsFinite(value) ? value.ToString("0.###", CultureInfo.InvariantCulture) : "N/A";
                string name = Name(field);
                while (name.Length > 0 && gfx.MeasureText(font, name).X > size.X - 180)
                {
                    name = name[..^1];
                }
                Write(name, 12, y);
                Write(formatted, size.X - 150, y);
                Write(Unit(field), size.X - 62, y);
            }
        }
        finally
        {
            gfx.EndScissor();
        }
        float track = TrackLength;
        gfx.DrawRectangle(position + new Vector2(size.X - 10, 128), new Vector2(4, track), new FishColor(48, 60, 72));
        float thumb = ThumbLength;
        float top = ThumbTop;
        gfx.DrawRectangle(position + new Vector2(size.X - 10, 128 + top), new Vector2(4, thumb), new FishColor(130, 180, 240));
        Write(visibleFields.Length == 0 ? "No matching sensors" : $"{scroll + 1}–{end} of {visibleFields.Length} · Drag or scroll to browse", 12, size.Y - 20);

        void Write(string text, float x, float y)
        {
            gfx.DrawTextColor(font, text, position + new Vector2(x, y), new FishColor(224, 230, 237));
        }
    }
}
