# rusEFI Tools

A minimal Windows x64 GUI application targeting .NET 10. FishGfx provides
the window, input, and OpenGL drawing; FishUI hosts the telemetry graph through
the FishGfx.FishUI adapter.

The resizable window opens at 1920 × 1080 with a dark telemetry graph showing
generated sample MAP, RPM, and TPS data. MAP and TPS share the left axis;
RPM uses the right axis. Drag the plot cursor to inspect values in the legend.
Drag the handles in the overview below the plot to resize the visible time range.
Drag the middle of the selected range to pan without changing its width. While
dragging the plot cursor, hold it near either edge to scroll gradually in that
direction. Scrolling stops when you release the mouse or reach the data boundary.
This is a sample display, not a live ECU connection or log-file reader.

`DesktopApplication` owns the window and rendering loop, `MainView` sizes the
graph, `TelemetryGraph` draws and handles interaction, and `SampleTelemetry`
provides the demonstration channels.

## Requirements

- Windows x64
- .NET 10 SDK (selected by `global.json`)
- A graphics driver supporting OpenGL 4.0 or newer

## Build and run

```powershell
git submodule update --init --recursive
dotnet build RusEfiTools.slnx -c Release
dotnet run --project src/RusEfiTools.Gui -c Release
```

You can also open `RusEfiTools.slnx` in Visual Studio and set
`RusEfiTools.Gui` as the startup project.

## Dependencies

[FishGfx](https://github.com/sbarisic/FishGfx) is pinned as a Git submodule
under `third_party/FishGfx`. Its nested
[FishUI](https://github.com/sbarisic/FishUI) submodule supplies the UI library
and theme assets. Both libraries use the MIT license; their license files
remain in their source directories. The FishUI library targets .NET 9 and
is referenced by the .NET 10 application.

The build copies GLFW, shaders, fonts, and FishUI theme assets to the output
directory. Assets resolve relative to the executable, so the application
can launch from a different working directory.
