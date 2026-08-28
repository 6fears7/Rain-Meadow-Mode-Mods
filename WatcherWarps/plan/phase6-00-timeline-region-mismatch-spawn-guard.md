# Phase 6 — Guard against spawning into a region the current timeline can't load

Parent: [watcher-warping-support.md](watcher-warping-support.md). New issue raised 2026-08-27,
independent of the warp-route work but living in the same mod because it's the same failure class
(Watcher-only regions leaking into a non-Watcher Meadow session).

## The bug

Rain Meadow persists each character's last position in
`MeadowProgression.progressionData.currentCharacterProgress.saveLocation` (a `WorldCoordinate`),
written on quit from `MeadowGameMode` (`GameModes/MeadowGameMode.cs:474`) into `meadow.json`.

On "start game" the menu turns that into the room the session loads into:

- `Menu/MeadowMenu.cs:334-349` `MeadowMenu.StartGame()` — sets
  `manager.menuSetup.startGameCondition = RegionSelect` and
  `manager.menuSetup.regionSelectRoom = saveLocation.ResolveRoomName()`.
  - Note lines 337-346 *try* to fall back to `MeadowProgression.defaultStartingRoom` when the room
    isn't in `RainWorld.roomIndexToName` / `roomNameToIndex`, but **line 349 then unconditionally
    overwrites `regionSelectRoom` with the raw `saveLocation.ResolveRoomName()` again** — the guard
    is dead code. Even if it worked, those dicts are built from every region present on disk
    regardless of timeline, so a Watcher room name resolves fine.
- `Meadow/MeadowPauseMenu.cs:191,200` — same assignment on the pause-menu "continue into saved
  spot" style paths (`ToOutskirts`/`ToHub` are explicit and safe; the resume paths are the risk).

`meadow.json` is a **single file, shared across all lobbies/timelines**. So: save your spot in a
Watcher region (e.g. `WARA`, `WARB`, `WARC`, `WARD`, `WARE`, `WARF`, `WARG`, `WAUA`, `WBLA`,
`WDSR`, `WGWR`, `WHIR`, `WPTA`, `WRFA`, `WRFB`, `WRRA`, `WRSA`, `WSKA`, `WSKB`, `WSKC`, `WSKD`,
`WSUR`, `WVWA`, `WTDA`, `WTDB`, `WSSR` …), quit, create/join a lobby on a **non-Watcher timeline**,
press start → the game tries to load a region that isn't in this timeline's region order.
`OverWorld..ctor` calls `Region.LoadAllRegions(PlayerTimelinePosition, game)` and later world-load
resolves the starting room against a region graph that doesn't contain it → NRE / crash on load-in.

## Desired behavior

If the resolved starting region is not valid for the lobby's current timeline, silently redirect
the spawn to Outskirts (`SU_C04`, the value of `MeadowProgression.defaultStartingRoom`), exactly
like `MeadowPauseMenu.ToOutskirts` already does:

```csharp
MeadowProgression.progressionData.currentCharacterProgress.saveLocation =
    new WorldCoordinate(suco4 /* RainWorld.roomNameToIndex["SU_C04"] */, -1, -1, 0);
manager.menuSetup.regionSelectRoom = "SU_C04";
```

Also overwrite the persisted `saveLocation` (not just the transient `regionSelectRoom`) so the
stale Watcher coordinate doesn't re-trigger this every launch, and so `MeadowGameMode.SpawnAvatar`
(`GameModes/MeadowGameMode.cs:80`, which swaps `location` back to `saveLocation` when the room
matches) doesn't undo the redirect.

## Timeline → valid regions

`SlugcatStats.getSlugcatStoryRegions(Name)` + `SlugcatStats.getSlugcatOptionalRegions(Name)` both
return `string[]` of region acronyms (verified via `ikdasm` against the installed
`Assembly-CSharp.dll`, 2026-08-27). Union of the two = every region a timeline's world can load.
The lobby timeline is `OnlineManager.lobby.meadowTimeline` (a slugcat-name string, `""` when unset);
convert with `new SlugcatStats.Name(meadowTimeline)`. `OnlineGameMode` already does exactly this in
`MeadowAvatarSettings`-adjacent helpers (`GameModes/OnlineGameMode.cs:215-228`).

Region acronym of the target room = substring before the first `_` in the resolved room name
(vanilla convention; `Region.GetRegionFullName` / `RainWorld.roomNameToIndex` follow it).

## Why this mod, not a Rain Meadow PR

Same reasoning as the rest of WatcherWarps' guard hooks — the Watcher regions only exist because
the Watcher mod + this ecosystem installed them; base Rain Meadow has no concept of them. Keep the
mitigation with the mod that introduces the hazard. (A separate note: the dead-code guard at
`MeadowMenu.cs:337-349` is an upstream bug worth reporting regardless.)

## Guard scope

Unlike every existing hook in this mod, this one must **NOT** gate on `Warps.IsMeadowWatcher()` —
the broken case is precisely when the timeline is *not* Watcher. Gate on
`OnlineManager.lobby?.gameMode is MeadowGameMode` only, then compare the saved region against
whatever the current timeline allows. This also catches non-Watcher mismatches for free
(e.g. an MSC region saved under a vanilla timeline).

## Subtask sequence

1. [[phase6-01-hook-target-investigation]] — confirm the exact set of methods that set
   `manager.menuSetup.regionSelectRoom` before a `ProcessID.Game` switch in a Meadow lobby
   (`MeadowMenu.StartGame`, the two `MeadowPauseMenu` resume paths, and whatever the
   `FastTravelScreen` → Game path uses). Decide: patch each call site, or patch one deeper
   chokepoint. Read-only.
2. [[phase6-02-region-validity-helper]] — add `Warps.RegionLoadableInCurrentTimeline(string
   roomOrRegion)` (story ∪ optional region set for `new SlugcatStats.Name(lobby.meadowTimeline)`),
   plus `Warps.RedirectSaveToOutskirts()` mirroring `MeadowPauseMenu.ToOutskirts`. Unit-testable
   in isolation from the hook.
3. [[phase6-03-spawn-guard-hook]] — new `src/TimelineRegionGuardHooks.cs`, registered in
   `Warps.Apply()`. Postfix the targets from 6.1; if `regionSelectRoom`'s region fails 6.2's
   check, call `RedirectSaveToOutskirts()` and log a warning. Depends on 6.1 + 6.2.
4. [[phase6-04-ingame-verification]] — save in a Watcher region, relaunch on the White timeline,
   confirm you wake in Outskirts with no crash and `meadow.json` now points at `SU_C04`.

## Status (2026-08-28)

Steps 1-3 DONE. `src/TimelineRegionGuardHooks.cs` created and registered in `Warps.Apply()`;
builds clean.

- 6.1: only risk site is `MeadowMenu.StartGame` (postfix). Pause-menu paths are safe; no resume
  button; FastTravelScreen never sets `regionSelectRoom`. Details in that file.
- 6.2: helpers live in `TimelineRegionGuardHooks` (not `Warps.cs`) to keep the class
  self-contained: `RegionLoadableInCurrentTimeline(string)` +
  `RedirectSaveToOutskirts(ProcessManager)`. Note `SlugcatStats.getSlugcatStoryRegions` /
  `getSlugcatOptionalRegions` are `[Obsolete]` in the installed assembly — used the renamed
  `SlugcatStoryRegions` / `SlugcatOptionalRegions` (return `List<string>`, not `string[]`).
- 6.3: postfix gates on `OnlineManager.lobby?.gameMode is MeadowGameMode` only (NOT
  `IsMeadowWatcher()`). `MeadowProgression.saveLocation` and `SaveProgression()` are `internal`
  and this mod links the non-publicised `Rain Meadow.dll`, so both are reached via
  `AccessTools` reflection (wrapped in try/catch — a failed re-save still leaves the launch
  safe).

REMAINING: step 4 [[phase6-04-ingame-verification]] — in-game test still not run.

## Note for next session

Created 2026-08-27. Investigation done up front: root cause pinned to the shared single-file
`meadow.json` `saveLocation` + the dead fallback guard at `MeadowMenu.cs:337-349`; fix pattern
lifted from `MeadowPauseMenu.ToOutskirts`; timeline→region API (`SlugcatStats.getSlugcatStoryRegions`
/ `getSlugcatOptionalRegions`) confirmed by disassembly. Start at 6.1. Nothing here is blocked.
The one open design question is 6.1's "each call site vs. one chokepoint" — leaning per-call-site
(`MeadowMenu.StartGame` + `MeadowPauseMenu` x2) since the engine-side coroutine that reads
`regionSelectRoom` is a fragile IEnumerator to patch.
