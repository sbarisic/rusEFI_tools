# rusEFI Tools

A minimal Windows x64 GUI application targeting .NET 10. FishGfx provides
the window, input, and OpenGL drawing; FishUI provides the two buttons through
the FishGfx.FishUI adapter.

The resizable window opens with **Button 1** and **Button 2** centered in it.
Clicking a button updates its label with its click count. The buttons stack
vertically when the window becomes narrow.

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
