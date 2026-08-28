# Phase 6.1 — Hook target investigation (read-only)

Parent: [[phase6-00-timeline-region-mismatch-spawn-guard]]

## Goal

Enumerate every code path that, in a Meadow lobby, sets `manager.menuSetup.regionSelectRoom`
(or otherwise picks the load-in room) and then triggers `ProcessManager.ProcessID.Game`. Pick
where to patch.

## Known call sites (from grep, 2026-08-27, sibling repo /home/preston/repos/Rain-Meadow)

- `Menu/MeadowMenu.cs:334-350` `MeadowMenu.StartGame()` — primary path (fresh start from menu).
- `Meadow/MeadowPauseMenu.cs:191` and `:200` — `ToOutskirts` / `ToHub`. These *set* an explicit
  safe destination, so they are not mismatch risks themselves, BUT confirm there isn't a separate
  "resume where I was" button that also assigns `regionSelectRoom` from `saveLocation`.
- `FastTravelScreen` path — `MeadowPauseMenu.Passage()` (`:178`) switches to
  `ProcessID.FastTravelScreen`. Trace what that screen does on region pick — does it also route
  through `regionSelectRoom` / `saveLocation`, and can it land on a Watcher region under a
  non-Watcher timeline? (It probably only offers regions valid for the timeline, but verify.)

## Deeper chokepoint option

`ikdasm` shows exactly one engine reader of `MenuSetup::regionSelectRoom` at line ~196855 of the
dumped IL, inside an `OverWorld`-area method that also checks `FastTravelInitCondition` /
`RegionSelect` and reads region files — likely `OverWorld.FirstWorldLoaded` / `WorldLoaded`
(an `IEnumerator` coroutine). Patching an iterator state machine is fragile; prefer the menu
call sites unless 6.1 finds a clean non-iterator method.

## Deliverable

Update this file with the final target list and the patch strategy, then unblock 6.3.

## CONCLUSION (2026-08-28)

`grep -rn "regionSelectRoom"` over /home/preston/repos/Rain-Meadow — every assignment:

- `Menu/MeadowMenu.cs:341,345,349` — all inside `MeadowMenu.StartGame()`. Line 349 is the
  final unconditional `= saveLocation.ResolveRoomName()` (the dead-guard overwrite). **This is
  the only mismatch-risk site.**
- `Meadow/MeadowPauseMenu.cs:191` (`ToOutskirts`) and `:200` (`ToHub`) — both first set
  `saveLocation` to an explicit safe coordinate (`suco4` / `targetHub`) and then derive
  `regionSelectRoom` from it. Not risks, and there is **no "resume where I was" button** in
  `MeadowPauseMenu` — the only ways back into Game from the pause menu are `ToOutskirts`,
  `ToHub`, and `Passage()` → `ProcessID.FastTravelScreen`.
- `FastTravelScreen` — never touches `regionSelectRoom`; the fast-travel path only offers
  regions valid for the current world, so it can't land on an out-of-timeline region.

**Strategy: single Harmony postfix on `MeadowMenu.StartGame` (private, via
`AccessTools.Method(typeof(MeadowMenu), "StartGame")`).** No deeper chokepoint needed; the
engine `OverWorld` iterator reader stays untouched. Done — see
`src/TimelineRegionGuardHooks.cs`.
