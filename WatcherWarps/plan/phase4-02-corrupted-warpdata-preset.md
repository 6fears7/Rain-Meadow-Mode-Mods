# Phase 4.2 — Corrupted-destination WarpPointData preset

Parent: [phase4-00-overview.md](phase4-00-overview.md). Depends on
[[phase4-01-injection-mechanism-investigation]]'s answer for how the resulting `WarpPointData`
actually gets attached to a live `WarpPoint` — this subtask is only about what the data itself
should contain and how to build it in code.

## What this subtask is

Write the actual `WarpPointData` construction for a "corrupted / failed warp" destination, mirroring
the parent plan's intent (line 39) but as code instead of hand-authored room-file content, since
[[phase4-00-overview]] established this mod has no content pipeline to author `_settings.txt`
entries into.

## Confirmed data shape (from decompiled reference)

- `EffectSettings.BadWarpCosmetics()`
  ([plan/reference/Watcher.WarpPoint.decompiled.cs:280-299](reference/Watcher.WarpPoint.decompiled.cs#L280-L299))
  is a ready-made static preset — `vignette=10, darkness=10, spiralGap=15, swirlIntensity=2,
  lensingIntensity=8, noiseIntensity=4, spiralTwist=15, agitationSpeed=4, spaghettification=10,
  outerRimCosmetic=false, badWarpCosmetic=true, spawnBigRift=true, activeDuration=150,
  triggerDuration=150`. Use this directly (`WarpPointData.EffectSettings.BadWarpCosmetics()`) —
  don't hand-roll the field values; that's exactly what the parent plan meant by "reusing vanilla's
  red/corrupted visual preset."
- `nonDynamicWarpPoint` is a derived getter, `cycleExpiry <= 0`
  (decompiled.cs:385) — a hand-authored fixed destination must set `cycleExpiry = 0` (the
  `WarpPointData(PlacedObject owner)` constructor already defaults it to `0`, decompiled.cs:413, so
  this likely needs no explicit override, just confirm it isn't reassigned elsewhere in the
  construction path).
- Fixed destination: set `destRegion` and `destRoom` directly (not `RegionString`, which is a
  derived property — decompiled.cs:389-407 — that falls back to parsing `destRoom`'s prefix when
  `destRegion` is null; setting `destRegion` explicitly is more robust and matches how vanilla's own
  `ChooseDynamicWarpTarget` non-dynamic case sets both).
- Do **not** route through `ChooseDynamicWarpTarget(badWarp: true)`
  (decompiled.cs:1133, 2008) or `GetAvailableBadWarpTargets` — those consult
  `RainWorldGame.GetStorySession.saveState` counters (decompiled.cs:1153) that don't exist in a
  Meadow session and would need per-client RNG-seed sync this plan explicitly avoids (parent plan
  line 17, 39).
- `accessibility` defaults to `WarpPointSpawnCondition.WatcherOnly` (decompiled.cs:343) — leave as
  default; Phase 5's Watcher-timeline gate already ensures the whole feature is off outside a
  Watcher-timeline lobby, so this field doesn't need touching.
- `uuidPair` is auto-generated per `WarpPointData` instance in its constructor (`Guid.NewGuid()`,
  decompiled.cs:412) — vanilla uses this to pair reciprocal two-way warp points
  (decompiled.cs:1908-1951 matches on `uuidPair` to find/update a paired exit). A one-off injected
  corrupted destination has no reciprocal partner to pair with; confirm leaving it as a fresh
  random GUID (i.e. unpaired) doesn't cause vanilla code to look for a nonexistent partner and
  throw/null-ref — check the `uuidPair`-matching call sites' null/no-match handling before shipping.

## What this subtask needs to decide

1. **Where in code this construction lives** — a small static helper (e.g. `Warps.cs`, which
   already exists in `src/` as a small shared-helpers file per the current codebase layout) that
   returns a ready-to-use `WarpPointData` given a `(destRegion, destRoom)` pair, so
   [[phase4-01-injection-mechanism-investigation]]'s chosen insertion point just calls it. Don't
   duplicate the field-by-field construction at each injection site.
2. **One-way or two-way.** A corrupted/failed warp destination is thematically one-directional in
   vanilla (you fall into a bad warp, you don't get a matching return trip for free) — decide
   whether to set `oneWay = true` / `oneWayEntrance` appropriately (decompiled.cs:359-363,373-383)
   so the destination room doesn't need its own reciprocal `WarpPoint` authored back. Cheaper than
   building a paired warp point, and matches vanilla's own bad-warp flavor.
3. **`destPos`** — vanilla bad-warps use `null` and let the room pick a default entry point
   (decompiled.cs:335, checked in `FromString`/`ToString` for `NULL` handling at
   decompiled.cs:583-593, 703-704) unless a specific landing spot inside the destination room
   matters for the curated room chosen in [[phase4-03-destination-curation-followup]]. Leave `null`
   unless a specific destination room needs a specific spawn point.

## Deliverable

A small helper producing a `WarpPointData` (or the `PlacedObject` wrapping it, depending on
subtask 1's answer) for a given fixed `(destRegion, destRoom)` pair, with
`EffectSettings.BadWarpCosmetics()` applied and the one-way/`destPos` decisions above made
explicit — ready for [[phase4-01-injection-mechanism-investigation]]'s chosen insertion point to
call once a destination is chosen.

## Note for next session

Implemented 2026-08-26: `Warps.BuildCorruptedWarpPlacedObject(string destRegion, string destRoom)`
in `src/Warps.cs` builds `new PlacedObject(PlacedObject.Type.WarpPoint, null)` +
`Watcher.WarpPoint.WarpPointData(po)` with `effectSettings = EffectSettings.BadWarpCosmetics()`,
fixed `destRegion`/`destRoom`, `destPos = null`, and `oneWay = true, oneWayEntrance = false` (so
`oneWayExit` is true — matches subtask decision 2, and also bypasses `TrySpawnWarpPoint`'s
non-dynamic destRoom-already-referenced gate found in
[[phase4-01-injection-mechanism-investigation]]). `cycleExpiry` is left at the constructor's
default `0` (`nonDynamicWarpPoint` true), per the confirmed data shape above — not reassigned.
`PlacedObject.data` is set explicitly after construction rather than relying on
`PlacedObject.GenerateEmptyData()` (confirmed via `ilspycmd -t PlacedObject` that it has no
`WarpPoint`-specific branch — it would leave `data` as some other `Data` subtype or null).
Builds clean (`dotnet build`, 0 errors, no new warnings).

Next: wire this into the `On.Room.Loaded` postfix
[[phase4-01-injection-mechanism-investigation]] specified (new
`src/CorruptedWarpInjectionHooks.cs`, calling `room.TrySpawnWarpPoint(po, saveInRegionState:
false)`), which is where [[phase4-03-destination-curation-followup]]'s room/destination choice
gets consumed — not yet done, and not part of this subtask's deliverable.
