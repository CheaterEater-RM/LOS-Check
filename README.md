# LOS Overlay & Cover Planner

Line-of-sight visualization with cover color-coding and bunker planning tools for RimWorld. Built using AI tools.

## Features

- **Three overlay modes**: Off, Static (direct LOS), Leaning (includes lean-around-corner)
- **Offensive/Defensive views**: See cover targets have from you, or cover you have from each direction
- **Cover color-coding**: Green (clear) to Red (heavy cover)
- **Pawn & turret gizmos**: Toggle overlay on drafted ranged pawns and player turrets
- **Planning tools** (architect menu): Place observer markers, hypothetical walls, cover, and openings
- **Combined view**: See union coverage from all observer markers simultaneously
- **Moving pawn tracking**: Overlay redraws as pawn moves to new cells
- **Combat Extended support**: Auto-detected soft dependency, uses cover height system

## Installation

1. Subscribe on Steam Workshop, or download and extract to your RimWorld Mods folder
2. Enable Harmony and LOS Check in the mod list
3. Harmony must load before this mod

## Usage

### Quick start
1. Draft a pawn with a ranged weapon and select them
2. Click the "LOS: Off" gizmo to cycle through modes
3. Right click to swap modes between offensive vs. defensive (outgoing vs. incoming fire)
4. Green = clear shot, yellow = partial cover, red = heavy cover, dark grey = no LOS

### Planning mode
1. Open the LOS Planning tab in the architect menu
2. Place Observer markers where you want shooters
3. Drag-place Plan Wall / Plan Cover / Plan Opening markers to test layouts
4. Select an observer to see its LOS overlay
5. Use Combined LOS View to see coverage from all observers at once

## Compatibility

- RimWorld 1.6 (Odyssey)
- Safe to add to existing saves
- Compatible with Combat Extended (soft dependency)

## Building from Source

1. Create RimWorld.Paths.props in the parent directory with your local paths
2. Open Source\LOSOverlay.sln in Visual Studio
3. Build (Ctrl+Shift+B) - output goes to 1.6\Assemblies\

## License

MIT for the mod. Textures are licensed under CC BY 3.0 from game-icons.net and have their own license.
