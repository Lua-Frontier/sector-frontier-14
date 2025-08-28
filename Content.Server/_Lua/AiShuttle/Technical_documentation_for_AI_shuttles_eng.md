### AI Shuttle and Fire Control System: Architecture, Behavior and Extension

Document version: 1.0
Scope: Server-side AI shuttle logic (Lua folders), presets, maps and fire control (FireControl)

---

## Overview

This document describes the structure and operation of the server-side AI shuttle system, interaction with the fire control subsystem, as well as configuration methods through components, prototypes and maps. The following files are covered:

- `Content.Server/_Lua/AiShuttle/AiShuttleBrainSystem.cs`
- `Content.Server/_Mono/FireControl/FireControllableComponent.cs`
- `Content.Server/_Mono/FireControl/FireControlServerComponent.cs`
- `Content.Server/_Mono/FireControl/FireControlSystem.cs`
- `Content.Shared/_Lua/AiShuttle/AiShuttleBrainComponent.cs`
- `Content.Shared/_Lua/AiShuttle/AiShuttlePresetComponent.cs`
- `Content.Shared/_Lua/AiShuttle/AiShuttlePresetPrototype.cs`
- `Resources/Maps/_Lua/ShuttleEvent/AI_shuttle/PerunAI.yml`
- `Resources/Maps/_Lua/ShuttleEvent/AI_shuttle/PozvizdAI.yml`
- `Resources/Maps/_Lua/ShuttleEvent/AI_shuttle/StribogAI.yml`
- `Resources/Prototypes/_Lua/AiShuttlePresets.yml`
- `Resources/Prototypes/_Lua/NPCs/ai_pilot.yml`
- `Resources/Prototypes/_Lua/NPCs/mob.yml`

Key subsystems:
- AI shuttle piloting and combat: `AiShuttleBrainSystem` + `AiShuttleBrainComponent`
- AI configuration presets: `AiShuttlePresetComponent`, `AiShuttlePresetPrototype`, and yml configuration file
- Fire Control (Gunnery Control System, GCS): `FireControlSystem` + components `FireControllableComponent`, `FireControlServerComponent`
- Maps with AI shuttle placement: `PerunAI.yml`, `PozvizdAI.yml`, `StribogAI.yml` in Resources\Maps\_Lua\ShuttleEvent\AI_shuttle\
- Pilot agent for shuttle console input: `MobShuttleAIPilotNF`/`MobShuttleAIPilot`

---

## AI Shuttle Architecture

### Configuration Components

- `AiShuttleBrainComponent` - stores behavior parameters:
  - Patrol: `AnchorEntityName`, `FallbackAnchor`, `MinPatrolRadius`, `MaxPatrolRadius`
  - Combat and FTL distances: `FightRangeMin/Max`, `FtlMinDistance`, `FtlExitOffset`, `PostFtlStabilizeSeconds`, `FtlCooldownSeconds`
  - Safety/targets: `RetreatHealthFraction`, `AvoidStationFire`, `ForbidRamming`, `TargetShuttlesOnly`, `TargetShuttles`, `TargetDebris`
  - Fire parameters: `MaxWeaponRange`, `ForwardAngleOffset`
  - Other: `CombatTriggerRadius`, `MinSeparation`, patrol FTL jump parameters (`PatrolFtlMin/MaxDistance`, `PatrolWaypointTolerance`)
  - Runtime state (serverOnly): `CurrentTarget`, `LastKnownTargetPos`, `PilotEntity`, `PatrolWaypoint`, `WingLeader`, `WingSlot`, `FacingMarker`, `StabilizeTimer`, `FtlCooldownTimer`, `InCombat`

- `AiShuttlePresetComponent` (network) - configures the same fields on grid entity, applied on server when brain starts.

- `AiShuttlePresetPrototype` - preset prototype, supports `NameMatch` for exact match by grid `MetaData.EntityName`.

### Server Brain System

- `AiShuttleBrainSystem`
  - Frequencies:
    - Sensors: 5 Hz - target selection, situational awareness update
    - Control: 9 Hz - piloting, formation holding, firing
  - Presets: one-time application either from `AiShuttlePresetComponent` on grid, or by `AiShuttlePresetPrototype` with `NameMatch`.
  - Wing assignment (formation) for ships named "Perun": groups of three (leader + 2 wingmen), fields `WingLeader`/`WingSlot`.
  - Pilot: ensures presence of pilot agent (`MobShuttleAIPilotNF` preferred; otherwise `MobShuttleAIPilot`) and its binding to nearest `ShuttleConsole` on grid.
  - Target selection logic:
    - Searches for enemy grids on same map, with `ShuttleDeedComponent` and available piloted console
    - Ignores friendly AI shuttles (presence of `AiShuttleBrainComponent` on target)
    - Within `CombatTriggerRadius`, considering patrol ring (`MinPatrolRadius..MaxPatrolRadius`)
    - Preference for closer targets (simple scoring)
  - Piloting/maneuver:
    - In combat: maintaining distance in `FightRangeMin/Max` window, orbital drift (tangential component), radial correction, obstacle avoidance (raycast), observing `MinSeparation` (anti-ram)
    - FTL micro-jumps: at large distance and cooldown `FtlCooldownTimer` → jump "behind target" with offset `FtlExitOffset`, post-stabilization on `PostFtlStabilizeSeconds`
    - Patrol: random waypoint generation and/or linear FTL jumps in `PatrolFtlMin/MaxDistance` range
  - Fire control: calls `FireControlSystem.TryAimAndFireGrid`, brain aims at enemy console position if found
  - Input formation: `DriveViaConsole` calculates held piloting buttons based on target direction and aiming marker, with damping and braking by situation

---

## Fire Control Subsystem (GCS)

### Components

- `FireControlServerComponent` - GCS server on grid:
  - Grid connection: `ConnectedGrid`
  - Weapon accounting: `Controlled`, `UsedProcessingPower`, limit `ProcessingPower`
  - GCS consoles: `Consoles`
  - Salvos: `UseSalvos`, `SalvoPeriodSeconds`, `SalvoWindowSeconds`, `SalvoJitterSeconds`

- `FireControllableComponent` - on weapons/turrets:
  - Server binding: `ControllingServer`
  - Fire cooldown: `NextFire`, `FireCooldown`
  - Fire sectors: `FireArcDegrees` (own), `UseGridNoseArc`, `GridNoseArcDegrees`

### System

- `FireControlSystem`:
  - Lifecycle: server connection/disconnection to grid when powered; tracking weapon transfer between grids; cleaning invalid references
  - Weapon registration: automatic, if on same grid with active server and enough `ProcessingPower` (cost by `ShipGunClassComponent`)
  - Aiming/firing:
    - `TryAimAndFireGrid(grid, worldTarget)` - checks arcs, FTL state, issues fire attempts for controlled weapons
    - `TryAimAndFireGrid(grid, targetGrid, suggestedAim)` - target velocity intercept prediction; salvo support
    - LOS/obstacle checking with rays within weapon grid
    - On successful check calls `GunSystem.AttemptShoot`
  - Diagnostics: fire sector visualization (server broadcasts event to clients)

---

## Maps and Presets

- Maps:
  - `PerunAI.yml`: grid named `CR-GF "Perun"`, contains `AiShuttleBrain` with hard parameters (anchor, radii, FTL, weapons, GCS servers and consoles). Uses wing formation for same-named grids on map.
  - `PozvizdAI.yml`: grid `CR-GF "Pozvizd"` with `AiShuttleBrain` (parameters moved to preset), GCS, weapons and power.
  - `StribogAI.yml`: grid `CR-GF "Stribog"`, similarly.

- YAML presets: `Resources/Prototypes/_Lua/AiShuttlePresets.yml`
  - `AiShuttlePreset` with `nameMatch` by exact grid name (in MetaData)
  - Examples: `Perun`, `Stribog`, `Pozvizd` - set patrol radii, combat distances, `MaxWeaponRange`, `MinSeparation`

- Pilots:
  - `MobShuttleAIPilotNF` - invisible pilot with `CanPilot` tag, used when brain spawns pilot
  - `MobShuttleAIPilot` - visible test pilot (with HTN), fallback option

---

## Data Flows and Interaction

1) `AiShuttleBrainSystem.Update` (sensor tick):
   - Target search among enemy grids; friendly AI grids ignored; target must have working shuttle control console
   - Update `CurrentTarget`, `LastKnownTargetPos`, `FacingMarker`
   - Formation assignment for PerunAI

2) `AiShuttleBrainSystem.Update` (control tick):
   - Formation maintenance (wingmen take leader's target, hold `FormationSpacing` with FTL catch-up capability)
   - Combat: orbit + radial correction, obstacle avoidance (`ComputeAvoidance`), prevent approach below `MinSeparation`
   - FTL jump decision (distance > max( `FtlMinDistance`, `MaxWeaponRange * 3` ) and cooldown)
   - Patrol without target: drift/FTL to random points
   - Fire command transmission: `FireControlSystem.TryAimAndFireGrid`

3) `FireControlSystem`:
   - Checks GCS state on grid, FTL, weapon arcs (`TargetWithinArcs`), LOS (`HasLineOfSight`)
   - For salvos - window and jitter on weapons
   - Rotates weapons `RotateToFaceSystem` with visibility, fires through `GunSystem`

---

## How to Change AI Behavior

- Quick value edits on map:
  - Change `AiShuttleBrain` component fields in YAML map (e.g., `PerunAI.yml`): patrol radii, combat distances, FTL parameters.
  - Pros: isolated for specific map; Cons: not automatically reused.

- Using presets by grid name:
  - Edit `Resources/Prototypes/_Lua/AiShuttlePresets.yml` or add new `AiShuttlePreset` with `nameMatch` exact grid name (`MetaData.name`).
  - Server applies preset automatically on first brain tick.

- Component preset on grid:
  - Add `AiShuttlePresetComponent` to grid (e.g., via mapper or on spawn), set needed fields.
  - Applied once over brain values.

- Aggressiveness/distance adjustment:
  - `FightRangeMin/Max`, `MaxWeaponRange`, `MinSeparation`, `CombatTriggerRadius`
  - FTL behavior: `FtlMinDistance`, `FtlExitOffset`, `PostFtlStabilizeSeconds`, `FtlCooldownSeconds`

- Wing formation (Perun only):
  - Grid names must match "Perun" (in MetaData). Grouping by three: index 0 leader, 1/2 wingmen.
  - Formation holding regulated by `FormationSpacing` on leader brain (inherited by wingmen in position calculation).

- Fire control:
  - Adjust weapon arcs: `FireArcDegrees` (weapon itself) and/or enable nose arc `UseGridNoseArc` + `GridNoseArcDegrees`.
  - Salvos on server: `UseSalvos`, `SalvoPeriodSeconds`, `SalvoWindowSeconds`, `SalvoJitterSeconds` in `FireControlServerComponent`.

- Obstacle avoidance:
  - Ray parameters hardcoded in `AiShuttleBrainSystem.ComputeAvoidance` (probe lengths, collision mask). For fine tuning requires code change.

---

## How to Add New AI Shuttle

1) Create grid map in `Resources/Maps/_Lua/ShuttleEvent/AI_shuttle/YourShip.yml`:
   - Specify `MetaData.name`, `AiShuttleBrain` (can be without parameters - will take default), GCS server (`GunneryServer*`), consoles (`ComputerShuttle`, `ComputerGunneryConsole`), weapons.

2) Add preset (optional) in `Resources/Prototypes/_Lua/AiShuttlePresets.yml`:
   - `- type: AiShuttlePreset`, `nameMatch: <your name from MetaData>` and required parameters.

3) Ensure grid has powered `GunneryServer*` and weapons with `FireControllableComponent` (usually already set on weapons).

4) Launch and check:
   - AI should spawn pilot (`MobShuttleAIPilotNF`) and bind it to shuttle console.
   - When enemy appears within `CombatTriggerRadius` - start maneuver and fire.

---

## Common Problems and Diagnostics

- AI doesn't move:
  - No `ShuttleConsole` on grid or pilot didn't bind. Check for `ComputerShuttle` and power.
  - Pilot didn't spawn: check for `MobShuttleAIPilotNF` prototype (or `MobShuttleAIPilot`).

- Doesn't fire:
  - GCS server not connected to grid: check `GunneryServer*` power and that grid has `FireControlGridComponent` (created by system on connection).
  - Weapons not registered: check that enough `ProcessingPower` and weapons have correct class (`ShipGunClassComponent`).
  - Target outside arc/LOS or grid in FTL.

- Gets too close/rams:
  - Increase `MinSeparation` and/or decrease `FightRangeMin`.

- FTL jumps too often:
  - Increase `FtlCooldownSeconds` or `FtlMinDistance`; decrease `MaxWeaponRange` to lower FTL threshold.

---

## Interfaces/Methods for Integration

- From AI to fire control:
  - `FireControlSystem.TryAimAndFireGrid(EntityUid gridUid, Vector2 worldTarget)`
  - `FireControlSystem.TryAimAndFireGrid(EntityUid gridUid, EntityUid targetGridUid, Vector2 suggestedAim)`

- Fire arc diagnostic utilities:
  - `FireControlSystem.CountWeaponsAbleToFireAt(gridUid, worldTarget)` - available fire assessment
  - `FireControlSystem.ToggleVisualization(entityUid)` - on/off direction visualization

- Weapon registration and server state:
  - `FireControlSystem.RefreshControllables(gridUid)`
  - `FireControlSystem.GetRemainingProcessingPower(server)`

---

## Target Selection Policies

- Ignoring friendly AI shuttles: target with `AiShuttleBrainComponent` won't be selected for attack.
- Requirement for piloted console on target (`ShuttleConsoleComponent`): reference for accurate aiming (`TryGetEnemyShuttleConsolePos`).
- Map focus: best targets cached in `_globalFocus` for consistency of multiple AI grids.

---

## Extension Notes

- For adding complex logic (ship class priority, armor consideration, group focus) extend sensor tick in `AiShuttleBrainSystem.Update` and scoring structure.
- For more advanced collision avoidance (navigation fields, multi-beam lidar), develop `ComputeAvoidance` and probing parameters.
- For squadron coordination of more than three ships - replace `AssignPerunWings` with N-ship distribution algorithm with roles and distances.
- For alternative combat styles - introduce modes and switch control coefficient sets in `DriveViaConsole`.

---

## Key Code References

- AI shuttle brain: `AiShuttleBrainSystem.Update`, `EnsurePilot`, `DriveViaConsole`, `ComputeAvoidance`, `ComputeHullDistance`, `AssignPerunWings`
- Brain configuration: `AiShuttleBrainComponent`
- Presets: `AiShuttlePresetComponent`, `AiShuttlePresetPrototype`, `Resources/Prototypes/_Lua/AiShuttlePresets.yml`
- Fire control: `FireControlSystem` (+ methods TryAimAndFireGrid/AttemptFire/TargetWithinArcs/HasLineOfSight)
- Example maps: `PerunAI.yml`, `PozvizdAI.yml`, `StribogAI.yml`
- Pilots: `MobShuttleAIPilotNF`, `MobShuttleAIPilot`

---

## Licensing

Code and data subject to project licenses. See LICENSE-AGPLv3.txt and LICENSE-MIT.TXT files in repository root.
