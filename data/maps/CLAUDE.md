# data/maps — map recipes

One JSON file per map id (`data/maps/<mapId>.json`), read by that map's build function.
Only `test_farm.json` exists so far; every other map still holds its placements as C#
literals. The reader/writer code lives in `src/World/` (MapRecipe, MapPlacement,
PlacementKinds/PlacementFields, MapRecipeFile, MapRecipeException, MapRecipeSeeds); the
graphical editor is the Haunt Mapper (`scenes/editor/MapStage.tscn` +
`addons/haunt_mapper/`).

- Map recipes are CONTENT, not save state — the same bucket as ItemDefs and CropDefs,
  never GameData. Read at map build time, never written at runtime; the editor is the
  only writer. No SaveMigrations versioning: a recipe change is a content change.
- A recipe stores tile coordinates and NAMES, never atlas coordinates and never pixel
  positions, because that is exactly what keeps `ForAct` wrapping every painted cell
  and `Prop.Anchor` owning every anchor. Unknown records round-trip verbatim, like
  unknown item ids — unknown kinds AND unknown fields both survive load and save
  untouched.
- A map with no recipe falls back to its C# literals, so every map stays constructible
  with no file present. A recipe that exists but cannot be read throws
  `MapRecipeException`, which always names the file. Maps are NOT becoming .tscn —
  `MapRegistry`'s and phase3-spec's "becomes PackedScene.Instantiate" comments are
  superseded.
- Canonical text format, for legible diffs (being mergeable is most of why this is JSON
  and not a scene): one placement per line, sorted by y then x then kind, fields in a
  fixed order, "\n" endings on every platform. Serialising the same recipe twice is
  byte-identical, and so is a load/save cycle. Values are strings, numbers and bools
  only — an object or array value cannot be held to one line per placement.
- Scatter/prop placements are DECORATIVE-ONLY on maps with field obstacles: the
  farm's clearable trees, stumps and boulders are save state (ObstacleGen seeds
  them; the axe and pick clear them), so its recipe keeps only the fallen log. A
  drawn obstacle that ignored the axe beside an identical one that falls would be
  the map lying about its own rules.
- Terrain painting stays generative and has no representation here; what moves into
  data is what a person would otherwise drag: props, scatter, spawn markers, doors,
  exits, signs, furniture and the interactables.
- Field keys are `PlacementFields` constants, never literals: a mistyped key is not an
  error, it is an unknown field that round-trips perfectly and is silently ignored by
  the builder — the worst possible failure. A sign's `text` field is [KEVIN]-provisional
  (copy moves to a table of its own once there is one).
- Seeding: `MapRecipeSeeds` exports a map's first recipe from its C# placement literals
  (fidelity by construction — never transcribe coordinates by hand). Once a map is
  seeded, its file is the map; the seed lives on as the missing-file fallback, and
  `MapSeedTests` is the drift guard — once someone drags a placement in the editor,
  file and seed part company on purpose and the guard says so.
