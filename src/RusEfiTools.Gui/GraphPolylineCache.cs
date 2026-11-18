using System.Numerics;

namespace RusEfiTools.Gui;

internal sealed class GraphPolylineCache
{
    private readonly Dictionary<(TelemetryChannel Channel, bool Overview), Entry> entries = [];

    public Vector2[] Get(TelemetryChannel channel, bool overview, Vector2 position, Vector2 size, float from, float to)
    {
        var key = (channel, overview);
        var geometry = new Geometry(position, size, from, to, channel.Series.Points.Count);
        if (entries.TryGetValue(key, out var entry) && entry.Geometry == geometry)
        {
            return entry.Points;
        }

        var samples = GraphSampleCache.Get(channel.Series.Points, from, to, (int)size.X, overview);
        var result = new List<Vector2>(samples.Length);
        Vector2? previous = null;
        foreach (var point in samples)
        {
            if (previous is not Vector2 a)
            {
                previous = point;
                continue;
            }
            var b = point;
            previous = point;
            if (b.X < from || a.X > to || b.X <= a.X)
            {
                continue;
            }
            float left = Math.Max(from, a.X);
            float right = Math.Min(to, b.X);
            float leftValue = a.Y + (b.Y - a.Y) * (left - a.X) / (b.X - a.X);
            float rightValue = a.Y + (b.Y - a.Y) * (right - a.X) / (b.X - a.X);
            if (result.Count == 0)
            {
                result.Add(Project(left, leftValue));
            }
            result.Add(Project(right, rightValue));
        }

        var points = result.ToArray();
        entries[key] = new Entry(geometry, points);
        return points;

        Vector2 Project(float x, float y) => position + new Vector2(
            (x - from) / (to - from) * size.X,
            (1 - channel.Normalize(y)) * size.Y);
    }

    // Loaded channel samples are immutable; loading another file creates new graph controls.
    private readonly record struct Geometry(Vector2 Position, Vector2 Size, float From, float To, int Count);
    private sealed record Entry(Geometry Geometry, Vector2[] Points);
}
