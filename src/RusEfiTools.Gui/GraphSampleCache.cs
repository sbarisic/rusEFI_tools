using System.Numerics;
using System.Runtime.CompilerServices;

namespace RusEfiTools.Gui;

internal static class GraphSampleCache
{
    private static readonly ConditionalWeakTable<List<Vector2>, Entries> Cache = new();

    public static Vector2[] Get(List<Vector2> points, float from, float to, int columns, bool overview)
    {
        var entries = Cache.GetOrCreateValue(points);
        var key = new Key(from, to, columns, points.Count);
        var entry = overview ? entries.Overview : entries.View;
        if (entry?.Key == key)
        {
            return entry.Points;
        }
        entry = new Entry(key, GraphSamples.ForDisplay(points, from, to, columns).ToArray());
        if (overview)
        {
            entries.Overview = entry;
        }
        else
        {
            entries.View = entry;
        }
        return entry.Points;
    }

    // Only the current view and full-log overview are retained per immutable series.
    // Weak keys release the cached samples when their log is replaced.
    private sealed class Entries
    {
        public Entry? Overview;
        public Entry? View;
    }

    private readonly record struct Key(float From, float To, int Columns, int Count);
    private sealed record Entry(Key Key, Vector2[] Points);
}
