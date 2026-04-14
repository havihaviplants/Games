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
- If tile spacing breaks again, check:
  - `PixelsPerUnit`
  - runtime tile sprite creation
  - `Grid.cellSize`
  - source tile PNG transparent padding

## Current Implementation
- Scene used: `Assets/Scenes/SampleScene.unity`
- Additional scenes:
  - `Assets/Scenes/BlacksmithScene.unity`
  - `Assets/Scenes/PetStorageScene.unity`
  - `Assets/Scenes/WarehouseScene.unity`
  - `Assets/Scenes/Village1Scene.unity`
  - `Assets/Scenes/Village2Scene.unity`
  - `Assets/Scenes/Village3Scene.unity`
  - `Assets/Scenes/HuntingVillageScene.unity`
- Main builder: `Assets/IsometricFarmVillageBuilder.cs`
- Player controller: `Assets/TopDownPlayerController2D.cs`
- Camera follow: `Assets/CameraFollow2D.cs`
- Scene portal trigger: `Assets/ScenePortal2D.cs`
- Scene spawn marker: `Assets/SceneSpawnPoint2D.cs`
- Scene warp state: `Assets/SceneWarpState.cs`

## What Exists Now
- Runtime-generated `Grid + Tilemap` village.
- `SampleScene` is now the start hub map.
- The builder reads the active scene name and generates different layouts for each destination scene.
- Layers:
  - ground
  - path
  - water
  - details
  - obstacles
- Player movement with `Rigidbody2D`.
- Collision on blocking tilemap layers.
- Camera follow attached at runtime.
- Trigger-based scene warps between the start hub and each destination map.
- Character art now uses new sheets from `Assets/Art` instead of the earlier placeholder sheets.

## Character Art Integration
- Active character sheets:
  - hero: `Pixel art soldier sprite sheet.png`
  - red soldier: `Medieval knight sprite sheet.png`
  - yellow soldier: `Pixel knight sprite sheet in four views.png`
- These new sheets are `1024x1024` four-view layouts and are sliced as `2x2`.
- The player controller now swaps the hero sprite by movement direction.
- Character scale is fitted by world height so the actors sit closer to the tilemap size instead of appearing oversized.
- Character textures loaded from `Assets/Art` now use `Point` filtering, and processed pose textures also stay on `Point` to avoid Unity-side blur.
- If character size needs more tuning, check:
  - `FitSpriteRendererToHeight(...)` in `Assets/IsometricFarmVillageBuilder.cs`
  - hero target height
  - enemy target height
  - source sprite crop in `CreateProcessedPoseSprite(...)`

## Warp Loop Fix
- Returning from a destination scene could immediately re-trigger the same portal and trap the player in a loop.
- Fix applied:
  - `SceneWarpState` now applies a short warp cooldown after scene transitions.
  - Hub return spawn points were also moved slightly away from their destination portals.

## Hub And Warp System
- `SampleScene` acts as the starting map / hub.
- Hub destinations currently created:
  - blacksmith
  - pet storage
  - warehouse
  - village 1
  - village 2
  - village 3
  - hunting village
- Each destination scene has a return portal back to `SampleScene`.
- Warp flow:
  - `ScenePortal2D` stores destination scene and spawn id
  - `SceneWarpState` keeps the pending spawn id during scene transition
  - `SceneSpawnPoint2D` marks named arrival points
  - `IsometricFarmVillageBuilder` places the player at the matching spawn point after the map is built

## Nested Map Expansion
- The map structure now supports a simple "ant-hill" style expansion:
  - start hub
  - village 1, village 2, village 3
  - each of those villages now branches into two hunting maps
- Added hunting scenes:
  - `Village1HuntAScene`
  - `Village1HuntBScene`
  - `Village2HuntAScene`
  - `Village2HuntBScene`
  - `Village3HuntAScene`
  - `Village3HuntBScene`
- Village scenes now create:
  - a return portal back to `SampleScene`
  - `Hunt A`
  - `Hunt B`
- Each hunt scene now creates:
  - a spawn point for arriving from its parent village
  - a return portal back to that parent village
- The current hunting maps are intentionally template-like prototype areas, built from existing RPG base assets, ponds, fences, rocks, trees, and simple path shapes.

## Tile Fix History
- Problem:
  - Some path tiles used edge or curved "crescent" variants in places that should have stayed flat.
  - Some shoreline tiles were visually rotated or mirrored the wrong way.
  - Earlier grid sizing caused visible cracks between tiles.
- Fix applied:
  - Path tiles were simplified to use the flat dirt tile `rpgTile024.png` as the default road surface.
  - Unnecessary curved path border placement logic was removed to reduce incorrect half-moon edge tiles.
  - Shoreline edge placement was corrected by direction, using transform rotation where needed instead of relying only on sprite flipping.
  - Grid sizing was changed to read the actual runtime sprite world size from `rpgTile024.png`, and `cellGap` is kept at zero.
- What to check if the issue comes back:
  - Any new border-placement logic in `BuildRpgPathTilemap()`
  - Any shoreline orientation logic in `BuildRpgPondTilemap()`
  - Any change to `PixelsPerUnit`, runtime tile sprite creation, or grid cell sizing

## Reference Notes
- Stable-looking tilemaps are currently preferred over aggressive decorative edge variation.
- When in doubt, use the flat base path tile instead of a curved edge tile.
- If a tile looks directionally wrong, prefer explicit rotation logic over improvised `flipX`/`flipY` combinations.

## Notes For Next Agent
- This is now tilemap-based, not just loose sprite placement.
- The village is still a prototype layout and can be refined visually.
- If visual seams remain even with correct cell sizing, the next likely fix is trimming or preprocessing source tile sprites to remove transparent borders.
- No local compiler validation was run in this environment because `dotnet`/`csc` were unavailable.

## Backup
- A timestamped backup of the current key files is stored outside `Assets` so Unity does not compile duplicate scripts.
- Pre-map-expansion backup path: `ProjectBackups/pre_hub_maps_2026-04-12_19-42-59`
- Latest script/settings backup path: `ProjectBackups/post_hub_maps_2026-04-12_20-05-30`
