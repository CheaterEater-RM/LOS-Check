# LOS Overlay & Cover Planner

Line-of-sight visualization with cover color-coding and bunker planning tools for RimWorld 1.6.

A RimWorld 1.6 mod by CheaterEater.

## Features

- **Three overlay modes**: Off, Static (direct LOS), Leaning (includes lean-around-corner)
- **Cover color-coding**: Green (clear) to Red (heavy cover), matching vanilla's angle-based cover algorithm
- **Pawn & turret gizmos**: Toggle overlay on drafted ranged pawns and player turrets
- **Planning tools** (architect menu): Place observer markers, hypothetical walls, cover, and openings
- **Combined view**: See union coverage from all observer markers simultaneously
- **Combat Extended support**: Auto-detected soft dependency, uses cover height system when CE is active

## Installation

1. Subscribe on Steam Workshop, or download and extract to your RimWorld `Mods` folder
2. Enable **Harmony** and **LOS Overlay & Cover Planner** in the mod list
3. Harmony must load before this mod

## Usage

### Quick start
1. Draft a pawn with a ranged weapon and select them
2. Click the "LOS: Off" gizmo to cycle through modes
3. Green = clear shot, yellow = partial cover, red = heavy cover, dark grey = no LOS

### Planning mode
1. Open the **LOS Planning** tab in the architect menu
2. Place **Observer** markers where you want shooters
3. Place **Plan Wall** / **Plan Cover** / **Plan Opening** markers to test layouts
4. Select an observer to see its LOS overlay
5. Use **Combined LOS View** to see coverage from all observers at once

## Compatibility

- **RimWorld 1.6** (Odyssey)
- Safe to add to existing saves
- Compatible with Combat Extended (soft dependency, auto-detected)
- No known conflicts

## Building from Source

The project uses a shared `RimWorld.Paths.props` file for game DLL references.

1. Create `RimWorld.Paths.props` in the parent directory of this repo with your local paths:

```xml
<Project>
  <PropertyGroup>
    <RimWorldManaged>C:\path\to\RimWorld\RimWorldWin64_Data\Managed</RimWorldManaged>
    <HarmonyAssemblies>C:\path\to\workshop\content\294100\2009463077\Current\Assemblies</HarmonyAssemblies>
  </PropertyGroup>
</Project>
```

2. Open `Source\LOSOverlay.sln` in Visual Studio
3. Build (Ctrl+Shift+B) — output goes to `1.6\Assemblies\`

## License

MIT