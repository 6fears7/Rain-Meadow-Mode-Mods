# Phase 3 — Disable progression gating on warp availability — Overview

Parent plan: [watcher-warping-support.md](watcher-warping-support.md) (Phase 3 section, lines
35-36). Independent of Phase 1/2's runtime `WarpPoint` state machine — this phase is about
whether a placed `WarpPoint` (or any other deactivatable placed object) is `active` at all when
a room loads, not about triggering/transfer. Can be developed in parallel with Phase 2, but
should be verified against a real placed filter object, which needs Phase 1's triggering to
actually reach a filtered `WarpPoint` in-game.

## Correction to the parent plan's framing

The parent plan describes this as patching `PlacedObject.deactivatedByWarpFilter` "to always
report the requirement met," modeled on `RegionGate_MeetRequirement`. Decompiling the actual
mechanism (`PUBLIC-Assembly-CSharp.dll` via `ilspycmd`, `PlacedObject.cs` and `RoomSettings.cs`)
shows the real shape is different in two ways:

1. **It's a one-time bulk pass at room-settings load, not a per-tick/per-object runtime check.**
   `RoomSettings.LoadPlacedObjects(string[] s, SlugcatStats.Timeline timelinePoint)`
   (`RoomSettings.cs:1533-1584`) runs once, from inside the `RoomSettings(Room room, string
   name, Region region, bool template, bool firstTemplate, SlugcatStats.Timeline timelinePoint,
   RainWorldGame game)` constructor (`RoomSettings.cs:1012`, calling `LoadPlacedObjects` at line
   1433). It builds two lists while parsing the room's placed objects:
   - `list`: every placed object whose `.data is PlacedObject.GenericFilterData` (the
     `RippleLevelFilterData`/`PrinceFilterData`/`RippleEggFilterData`/`FilterData`/
     `CompetitiveFilterData` markers) **whose `Active(roomSettings, timelinePoint)` returned
     `false`**.
   - `list2`: every placed object of `PlacedObject.Type.WarpFilter` specifically (unconditional,
     no `Active()` check at all — see point 2 below).

   Then, for every *other* deactivatable placed object in the room within a filter's radius
   (`Custom.DistLess(pos, filter.owner.pos, filter.Rad)`), each `list` entry calls
   `DeactivatePlacedObject(pObj)` (default: `pObj.active = false`), and each `list2` entry sets
   `pObj.deactivatedByWarpFilter = true` directly. **There is nothing to patch per-`WarpPoint`
   at runtime** — the fix has to happen inside (or before) this one parse pass, which means the
   hook target is `RoomSettings.LoadPlacedObjects` or the individual filter classes' `Active()`
   overrides, not `Watcher.WarpPoint` itself. This reframes Phase 3 as a `RoomSettings`/
   `PlacedObject` hook, structurally unrelated to Phases 1/2's `Watcher.WarpPoint` hooks.

2. **`PlacedObject.Type.WarpFilter` is not a progression gate — it's a separate, unconditional
   radius-based deactivation zone**, evaluated by simple distance (`list2` above), never
   consulting `GenericFilterData.Active()` at all. It has no save-state/progression logic of its
   own; it exists in 18 rooms in the Watcher room data (vs. 121 rooms using one of the three real
   progression filters), likely for level-design reasons (e.g. deactivating a `WarpPoint`
   unconditionally in a specific room layout) unrelated to ripple level, Prince encounters, or
   ripple-egg count. Lumping it in with the other three under "progression gating" is the parent
   plan's framing, not the actual code's — it needs its own decision, not the same treatment.
   This is [[phase3-03-warpfilter-scope-decision]].

3. **The three real progression filters already return `false` unconditionally outside a story
   session today** — this is the actual bug Phase 3 fixes, and it's more severe than "gated by
   progression": `RippleLevelFilterData.Active`, `PrinceFilterData.Active`, and
   `RippleEggFilterData.Active` (`PlacedObject.cs:1237-1358`) all start with `if
   (roomSettings.game == null || !roomSettings.game.IsStorySession) { return false; }`. Since a
   Meadow lobby is never a story session, **every placed object within range of one of these
   three filters is already being force-deactivated in every Meadow lobby today**, regardless of
   species or timeline — not "gated by ripple level," but unconditionally off. This is
   [[phase3-02-progression-filter-guard]].

## Hookability is unconfirmed and gates everything else

Unlike `Watcher.WarpPoint` (a top-level class, already proven hookable via `On.Watcher.WarpPoint.*`
in Phase 1/Story mode), `RoomSettings.LoadPlacedObjects` and the `GenericFilterData` subclasses
are nested types (`PlacedObject.RippleLevelFilterData`, etc.) whose `Active` override is a
`public override bool` on a class nested inside `PlacedObject`. Whether BepInEx's MonoMod
HookGen produces an `On.RoomSettings.LoadPlacedObjects` / `On.PlacedObject.RippleLevelFilterData.Active`
stub for these — or whether a manual `MonoMod.RuntimeDetour.Hook` against the nested type is
needed instead — has not been checked. This is [[phase3-01-hook-target-investigation]] and must
be done first; it decides the hook style every other subtask in this phase writes against.

## Mod structure

Same as Phases 1/2: new `On.*` (or manual `Hook`) code added to the standalone `WatcherWarps/`
project, guarded by the same `OnlineManager.lobby?.gameMode is MeadowGameMode &&
OnlineManager.lobby.meadowTimeline == Watcher.WatcherEnums.SlugcatStatsName.Watcher.value` check
established in Phase 1, so story-mode `RoomSettings` parsing (and non-Watcher-timeline Meadow
lobbies) keep the vanilla filter behavior untouched.

## Subtask sequence

1. [[phase3-01-hook-target-investigation]] — confirm whether `RoomSettings.LoadPlacedObjects`
   and/or the three filter `Active()` overrides are reachable via HookGen `On.*` stubs or need a
   manual `RuntimeDetour.Hook`, and confirm `roomSettings.game` is a live, already-assigned
   per-session `RainWorldGame` at the point `Active()` runs (so `OnlineManager.lobby` reads are
   valid there). Foundation subtask — every other subtask's hook code depends on the answer.
2. [[phase3-02-progression-filter-guard]] — the actual fix: make `RippleLevelFilterData.Active`,
   `PrinceFilterData.Active`, and `RippleEggFilterData.Active` report `true` under the Meadow +
   Watcher guard instead of their real (currently always-`false`-outside-story-mode) logic. This
   is the core of what the parent plan called "Phase 3."
3. [[phase3-03-warpfilter-scope-decision]] — separate investigation + user decision on whether
   `PlacedObject.Type.WarpFilter`'s unconditional radius-based deactivation should also be
   suppressed under Meadow, since it isn't progression-gated and disabling it may not match what
   those 18 rooms' level design intended. Do not fold this into subtask 2's fix without an
   explicit decision.

Subtask 2 depends only on subtask 1's answer (same hook style). Subtask 3 is independent of
subtask 2's code but depends on subtask 1 for the same reason, and is a decision task before it's
a code task — it may conclude "leave `WarpFilter` untouched," which is a valid outcome, not a
placeholder for more code.

## Note for next session

Nothing implemented yet as of 2026-08-26 — this overview and its three subtask files are the
full Phase 3 breakdown, based on decompiling `RoomSettings.cs` and the `PlacedObject` filter
classes (`PlacedObject.cs:1079-1385`, `RoomSettings.cs:1002-1584`) via `ilspycmd` against
`PUBLIC-Assembly-CSharp.dll`. Start with phase3-01 (hook-target investigation) — it determines
the hook style for both remaining subtasks. Phase 3 has no code dependency on Phase 1/2's
subtask files, but verifying it in-game does need a Phase-1-triggerable non-`Player` avatar
reaching a filtered `WarpPoint`, so end-to-end verification should wait until Phase 1 is
in-game-tested.
