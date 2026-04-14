# Unity Village Progress

## Current Goal
- Build a top-down 2D village scene in Unity using tightly packed tilemap tiles.
- Prevent visible gaps between tiles.
- Keep a simple playable prototype with movement, collision, and camera follow.

## Important Constraint
- Tile spacing must not open up.
- The project currently fixes this by matching the `Grid.cellSize` to the real world size of a runtime-loaded `64x64` RPG tile sprite.
- In `Assets/IsometricFarmVillageBuilder.cs`, `GetRpgTileWorldSize()` reads the actual tile sprite bounds and `BuildRpgVillage()` applies that size to the `Grid`.
- `grid.cellGap` is forced to `Vector3.zero`.

## Current Implementation
- Scene used: `Assets/Scenes/SampleScene.unity`
- Additional scenes include Blacksmith, PetStorage, Warehouse, Village1-3, HuntingVillage, and Village1-3 Hunt A/B.
- Main builder: `Assets/IsometricFarmVillageBuilder.cs`
- Player controller: `Assets/TopDownPlayerController2D.cs`
- Scene portal trigger: `Assets/ScenePortal2D.cs`
- Scene spawn marker: `Assets/SceneSpawnPoint2D.cs`
- Scene warp state: `Assets/SceneWarpState.cs`

## What Exists Now
- Runtime-generated `Grid + Tilemap` village.
- Start hub plus nested village and hunting maps.
- Player movement with `Rigidbody2D`.
- Collision on blocking tilemap layers.
- Trigger-based scene warps between connected maps.
- Water collision enabled so ponds are not walkable.
- Additional playability carving keeps spawn/portal routes clear.

## Nested Map Expansion
- Start hub.
- Village 1, Village 2, Village 3.
- Each of those villages branches into two hunting maps.
- Village scenes create a return portal plus `Hunt A` and `Hunt B` portals.
- Each hunt scene creates a spawn point for arrival from its parent village and a return portal back to that village.

## Backup
- Pre-map-expansion backup path: `ProjectBackups/pre_hub_maps_2026-04-12_19-42-59`
- Latest script/settings backup path: `ProjectBackups/post_hub_maps_2026-04-12_20-05-30`
- Current full snapshot path: `ProjectBackups/snapshot_2026-04-14_18-19-26`
