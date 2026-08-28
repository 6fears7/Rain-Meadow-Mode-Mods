# Phase 4 — Corrupted / "failed warp" destinations — Overview

Parent plan: [watcher-warping-support.md](watcher-warping-support.md) (Phase 4 section, line 39).
Depends on Phases 1-3 being in place mechanically (any-avatar triggering, region/room transfer,
and un-gated filters) — Phase 4 only decides *what a WarpPoint's destination data says* and *how
that data gets onto a placed object in the first place*; it does not touch the trigger/transfer
pipeline itself.

## Correction to the parent plan's framing

The parent plan's one paragraph (line 39) reads as if this is purely a content task: "hand-place
dedicated warp points whose `WarpPointData` sets a fixed `destRegion`/`destRoom`... Building the
curated list itself is a content task, not a code task." That's true for Phase 6's curated-list
work, but it quietly assumes there's *somewhere* to hand-place a `PlacedObject` into — and this
repo has no such place:

- `WatcherWarps` is a pure BepInEx code plugin (`Mod/modinfo.json` lists no room/world content,
  just `Mod/plugins/WatcherWarps.dll`). There is no `mods/uo_watcherwarps/world/*-rooms/` folder
  shipping overridden `_settings.txt` files the way vanilla/Watcher rooms are authored.
- Every existing hook in `src/` (`ProgressionFilterGuardHooks.cs`, `WarpPointTriggerHooks.cs`,
  etc.) patches *behavior* on objects that already exist in a room from vanilla/Watcher's own
  room data. None of them create a new `PlacedObject` at runtime.

So "hand-place a dedicated warp point" cannot mean editing a room's `_settings.txt` the way
Phase 6's curated list assumes real Watcher rooms already do it — it has to mean **injecting a
`WarpPointData`-backed `PlacedObject` into a room's `RoomSettings` (or directly into a live
`Room`) from code**, at a location this mod controls. Whether that's even possible, and which of
several plausible injection points is right, is unconfirmed and is [[phase4-01-injection-mechanism-investigation]]
— it gates every other subtask here the same way Phase 3's hookability check did.

## Subtask sequence

1. [[phase4-01-injection-mechanism-investigation]] — determine how (and where) this mod can get a
   new `Watcher.WarpPoint`-backed `PlacedObject` into a room it does not ship content for: a
   post-hook on `RoomSettings` construction/`LoadPlacedObjects` that appends an in-memory
   `PlacedObject`, vs. constructing a live `Watcher.WarpPoint` `UpdatableAndDeletable` directly
   against an already-loaded `Room` and skipping `RoomSettings`/save-parity concerns entirely.
   Foundation subtask — the answer decides the hook style and the shape of subtask 2's data, and
   whether curated destinations (subtask 3) need to be tied to specific pre-existing rooms or can
   ride along any room this mod already hooks (e.g. wherever Phase 1's trigger-generalization
   already runs).
2. [[phase4-02-corrupted-warpdata-preset]] — once injection is possible, build the actual
   "corrupted destination" data: a `WarpPointData` with `effectSettings =
   EffectSettings.BadWarpCosmetics()` (confirmed at
   [plan/reference/Watcher.WarpPoint.decompiled.cs:280-299](reference/Watcher.WarpPoint.decompiled.cs#L280-L299))
   and a fixed, non-dynamic `destRegion`/`destRoom` (`cycleExpiry <= 0` →
   `nonDynamicWarpPoint`, decompiled.cs:385) rather than routing through vanilla's live
   `ChooseDynamicWarpTarget(badWarp: true)` RNG roll. This is the actual mechanical deliverable
   the parent plan called "Phase 4."
3. [[phase4-03-destination-curation-followup]] — the curated list of which rooms are legitimate
   "corrupted" destinations, and which rooms get a bad-warp point injected pointing at them. This
   is explicitly deferred content work per the parent plan (line 39) and overlaps with Phase 6's
   region-graph curation — flag the overlap rather than duplicating Phase 6's TSV analysis, and
   don't block subtasks 1-2 on it.

Subtask 2 depends on subtask 1's answer (what shape of code is even injecting the data). Subtask 3
depends on subtask 1 only insofar as it needs to know which rooms are viable injection targets;
otherwise it's an independent, deferrable decision task, not a code task.

## Note for next session

Updated 2026-08-26: [[phase4-01-injection-mechanism-investigation]] is resolved. Decompiling
`Room.cs` (`PUBLIC-Assembly-CSharp.dll` via `ilspycmd`) turned up a public, first-party API —
`Room.TrySpawnWarpPoint(PlacedObject po, bool saveInRegionState)` — that vanilla itself uses
(`Room.LoadWarpsFromSaveState()`) to spawn save-state-driven `WarpPoint`s that have no backing
room-content `PlacedObject` at all. No `RoomSettings` hook and no manual `Watcher.WarpPoint`
construction/list-registration is needed: a new `On.Room.Loaded` postfix building a throwaway
`PlacedObject` with our `WarpPointData` and calling `self.TrySpawnWarpPoint(po, saveInRegionState:
false)` handles construction, dedup, and object-list registration for free. One real constraint
surfaced: non-dynamic (`nonDynamicWarpPoint`) warp points additionally require the target room to
already contain *some* real `PlacedObject.Type.WarpPoint`/`SpinningTopSpot` referencing the same
`destRoom`, unless `oneWayExit = true` is set — see phase4-01's deliverable section for the full
writeup and what this means for room selection. [[phase4-02-corrupted-warpdata-preset]] proceeded
directly against this mechanism.

Updated 2026-08-26 (later): the `On.Room.Loaded` postfix is now wired —
`src/CorruptedWarpInjectionHooks.cs`, registered from `Warps.Apply()`. It's gated behind the usual
`MeadowGameMode` + Watcher-timeline check and currently only fires against one hardcoded
proof-of-concept room/destination pair (`warf_a06` -> `WARC`/`warc_b12`), clearly marked as a
placeholder to prove the mechanism, not curated content. Builds clean. **Not yet tested in-game** —
someone needs to actually load into `warf_a06` in a Meadow Watcher-timeline lobby and confirm the
injected corrupted warp point appears, is triggerable, and functions per Phases 1-3's pipeline.
Once that's confirmed, [[phase4-03-destination-curation-followup]]'s gating condition ("mechanism
confirmed end-to-end against a test room") is satisfied and real destination curation can start —
replacing the hardcoded test pair with a real lookup.
