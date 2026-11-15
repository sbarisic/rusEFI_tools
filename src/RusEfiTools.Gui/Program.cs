using System.Diagnostics;
using System.Numerics;
using FishGfx;
using FishGfx.Graphics;
using FishGfx.FishUI;
using FishUI;
using FishUI.Controls;

using var window = new RenderWindow(new RenderWindowOptions
{
    Title = "rusEFI Tools",
    Width = 640,
    Height = 360,
    Resizable = true,
    VSync = true
});
using var graphics = new FishUIGraphicsBackend(window);
using var input = new FishUIInputAdapter(window);
var settings = new FishUISettings();
using var ui = new FishUI.FishUI(settings, graphics, input, new NullFishUIEvents(), graphics.FileSystem);
ui.Init();
settings.LoadTheme("data/themes/gwen.yaml");

var buttons = new[] { CreateButton(1), CreateButton(2) };
foreach (var button in buttons)
    ui.AddControl(button);

var camera = new Camera();
Resize(window.Width, window.Height);
window.Resized += (_, e) => Resize(e.Width, e.Height);

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
    // Clear last frame's transitions before GLFW queues new input events.
    input.BeginFrame();
    window.PollEvents();
    if (window.IsCloseRequested)
        break;

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
        ClearColor = new Color(235, 238, 242),
        Time = (float)now
    }))
    {
        using (graphics.UseRenderPass(pass, pass.View, pass.State))
            ui.TickDraw(deltaTime, (float)now);
    }
    frame.Present();
}

Button CreateButton(int number)
{
    var button = new Button
    {
        ID = $"button{number}",
        Text = $"Button {number}",
        Size = new Vector2(160, 44),
        TabIndex = number - 1
    };
    int clicks = 0;
    button.OnButtonPressed += (_, mouseButton, _) =>
    {
        if (mouseButton == FishMouseButton.Left)
            button.Text = $"Button {number}: {++clicks}";
    };
    return button;
}

void Resize(int width, int height)
{
    if (width <= 0 || height <= 0)
        return;
    ui.Resized(width, height);
    camera.SetOrthogonal(0, 0, width, height);
    bool stacked = width < 368;
    for (int i = 0; i < buttons.Length; i++)
    {
        float x = stacked ? (width - 160) / 2f : (width - 336) / 2f + i * 176;
        float y = stacked ? (height - 104) / 2f + i * 60 : (height - 44) / 2f;
        buttons[i].Position = new FishUIPosition(PositionMode.Absolute, new Vector2(x, y));
    }
}
