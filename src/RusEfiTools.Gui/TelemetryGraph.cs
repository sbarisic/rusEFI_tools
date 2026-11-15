using System.Globalization;
using System.Numerics;
using FishUI;
using FishUI.Controls;

namespace RusEfiTools.Gui;

internal sealed class TelemetryGraph : Control
{
    private static readonly FishColor Background = new(14, 19, 25);
    private static readonly FishColor Grid = new(48, 60, 72);
    private static readonly FishColor Text = new(224, 230, 237);
    private readonly TelemetryChannel[] channels;
    private readonly GraphTimeline timeline;
    private readonly HashSet<TelemetryChannel> hiddenChannels = [];
    private float duration => timeline.Duration;
    private float start { get => timeline.Start; set => timeline.Start = value; }
    private float end { get => timeline.End; set => timeline.End = value; }
    private float cursor { get => timeline.Cursor; set => timeline.Cursor = value; }
    public bool ShowOverview { get; set; }
    private IEnumerable<TelemetryChannel> VisibleChannels => channels.Where(IsChannelVisible);
    private DragTarget dragging;
    private float dragAnchorTime;
    private float dragStart;
    private float dragEnd;

    private enum DragTarget { None, Cursor, Start, End, Range }

    public TelemetryGraph(TelemetryChannel[] channels, GraphTimeline timeline)
    {
        this.channels = channels;
        this.timeline = timeline;
    }

    public bool IsChannelVisible(TelemetryChannel channel)
    {
        return !hiddenChannels.Contains(channel);
    }

    public void SetChannelVisible(TelemetryChannel channel, bool visible)
    {
        if (visible)
        {
            hiddenChannels.Remove(channel);
        }
        else
        {
            hiddenChannels.Add(channel);
        }
    }

    private (Vector2 Position, Vector2 Size) Plot => (
        GetAbsolutePosition() + new Vector2(78, 28),
        new Vector2(Math.Max(1, ScaledSize.X - 280), Math.Max(1, ScaledSize.Y - (ShowOverview ? 148 : 36))));

    private (Vector2 Position, Vector2 Size) Overview => (
        GetAbsolutePosition() + new Vector2(78, Math.Max(100, ScaledSize.Y - 78)),
        new Vector2(Math.Max(1, ScaledSize.X - 280), 32));

    public override void DrawControl(FishUI.FishUI ui, float deltaTime, float time)
    {
        // The base implementation draws a debug hover/selection outline.
        var gfx = ui.Graphics;
        var (position, size) = Plot;
        gfx.DrawRectangle(GetAbsolutePosition(), ScaledSize, Background);

        int divisions = size.Y < 260 ? 4 : 10;
        for (int index = 0; index <= divisions; index++)
        {
            float fraction = (float)index / divisions;
            float y = position.Y + size.Y * (1 - fraction);
            gfx.DrawLine(new Vector2(position.X, y), new Vector2(position.X + size.X, y), 1, Grid);
        }

        for (int index = 0; index <= 8; index++)
        {
            float fraction = index / 8f;
            float x = position.X + fraction * size.X;
            gfx.DrawLine(new Vector2(x, position.Y), new Vector2(x, position.Y + size.Y), 1, Grid);
        }

        foreach (var channel in VisibleChannels)
        {
            DrawSeries(ui, channel, position, size, start, end, channel.Series.Color, 2);
        }

        gfx.DrawRectangleOutline(position, size, Grid);
        DrawChannelScales(ui, position, size);
        DrawCursor(ui, position, size);
        DrawLegend(ui, position + new Vector2(16, 16));
        if (ShowOverview)
        {
            DrawOverview(ui);
        }
    }

    private void DrawChannelScales(FishUI.FishUI ui, Vector2 position, Vector2 size)
    {
        int column = 0;
        foreach (var channel in VisibleChannels.OrderByDescending(channel => channel.RightAxis))
        {
            float x = position.X + size.X + 12 + column * 60;
            Label(ui, channel.Series.Name.Split(' ')[0], new Vector2(x, position.Y - 22), color: channel.Series.Color);
            Label(ui, channel.AxisMaximum.ToString("N0", CultureInfo.InvariantCulture),
                new Vector2(x, position.Y - 7), color: channel.Series.Color);
            Label(ui, "0", new Vector2(x, position.Y + size.Y - 7), color: channel.Series.Color);
            column++;
        }
    }

    private void DrawLegend(FishUI.FishUI ui, Vector2 position)
    {
        var visible = VisibleChannels.ToArray();
        if (visible.Length == 0)
        {
            Label(ui, "No channels configured for this view.", position);
            return;
        }

        var legendSize = new Vector2(240, 16 + visible.Length * 22);
        ui.Graphics.DrawRectangle(position, legendSize, new FishColor(22, 29, 38, 240));
        ui.Graphics.DrawRectangleOutline(position, legendSize, Grid);
        for (int index = 0; index < visible.Length; index++)
        {
            var channel = visible[index];
            var row = position + new Vector2(12, 10 + index * 22);
            ui.Graphics.DrawLine(row + new Vector2(0, 7), row + new Vector2(26, 7), 3, channel.Series.Color);
            float? value = channel.Series.GetValueAt(cursor);
            Label(ui, $"{channel.Series.Name}  {value:0.0}", row + new Vector2(36, 0));
        }
    }

    private void DrawCursor(FishUI.FishUI ui, Vector2 position, Vector2 size)
    {
        float x = position.X + (cursor - start) / (end - start) * size.X;
        ui.Graphics.DrawLine(new Vector2(x, position.Y), new Vector2(x, position.Y + size.Y), 1, Text);
        ui.Graphics.DrawLine(new Vector2(x - 5, position.Y - 8), new Vector2(x, position.Y), 2, Text);
        ui.Graphics.DrawLine(new Vector2(x + 5, position.Y - 8), new Vector2(x, position.Y), 2, Text);

        foreach (var channel in VisibleChannels)
        {
            float? value = channel.Series.GetValueAt(cursor);
            if (!value.HasValue || !float.IsFinite(value.Value))
            {
                continue;
            }

            float normalizedValue = Math.Clamp(value.Value / channel.AxisMaximum, 0, 1);
            var center = new Vector2(x, position.Y + (1 - normalizedValue) * size.Y);

            // A dark border keeps the marker distinct from the cursor and grid.
            ui.Graphics.DrawCircle(center, 6, Background);
            ui.Graphics.DrawCircle(center, 4, channel.Series.Color);
        }

    }

    private void DrawOverview(FishUI.FishUI ui)
    {
        var (position, size) = Overview;
        ui.Graphics.DrawRectangle(position, size, new FishColor(26, 34, 43));
        var preview = channels.FirstOrDefault(channel => channel.RightAxis) ?? channels.FirstOrDefault();
        if (preview != null)
        {
            DrawSeries(ui, preview, position, size, 0, duration, new FishColor(150, 165, 182), 1);
        }

        float left = position.X + start / duration * size.X;
        float right = position.X + end / duration * size.X;
        ui.Graphics.DrawRectangle(new Vector2(left, position.Y), new Vector2(right - left, size.Y),
            new FishColor(80, 140, 220, 65));
        ui.Graphics.DrawRectangleOutline(new Vector2(left, position.Y), new Vector2(right - left, size.Y),
            new FishColor(130, 180, 240));
        foreach (float x in new[] { left, right })
        {
            ui.Graphics.DrawRectangle(new Vector2(x - 4, position.Y + 7), new Vector2(8, size.Y - 14),
                new FishColor(150, 195, 250));
        }
        Label(ui, $"Time (s)    Cursor: {cursor:0.00} s    View: {start:0.00} - {end:0.00} s",
            position + new Vector2(0, -22));

        float cursorX = position.X + cursor / duration * size.X;
        ui.Graphics.DrawLine(new Vector2(cursorX, position.Y),
            new Vector2(cursorX, position.Y + size.Y), 1, Text);
        for (int index = 0; index <= 8; index++)
        {
            float fraction = index / 8f;
            float x = position.X + fraction * size.X;
            float bottom = position.Y + size.Y;
            ui.Graphics.DrawLine(new Vector2(x, bottom), new Vector2(x, bottom + 5), 1, Text);
            Label(ui, (fraction * duration).ToString("0.0", CultureInfo.InvariantCulture),
                new Vector2(x, bottom + 10), 0.5f);
        }
    }

    private static void DrawSeries(FishUI.FishUI ui, TelemetryChannel channel,
        Vector2 position, Vector2 size, float from, float to, FishColor color, float thickness)
    {
        var points = channel.Series.Points;
        for (int index = 1; index < points.Count; index++)
        {
            var a = points[index - 1];
            var b = points[index];
            if (b.X < from || a.X > to || b.X <= a.X)
            {
                continue;
            }

            float left = Math.Max(from, a.X);
            float right = Math.Min(to, b.X);
            float leftValue = a.Y + (b.Y - a.Y) * (left - a.X) / (b.X - a.X);
            float rightValue = a.Y + (b.Y - a.Y) * (right - a.X) / (b.X - a.X);
            Vector2 Project(float x, float y) => position + new Vector2(
                (x - from) / (to - from) * size.X,
                (1 - Math.Clamp(y / channel.AxisMaximum, 0, 1)) * size.Y);
            ui.Graphics.DrawLine(Project(left, leftValue), Project(right, rightValue), thickness, color);
        }
    }

    private static void Label(FishUI.FishUI ui, string text, Vector2 position, float alignment = 0, FishColor? color = null)
    {
        var font = ui.Settings.FontDefault;
        if (font == null)
        {
            return;
        }
        position.X -= ui.Graphics.MeasureText(font, text).X * alignment;
        ui.Graphics.DrawTextColor(font, text, position, color ?? Text);
    }

    public override void HandleMousePress(FishUI.FishUI ui, FishInputState state, FishMouseButton button, Vector2 position)
    {
        base.HandleMousePress(ui, state, button, position);
        if (button != FishMouseButton.Left)
        {
            return;
        }

        var (plotPosition, plotSize) = Plot;
        var (overviewPosition, overviewSize) = Overview;
        dragging = DragTarget.None;
        float left = overviewPosition.X + start / duration * overviewSize.X;
        float right = overviewPosition.X + end / duration * overviewSize.X;
        const float handleHitRadius = 10;
        bool overviewHeight = position.Y >= overviewPosition.Y
            && position.Y <= overviewPosition.Y + overviewSize.Y;

        if (ShowOverview && overviewHeight && position.X >= left - handleHitRadius && position.X <= right + handleHitRadius)
        {
            float leftDistance = Math.Abs(position.X - left);
            float rightDistance = Math.Abs(position.X - right);
            if (Math.Min(leftDistance, rightDistance) <= handleHitRadius)
            {
                dragging = leftDistance <= rightDistance ? DragTarget.Start : DragTarget.End;
            }
            else
            {
                dragging = DragTarget.Range;
            }

            // Anchor to the initial grab so neither handles nor the range jump on press.
            dragAnchorTime = (position.X - overviewPosition.X) / overviewSize.X * duration;
            dragStart = start;
            dragEnd = end;
        }
        else if (Inside(position, plotPosition, plotSize))
        {
            dragging = DragTarget.Cursor;
        }
        MoveCursor(position);
    }

    public override void HandleMouseMove(FishUI.FishUI ui, FishInputState state, Vector2 position)
    {
        base.HandleMouseMove(ui, state, position);
        MoveCursor(position);
    }

    public override void HandleMouseRelease(FishUI.FishUI ui, FishInputState state, FishMouseButton button, Vector2 position)
    {
        base.HandleMouseRelease(ui, state, button, position);
        if (button == FishMouseButton.Left)
        {
            dragging = DragTarget.None;
        }
    }

    private void MoveCursor(Vector2 position)
    {
        var (plotPosition, plotSize) = Plot;
        var (overviewPosition, overviewSize) = Overview;
        float time = (position.X - overviewPosition.X) / overviewSize.X * duration;
        float offset = time - dragAnchorTime;
        switch (dragging)
        {
            case DragTarget.Cursor:
                cursor = start + Math.Clamp((position.X - plotPosition.X) / plotSize.X, 0, 1) * (end - start);
                break;
            case DragTarget.Start:
                start = Math.Clamp(dragStart + offset, 0, end - Math.Min(1, duration));
                break;
            case DragTarget.End:
                end = Math.Clamp(dragEnd + offset, start + Math.Min(1, duration), duration);
                break;
            case DragTarget.Range:
                float width = dragEnd - dragStart;
                start = Math.Clamp(dragStart + offset, 0, duration - width);
                end = start + width;
                break;
        }
        cursor = Math.Clamp(cursor, start, end);
    }

    protected override void OnFishUIPostInputUpdate(FishUI.FishUI ui, float deltaTime, float time)
    {
        base.OnFishUIPostInputUpdate(ui, deltaTime, time);
        if (dragging == DragTarget.None)
        {
            return;
        }

        // Poll held state each frame so release outside the graph also ends the drag.
        if (!ui.Input.IsMouseDown(FishMouseButton.Left))
        {
            dragging = DragTarget.None;
            return;
        }

        Vector2 mouse = ui.Input.GetMousePosition();
        MoveCursor(mouse);
        if (dragging != DragTarget.Cursor)
        {
            return;
        }

        var (position, size) = Plot;
        float edgeWidth = Math.Min(64, size.X * 0.1f);
        float leftStrength = Math.Clamp((position.X + edgeWidth - mouse.X) / edgeWidth, 0, 1);
        float rightStrength = Math.Clamp((mouse.X - (position.X + size.X - edgeWidth)) / edgeWidth, 0, 1);
        float direction = rightStrength - leftStrength;
        float width = end - start;

        // At the outer edge, pan by 15% of the visible time span per second.
        // Limit long frames so returning from a stall cannot jump across the log.
        float shift = direction * width * 0.15f * Math.Clamp(deltaTime, 0, 0.1f);
        start = Math.Clamp(start + shift, 0, duration - width);
        end = start + width;
        MoveCursor(mouse);
    }

    private static bool Inside(Vector2 point, Vector2 position, Vector2 size)
    {
        return point.X >= position.X && point.X <= position.X + size.X
            && point.Y >= position.Y && point.Y <= position.Y + size.Y;
    }
}
