
# Phase 1.4 — Generalize `SuckInCreatures()`'s pull/gravity effects

Parent: [phase1-00-overview.md](phase1-00-overview.md). Depends on: [[phase1-02-creature-warp-state]].
Independent of (but thematically follows): [[phase1-03-trigger-detection]].

## Goal
While a `WarpPoint` is in `State.EnterWarp`, the local non-`Player` avatar should get pulled
toward the warp point, have gravity zeroed, and get its post-warp cooldown set — the same
treatment vanilla gives a `Player` caught in the pull radius.

## Current vanilla behavior
[plan/reference/Watcher.WarpPoint.decompiled.cs:2673-2797](reference/Watcher.WarpPoint.decompiled.cs#L2673-L2797)
(`SuckInCreatures()`, called every frame `currentState == State.EnterWarp` from `Update()` at
line 2414). Walks every `PhysicalObject` in the room; for anything that's a `Creature`:
- Drops all grasps, applies stun (unless it's a Slugcat/SlugNPC).
- **`Player`-only block** (lines 2743-2769): sets `warpPointCooldown = 80`; if within
  `PullRadius` or `creature == playerTriggeredWarpPoint`, zeros gravity
  (`player.SetLocalGravity(0f)`/`customPlayerGravity = 0f`), dampens velocity, pulls toward a
  point above the warp (`val`), sets `standingInWarpPointProtectionTime = 10`; near the end of
  the activation window, clears `performingActivationTimer` and resets camo-related fields.
- Blacklist check (`BlackListedCreatureTypes`) applies to *all* creatures, species-agnostic
  already.
- The final orbit-pull (`OrbitVector`, lines 2775-2794) also applies to all physical objects
  generically — only the `Player`-specific gravity-zeroing/velocity-damping block above is
  actually gated to `Player`.

## What to change
Add an `On.Watcher.WarpPoint.SuckInCreatures` hook (there is currently **no** existing Rain
Meadow hook on this method — confirmed by grepping `Story/StoryHooks.cs` for `SuckInCreatures`,
only `Update`/`WarpPrecast`/`NewWorldLoaded_Room` are hooked there — so this hook is new, not a
generalization of an existing one). Pattern: call `orig(self)` first (handles the
species-agnostic parts — blacklist, stun, general orbit pull — for the local avatar just like
any other creature), then run a parallel block for the local avatar only, guarded by the
[[phase1-02-creature-warp-state]] local-avatar helper, replicating the `Player`-only block
above but reading/writing the [[phase1-02-creature-warp-state]] `WarpState` instead of `Player`
fields, and comparing against `WarpState.triggeredWarpPoint == self` instead of
`creature == playerTriggeredWarpPoint`.

For the gravity zeroing specifically: `SetLocalGravity`/`GetLocalGravity` are called on
`physicalObject` generically elsewhere in this same method (line 2783-2784, the non-Player
generic path already does this for any `PhysicalObject`!) — re-check whether the local avatar
even needs the `Player`-specific gravity block duplicated, or whether it already gets adequate
gravity handling through the generic `physicalObject` path at
[reference/Watcher.WarpPoint.decompiled.cs:2780-2794](reference/Watcher.WarpPoint.decompiled.cs#L2780-L2794)
that runs for every creature regardless of type. If it does, this subtask shrinks to just:
`warpPointCooldown`/`standingInWarpPointProtectionTime`/`performingActivationTimer` bookkeeping
— confirm this by reading both blocks side by side before writing the hook, not by assuming.

## Done when
- A Lizard avatar standing in a `WarpPoint`'s pull radius during `EnterWarp` visibly gets
  pulled/leviated the same way a `Player` does (manual visual check — this is physics/feel,
  not something to assert from logs alone).
- `warpPointCooldown` analog is set afterward so the same avatar doesn't immediately re-trigger
  the same warp point.

## Note for next session
Done as of 2026-08-26. Implemented at
[WatcherWarps/src/SuckInCreaturesHooks.cs](../WatcherWarps/src/SuckInCreaturesHooks.cs):
- `On.Watcher.WarpPoint.SuckInCreatures` hook, registered from
  [Warps.cs](../WatcherWarps/src/Warps.cs) alongside the phase1-03 trigger hook.
- Confirmed by reading decompile lines 2775-2794 side by side with the Player-only block
  (2743-2769): the generic `physicalObject` path already zeroes gravity and runs
  `OrbitVector` for *any* `Creature` in range, non-Player included — since `orig(self)`
  runs first and the local avatar is just another `PhysicalObject` in that loop, gravity
  zeroing/pull is already handled with no extra code needed. This confirmed the plan's
  "shrinks to just bookkeeping" fallback, so the hook only replicates
  `warpPointCooldown = 80` and `standingInWarpPointProtectionTime = 10`
  (`triggeredWarpPoint == self` compared instead of `creature == playerTriggeredWarpPoint`).
  `performingActivationTimer` reset (lines 2763-2768) was skipped — confirmed it's
  Watcher-camo-only bookkeeping (`cancelCamoCooldown`/`camoInputsNeedReset`, both fields
  phase1-02 deliberately left off `CreatureWarpState`), not warp-pull related, and nothing
  in the codebase sets `performingActivationTimer` for a non-Player creature in the first
  place.
- The hook replicates vanilla's per-object filter chain (decompile lines 2711-2724:
  `BlackListedObjectTypes`, `creatureSuckRadius`/`strongPullRadius * 2.5f` distance caps,
  `canWarpToVoidWeaverEnding`, ripple-layer/`IsObjectImportant` check) before applying the
  bookkeeping, so the local avatar only gets treated as "caught in the pull radius" under
  the same conditions a `Player` would be.
- `dotnet build -p:InstallToGame=false` from `WatcherWarps/` succeeds, 0 warnings/errors.

Not done: the "Done when" manual visual check (Lizard avatar visibly pulled/leviated,
`warpPointCooldown` analog preventing immediate re-trigger) — requires a live game session
with a Meadow lobby, not just a compile check. Next: phase1-05 (`ChangeState` effects) is
next in the subtask sequence and is independent of this one.
