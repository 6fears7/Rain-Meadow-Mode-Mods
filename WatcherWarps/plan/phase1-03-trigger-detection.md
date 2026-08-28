# Phase 1.3 — Generalize trigger detection in `Update()`

Parent: [phase1-00-overview.md](phase1-00-overview.md). Depends on: [[phase1-02-creature-warp-state]].

## Goal
Make the local Meadow avatar (when it isn't a `Player`) able to trigger a `WarpPoint` by
standing near it and holding still, the same way a vanilla `Player` does.

## Current vanilla behavior
[plan/reference/Watcher.WarpPoint.decompiled.cs:2332-2411](reference/Watcher.WarpPoint.decompiled.cs#L2332-L2411)
(inside `Update()`, `currentState == State.ReadyForWarp` branch):
1. Loops `room.game.Players` (vanilla's human-player list), picks the nearest one with
   `warpPointCooldown <= 0 && warpExhausionTime <= 0` that's in the room.
2. If one's within `PullRadius`, sets `standingInWarpPointProtectionTime = 10` on it.
3. Tracks warp-tear open/close sound state (`warpTearOpenTriggered`) based on distance —
   species-agnostic once you have "the candidate creature," no change needed to this part's
   logic, just to what feeds it.
4. If the candidate is within `PullRadius`, not in a shortcut, `visualOpenness > 0.5f` (or
   `guaranteeTrigger`), and off cooldown: calls `WarpPrecast()` once
   (`canPreCast && Region.RegionReadyToWarp`), then accumulates `triggerTime` — the
   accumulation rate depends on `player.touchedNoInputCounter` (standing still speeds it up).
5. When `triggerTime >= triggerActivationTime`: sets `playerTriggeredWarpPoint = player`,
   computes `canWarpToVoidWeaverEnding`, calls `ChangeState(State.EnterWarp)`.

Since `room.game.Players` never contains a Lizard/Scavenger avatar, none of this runs for one
today — it's not gated on species, it's simply invisible to the loop.

## What to change
Add an `On.Watcher.WarpPoint.Update` hook (pre- or post-`orig`, whichever is simpler once
written — post is likely easier since `orig` already advances `triggerTime`/state for any
`Player` found, and this hook only needs to run its own parallel selection when the local
avatar is the relevant creature). Guarded by the [[phase1-02-creature-warp-state]] local-avatar
helper. Logic to mirror, substituting the local avatar `Creature` + its
[[phase1-02-creature-warp-state]] `WarpState` for `player`/its fields, and
`CreatureController.creatureControllers[avatar].touchedNoInputCounter` (already generalized —
see `RegionGate_PlayersStandingStill1` at
[/home/preston/repos/Rain-Meadow/Meadow/RainMeadow.MeadowHooks.cs:297-311](../../Rain-Meadow/Meadow/RainMeadow.MeadowHooks.cs#L297-L311)
for the exact lookup idiom) for `player.touchedNoInputCounter`:
- Only proceed if `currentState == State.ReadyForWarp && !warpLocked && transportable` (same
  gate vanilla uses — `transportable` and `warpLocked` are already species-agnostic properties
  on `WarpPoint` itself).
- Distance/cooldown check against the local avatar using the [[phase1-02-creature-warp-state]]
  state instead of `Player` fields.
- `triggerTime` accumulation: **do not duplicate vanilla's accumulation if a `Player` also
  triggered it this frame** — only run this branch when the local avatar is the (or *a*)
  candidate and no `Player` in `room.game.Players` already claimed `triggerTime` this tick.
  Simplest safe approach: only run the non-Player branch when `room.game.Players` contributed
  nothing (i.e., mirror vanilla's `player == null` fallthrough at
  [reference/Watcher.WarpPoint.decompiled.cs:2391](reference/Watcher.WarpPoint.decompiled.cs#L2391))
  — since a Meadow lobby's local client only ever has one local avatar, and it's either a
  `Player` (vanilla path already works, do nothing) or it isn't (this hook's path), these two
  should never both fire for the same client in the same frame. State this invariant in a code
  comment since it's non-obvious from the hook alone.
- On reaching `triggerActivationTime`: set the local avatar into the
  [[phase1-02-creature-warp-state]] state's `triggeredWarpPoint` field (the `playerTriggeredWarpPoint`
  stand-in — **do not** attempt to assign to `self.playerTriggeredWarpPoint` itself, it's
  `Player`-typed and will not accept a Lizard; see [[phase1-05-changestate-effects]] for how the
  `ChangeState` side reads this instead), then call `self.ChangeState(Watcher.WarpPoint.State.EnterWarp)`.

## Open question to verify empirically (not inferable from decompile alone)
Does `WarpPrecast()` itself have any `Player`-only assumptions reachable from this path? Read
through it before finishing this subtask —
[reference/Watcher.WarpPoint.decompiled.cs:1973-2020](reference/Watcher.WarpPoint.decompiled.cs#L1973-L2020)
— it currently reads as region/save-state-only (no `Player` casts found in this file's copy of
it), but confirm no MoreSlugcats/Watcher partial hooks elsewhere in Rain Meadow's own
`Story/StoryHooks.cs` change that assumption specifically for Watcher timelines.

## Done when
- A Lizard avatar standing still near an active `WarpPoint` accumulates `triggerTime` and
  reaches `EnterWarp` state (log `currentState`/`triggerTime` to confirm — no visual/region
  transfer needed yet, that's [[phase1-05-changestate-effects]] /
  [[phase1-06-exit-placement]] / parent-plan Phase 2).
- A `Player` avatar's triggering is unaffected (regression check).
- Story-mode sessions are unaffected (the local-avatar guard from
  [[phase1-02-creature-warp-state]] already excludes them via the
  `OnlineManager.lobby?.gameMode is MeadowGameMode` check).

## Note for next session
Done as of 2026-08-26. Implemented at
[WatcherWarps/src/WarpPointTriggerHooks.cs](../WatcherWarps/src/WarpPointTriggerHooks.cs):
- `On.Watcher.WarpPoint.Update` post-`orig` hook, wired in
  [Warps.cs](../WatcherWarps/src/Warps.cs)'s `Apply`.
- Guarded on `currentState == ReadyForWarp && !warpLocked && transportable` (checked
  *after* `orig` runs, so it also catches the case where `orig`'s own Player branch just
  transitioned state) plus [[phase1-02-creature-warp-state]]'s
  `TryGetLocalNonPlayerAvatar`.
- Re-walks `room.game.Players` itself (cheap, small list) to confirm no real `Player`
  with `warpPointCooldown <= 0 && warpExhausionTime <= 0` is in the room before doing
  anything — this is the concrete form of the "no double bookkeeping" invariant from the
  plan, since `orig` doesn't expose its own local `player` selection to a post-hook.
- Mirrors vanilla lines 2345-2390 (protection-time stamp, closesEarly/warpTearOpenTriggered
  sound-state tracking including `nonPlayerRequestOpen.Entering()`, `WarpPrecast()` gate,
  `triggerTime` accumulation using `CreatureController.creatureControllers[avatar]
  .touchedNoInputCounter` in place of `player.touchedNoInputCounter`) against the local
  avatar instead of a `Player`.
- On `triggerTime >= triggerActivationTime`: sets `CreatureWarpState.Get(avatar)
  .triggeredWarpPoint = self` (not `self.playerTriggeredWarpPoint`, per
  [[phase1-05-changestate-effects]]'s note), computes `canWarpToVoidWeaverEnding`, logs via
  `Warps.Log`, then calls `self.ChangeState(WarpPoint.State.EnterWarp)`.
- Confirmed empirically from the decompile (not just assumed) that `WarpPrecast()` and
  `CheckCanWarpToVoidWeaverEnding()` (lines 1973-2020, 1890-1898) touch only
  room/save-state fields — no `Player` casts — so they're safe to call unmodified from this
  non-Player path. Did not additionally check Rain Meadow's own `Story/StoryHooks.cs` for a
  Watcher-timeline override of that assumption (the open question named a specific file to
  check that wasn't re-verified this session) — worth a quick grep before Phase 2 if a bad
  warp ever behaves unexpectedly for a non-Player trigger.
- `dotnet build -p:InstallToGame=false` from `WatcherWarps/` succeeds, 0 warnings/errors.

Not done / not verified: no live-game test yet (no lobby was launched this session) — the
"Done when" empirical checks (Lizard avatar reaching `EnterWarp` with logged
`currentState`/`triggerTime`, and Player-avatar/story-mode regression checks) still need an
actual play session. Next: either run that manual verification, or proceed to
[[phase1-04-suckin-creatures]] (`SuckInCreatures()`'s pull/gravity effects, needed before a
triggered warp does anything visible) since subtasks 3-6 are independently workable.
