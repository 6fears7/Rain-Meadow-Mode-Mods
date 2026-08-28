# Phase 3.2 — Progression filter guard

Parent: [phase3-00-overview.md](phase3-00-overview.md). Depends on
[[phase3-01-hook-target-investigation]]'s answer for hook style. This is the actual fix the
parent design doc called "Phase 3."

## What needs to change

Three `PlacedObject.GenericFilterData` subclasses, all in the decompiled `PlacedObject.cs`
(persist a decompiled copy alongside [plan/reference/Watcher.WarpPoint.decompiled.cs](reference/Watcher.WarpPoint.decompiled.cs)
if not already done by subtask 1 — re-derive via `ilspycmd -t "PlacedObject+RippleLevelFilterData"
PUBLIC-Assembly-CSharp.dll`, which dumps the whole containing `PlacedObject` class):

1. **`RippleLevelFilterData.Active`** (`PlacedObject.cs:1237-1248`):
   ```csharp
   public override bool Active(RoomSettings roomSettings, SlugcatStats.Timeline timelinePoint)
   {
       if (roomSettings.game == null || !roomSettings.game.IsStorySession) { return false; }
       if (roomSettings.game.GetStorySession.saveState.deathPersistentSaveData.rippleLevel >= minimumRippleLevel)
           return roomSettings.game.GetStorySession.saveState.deathPersistentSaveData.rippleLevel <= maximumRippleLevel;
       return false;
   }
   ```
2. **`PrinceFilterData.Active`** (`PlacedObject.cs:1285-1304`) — same `!IsStorySession → false`
   guard, then branches on `postGame`/`greaterOrEqual` against
   `saveState.miscWorldSaveData.numberOfPrinceEncounters`.
3. **`RippleEggFilterData.Active`** (`PlacedObject.cs:1347-1358`) — same `!IsStorySession →
   false` guard, then checks `saveState.miscWorldSaveData.hasRippleEggWarpAbility`.

All three need to return `true` (not run their real save-state logic at all) when
`OnlineManager.lobby?.gameMode is MeadowGameMode && OnlineManager.lobby.meadowTimeline ==
Watcher.WatcherEnums.SlugcatStatsName.Watcher.value` — the same guard from Phase 1. Outside that
guard (story mode, or a Meadow lobby on a non-Watcher timeline), fall through to `orig(...)` so
vanilla behavior — including the existing always-`false`-outside-story-mode gate — is unchanged.

## Gotcha: `RippleEggFilterData` has a non-default `DeactivatePlacedObject` override

Unlike the other two (which use the inherited default `DeactivatePlacedObject` → `pObj.active =
false`), `RippleEggFilterData.DeactivatePlacedObject` (`PlacedObject.cs:1360-1366`) instead sets
`(pObj.data as RippleSpawnEggData).threshold = threshold` — it doesn't deactivate the object at
all, it configures a different threshold value on it. This only matters if `Active()` returns
`false` (that's what causes `DeactivatePlacedObject` to be called at all, per
`RoomSettings.LoadPlacedObjects`'s `list`-building loop in [[phase3-00-overview]]). Making
`Active()` return `true` under the Meadow guard means `DeactivatePlacedObject` is simply never
called for that filter instance in a Watcher-timeline Meadow lobby — no separate handling needed,
just confirm the guarded `true` return skips the whole `list`-add branch as intended, not just
suppresses the deactivation call some other way.

## Verification

1. Place a `RippleLevelFilter`/`PrinceFilter`/`RippleEggFilter` near a `WarpPoint` in a test
   room (or find an existing room in the 121 identified by grepping `mods/watcher/world/*-rooms/
   *_settings.txt` for these filter type names — see [[phase3-00-overview]]'s point 3).
2. Load that room in a Meadow lobby created with the Watcher timeline: confirm the nearby
   `WarpPoint` stays `active` (or, for the ripple-egg case, that the `RippleSpawnEggData`
   threshold isn't overwritten) regardless of the local save's ripple level / Prince encounters /
   ripple-egg-warp-ability flag.
3. Load the same room in a **story-mode** session (or a Meadow lobby on a non-Watcher timeline):
   confirm the filter still deactivates nearby objects exactly as before — this is the primary
   regression check, since the guard must not change behavior for anyone outside the target
   scenario.

## Note for next session

Implemented 2026-08-26. `src/ProgressionFilterGuardHooks.cs` adds three `On.*` postfix-style
hooks (each calls `orig` and either returns `true` under the Meadow+Watcher guard or falls
through to `orig`'s return value) on `On.PlacedObject.RippleLevelFilterData.Active`,
`On.PlacedObject.PrinceFilterData.Active`, and `On.PlacedObject.RippleEggFilterData.Active`,
matching phase3-01's decision. `ProgressionFilterGuardHooks.Apply()` is wired into
`Warps.Apply` in `src/Warps.cs`. Build succeeds (`dotnet build`), which also confirms the
three HookGen stubs exist with the expected signatures — no manual `RuntimeDetour.Hook`
needed, matching phase3-01's finding.

Not yet verified in-game (see this file's "Verification" section above) — that needs a test
room with one of the three filter types near a `WarpPoint`, loaded in a Meadow lobby on the
Watcher timeline, and separately in story mode as a regression check. Phase 3.3
([[phase3-03-warpfilter-scope-decision]]) is still open and independent of this change.
