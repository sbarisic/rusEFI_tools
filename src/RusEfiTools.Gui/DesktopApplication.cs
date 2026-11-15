using System.Diagnostics;
using FishGfx;
using FishGfx.FishUI;
using FishGfx.Graphics;
using FishUI;

namespace RusEfiTools.Gui;

internal sealed class DesktopApplication
{
    public void Run()
    {
        using var window = new RenderWindow(new RenderWindowOptions
        {
            Title = "rusEFI Tools",
            Width = 1920,
            Height = 1080,
            Resizable = true,
            VSync = true
        });

        using var graphics = new FishUIGraphicsBackend(window);
        using var input = new FishUIInputAdapter(window)
        {
            // The adapter starts disabled. Desktop controls must receive input.
            Enabled = true
        };

        var settings = new FishUISettings();
        using var ui = new FishUI.FishUI(
            settings, graphics, input, new NullFishUIEvents(), graphics.FileSystem);

        ui.Init();
        settings.LoadTheme("data/themes/gwen.yaml");

        var view = new MainView(ui);
        using var logLoader = new LogDropLoader(window, view);
        var camera = new Camera();
        Resize(window.Width, window.Height);
        window.Resized += OnWindowResized;

        try
        {
            RunLoop(window, graphics, input, ui, camera, logLoader);
        }
        finally
        {
            window.Resized -= OnWindowResized;
        }

        void OnWindowResized(object? sender, WindowResizeEventArgs args)
        {
            Resize(args.Width, args.Height);
        }

        void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            ui.Resized(width, height);
            camera.SetOrthogonal(0, 0, width, height);
            view.Resize(width, height);
        }
    }

    private static void RunLoop(
        RenderWindow window,
        FishUIGraphicsBackend graphics,
        FishUIInputAdapter input,
        FishUI.FishUI ui,
        Camera camera,
        LogDropLoader logLoader)
    {
        var state = RenderState.Default with
        {
            CullMode = CullMode.None,
            DepthTestEnabled = false,
            DepthWriteEnabled = false
        };

        var timer = Stopwatch.StartNew();
        double previousTime = 0;

        while (!window.IsCloseRequested)
        {
            // Clear old transitions before GLFW queues this frame's input.
            input.BeginFrame();
            window.PollEvents();
            logLoader.Tick();

            if (window.IsCloseRequested)
            {
                break;
            }

            double now = timer.Elapsed.TotalSeconds;
            float deltaTime = (float)(now - previousTime);
            previousTime = now;

            if (window.Width <= 0 || window.Height <= 0)
            {
                Thread.Sleep(16);
                continue;
            }

            ui.TickUpdate(deltaTime, (float)now);

            using var frame = window.Graphics.BeginFrame();
            using (var pass = frame.BeginPass(window.Graphics.Backbuffer, new RenderPassDescriptor
            {
                View = new RenderView(camera),
                State = state,
                ColorLoadAction = RenderLoadAction.Clear,
                ClearColor = new Color(14, 19, 25),
                Time = (float)now
            }))
            {
                using (graphics.UseRenderPass(pass, pass.View, pass.State))
                {
                    ui.TickDraw(deltaTime, (float)now);
                }
            }

            frame.Present();
        }
    }
}
