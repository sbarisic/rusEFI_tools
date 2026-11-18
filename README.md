# rusEFI Tools

A minimal Windows x64 GUI application targeting .NET 10. FishGfx provides
the window, input, and OpenGL drawing; FishUI hosts the telemetry graph through
the FishGfx.FishUI adapter.

The resizable window opens at 1920 × 1080 with two stacked telemetry graphs showing
generated sample MAP, RPM, and TPS data. Use **Add graph** and **Remove last graph**
to show one to four views. Channel assignments, colors, and scales are configured in `data_app/Config.json`.
All views share the cursor, visible time range, and a single bottom overview.
Each channel has color-matched scale limits inside the plot's right edge.
Drag the plot cursor to inspect values in the legend.
Drag the handles in the overview below the plot to resize the visible time range.
Drag the middle of the selected range to pan without changing its width. While
dragging the plot cursor, hold it near either edge to scroll gradually in that
direction. Scrolling stops when you release the mouse or reach the data boundary.
Drop one `.mlg` log onto the window to replace the sample data. A `.zip` containing
exactly one `.mlg` also works without extracting files. The loader supports binary
[MLVLG v2](https://www.efianalytics.com/TunerStudio/docs/MLG_Binary_LogFormat_2.0.pdf)
logs with a `Time` field in seconds, including the BossECU logs. It plots the
channels named in the configuration. The **Cursor data inspector**
shows all logged sensor fields at the shared cursor's selected timestamp,
including fields not plotted. Filter by name or unit and scroll to browse.
Drag the scrollbar thumb or drag the rows vertically to scroll the inspector.
Numeric signals are interpolated between samples; flags and other discrete
display types hold their previous recorded state until the next sample.
The inspector uses elapsed log time, matching the graphs. Marker text is not
displayed yet. There is no live ECU connection.

Loading runs in the background. Dropping another log cancels the previous load.
The status line reports loading, completion, or errors; a failed load leaves the
current graphs intact. Source files are read only and never copied or modified.
Checksums and record lengths are validated, and time starts at zero for each log.
All raw samples of the configured plotted channels remain in memory; rendering groups
samples by screen column while preserving peaks to keep long logs practical.
Other sensor data is retained in compressed memory blocks and decoded on demand;
only visible inspector rows are drawn, and values are cached until the cursor moves.
Reduced samples are shared across views, plot geometry is reused while moving
the cursor, and each trace is submitted as one FishGfx polyline.

`DesktopApplication` owns the window and rendering loop, `MainView` manages the
graph rows, `TelemetryGraph` draws and handles interaction, `GraphTimeline`
holds the shared time range and cursor, and `SampleTelemetry` provides the
demonstration channels.

## Configuration

The application reads `data_app/Config.json` beside the executable at startup.
The file is included in build and publish output. Missing files, blank files,
`{}`, and the existing `_comment` placeholder use the default two-graph layout. Invalid JSON
shows an error in the status line and uses defaults without overwriting the file.

Edit `src/RusEfiTools.Gui/data_app/Config.json` before building, or edit
`data_app/Config.json` beside the executable for an existing installation.
Restart the application after editing. Settings are not automatically saved by
the Add/Remove graph buttons; Add graph copies the first configured graph.

The included example has three graphs: RPM/MAP, TPS/airflow, and measured/target
AFR, with a 1920 × 1080 window and a 440px inspector. Both AFR channels use 10–20,
TPS uses 0–100%, and airflow uses 0–360 kg/h.

- `Gui.Graphs` contains one to four views, in display order. Each view's
  `Channels` array sets its channels and legend order; an empty array is allowed.
- `Name` matches the full log field name, ignoring case. It does not use fuzzy
  matching. Missing channels are skipped and listed in the status line.
- `Color` is optional `#RRGGBB`. Defaults retain MAP blue, RPM red, and TPS green;
  other channels use a small color palette.
- `Min` and `Max` are optional. Omitted limits use the full loaded channel range,
  which stays fixed while panning. Constant signals get a nonzero range. Explicit
  limits must have `Min < Max`; values outside the range are clamped to the edge.
  Colors and limits are independent for each graph, including repeated channels.
- `ShowGramsPerSecond: true` displays kg/h channels as g/s in the graph legend
  and scale labels (divide by 3.6). `Min` and `Max` remain in the original log
  units: 0–360 kg/h displays as 0–100 g/s. The default is `false`. Other source
  units are unaffected; raw data and the inspector retain the logged units.
- `Gui.Window` sets initial `Width` and `Height` (minimum 640 × 480).
- `Gui.Inspector` sets `Visible` and `Width` (minimum 200px, capped at 35% of the
  actual window width). Hidden inspectors give the space back to the graphs.
- All graphs share the same cursor and timeline. Before a log loads, only the
  generated MAP/RPM/TPS demonstration channels are available.

Invalid settings show an error and use defaults without rewriting your file.
Future log settings belong in `Logs`; these values are retained as JSON but do
not change loading behavior yet.

For example, a single graph with custom limits:

```json
{
  "Gui": {
    "Graphs": [
      {
        "Channels": [
          { "Name": "RPM", "Color": "#FF4E4E", "Min": 500, "Max": 7500 },
          { "Name": "Air: Flow estimate", "Color": "#FFC857" }
        ]
      }
    ]
  },
  "Logs": {}
}
```

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
