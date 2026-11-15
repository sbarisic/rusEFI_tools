namespace RusEfiTools.Gui;

// All graph views share this instance; no event mirroring or per-view time copies.
internal sealed class GraphTimeline
{
    public float Duration { get; }
    public float Start { get; set; }
    public float End { get; set; }
    public float Cursor { get; set; }

    public GraphTimeline(float duration)
    {
        Duration = Math.Max(1, duration);
        End = Duration;
        Cursor = Duration * 0.55f;
    }
}
