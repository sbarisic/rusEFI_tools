using System.Numerics;

namespace RusEfiTools.Gui;

internal static class GraphSamples
{
    public static float? ValueAt(List<Vector2> points, float time)
    {
        if (points.Count == 0 || time < points[0].X || time > points[^1].X)
        {
            return null;
        }
        int index = LowerBound(points, time);
        if (index == 0 || points[index].X == time)
        {
            return points[index].Y;
        }
        var a = points[index - 1];
        var b = points[index];
        return a.Y + (b.Y - a.Y) * (time - a.X) / (b.X - a.X);
    }

    // Keep the first, last and extrema of each screen column. Raw samples remain
    // intact for cursor readouts and zooming; short spikes survive overview rendering.
    public static IEnumerable<Vector2> ForDisplay(List<Vector2> points, float from, float to, int columns)
    {
        int index = Math.Max(0, LowerBound(points, from) - 1);
        int limit = Math.Min(points.Count, LowerBound(points, to) + 1);
        while (index < limit)
        {
            int first = index;
            int minimum = index;
            int maximum = index;
            int column = Column(points[index].X);
            index++;
            while (index < limit && Column(points[index].X) == column)
            {
                if (points[index].Y < points[minimum].Y)
                {
                    minimum = index;
                }
                if (points[index].Y > points[maximum].Y)
                {
                    maximum = index;
                }
                index++;
            }
            yield return points[first];
            int earlier = Math.Min(minimum, maximum);
            int later = Math.Max(minimum, maximum);
            if (earlier > first)
            {
                yield return points[earlier];
            }
            if (later > earlier && later > first)
            {
                yield return points[later];
            }
            if (index - 1 > Math.Max(first, later))
            {
                yield return points[index - 1];
            }
        }

        int Column(float time) => (int)MathF.Floor((time - from) / (to - from) * Math.Max(1, columns));
    }

    private static int LowerBound(List<Vector2> points, float time)
    {
        int left = 0;
        int right = points.Count;
        while (left < right)
        {
            int middle = left + (right - left) / 2;
            if (points[middle].X < time)
            {
                left = middle + 1;
            }
            else
            {
                right = middle;
            }
        }
        return left;
    }
}
