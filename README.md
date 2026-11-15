# rusEFI Tools

A minimal Windows x64 GUI application targeting .NET 10. FishGfx provides
the window, input, and OpenGL drawing; FishUI hosts the telemetry graph through
the FishGfx.FishUI adapter.

The resizable window opens at 1920 × 1080 with two stacked telemetry graphs showing
generated sample MAP, RPM, and TPS data. Use **Add graph** and **Remove last graph**
to show one to four views. Channel assignments are configured in `MainView`.
All views share the cursor, visible time range, and a single bottom overview.
Each channel has color-matched scale limits on the right.
Drag the plot cursor to inspect values in the legend.
Drag the handles in the overview below the plot to resize the visible time range.
Drag the middle of the selected range to pan without changing its width. While
dragging the plot cursor, hold it near either edge to scroll gradually in that
direction. Scrolling stops when you release the mouse or reach the data boundary.
Drop one `.mlg` log onto the window to replace the sample data. A `.zip` containing
exactly one `.mlg` also works without extracting files. The loader supports binary
[MLVLG v2](https://www.efianalytics.com/TunerStudio/docs/MLG_Binary_LogFormat_2.0.pdf)
logs with `Time` (seconds), `MAP` (kPa), `RPM` (rpm), and `TPS` (%) fields, including
the BossECU logs. It plots these three channels; other fields and marker text are
not displayed yet. There is no live ECU connection.

Loading runs in the background. Dropping another log cancels the previous load.
The status line reports loading, completion, or errors; a failed load leaves the
current graphs intact. Source files are read only and never copied or modified.
Checksums and record lengths are validated, and time starts at zero for each log.
All raw samples of the three plotted channels remain in memory; rendering groups
samples by screen column while preserving peaks to keep long logs practical.
Reduced samples are shared across views, plot geometry is reused while moving
the cursor, and each trace is submitted as one FishGfx polyline.

`DesktopApplication` owns the window and rendering loop, `MainView` manages the
graph rows, `TelemetryGraph` draws and handles interaction, `GraphTimeline`
holds the shared time range and cursor, and `SampleTelemetry` provides the
demonstration channels.

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

Headless graph interaction checks:

```powershell
dotnet test tests/RusEfiTools.Gui.Tests -c Release
```

To include local log-file integration checks, set `RUSEFI_TEST_LOGS` to the
directory containing the BossECU `.mlg` and `.zip` files before running the tests.

For an opt-in rendering benchmark, set `RUSEFI_PROFILE_LOG` to an `.mlg` path and
run the `GraphRenderingPerformanceTests` test with detailed console logging.
It uses a hidden 1920 × 1080 OpenGL window, two views with three channels each,
VSync off, five warm-up frames, and thirty measured frames per scenario. It also
compares individual and batched line pixels in memory; it writes no screenshots.

With `fine_tune_last.mlg` (79,658 samples), local median frame times improved
from 203 ms to approximately 3 ms for cursor movement and from 225 ms to
11–13 ms for changing the time range. UI submissions fell from roughly 36,800
to 128 per frame. These are hidden-window measurements, not a manual drag test
or a guarantee on other graphics hardware.

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
