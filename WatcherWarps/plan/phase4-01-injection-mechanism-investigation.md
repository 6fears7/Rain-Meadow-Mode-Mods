# Phase 4.1 — Injection mechanism investigation

Parent: [phase4-00-overview.md](phase4-00-overview.md). Foundation subtask — do first; subtasks 2
and 3 both depend on which mechanism this settles on.

## Question

Since `WatcherWarps` ships no room content of its own (confirmed in [[phase4-00-overview]] — no
`world/*-rooms/` folder, `Mod/modinfo.json` lists only the plugin DLL), how does a curated
"corrupted destination" `Watcher.WarpPoint` actually get placed into an existing vanilla/Watcher
room at runtime, entirely from code?

## What's already confirmed

1. **`Watcher.WarpPoint` always needs a backing `PlacedObject`.** Its only constructor is
   `WarpPoint(Room room, PlacedObject placedObject)`
   ([plan/reference/Watcher.WarpPoint.decompiled.cs:1063](reference/Watcher.WarpPoint.decompiled.cs#L1063)).
   There is no data-only/parameterless path — whatever mechanism this subtask picks has to produce
   a real `PlacedObject` (with a `WarpPointData` payload) before a `WarpPoint` behavior object can
   exist at all.
2. **`RoomSettings`'s constructor is already proven hookable, in both directions, by the sibling
   Rain-Meadow repo.** `On.RoomSettings.ctor_Room_string_Region_bool_bool_Timeline_RainWorldGame`
   and the matching `IL.` hook are both used today at
   `/home/preston/repos/Rain-Meadow/Game/RainMeadow.GameHooks.cs:49-50,543-551` (an on-hook that
   rewrites `timelinePoint` before calling `orig`). This confirms the constructor family is safe to
   wrap with an `On.*` postfix — the open question isn't "can this constructor be hooked," it's
   "does appending to `self.placedObjects` after `orig()` returns actually get realized into a
   live `WarpPoint` the same way a room-authored one would."
3. **No existing precedent anywhere in this repo or the sibling Rain-Meadow repo for injecting a
   brand-new `PlacedObject` into a room at runtime.** Grepped both repos for
   `placedObjects.Add`/`new PlacedObject(` outside of the game's own decompiled source — nothing.
   This is genuinely new ground for both codebases, not a "the pattern exists, just find it" task.

## What's still open

1. **Does appending a `PlacedObject` to `RoomSettings.placedObjects` after `orig()` runs (in a
   postfix on the `RoomSettings` constructor) get picked up by whatever *later* code iterates
   `roomSettings.placedObjects` to realize objects into the room** (the normal per-room object
   construction pass — locate it, likely in `Room.Loaded()` or `AbstractRoom`/`Room` ctor, that
   turns each `PlacedObject` whose `.active` is true into a live behavior object such as
   `WarpPoint`)? If that pass runs strictly during/after the `RoomSettings` constructor returns and
   just walks the list by index, a postfix append should work. If it snapshots the list earlier
   (e.g. captures `.Count` before the postfix fires, or the realization pass runs inside a
   different, earlier-firing constructor/method entirely), appending post-hoc won't construct
   anything and a different insertion point is needed — check this by decompiling whatever calls
   `RoomSettings.LoadPlacedObjects`'s output next, not by assuming.
2. **Alternative: skip `RoomSettings`/`PlacedObject` realization entirely and construct
   `Watcher.WarpPoint` directly against an already-loaded `Room`.** Since the constructor only
   needs `(Room room, PlacedObject placedObject)`, a hook on something that fires once per fully
   loaded room (e.g. `On.Room.Loaded`, or wherever Phase 1/2's existing hooks already run per-room
   — check `src/WarpPointTriggerHooks.cs` and `src/WorldLoadedHooks.cs` for a room-loaded hook this
   phase could piggyback on) could manually build a throwaway `PlacedObject` with a hand-authored
   `WarpPointData`, `new Watcher.WarpPoint(room, thatPlacedObject)`, and add it to
   `room.updateObjectList` (or whatever list vanilla's own realization pass adds a `WarpPoint`
   into — confirm by checking what the vanilla realization pass does with the constructed object,
   not just the `WarpPoint` constructor itself). This sidesteps question 1's ordering risk
   entirely, at the cost of re-implementing a small slice of what `RoomSettings` realization
   already does (e.g. whether it also needs `room.AddObject(warpPoint)` specifically, versus some
   other insertion API — check vanilla's realization call site for the exact method used).
3. **Per-client duplication risk.** Whichever mechanism is chosen must only run once per room load
   per client, under the same `MeadowGameMode` + Watcher-timeline guard established in Phase 1 —
   confirm the chosen hook doesn't re-fire and re-inject a duplicate `WarpPoint` on room
   re-entry/re-realization (e.g. leaving/re-entering the same room in one session), since nothing
   here goes through save-state persistence the way a vanilla room-authored object would.
4. **Which rooms need this.** This subtask is about the *mechanism*, not the destination list —
   but proving the mechanism requires picking one real test room to inject into. Use any
   convenient, already-reachable Watcher room for verification; defer the actual curated list to
   [[phase4-03-destination-curation-followup]].

## Deliverable

A short note (append below once done) stating: which insertion point was chosen (postfix on
`RoomSettings` ctor appending to `placedObjects`, vs. direct `Watcher.WarpPoint` construction
against an already-loaded `Room`), the exact hook target and list/method used to make the
constructed `WarpPoint` actually participate in the room (visible, updating, triggerable by Phase
1's generalized triggering), and confirmation that re-entering the same room doesn't duplicate it.
That decision is what [[phase4-02-corrupted-warpdata-preset]] codes against.

## Deliverable (found 2026-08-26)

Decompiled `Room` from
`~/.local/share/Steam/steamapps/common/Rain World/BepInEx/utils/PUBLIC-Assembly-CSharp.dll` via
`ilspycmd -t Room`. Neither speculated mechanism (postfix-append to `RoomSettings.placedObjects`,
or manually constructing `Watcher.WarpPoint` + hand-rolling its list registration) is needed —
vanilla already ships a public, first-party API for exactly this, and this repo should call it
directly instead of reinventing any part of it.

**Chosen mechanism: `On.Room.Loaded` postfix calling `Room.TrySpawnWarpPoint(PlacedObject po, bool
saveInRegionState = true)` directly against the already-loaded `Room`.** No `RoomSettings`
involvement at all.

Evidence, from `Room.cs`:

1. `Room.Loaded()` (decompiled.cs line 768) calls `LoadWarpsFromSaveState()` (line 1270, inside a
   two-pass loop, right before the `roomSettings.placedObjects` realization loop starts at line
   1272). `LoadWarpsFromSaveState()` (line 3264) is vanilla's own mechanism for spawning
   `WarpPoint`s that have **no backing `PlacedObject` in room content at all** — it reads
   `game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints` /
   `...deathPersistentSaveData.spawnedWarpPoints`, builds a throwaway `new
   PlacedObject(PlacedObject.Type.WarpPoint, null)` with hand-populated `WarpPointData`, and calls
   `TrySpawnWarpPoint(placedObject, saveInRegionState: false)` on it (lines 3288-3291,
   3298-3301). This is vanilla proving its own pattern: a save-state-driven (not
   room-content-driven) `PlacedObject` is a normal, supported way to get a `WarpPoint` into a room.
2. `TrySpawnWarpPoint(PlacedObject po, bool saveInRegionState = true)` (line 3316) is public, takes
   exactly the `PlacedObject` shape [[phase4-02-corrupted-warpdata-preset]] needs to build, and
   does three things useful to us for free:
   - Dedups against `warpPoints` (the room's live list) by `WarpPoint.IdentifyingString(...)` and by
     `destRoom` match (lines 3320-3330) — if an equivalent warp already exists, it returns the
     existing one instead of creating a duplicate.
   - For non-dynamic warp points (`nonDynamicWarpPoint == true`, which is what
     [[phase4-02-corrupted-warpdata-preset]] wants for a fixed corrupted destination) it also
     requires a matching `PlacedObject.Type.WarpPoint` (or `SpinningTopSpot`) already exist in
     `roomSettings.placedObjects` with the same `destRoom` (lines 3331-3351) as an extra sanity
     gate — **this means a purely-injected non-dynamic warp with a `destRoom` not already
     referenced by any real placed object in that room will return `null` and silently not spawn**.
     This is the one real constraint the mechanism imposes; noted for subtask 2/3 to work around
     (e.g. either pick injection rooms that already contain some vanilla warp point referencing the
     same room, or set `oneWayExit = true` to skip this check, per the same condition line).
   - Delegates to `ForceSpawnWarpPoint(po, saveInRegionState)` (line 3355), which does
     `new WarpPoint(this, po)` (satisfies the constructor requirement from item 1 above) and then
     `AddObject(warpPoint)` (line 3405).
3. `Room.AddObject` (line 4660-4662) does `if (obj is WarpPoint) warpPoints.Add(obj as WarpPoint)`
   — the same list every other room-authored `WarpPoint` ends up in, and (per the base
   `UpdatableAndDeletable` add path) into `updateList`, so it updates via `WarpPoint.Update` every
   tick exactly like a real one. Concretely this means Phase 1's existing
   `On.Watcher.WarpPoint.Update` hook in `src/WarpPointTriggerHooks.cs` requires **no changes** —
   an injected `WarpPoint` is indistinguishable from a room-authored one to that hook or to
   anything else that walks `room.warpPoints`/`room.updateList`.

**Duplication risk (open question 3): resolved, no extra guard code needed.** `Room` instances are
recreated fresh each time a room is loaded/re-entered (`warpPoints = new List<WarpPoint>()` in the
`Room` ctor, line 732), and `Loaded()` runs exactly once per `Room` instance. Calling
`self.TrySpawnWarpPoint(...)` exactly once from an `On.Room.Loaded` postfix therefore fires exactly
once per room load, with no possibility of firing twice within the same `Room` instance — this
sidesteps the ordering/snapshotting risk the `RoomSettings`-postfix alternative (open question 1)
would have had, since we're not racing a separate realization pass at all. Use
`saveInRegionState: false` (matching vanilla's own `LoadWarpsFromSaveState` calls) so the injected
corrupted warp isn't written into `SaveState.miscWorldSaveData`/`deathPersistentSaveData` — it
should be re-derived from code on every load, not persisted, since [[phase4-02-corrupted-warpdata-preset]]'s
data is fully static/deterministic per room and doesn't need save-state round-tripping.

**No existing `On.Room.Loaded` hook in this repo** (checked `src/WorldLoadedHooks.cs` — that hooks
`On.OverWorld.WorldLoaded`, a different, world-level event; `src/WarpPointTriggerHooks.cs` hooks
`On.Watcher.WarpPoint.Update`, not room load). This will be the first hook of its kind in
`WatcherWarps`; a new file (e.g. `src/CorruptedWarpInjectionHooks.cs`) registered from
`WatcherWarpsPlugin.cs` alongside the other `*.Apply()` calls is the natural location, matching this
repo's existing one-hook-per-file convention.

**Answer to the deliverable's original questions:**
- Insertion point: direct `Room.TrySpawnWarpPoint` call, not a `RoomSettings` postfix.
- Exact hook target: `On.Room.Loaded` postfix.
- List/method that registers it: `Room.AddObject` (called internally by `ForceSpawnWarpPoint`, in
  turn called by `TrySpawnWarpPoint`) — no manual list manipulation needed on our side.
- Re-entry duplication: confirmed not possible by construction (fresh `Room`/`warpPoints` per load,
  one `Loaded()` call per instance).
- Test room: deferred to [[phase4-03-destination-curation-followup]] as planned; the non-dynamic
  `destRoom`-must-already-be-referenced constraint (point 2 above) narrows which rooms are cheap
  first choices — prefer a room that already has *any* vanilla `WarpPoint` placed object (regardless
  of that object's own destination) for the first proof-of-concept, or set `oneWayExit = true` on
  the injected data to bypass the check entirely.

[[phase4-02-corrupted-warpdata-preset]] can now proceed: it needs to build a `PlacedObject.Type.WarpPoint`-typed
`PlacedObject` with a `WarpPoint.WarpPointData` payload (`effectSettings =
EffectSettings.BadWarpCosmetics()`, fixed `destRegion`/`destRoom`, `nonDynamicWarpPoint` implied by
`cycleExpiry <= 0`, and per point 2 above either targeting a `destRoom` already referenced by a real
warp in the injection room or setting `oneWayExit = true`), then pass it to
`room.TrySpawnWarpPoint(po, saveInRegionState: false)` from the new `On.Room.Loaded` postfix.
