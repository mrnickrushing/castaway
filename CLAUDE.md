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
