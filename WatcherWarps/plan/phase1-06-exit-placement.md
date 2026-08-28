# Phase 1.6 — Generalize exit/placement (`ExitWarp` body, `CoolDown` body, `AddPlayerBackIn`)

Parent: [phase1-00-overview.md](phase1-00-overview.md). Depends on: [[phase1-02-creature-warp-state]].
Read alongside: [[phase1-05-changestate-effects]] (adjacent code, see note below).

## Goal
Make the local non-`Player` avatar actually get placed/animated correctly while a `WarpPoint`
finishes its local (same-room) exit sequence, and make the `CoolDown` state's proximity-based
timer notice it.

## Scope warning — possible overlap with parent-plan Phase 2
`AddPlayerBackIn` specifically handles *arriving* at a destination room (used from the
bad-warp animation and warp-defer paths — see call sites at
[reference/Watcher.WarpPoint.decompiled.cs:2239](reference/Watcher.WarpPoint.decompiled.cs#L2239)
and [:2273](reference/Watcher.WarpPoint.decompiled.cs#L2273)). The parent plan's Phase 2 is
about `OverWorld.WorldLoaded()` not relocating non-Player creatures across a *region* swap —
a different mechanism. This subtask's `AddPlayerBackIn` work only matters for the
**same-room** placement callback path (a creature already in the destination room's world,
being positioned at the warp point) — read [[phase1-00-overview]] and the parent plan's Phase 2
section again before implementing this part, and if it turns out `AddPlayerBackIn` is only
ever reached *after* Phase 2's region-swap machinery has already run, consider deferring this
half of the subtask to whichever phase actually owns that call path, rather than duplicating
work. Flag this explicitly in your own progress notes when you pick this subtask up.

## Current vanilla behavior
- **`ExitWarp` state body**
  ([reference/Watcher.WarpPoint.decompiled.cs:2433-2452](reference/Watcher.WarpPoint.decompiled.cs#L2433-L2452)):
  loops `room.game.Players`, for each `Player` in this room: toggles `levitationActive`,
  calls `TickLevitation(levitateUp: false)`, calls `OneWayPlacement(placedObject.pos, l)`,
  sets `eyesClosedTime = 5`, `repelLocusts`, `warpPointCooldown = 80`.
- **`CoolDown` state body**
  ([reference/Watcher.WarpPoint.decompiled.cs:2485-2506](reference/Watcher.WarpPoint.decompiled.cs#L2485-L2506)):
  finds the farthest `Player` in `room.game.Players` in this room, uses that distance to scale
  how fast `successfullWarpCooldownTimer` ticks down; species-agnostic once you swap the
  candidate source, no other logic changes needed.
- **`AddPlayerBackIn(int i, bool placeInRoom)`**
  ([reference/Watcher.WarpPoint.decompiled.cs:3450-3464](reference/Watcher.WarpPoint.decompiled.cs#L3450-L3464)):
  indexes `room.game.Players[i]` directly (not a loop — takes an index into the vanilla list),
  places the entity in the room if needed, then calls
  `(room.game.Players[i].realizedCreature as Player).OneWayPlacement(...)` and sets
  `warpPointCooldown`/`repelLocusts`.

## What to change
- `ExitWarp`/`CoolDown`: these are inside `Update()`, same method [[phase1-03-trigger-detection]]
  already hooks — extend that same `On.Watcher.WarpPoint.Update` hook (don't add a second
  hook on the same method) to also run the local-avatar equivalent of these two state bodies
  when `self.currentState` is `ExitWarp`/`CoolDown` and the local avatar is the one that
  triggered this warp point (`WarpState.triggeredWarpPoint == self`, from
  [[phase1-02-creature-warp-state]]).
  - `OneWayPlacement` is `Player`-only API — check whether an equivalent exists on `Creature`
    generally, or whether the simplest correct substitute for a non-Player avatar is just
    `creature.abstractCreature.pos`/`PlaceInRoom` at `placedObject.pos` without the
    levitation flourish. Levitation/eye-closing are cosmetic polish, not required for
    correctness — acceptable to skip them for v1 and note it as a follow-up if `OneWayPlacement`
    turns out to be nontrivial to replicate generically.
- `AddPlayerBackIn`: only relevant to this feature if reachable outside the region-swap path
  per the scope warning above. If it is: add a parallel method (not a hook on
  `AddPlayerBackIn` itself, since it's index-based into `room.game.Players` and the local
  avatar was never added to that list) that performs the same placement/cooldown/repelLocusts
  steps directly on the local avatar's `Creature`, called from wherever the equivalent
  `Player`-path call site is (the badWarp-animation and warpDefer branches in `Update()`,
  same method as above).

## Done when
- After a non-Player avatar completes a warp trigger sequence through
  [[phase1-03-trigger-detection]] and [[phase1-05-changestate-effects]], it ends up placed at
  (or near) the warp point in the same room, not stuck mid-animation or left at its
  pre-warp position.
- `CoolDown`'s timer responds to the local avatar's distance from the warp point the same way
  it would for a `Player`.

## Note for next session
Implemented (2026-08-26) in `src/WarpPointTriggerHooks.cs`, extending the existing
`On.Watcher.WarpPoint.Update` hook with `HandleExitWarp`/`HandleCoolDown` branches:
- `ExitWarp`: gated on `CreatureWarpState.Get(avatar).triggeredWarpPoint == self` (only the
  avatar that triggered this warp point gets placed, unlike vanilla's "every Player physically
  in the room" loop — an intentional v1 simplification). Placement replicates the position/
  velocity half of `Player.OneWayPlacement` generically against `PhysicalObject.bodyChunks`
  (confirmed via `ikdasm` disassembly of `PUBLIC-Assembly-CSharp.dll` that it only touches
  `bodyChunks[0]`/`[1]` plus Player-only `oneWayPlacementCooldown`/camo bookkeeping — no
  `Creature`-level equivalent exists, so this was the correct generalization rather than a
  found-and-missed API). Levitation/`eyesClosedTime` skipped as cosmetic (confirmed
  `eyesClosedTime` is a `Player`-only field via `ikdasm`); `repelLocusts` is a `Creature` field
  so that part carries over unchanged. `warpPointCooldown = 80` is set on the local avatar's
  `CreatureWarpState`.
- `CoolDown`: `orig()` already applies its Player-only distance-scaled decrement by the time our
  hook runs, so this corrects after the fact — if the local avatar is farther from the warp
  point than every real Player (or there are no real Players, where vanilla falls back to a
  flat fast-tick rate), the wrong decrement is undone and reapplied using the correct farthest
  distance.

## Update 2026-08-27 — arrival positioning / "no 0,0 drop" pass (user request)

User reported the demo warp trigger was placed oddly and asked to guarantee no warp
**destination** ever drops the avatar at (0,0), matching base-game behavior. Changes made
(build clean, not yet playtested):

1. **`HandleExitWarp` no longer gates on `triggeredWarpPoint == self`.** The v1 simplification
   noted above was wrong for *arrival*: the warp point running `ExitWarp` in the destination
   room is the freshly spawned destination-side instance, never the one the avatar walked into,
   so the old check never matched post-warp and the avatar was left at its pre-warp abstract
   tile (bottom-left). Now mirrors vanilla's "any Player in the room" loop — places the local
   avatar at `self.placedObject.pos` for any in-room `ExitWarp` point, sets
   `warpPointCooldown = 80` and `triggeredWarpPoint = self`. This is now the load-bearing
   arrival placement.
2. **`WorldLoadedHooks`** now resolves a fallback `destPos` (authored `DynamicWarpTarget` in the
   realized destination room, else `Warps.ReachablePos`) when `OverWorld.warpData.destPos` is
   null/zero, and logs a warning instead of silently leaving the avatar at (0,0). Grasp
   registration refactored into `RegisterGrasps`.
3. **`CorruptedWarpInjectionHooks`** now sets `po.pos = Warps.ReachablePos(self)` on both the
   forward and return leg — previously `BuildCorruptedWarpPlacedObject` left `pos` at
   `Vector2.zero`, so every injected `BadWarpLinks`/`OverlayRegionWarpLinks` warp (and its
   return-leg trigger) spawned unreachable in the bottom-left corner. Only `TestWarpHook` had
   been setting it.
4. **`Warps.ReachablePos` rewritten.** Was: prefer a `RoomExit` shortcut mouth (room-edge, bad
   for both triggers and arrivals), else nearest non-solid tile. Now: nearest *standable* tile
   to room centre (non-solid/non-shortcut, solid-or-slope footing below, headroom above —
   mirrors `MeadowGameMode.RegisterPlacesInRoom`'s `ValidPlacement`), then nearest non-solid
   tile, then the old shortcut-mouth behavior, then centre. Affects `ArrivalMarkerHooks`,
   `CorruptedWarpInjectionHooks`, `TestWarpHook`.

Still open: in-game verification; `TestWarpHook` still active (remove with real curation);
`DaemonWarpRedirectHooks` destinations still one-way and rely on `ArrivalMarkerHooks` +
change 1 for positioning (should be covered now, but untested).

**`AddPlayerBackIn` deliberately deferred, not implemented.** Both vanilla call sites
(decompile lines 2239, 2273) only fire on the `badWarpAnimStarted`/`warpDeferPlayerSpawnRoomName`
paths, which exist specifically to place an avatar that has just arrived via a *region* swap —
i.e. they only matter once something has already relocated the non-Player avatar's realized
creature into this destination room across regions. That relocation is `OverWorld.WorldLoaded`
non-Player handling, which is parent-plan Phase 2's job and doesn't exist yet. Implementing the
placement half here now would be untestable dead code until Phase 2 lands. Revisit this when
Phase 2 is implemented — wire the equivalent placement (same `PlaceAtWarpPoint` helper added in
this subtask) into whichever hook Phase 2 adds around those two call sites, rather than
duplicating it.
