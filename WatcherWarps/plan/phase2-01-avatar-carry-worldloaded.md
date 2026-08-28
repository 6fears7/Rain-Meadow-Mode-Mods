# Phase 2.1 — Carry the triggering avatar's stomach/grasp contents through `OverWorld.WorldLoaded`

Parent: [phase2-00-overview.md](phase2-00-overview.md). Depends on: [[phase1-02-creature-warp-state]]
(reads `triggeredWarpPoint`/avatar identity from it). No dependency on [[phase2-02-performwarp-strip]]
or [[phase2-03-cross-client-rpc]] — do this one first regardless.

## Goal

Make the local triggering client's own swallowed/grasped objects follow it across the region
swap when the avatar is not a `Player`. Purely local bookkeeping — no RPCs, no other clients'
state touched.

## Where the gap is

`OverWorld_WorldLoaded` in
[Game/RainMeadow.EntityHooks.cs:508-651](../../Rain-Meadow/Game/RainMeadow.EntityHooks.cs#L508)
has two `foreach (var absplayer in self.game.Players)` loops, both after `orig(self, warpUsed)`
has already run:

- **`warpUsed` branch** (~line 617-636): fixes `pos.Tile` from `self.warpData.destPos`, calls
  `player.slugOnBack?.DropSlug()`, then carries `player.objectInStomach` and every non-null
  `player.grasps[k].grabbed` into the new world via `newWorldSession.ApoEnteringWorld(...)`.
- **plain-gate `else` branch** (~line 638-644): same `objectInStomach` carry only, for the
  ordinary (non-warp) gate-crossing path.

Both loops read `absplayer.realizedCreature is Player player` — for a Lizard/Scavenger/etc.
avatar this cast fails and the iteration silently skips it. The avatar's own
`AbstractCreature` already transfers correctly (that's driven by `game.Players` membership only
for the `beingMoved` toggling higher up, which — check this before writing code — actually
iterates `OnlineManager.lobby.playerAvatars` at lines ~592-610, already avatar-species-agnostic;
confirm this by re-reading those two `foreach` blocks side by side, since it means the
avatar-move itself is *already* fine and only the stomach/grasp/position-fixup loops below are
the actual gap).

## What to build

A generalized version of both loops (or a shared local helper called from both) that:

1. Enumerates the set of currently-active Meadow avatars the same way the existing
   `beingMoved`-toggle loop does (`OnlineManager.lobby.playerAvatars`, filtering
   `type != OnlineEntity.EntityId.IdType.none` and resolving `FindEntity(true)` to an
   `OnlinePhysicalObject` whose `.apo is AbstractCreature ac`), rather than `self.game.Players`.
2. For each resolved `AbstractCreature ac` with a realized `Creature` `c`:
   - Guard `c is Player player` for the existing vanilla-Player path (leave it untouched —
     don't regress the working case).
   - Add an `else` (or unify into one code path keyed off `c` instead of `player`) that reads
     the same three things off the *creature*, not off `Player`-typed fields:
     - Position fixup: `ac.pos.Tile` from `destPos` — this one's already creature-generic in
       vanilla (`AbstractCreature.pos` isn't a `Player`-only field), just needs to run for the
       non-Player branch too.
     - `slugOnBack`/`DropSlug()` — vanilla-Player-specific (carrying a rescued Slugcat NPC on
       your back); confirm with the user whether Watcher-timeline Meadow avatars can ever have
       an equivalent before deciding whether this needs a non-Player analogue or can be
       skipped as N/A for this species.
     - `objectInStomach`/`grasps[k].grabbed` — check whether these fields live on `Creature`
       itself or are genuinely `Player`-only (grep the publicized `PUBLIC-Assembly-CSharp.dll`
       decompile of `Creature.cs`/`Player.cs` — `grasps` is a `Creature` field in vanilla, only
       `objectInStomach` needs checking). Whichever are `Creature`-level, read them directly off
       `c`; whichever are `Player`-only, this is the same category of gap
       [[phase1-02-creature-warp-state]] solved for the trigger bookkeeping — either the state
       doesn't apply to non-Player creatures at all (e.g. if only `Player` can swallow objects
       in Watcher content) or it needs a similar companion-state field.
3. Calls `newWorldSession.ApoEnteringWorld(...)` for each transferred object exactly as vanilla
   does, unconditionally on species.

## Gotchas

- Don't touch the `warpUsed`/gate-switch branching logic itself, or the `beingMoved` toggle
  loop, or anything before `orig(self, warpUsed)` runs — this subtask only extends what happens
  to already-known-good `self.game.Players`-style follow-up bookkeeping to cover non-Player
  avatars too.
- `self.game.Players` (vanilla `RainWorldGame.Players`) and `OnlineManager.lobby.playerAvatars`
  are **not the same collection** — the former is vanilla's human-player list (empty of
  non-Player Meadow avatars, same root gap as [[phase1-00-overview]] describes for `Update()`),
  the latter is Meadow's own avatar registry. Iterate the latter for anything that needs to see
  every avatar, but keep any code that must stay scoped to genuine human `Player`s (if any
  exists in the surrounding method) reading `self.game.Players` unchanged.
- Guard the new code with the same `MeadowGameMode` + `meadowTimeline == Watcher` check as
  Phase 1's hooks — don't run it for Story mode or non-Watcher Meadow timelines, since this
  method is shared infrastructure used by ordinary gate crossings too.

## Done when

- A non-`Player` avatar (Lizard or Scavenger) carrying a grasped item through a placed
  `WarpPoint` in a test room arrives in the destination room still holding that item.
- The existing vanilla `Player`-avatar warp path is unchanged (re-test a `Player` avatar warp
  after this change to confirm no regression).

## Note for next session

Implemented 2026-08-26 as `src/WorldLoadedHooks.cs` (`On.OverWorld.WorldLoaded +=`, applied from
`Warps.Apply`). Resolved the two open questions before writing code:

1. The `beingMoved` loop **is** already avatar-generic — confirmed at
   `Rain-Meadow/Game/RainMeadow.EntityHooks.cs:675-691`, it iterates
   `OnlineManager.lobby.playerAvatars` and resolves `OnlinePhysicalObject`/`AbstractCreature`
   directly, no `Player` cast. No change needed there.
2. Checked via `ilspycmd` against `PUBLIC-Assembly-CSharp.dll`: `grasps` is declared on
   `Creature` (inherited by `Player`); `objectInStomach` and `slugOnBack` are both declared
   directly on `Player` with no `Creature`-level equivalent. Nothing in Watcher content gives a
   non-Player creature stomach-swallow or slug-carry behavior, so both are treated as N/A for
   this species rather than needing a companion-state field.

Implementation approach: rather than re-deriving "the triggering client's local avatar" via a
fresh `playerAvatars` scan, reused the existing `CreatureWarpState.TryGetLocalNonPlayerAvatar`
helper (`src/CreatureWarpState.cs`, already used by `SuckInCreaturesHooks`/`ChangeStateHooks`) —
it already encodes the `MeadowGameMode` + `meadowTimeline == Watcher` guard plus the
single-local-avatar (`mgm.avatars[0]`) lookup this subtask needed, so no new guard/enumeration
code was written.

The hook calls `orig(self, warpUsed)` first (letting Rain Meadow's own `game.Players`-based
Player loop run unmodified — a no-op for a non-Player avatar), then only for `warpUsed == true`:
fixes `avatar.abstractCreature.pos.Tile` from `self.warpData.destPos` (same field, read after
`orig` since `OverWorld.warpData` and `specialWarpPointGoal` are the same object reference per
decompile of `OverWorld.InitiateSpecialWarp_WarpPoint`/`WorldLoaded`, so it reflects any
`GenerateDestinationLocation` fill-in `orig` performed), then registers each grasped object into
`self.activeWorld.GetResource()` (the new `WorldSession`, valid post-`orig` since `activeWorld`
has been swapped by then) via `ApoEnteringWorld`. The plain-gate (non-`warpUsed`) branch was
deliberately left alone — vanilla only carries `objectInStomach` there (Player-only, N/A) since
ordinary gate crossings keep the room's entity list intact and grasped objects already follow it.

Build verified (`dotnet build`, 0 errors, pre-existing-style nullable warnings only). **Not yet
verified in-game** — neither this subtask nor Phase 1 has been playtested as of this writing;
the user explicitly chose to proceed with implementation ahead of Phase 1 verification. Next
session should playtest both together: a non-Player avatar (Lizard or Scavenger) grasping an
object, triggering a placed `WarpPoint`, and confirming it still holds the object in the
destination room, then re-test a vanilla `Player` avatar warp to confirm no regression.
