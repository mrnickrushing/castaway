# Last Tide: working notes for coding agents

Shipwreck island survival for iPhone (landscape), Unity **6000.0.85f1**, URP 17. Genre reference: Last Day on Earth
style "walk into the green ring and the action happens" gathering, crafting and base building. Own art, names and
setting only.

- `Assets/Scripts/Core` (no Unity dependency): `Content` (items, recipes, camp structures, harvest nodes, tuning),
  `Session` (survival meters, harvesting, building by delivering materials, crafting jobs, combat damage, death and
  the lost backpacks, one per fall, chest, rain catcher; rewards wait until they fit (`Fits`), paid-for items that
  don't fit wait in `State.waiting` and drop in by themselves), `Quests` (the survival guide), `GameState` (the save).
- `Assets/Scripts/Game`: everything visual is built from code at runtime (`GameRoot` boots in the empty
  `Assets/Scenes/Main.unity`). `World/Island` (height function, terrain mesh, sea), `World/WorldBuilder` (places
  everything from a fixed seed; harvest keys like `palm#12` must stay stable between launches), `World/Spots`
  (rings: `NodeSpot`, `SiteSpot`, `BagSpot`), `World/Models` (procedural models until drop-in art exists in
  `Resources/Models/<Category>/<id>`), `Actors/` (castaway rig and controls, boars), `UI/Hud*` (joystick, meters,
  guide card, labels over rings, backpack, crafting, chest).
- The wider game (`Core/World.cs` content, `Core/Session.World.cs` rules): levels and XP, energy that refills in real
  time (`World.EnergyMax`, `EnergyEvery`), doubloons, eight locations on the archipelago map (SAIL fast for energy or PADDLE
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
  Path (step rewards claimed on the free track and the Ultra track). Backpack friction (`UI/Hud.Bag.cs`): green QUEST tabs on what the
  current step uses (`Hud.QuestNeeds`, `Quest.Item` for gathering steps), FULL / "2 LEFT" under BAG, MAKE ROOM
  (`Session.MakeRoom`: tops up stacks, stores spare materials at home), TO POCKETS / TO BACKPACK (`Session.MoveSlot`),
  and leaving home with under 3 free slots asks STORE & GO / GO ANYWAY. UI: `Hud.Panels` (inventory + station/storage),
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
- Far detail: any model with an `<id>_far` next to it (Scan or Trees category) gets a LODGroup in `ModelRegistry.Spawn`
  (full detail above 0.6 of screen height; `GameRoot` sets lodBias 1). `island_tree_02_far` (6k tris, shares the near
  tree's textures through its JSON slot names) and `Trees/palm_far` keep every island near 1.2M tris (was ~1.95M).
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
- Playtesting: `Game/Debug/PlayBot.cs` (editor only) plays the whole guide from a new game: it walks the stick to
  `GuideArrow.GoalPosition` along an A* path (slopes, water, obstacles), fights with auto-attack, sails, skips movies and
  taps through story cards; menu steps call the same Session method as the button. Every shortcut is logged as `GRANT`.
  Drive it from the CLI: delete the save, enter Play mode, `LastTide.Game.PlayBot.Begin(3)`, read `PlayBot.Report(40)`.
  `PlayBot.Visit(loc, phase)` lands on an island at a time of day for screenshots; `PlayBot.Heal = false` for fair fights.
  Balance check (2026-11-09): an iron harpoon + the hide set beats Red Jack and his raiders with health to spare.
- Landmarks are photoscanned where possible (`Models.ScanCave`, `ScanPirateCamp`, `ScanRuins`); the cliffs are
  one-sided scans, so their faces must look at the player (yaw 0). Red Jack's palisade stands straight `palisade_log`s
  (`dead_tree_trunk` cut to 300 tris by `Tools/blender/lod.py`, sharing its textures through the JSON slot name). The terrain's rock layer is Poly Haven `lichen_rock`.
- Island outlines differ by biome (`Island.Shore`): Driftwood Shore is a long sandbar, the lighthouse sits on a point
  running north, Red Jack's ship lies in a bay on the pirate island, Limestone Ridge has a second peak, Palm Grove a
  lagoon (`Island.Lagoon`; scenery checks `Island.Height` so nothing floats in it). Keep noise inputs off whole
  numbers: Unity's Perlin is flat on the lattice and drew a straight seam across the terrain. Location islands land a few metres above
  the waterline with the raft at the water's edge (`Island.RaftAt`); the ground noise is offset per island (`_gx`, `_gz`).
- The guide (quest card + green arrow) always says what to do next: build steps short of a material name it ("need 6
  Wood", or "craft 2 Rope (CRAFT)") and the arrow leads to a node that yields it, or its ingredients, then the station;
  crafting steps (`Quest.Craft`) list missing ingredients, then "ready: tap CRAFT". When a hunt target is all dead, the
  arrow leads toward the next den but stops 30 m short (dens only refill with the castaway over 26 m away).
- Trips and pacing: `Core/Session.Trip.cs` logs a trip from landing away until home (`State.trip`: finds, kills, XP,
  doubloons, falls; recipes learned and survivors rescued as a diff), then `State.lastTrip` waits for the HUD's BACK AT
  card (`UI/Hud.Trip.cs`: FOUND, DEFEATED, NOW YOU CAN from `Session.NowPossible`, NEXT; the PlayBot closes it).
  `Session.Settled` (shelter built and day 2, or level 3) gates the season button, gift, the shop "+"s and the map's
  RADIO/TRADE/SHOP (`UI/Hud.Pacing.cs`, `_later`), with a toast when they appear.
- Island rewards (`Core/Content.Islands.cs`, `Core/Session.Islands.cs`, `UI/Hud.Islands.cs`, models in
  `World/IslandModels.cs`): each location has a material found only there (its own node, its sailors' bags, a 30% drop
  from its creatures), a plan picked up once near its landmark (node `plan`, flag `plan_<location>`, a story card) and
  the permanent camp improvement it teaches (`perk_*`: a workbench recipe built once, no item; `ApplyPerk` sets the
  flag and the rules read `FoodScale`, `RegenScale`, `HungerScale`, `NightAggro`, `SailScale`, the weir's extra fish
  and the chest's `ChestBrace` slots). `WorldBuilder.PlanSpot` always draws its position so the island layout stays
  the same once the plan is taken. The map's island panel shows an "Own:" line; CRAFT has a Camp tab.
- Combat feedback (`App/CombatFx.cs`, `UI/Hud.Combat.cs`): enemies telegraph each strike for `EnemyDef.Windup` seconds
  (they stop, crouch, a red ring of reach 2.1 m fills under them, red "!" overhead); stepping out of reach makes them
  miss ("DODGED"). Hits play a hit-stop (`CombatFx.HitStop`, keeps the bot's timeScale) and a weapon sound
  (`CombatFx.Impact`: slash/stab/chop/mine/hit); being hit shakes the camera, pops the damage and shows a red arc
  toward the attacker. Amber "!" on noticing, grey "?" when sneaking alone kept one from noticing, DANGER + a red
  vignette when two or more are after the castaway within 25 m, and the held weapon's condition bar under its button.
- Fight reach is measured along the ground (`Enemy.Nearest`, enemy attack, Biscuit's bite): 3D distance stalled fights
  on slopes. The drowned smugglers (5 dens, back in 40 s) and the cave/camp barrels (rum) feed the late story steps.
- Story cards use `Resources/UI/Story/<key>.jpg` (16:9, from `ai_media.py story`), else the small map painting.
- Realistic people (Sketchfab, CC BY; `Tools/art/sketchfab.py` keeps uid/author/role and writes `CREDITS.md`; the
  in-game credits are in `UI/Hud.Help.cs`): `Tools/blender/cast_import.py` renames each rig to the Unreal mannequin's
  core bones, stands it up from its bones (some rest poses lie down), decimates and packs maps into
  `Resources/Models/Cast/<id>.fbx|json|tex` (Humanoid, `Editor/ArtImport.cs`). All of them share one clip set,
  `Cast/moves.fbx` (Quaternius Universal Animation Library, CC0, slimmed by `cast_moves.py`; clips baked by body
  orientation), played through a Playables mixer (`Actors/HumanMotion.cs`, no AnimatorController). `CharacterArt`
  maps the game's clip names (Idle/Walk/Run/Sword/HitReact/Death/Wave/Duck) onto them; the castaway (`SurvivorArt`)
  walks/jogs/sprints by speed, crouch-walks when sneaking, kneels to build, and keeps the bone-aimed work poses on top.
  Cast: castaway (player; dressed like the story paintings, cream linen shirt, red sash, brown breeches, by
  `Tools/blender/cast_recolour.py`, which `sketchfab.py cast castaway` reruns), captain (Red Jack), hunter (raiders), mara, tomas, ned, revna, smuggler/smuggler2. Only
  CC BY/CC0 originals: no fan art of other games/films. Sketchfab glbs sometimes lack their textures (both animated
  boars did): render a preview before using one.
- Realistic animals (`Tools/art/sketchfab.py creature`, `Tools/blender/creature_import.py`, Generic rigs in
  `Resources/Models/Creatures`): the giant crab plays its own scuttle clip faster with its pace and rears to strike; the
  viper (a CC0 snake scan, thickened by `girth`) has no usable clip, so `Actors/CreatureArt.cs` bends its ~100 spine
  bones into a travelling S and raises the neck to strike. Props from Sketchfab go through `sketchfab.py prop` into
  `Models/Scan` and are reached through `ModelRegistry.ScanFor` (the rowboat, also beached on Driftwood Shore and tied
  up at the pier; the lighthouse, turned by `WorldBuilder.LighthouseYaw` so its door faces the beach; the log raft
  that leaves every island). `scan_import.py` honours glTF's metallicFactor and gives specular-gloss materials a plain
  finish (their maps aren't metallic/roughness). The iron veins are a Sketchfab ore scan; gulls (`World/Gulls.cs`, a
  rigged low-poly gull whose wings `CreatureArt.Wings` beats) circle each island's shore.
- Scans import Z-up with a -90° X rotation on their root: turn them with `Quaternion.Euler(0, yaw, 0) * t.rotation`,
  never by assigning a fresh rotation (that lays every fern, table and cliff on its side). The pets stay Quaternius: no realistic dog
  on Sketchfab has walk and run clips.
- Trees, roofs and towers between the camera and the castaway (or the build ghost) switch to shadows-only while they
  block the view (`App/SeeThrough.cs`, ticked by `CameraRig`; bounds rays, no shader change, so the scans' URP Lit
  variants stay as they are). The HUD canvas uses `Expand` from 1920x1080: an iPad gets 1920x1440 and the panels fit.
  Phones (`UIKit.Phone`: under 4 in tall; in the Editor a Game view wider than 1.9:1) grow every `UIKit.Label` toward
  44 units by best fit (`UIKit.SetSize`, never below the size asked for), scale the field HUD by 1.2 (`GrowHudOnPhones`),
  and lay Help out as a scrolling page (`UIKit.Scroller`). Check new screens at 1800x830 as well as 1440x1080.
- App Store listing: `docs/appstore/listing.json` + `screens/`, written by `Tools/appstore/listing.py` (Codemagic
  `lasttide-listing` in the train app); see `docs/STORE.md`.
- Accounts (`Game/Platform`): Sign in with Apple (`AppleAccount`, lupidan package; links RevenueCat purchases, names
  the castaway), the save in the player's iCloud key-value store (`CloudSave` + `Plugins/iOS/LastTideCloud.mm`; no
  server; `Core/CloudRules`: a new phone adopts it, a newer save from another device is offered, never overwritten),
  local reminders (`Reminders`, `Session.ReminderPlan`; permission asked on the first voyage), Settings rows for all
  of it plus Name and Delete account (erases iCloud + local, signs out). `Editor/IosCapabilities.cs` writes
  `LastTide.entitlements` with literal ids; the bundle id's capabilities come from `Tools/appstore/capabilities.py`.
- Shaders in `Resources/Shaders` (`LastTide/VertexLit`, `Terrain`, `Water`, `Ring`, `Ghost`, `Additive`).

## Verify every change in the real Editor

```bash
unity pipeline install --project-path .     # once per clone (local dev tool, never commit it)
unity open . --until-ready
Tools/unity-recompile.sh
Tools/unity-test.sh                          # leaves Play mode first (the runner refuses to start during Play)
Tools/unity-capture.sh out.png 8
~/.dotnet8/dotnet test Tools/CoreTests       # Core tests without Unity
```

In the Editor: WASD walks, F9 deletes the save.

## Before committing

- Keep `com.unity.pipeline` out of `Packages/manifest.json`; don't commit `Packages/packages-lock.json` or
  `ProjectSettings/Packages/com.unity.pipeline/`.
- Recompile with 0 errors, tests passing, Play mode with 0 Console errors. Commit `.meta` files.

## iPhone builds

App Store Connect app "Last Tide", Apple ID 6832382482, bundle `com.rushingtech.lasttide`, team PH4AKDQ4Q7.
`Tools/ios-export.sh` exports the Xcode project and uploads it as the public `ios-latest` release asset. The App Store
Connect API key lives only in the Codemagic app **train** (variable group `ios_credentials`; Codemagic can't copy secure
values), so Last Tide's jobs run there from the train repo's branch `codemagic/lasttide`: `lasttide-testflight`
(downloads the export, numbers the build after the latest TestFlight one, signs, uploads) and `lasttide-iap`
(`Tools/appstore/iap.py`, creates the in-app purchases; safe to rerun). This repo's `codemagic.yaml` has the same
workflows for the day the key is added to the castaway app. The plan has Mac mini M2 machines only; a Mac can take
several minutes to free up. Only send TestFlight builds when asked.
