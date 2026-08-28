# Phase 1.5 — Generalize `ChangeState()`'s entry/exit side effects

Parent: [phase1-00-overview.md](phase1-00-overview.md). Depends on: [[phase1-02-creature-warp-state]],
[[phase1-03-trigger-detection]] (needs `WarpState.triggeredWarpPoint` to be set by the time
`EnterWarp` is entered).

## Goal
Make entering/leaving warp states not crash and not silently skip the local non-`Player`
avatar's camera cutscene / cooldown reset / levitation stop.

## The critical bug this subtask exists to prevent
[plan/reference/Watcher.WarpPoint.decompiled.cs:2618-2636](reference/Watcher.WarpPoint.decompiled.cs#L2618-L2636)
— on entering `State.EnterWarp`, vanilla unconditionally runs:
```
room.game.cameras[0].EnterCutsceneMode(playerTriggeredWarpPoint.abstractCreature, RoomCamera.CameraCutsceneType.Standard);
```
`playerTriggeredWarpPoint` is `Player`-typed (decompile line 805) and is **only ever assigned**
in vanilla's own `Update()` trigger loop when a `Player` was found
([reference/Watcher.WarpPoint.decompiled.cs:2386](reference/Watcher.WarpPoint.decompiled.cs#L2386)).
[[phase1-03-trigger-detection]] intentionally does *not* (cannot) assign into this field for a
Lizard/Scavenger. If [[phase1-03-trigger-detection]]'s hook calls
`self.ChangeState(State.EnterWarp)` for a non-Player local avatar and vanilla's own
`ChangeState` body runs unmodified, `playerTriggeredWarpPoint` is `null` and this line throws
`NullReferenceException`. This has to be prevented, not patched around after the fact.

## Current vanilla behavior (full picture)
[reference/Watcher.WarpPoint.decompiled.cs:2568-2671](reference/Watcher.WarpPoint.decompiled.cs#L2568-L2671)
(`ChangeState(State nextState)`), the `Player`-touching branches:
- Leaving `State.ExitWarp` (lines 2582-2591): `(room.game.Players[i].realizedCreature as Player).StopLevitation()` for every vanilla player in the room.
- Entering `State.EnterWarp` (lines 2618-2636): sets `warpPointCooldown = 80` on every
  `room.game.Players` entry in this room, plays a sound, **the crash line above**, clears
  pending-transfer gravity lists, resets `activationTime`, starts the camera's
  `WarpPointTimer` if none is running.
- Entering `State.ExitWarp` (line 2638-2644): calls `PerformWarp()` if coming from
  `EnterWarp` — **already species-agnostic**, no change needed (see
  [[phase1-00-overview]] point 4).

## What to change
Add an `On.Watcher.WarpPoint.ChangeState` hook. Because of the crash risk above, **do not**
simply call `orig` and patch afterward for the `EnterWarp` transition — the unsafe line runs
inside `orig` itself. Options, in order of preference:
1. **Pre-hook branch + skip orig's unsafe branch via IL patch**: heavier to write but keeps
   vanilla's method structure intact for the `Player` case. Only worth it if option 2 proves to
   have side effects that matter.
2. **Pre-hook that fills a real (dummy-safe) value before calling orig**: not possible —
   `playerTriggeredWarpPoint` cannot hold a non-Player value, full stop, so there is no dummy
   assignment that avoids the crash.
3. **Recommended**: pre-hook that, when the local avatar (non-Player) is the one entering
   `EnterWarp` (check `self`'s [[phase1-02-creature-warp-state]] `WarpState.triggeredWarpPoint == self`
   for the local avatar), replicates the entering-`EnterWarp` side effects itself using the
   local avatar's `AbstractCreature` for `EnterCutsceneMode`, then calls `orig(self, nextState)`
   guarded so its `playerTriggeredWarpPoint`-touching line doesn't execute — the cleanest way to
   do that without an IL patch is to temporarily special-case: since `room.game.Players` won't
   contain the local avatar anyway, `orig`'s `warpPointCooldown`-setting loop is already a
   no-op for it, so the *only* unsafe statement is the `EnterCutsceneMode` line itself. Confirm
   via IL inspection (`ikdasm`/ilspy on the compiled method, not just the decompile's line
   numbers, since decompiled line numbers don't map 1:1 to IL offsets) whether an `IL.*` hook
   can cheaply no-op just that one call when `playerTriggeredWarpPoint == null`, vs. whether
   it's simpler to have this mod's hook run first, do its own `EnterCutsceneMode` call for the
   local avatar, and then IL-patch only the null-guard around vanilla's line (`if
   (playerTriggeredWarpPoint != null) EnterCutsceneMode(...)`) so both paths coexist safely.
   Decide the exact mechanism during implementation — this is a call worth making with the IL
   in front of you, not blind.
- Leaving `State.ExitWarp`: add the local avatar's `StopLevitation()`-equivalent call if/when
  [[phase1-06-exit-placement]] gives it levitation state to stop (these two subtasks touch
  adjacent code — read that one before finalizing this one's `ExitWarp`-exit branch, since it
  may turn out there's nothing to stop if [[phase1-06-exit-placement]]'s placement approach
  doesn't use levitation for non-Player creatures at all).

## Done when
- Triggering a warp as a non-Player avatar reaches `EnterWarp` **without** a
  `NullReferenceException` on `playerTriggeredWarpPoint`.
- The camera cutscene follows the correct (local, non-Player) creature.
- Triggering as a `Player` is completely unaffected (regression check — run this check first,
  before the non-Player case, since a mistake here is the single highest-risk regression in all
  of Phase 1).

## Note for next session
Implemented 2026-08-26 as `src/ChangeStateHooks.cs`, wired into `Warps.Apply()`. Went with a
variant of option 3 that avoids IL patching entirely (no ikdasm/ILSpy session available to
verify IL offsets against): the `On.Watcher.WarpPoint.ChangeState` hook calls `orig` completely
untouched for every transition except "entering `EnterWarp` with `playerTriggeredWarpPoint ==
null`" — i.e. every real-`Player` transition is bit-for-bit vanilla via `orig`, zero regression
risk for that path (the plan's top regression-check priority). Only the crash-risk transition is
intercepted, and for that one call `orig` is skipped entirely; both halves of vanilla
`ChangeState` (leaving-`currentState` chain, decompile 2572-2591, and entering-`EnterWarp`
block, decompile 2618-2636) are manually replicated in C#, with the crash line replaced by
`avatar?.abstractCreature` sourced from `CreatureWarpState.TryGetLocalNonPlayerAvatar`, guarded
so a null avatar just skips `EnterCutsceneMode` (logs a warning) instead of crashing.

Deliberately did not add a `StopLevitation()`-equivalent for the local avatar in the
leaving-`ExitWarp` branch — left as vanilla (Player-list-only), per this file's own note to read
[[phase1-06-exit-placement]] first. Build verified clean (`dotnet build`, 0 errors) but **not
yet tested in-game** — next session should verify in a live Meadow lobby: (1) Player-triggered
warp still works identically (regression check, do this first), (2) non-Player avatar (Lizard/
Scavenger) triggering a warp reaches `EnterWarp` without NRE and the cutscene camera follows the
avatar correctly. After that, move to [[phase1-06-exit-placement]], which may retroactively
require adding the deferred `StopLevitation` call here.
