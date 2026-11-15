using System.Diagnostics;
using System.Numerics;
using FishGfx;
using FishGfx.FishUI;
using FishGfx.Graphics;
using FishUI;
using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class GraphRenderingPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void ProfileRealLogWithHiddenOpenGlWindow()
    {
        string? path = Environment.GetEnvironmentVariable("RUSEFI_PROFILE_LOG");
        if (path == null)
        {
            return;
        }
        var log = MlgLogReader.Load(path, TestContext.Current.CancellationToken);
        using var window = new RenderWindow(new RenderWindowOptions
        {
            Width = 1920, Height = 1080, Visible = false, FocusOnShow = false, VSync = false
        });
        using var graphics = new FishUIGraphicsBackend(window);
        using var input = new FishUIInputAdapter(window);
        var settings = new FishUISettings();
        using var ui = new FishUI.FishUI(settings, graphics, input, new NullFishUIEvents(), graphics.FileSystem);
        ui.Init();
        settings.LoadTheme("data/themes/gwen.yaml");
        ui.Resized(1920, 1080);
        var timeline = new GraphTimeline(log.Channels[0].Series.Points[^1].X);
        for (int index = 0; index < 2; index++)
        {
            ui.AddControl(new TelemetryGraph(log.Channels, timeline)
            {
                Position = new FishUIPosition(PositionMode.Absolute, new Vector2(24, 56 + index * 444)),
                Size = new Vector2(1872, 444 + (index == 1 ? 112 : 0)),
                ShowOverview = index == 1
            });
        }
        var camera = new Camera();
        camera.SetOrthogonal(0, 0, 1920, 1080);
        var state = RenderState.Default with { CullMode = CullMode.None, DepthTestEnabled = false, DepthWriteEnabled = false };
        var individual = CapturePolyline(false);
        var batched = CapturePolyline(true);
        Assert.True(individual.AsSpan().SequenceEqual(batched), "Batched and individual line pixels must match.");
        Assert.True(batched.Any(pixel => pixel != Color.Black), "The comparison must contain rendered pixels.");
        foreach (string scenario in new[] { "cursor", "resize" })
        {
            var times = new List<double>();
            long allocations = 0;
            for (int index = 0; index < 35; index++)
            {
                timeline.Start = scenario == "resize" ? index * 0.5f : 0;
                timeline.End = scenario == "resize" ? timeline.Duration - index : timeline.Duration;
                timeline.Cursor = (timeline.Start + timeline.End) / 2 + index;
                long before = GC.GetAllocatedBytesForCurrentThread();
                long stamp = Stopwatch.GetTimestamp();
                ui.TickUpdate(1 / 60f, index / 60f);
                using (var frame = window.Graphics.BeginFrame())
                {
                    using (var pass = frame.BeginPass(window.Graphics.Backbuffer, new RenderPassDescriptor
                    {
                        View = new RenderView(camera), State = state,
                        ColorLoadAction = RenderLoadAction.Clear, ClearColor = new Color(14, 19, 25)
                    }))
                    {
                        using (graphics.UseRenderPass(pass, pass.View, pass.State))
                        {
                            ui.TickDraw(1 / 60f, index / 60f);
                        }
                    }
                    frame.Present();
                }
                if (index >= 5)
                {
                    times.Add(Stopwatch.GetElapsedTime(stamp).TotalMilliseconds);
                    allocations += GC.GetAllocatedBytesForCurrentThread() - before;
                }
            }
            times.Sort();
            output.WriteLine($"{scenario}: median {times[times.Count / 2]:F2} ms, p95 {times[(int)(times.Count * .95)]:F2} ms, " +
                $"{allocations / times.Count:N0} bytes/frame, {graphics.LastFrameDrawCallCount:N0} UI draw submissions");
        }

        Color[] CapturePolyline(bool batch)
        {
            Vector2[] points = [new(100, 200), new(180, 120), new(250, 230), new(300, 230), new(300, 300)];
            using var frame = window.Graphics.BeginFrame();
            using (var pass = frame.BeginPass(window.Graphics.Backbuffer, new RenderPassDescriptor
            {
                View = new RenderView(camera), State = state,
                ColorLoadAction = RenderLoadAction.Clear, ClearColor = Color.Black
            }))
            {
                using (graphics.UseRenderPass(pass, pass.View, pass.State))
                {
                    graphics.BeginDrawing(0);
                    if (batch)
                    {
                        graphics.DrawPolyline(points, 2, FishColor.Red);
                    }
                    else
                    {
                        for (int index = 1; index < points.Length; index++)
                        {
                            graphics.DrawLine(points[index - 1], points[index], 2, FishColor.Red);
                        }
                    }
                    graphics.EndDrawing();
                }
            }
            window.ReadPixels();
            var pixels = window.PixelData.ToArray();
            frame.Present();
            return pixels;
        }
    }
}
