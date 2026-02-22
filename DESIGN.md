# LOS Overlay & Cover Planner — Design Document

## Overview
Line-of-sight overlay with cover color-coding for pawns, turrets, and planning positions. Three modes: Off, Static (direct LOS), Leaning (includes lean-around-corner). Planning tools let you place hypothetical walls, cover, and observer markers to test bunker layouts before building.

## Core Mechanics

### LOS Calculation
- Static mode: `GenSight.LineOfSight()` from observer to each cell in weapon range
- Leaning mode: adds `ShootLeanUtility.LeanShootingSourcesFromTo()` lean positions
- Custom validator supports hypothetical walls/openings

### Cover Computation
Matches vanilla `CoverUtility.TryFindAdjustedCoverInCell()`:
- Check 8 adjacent cells around target for cover-providing things
- Angle-based falloff: 100% at <15°, 80% at <27°, 60% at <40°, 40% at <52°, 20% at <65°, 0% beyond
- Non-cardinal adjacency: 1.75x angle penalty
- Distance reduction: 33% if <1.9 cells, 67% if <2.9 cells
- Returns best effective cover from all adjacent cover sources

### CE Integration
Soft dependency via reflection. Resolves `CoverHeight` StatDef at runtime. Fallback: `fillPercent * 2.5m`.

## Architecture

### Harmony Patches
| Patch Target | Type | Purpose |
|---|---|---|
| `Pawn.GetGizmos` | Postfix | Add LOS gizmo to drafted ranged pawns |
| `Building_Turret.GetGizmos` | Postfix | Add LOS gizmo to player turrets |

### MapComponents
| Component | Purpose |
|---|---|
| `HypotheticalMapState` | Manages hypothetical walls/cover/openings, observer positions |
| `LOSOverlayMapComponent` | Per-frame overlay drawing, selection change tracking |

### Key Classes
| Class | Purpose |
|---|---|
| `LOSCalculator` | Core LOS/cover math (static + leaning modes) |
| `OverlayRenderer` | Color gradient rendering with material caching |
| `Gizmo_LOSMode` | Off/Static/Leaning cycle, auto-show on selection |
| `PlanningMarker` | ThingWithComps for observer/wall/cover/open markers |
| `ICoverProvider` | Abstraction for vanilla vs CE cover systems |
| `Designator_*` | 6 architect menu tools for planning |

## Settings
| Setting | Default | Description |
|---|---|---|
| `DefaultRange` | 30 | Overlay range when weapon range unavailable |
| `OverlayOpacity` | 0.35 | Transparency of overlay cells |
| `ShowOnPawnSelect` | false | Auto-show on drafted pawn selection |
| `ShowOnTurretSelect` | true | Auto-show on turret selection |

## API Verification (RimWorld 1.6)
All signatures verified against decompiled Assembly-CSharp.dll:
- `GenSight.LineOfSight(IntVec3, IntVec3, Map, bool, Func<IntVec3,bool>, int, int)` ✓
- `GenSight.LineOfSight(IntVec3, IntVec3, Map)` (3-param overload) ✓
- `ShootLeanUtility.LeanShootingSourcesFromTo(IntVec3, IntVec3, Map, List<IntVec3>)` ✓
- `Building_Turret.AttackVerb` (abstract property) ✓
- `Building_TurretGun.gun` (public field) ✓
- `CoverUtility` angle thresholds: 15/27/40/52/65 degrees ✓
- `GenGrid.CanBeSeenOver(Building)`: Fillage.Full + not open door ✓