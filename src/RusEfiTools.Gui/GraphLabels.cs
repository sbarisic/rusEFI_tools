using System.Globalization;
using System.Numerics;
using FishUI;

namespace RusEfiTools.Gui;

internal sealed class GraphLabels
{
    private static readonly FishColor Text = new(224, 230, 237);
    private static readonly FishColor Background = new(22, 29, 38, 245);
    private FontRef? font;

    private FontRef? GetFont(FishUI.FishUI ui)
    {
        var source = ui.Settings.FontDefault;
        if (source == null)
        {
            return null;
        }
        return font ??= ui.Graphics.LoadFont(source.Path, source.Size * 1.25f, source.Spacing, Text);
    }

    public void DrawLegend(FishUI.FishUI ui, TelemetryChannel[] channels, float cursor, Vector2 plot, Vector2 size)
    {
        var font = GetFont(ui);
        if (font == null || channels.Length == 0)
        {
            return;
        }
        var gfx = ui.Graphics;
        var values = channels.Select(channel =>
        {
            float? value = GraphSamples.ValueAt(channel.Series.Points, cursor);
            return value.HasValue ? channel.DisplayValue(value.Value).ToString("0.0", CultureInfo.InvariantCulture) : "N/A";
        }).ToArray();
        float valueWidth = values.Max(value => gfx.MeasureText(font, value).X);
        float nameWidth = channels.Max(channel => gfx.MeasureText(font, channel.DisplayName).X);
        float width = Math.Min(Math.Max(280, 72 + nameWidth + valueWidth), Math.Max(1, size.X * 0.55f - 24));
        float rowHeight = Math.Max(26, gfx.MeasureText(font, "Mg").Y + 8);
        var position = plot + new Vector2(16, 16);
        var box = new Vector2(width, Math.Min(16 + channels.Length * rowHeight, Math.Max(1, size.Y - 32)));
        gfx.DrawRectangle(position, box, Background);
        gfx.DrawRectangleOutline(position, box, new FishColor(48, 60, 72));
        gfx.BeginScissor(position, box);
        try
        {
            for (int index = 0; index < channels.Length; index++)
            {
                var channel = channels[index];
                var row = position + new Vector2(12, 8 + index * rowHeight);
                gfx.DrawLine(row + new Vector2(0, 9), row + new Vector2(26, 9), 3, channel.Color);
                string name = Fit(ui, font, channel.DisplayName, width - valueWidth - 72);
                gfx.DrawTextColor(font, name, row + new Vector2(36, 0), Text);
                float valueX = position.X + width - 12 - gfx.MeasureText(font, values[index]).X;
                gfx.DrawTextColor(font, values[index], new Vector2(valueX, row.Y), Text);
            }
        }
        finally
        {
            gfx.EndScissor();
        }
    }

    public void DrawScales(FishUI.FishUI ui, TelemetryChannel[] channels, Vector2 plot, Vector2 size)
    {
        var font = GetFont(ui);
        if (font == null || channels.Length == 0)
        {
            return;
        }
        var gfx = ui.Graphics;
        float available = Math.Max(1, size.X * 0.45f - 24);
        float[] widths = channels.Select(channel => Math.Max(72, gfx.MeasureText(font, channel.Name).X + 24)).ToArray();
        float total = widths.Sum();
        float scale = Math.Min(1, available / total);
        float x = plot.X + size.X - total * scale - 8;
        float lineHeight = Math.Max(20, gfx.MeasureText(font, "Mg").Y + 4);
        gfx.BeginScissor(plot, size);
        try
        {
            for (int index = 0; index < channels.Length; index++)
            {
                var channel = channels[index];
                float width = widths[index] * scale;
                gfx.DrawRectangle(new Vector2(x, plot.Y + 4), new Vector2(width, lineHeight * 2 + 8), Background);
                gfx.DrawRectangle(new Vector2(x, plot.Y + size.Y - lineHeight - 8), new Vector2(width, lineHeight + 4), Background);
                Draw(channel.Name, plot.Y + 8);
                Draw(channel.DisplayValue(channel.AxisMaximum).ToString("0.###", CultureInfo.InvariantCulture), plot.Y + 8 + lineHeight);
                Draw(channel.DisplayValue(channel.AxisMinimum).ToString("0.###", CultureInfo.InvariantCulture), plot.Y + size.Y - lineHeight - 4);
                x += width;

                void Draw(string text, float y)
                {
                    gfx.DrawTextColor(font, Fit(ui, font, text, width - 16), new Vector2(x + 8, y), channel.Color);
                }
            }
        }
        finally
        {
            gfx.EndScissor();
        }
    }

    private static string Fit(FishUI.FishUI ui, FontRef font, string text, float width)
    {
        if (ui.Graphics.MeasureText(font, text).X <= width)
        {
            return text;
        }
        const string suffix = "...";
        if (ui.Graphics.MeasureText(font, suffix).X > width)
        {
            return "";
        }
        while (text.Length > 0 && ui.Graphics.MeasureText(font, text + suffix).X > width)
        {
            text = text[..^1];
        }
        return text + suffix;
    }
}
