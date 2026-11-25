# Yurei Crosshair

Crosshair overlay with a Yurei black & white menu. Targets .NET Framework 4.8 (built into Windows 10/11).

## Build on Windows
- Visual Studio 2022: open `YureiCrosshair.csproj` and build (Release).
- Or from a terminal: `dotnet build -c Release`
  (if it complains about reference assemblies, install the ".NET Framework 4.8 targeting pack" in the VS Installer)

## Files
- `Program.cs`            entry point, single-instance check
- `MenuForm.cs`           the menu (tabs, sliders, profiles, tray icon, hotkeys)
- `OverlayForm.cs`        click-through always-on-top window that draws the crosshair
- `CrosshairRenderer.cs`  draws every crosshair style (used by overlay + preview)
- `Controls.cs`           theme colors + custom slider/toggle/button/swatch/preview controls
- `logo.png`              the Yurei logo (embedded into the exe)
- `Settings.cs`           crosshair settings + config save/load (%AppData%\YureiCrosshair\config.ini)
- `Native.cs`             Win32 calls

## Change the theme
Edit the colors at the top of `Controls.cs` (`Theme.Accent` is the white highlight).

## Hotkeys
Defaults: INSERT = menu, HOME = crosshair on/off, PAGE DOWN = hide from taskbar. Change them in the SETTINGS tab.
