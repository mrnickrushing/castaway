# Last Tide: working notes for coding agents

Shipwreck island survival for iPhone (landscape), Unity **6000.0.85f1**, URP 17. Genre reference: Last Day on Earth
style "walk into the green ring and the action happens" gathering, crafting and base building. Own art, names and
setting only.

- `Assets/Scripts/Core` (no Unity dependency): `Content` (items, recipes, camp structures, harvest nodes, tuning),
  `Session` (survival meters, harvesting, building by delivering materials, crafting jobs, combat damage, death and
  the lost backpack, chest, rain catcher), `Quests` (the survival guide), `GameState` (the save).
- `Assets/Scripts/Game`: everything visual is built from code at runtime (`GameRoot` boots in the empty
  `Assets/Scenes/Main.unity`). `World/Island` (height function, terrain mesh, sea), `World/WorldBuilder` (places
  everything from a fixed seed; harvest keys like `palm#12` must stay stable between launches), `World/Spots`
  (rings: `NodeSpot`, `SiteSpot`, `BagSpot`), `World/Models` (procedural models until drop-in art exists in
  `Resources/Models/<Category>/<id>`), `Actors/` (castaway rig and controls, boars), `UI/Hud*` (joystick, meters,
  guide card, labels over rings, backpack, crafting, chest).
- The wider game (`Core/World.cs` content, `Core/Session.World.cs` rules): levels and XP, energy that refills in real
  time (`World.EnergyMax`, `EnergyEvery`), doubloons, eight locations on the archipelago map (run for energy or walk
  for free in real time; level gates; loot grows back `Refresh` seconds after leaving), radio distress calls that send
  the castaway to rescue a survivor at a location (`SignalDef`), and the trading post (`OfferDef`, doubloons only, no
  real-money purchases yet). Locations are their own islands: `Island.Configure(location)` reshapes the height
  function by `Biome`, `WorldBuilder.PopulateZone` places the landmark, loot, survivor and enemy dens; harvest keys are
  prefixed `<location>:`. `Map/MapView` is the 3D map (own camera at `MapView.Origin`), `UI/Hud.Map|Radio|Offers` its
  screens. Enemies (`Actors/Enemy`, `EnemyBody`): boar, giant crab, viper, Red Jack's raiders.
- Art: `Tools/art/ai_assets.py` (castaway: Higgsfield multi-image 3D with rigging, ~35 credits; boar and palm: Tripo,
  12 each) then Blender import; the castaway's work poses are driven bone by bone in `Actors/ArtRigs.cs`.
  `Tools/art/sheets.py` makes a whole grid of icons or postcards in one image (2.75 credits) and cuts it into
  `Resources/UI/Items` and `Resources/UI/Places`. `Tools/art/audio.py` turns the takes in `art-src/audio` into
  `Resources/Audio/*.ogg`. Credits are the user's money: ask before spending, check `higgsfield account transactions`.
- Story: `Core/Story.cs` (chapters, scenes keyed by the guide step they open), played by `UI/Hud.Story.cs`. The arc
  ends with Red Jack (boss enemy `redjack` at the Pirate Camp) and sailing his ship (`ShipSpot`) on the Last Tide.
- Purchases: see `docs/STORE.md`. Doubloon spends: refill energy, rush a journey or a craft, recover the lost backpack.
  Help and settings: `UI/Hud.Help.cs`.
- Base (`Core/Content.Base.cs`, `Core/Session.Base.cs`): pockets (first 5 backpack slots, kept on passing out), clothes and
  armour (`GearSlot`), split/sort/drop, station levels and upgrades, crafting several at once ("hand" recipes anywhere),
  free building on the camp grid (`PieceDef`, `PieceState`; floors, edge walls, objects; storage boxes), the Survivor's
  Path (step rewards claimed on the free track and the Ultra track). UI: `Hud.Panels` (inventory + station/storage),
  `Hud.Blueprints`, `Hud.Build` (grid, ghost, rotate/confirm), `Hud.Path`. Piece models: `World/Pieces.cs`; icons for
  things without painted art are rendered at runtime (`UI/ModelIcons.cs`).
- Free models: `Tools/art/free_assets.py` (Kenney kits, Quaternius characters, CC0, see `CREDITS.md`) converts them
  with `Tools/blender/free_import.py` to vertex-coloured FBX in `Models/Free` and `Models/People`, drawn with
  `Materials.Kit` (VertexLit without the uv2 surface channel those meshes don't have). Prefer these over paid AI art.
- Night and the field HUD (`Core/Session.Night.cs`, `App/DayNight.cs`): a 12-minute day (`Session.DayLength`, the last
  third is night); `DayNight` drives sun/moon, ambient, fog, `_NightGlow` and a night colour-grading Volume (the map is
  always drawn by day). At night and while sneaking, enemy aggro range scales by `AggroScale`. Sailors' bags
  (`LootSpot`, node `lootbag`) roll 2-4 items from the location loot into a loot window (`Hud.OpenLoot`). Biscuit the dog
  (`Actors/Pet.cs`, quest step `dog`, `PetSpot` on Driftwood Shore) follows and bites (`Session.PetDamage`). Level
  rewards wait until claimed from LEVEL UP! (`ClaimLevelRewards`). HUD (LDoE style): name/health top-left, quest card
  with fold, radar minimap (`Hud.Minimap`), hand/weapon/target/sneak/AUTO buttons (`Hud.Panels`), XP bar at the bottom.
  The map is a parchment chart with ink sketches; water is cut smoothly where it ends (`MapView.BuildSea`).
- Season and events (`Core/Season.cs` content and tuning, `Core/Session.Season.cs` rules, `UI/Hud.Season.cs`): four-week
  seasons from `Season.EpochDay`; every XP point is a season point (plus three free daily takes); 30 levels, each with a free
  and a premium reward (`lasttide.season` buys premium for the current season only). Events run on a 14-day cycle
  (`EventDef.StartDay/Days`), each counting one stat through `Session.Tally` ("kill:boar", "kill:raider", "gather:wood",
  "gather:fish", "loot", "craft", "nightkill") with three milestones. Days turn at local midnight (`UtcOffsetSeconds`).
  Rewards include supply crates and gold chests (`OpenCrate`, the inventory's OPEN) and companions (`pet_<id>` items unlock
  `Season.Pets`; `Pet.FitHeight` sizes the free animal models by their skeleton). `Hud.ShowRewards` is the reward card.
- Photoscanned art (Poly Haven + ambientCG, CC0, free): `Tools/art/polyhaven.py` packs the six terrain layers
  (grass, leaf litter, dirt, sand, rock, pebbles; height maps normalised so the height blend stays soft) and imports
  models through `Tools/blender/scan_import.py` (sets split into `<id>_a`, `_b`...; rocks/logs rebaked onto a clean
  low-poly mesh; plants keep alpha leaf cards, two-sided; trees whose leaves are separate cards get thinned), then
  `slim` (drop unused pieces, cap maps at 1024, normals to JPG). Output: `Resources/Models/Scan/<piece>.fbx` + `.json`
  (one material slot per submesh) + `tex/`. `ModelRegistry.ScanFor` swaps kit models for scans everywhere; camp
  structures are built from scans in `Models.ScanStructure` (sail lean-to from the ship's sailcloth, stone fire ring,
  table with tools; `Span` lays a pole between two points whatever axis it was modelled along). `GrassField` draws
  GPU-instanced grass clumps by `Island.GrassAmount`. Budgets: trees ~22k tris, rocks 1-4k, plants 1-3k; check
  `UnityEditor.UnityStats.triangles` (about 1.2M at the default zoom) after adding art.
- Look: ACES tonemapping, HDRI reflections (`Resources/Sky`), SSAO (radius 0.4), shadows to 50 m; the sea refracts
  the opaque texture with depth absorption and caustics (`Water.shader`, needs URP opaque + depth textures). Never add
  a negative lift to the grading: it crushes the night to black.
- Building animations (`World/BuildAnim.cs`): `Assembly` drops a camp structure's parts in, in child order, as
  deliveries pay for them (`SiteSpot`), inside a driftwood scaffold (`Models.Pole`) that topples when it's done; the
  ghost outlines only the parts still to come. Build-mode pieces play `Appear` (floors settle, walls rise with hammer
  taps, objects drop and squash) and `Crumble` when taken down; `BuildFx` has dust, chips, sparks and the busy look of
  a station with a craft running. Build a structure's parts in the order they'd go up (poles before the ridge).
- Loading and movies: `UI/LoadingScreen.cs` covers launch (`GameRoot.Start` builds the island a step at a time behind
  it, `_ready` holds `Update` until then) and landfall after a voyage (`EnterArrived`). It always shows the Rushing
  Technologies logo (`Resources/UI/Brand/rushingtech.png`, cut from the master `Tools/brand/rushingtech_logo.png` by
  luminance; every game's loading screen carries it). Story movies: `SceneDef.Movie` names a clip that `UI/Cinema.cs`
  plays once (flag `movie_<id>`) before the scene's lines, full screen with sound, a SKIP button and the chapter title;
  ambience/music duck via `Sfx.Duck`. Clips and the loading art come from `Tools/art/ai_media.py` (Higgsfield: still,
  review, then video; paid) -> `install` writes `StreamingAssets/Videos/<id>.mp4`, the still to `Resources/UI/Cinematics`
  (shown until the first frame or if the clip can't play) and VP8 copies in `Library/EditorVideos` (the Linux Editor
  can't decode H.264). Needs the `com.unity.modules.video` package.
- Shaders in `Resources/Shaders` (`LastTide/VertexLit`, `Terrain`, `Water`, `Ring`, `Ghost`, `Additive`).

## Verify every change in the real Editor

```bash
unity pipeline install --project-path .     # once per clone (local dev tool, never commit it)
unity open . --until-ready
Tools/unity-recompile.sh
unity command --project-path . test run editor --timeout 600 --result-only
Tools/unity-capture.sh out.png 8
~/.dotnet8/dotnet test Tools/CoreTests       # Core tests without Unity
```

In the Editor: WASD walks, F9 deletes the save.

## Before committing

- Keep `com.unity.pipeline` out of `Packages/manifest.json`; don't commit `Packages/packages-lock.json` or
  `ProjectSettings/Packages/com.unity.pipeline/`.
- Recompile with 0 errors, tests passing, Play mode with 0 Console errors. Commit `.meta` files.

## iPhone builds

`Tools/ios-export.sh` exports the Xcode project and uploads it as the `ios-latest` release asset (same flow as
Ironhorse Frontier's Codemagic setup). Bundle id `com.rushingtech.lasttide`; the App Store Connect app and a
Codemagic workflow still need creating before the first TestFlight build.
