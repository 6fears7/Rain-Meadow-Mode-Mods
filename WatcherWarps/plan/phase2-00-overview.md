# Phase 2 — Generalize the region/room transfer — Overview

Parent plan: [watcher-warping-support.md](watcher-warping-support.md) (Phase 2 section, lines
27-33). Depends on Phase 1 being verified first (a triggering non-`Player` avatar with working
[[phase1-02-creature-warp-state]] bookkeeping) — see [[phase1-00-overview]]'s note for next
session.

## Why this is a separate phase from Phase 1

Phase 1 gets a non-`Player` avatar through the `ReadyForWarp → EnterWarp → ExitWarp →
CoolDown` state machine and `PerformWarp()` far enough that the *triggering client's own room*
reflects a warp. It does not touch region/world loading at all. The actual "you are now in a
different region" transfer is owned by three methods in the sibling repo, none of which know
about non-`Player` avatars today:

1. **`OverWorld.WorldLoaded` hook** (`OverWorld_WorldLoaded` in
   [Game/RainMeadow.EntityHooks.cs:508-651](../../Rain-Meadow/Game/RainMeadow.EntityHooks.cs#L508)) —
   after `orig(self, warpUsed)` runs, a `foreach (var absplayer in self.game.Players)` loop
   (two copies — one for the `warpUsed` branch, one for the plain-gate `else` branch) fixes up
   position (`destPos` → `pos.Tile`), drops `slugOnBack`, and carries `objectInStomach` +
   grasped items into the new world via `newWorldSession.ApoEnteringWorld(...)`. Every line in
   both loops casts `absplayer.realizedCreature is Player player` — a non-`Player` avatar is
   silently skipped, so its stomach/grasp contents never transfer even though the avatar's own
   `AbstractCreature` already rode along with the world swap. This is [[phase2-01-avatar-carry-worldloaded]].
2. **`WarpPoint_PerformWarp` hook** (`Story/StoryHooks.cs:495-539`) — the entity-stripping pass
   that removes non-owned remote `OnlinePhysicalObject`s from the abstracted source room before
   the warp completes. It early-returns via `if (!isStoryMode(out var storyGameMode)) return;`
   at line 497, so **none of this runs in a Meadow lobby today regardless of avatar species** —
   this isn't a Player-vs-non-Player gap, it's a Story-vs-Meadow gap. This is
   [[phase2-02-performwarp-strip]].
3. **Cross-client consistency** — nothing today tells *other* clients that a warp happened.
   Story mode's answer is `StoryRPCs.NormalExecuteWatcherRiftWarp`/`EchoExecuteWatcherRiftWarp`
   (`Story/StoryRPCs.cs:248-267`) calling `StoryHelpers.PerformWarpHelper`
   (`Story/StoryHelpers.cs:69-123`), fired from `WarpPoint_NewWorldLoaded_Room`
   (`Game/RainMeadow.EntityHooks.cs` — the `On.Watcher.WarpPoint.orig_NewWorldLoaded_Room` hook)
   when `OnlineManager.lobby.isOwner`. That whole path is gated by `isStoryMode(...)`, same gap
   as above, and it's built around `Player`-shaped assumptions in `PerformWarpHelper` (it
   doesn't touch avatar entities directly, but the RPC's synthetic local `WarpPoint` + camera
   forcing is written for a single human player's screen, not for keeping a remote client's
   view of *someone else's* `OnlineCreature` in sync). This is [[phase2-03-cross-client-rpc]].

## Mod structure

Same as Phase 1: everything is added to the standalone `WatcherWarps/` project from
[[phase1-01-mod-scaffold]], as new `On.*` hooks that chain alongside Rain Meadow's existing
`RainMeadow.EntityHooks`/`StoryHooks` — not edits to the sibling `/home/preston/repos/Rain-Meadow`
tree. Every hook in this phase must carry the same `OnlineManager.lobby?.gameMode is
MeadowGameMode && meadowTimeline == Watcher` guard established in Phase 1, so Story-mode and
non-Watcher-timeline lobbies keep running the vanilla/Story-hook paths untouched.

## Subtask sequence

1. [[phase2-01-avatar-carry-worldloaded]] — generalize `OverWorld_WorldLoaded`'s post-`orig`
   `game.Players` loops (position fixup, stomach/grasp carry) to the triggering non-`Player`
   avatar. Purely local to the triggering client — no networking. Do this first: it's the
   smallest, most self-contained piece, and the other two subtasks assume the avatar's own
   held/swallowed objects already transfer correctly before they add cross-client bookkeeping
   on top.
2. [[phase2-02-performwarp-strip]] — Meadow-mode branch for `WarpPoint_PerformWarp`'s
   non-owned-entity stripping, currently disabled outside Story mode entirely. Independent of
   subtask 1 (different hook, different method) but logically "the room-side half of the same
   transfer subtask 1 does the avatar-side half of."
3. [[phase2-03-cross-client-rpc]] — new RPC + broadcast call modeled on
   `NormalExecuteWatcherRiftWarp`/`PerformWarpHelper`, so remote clients' view of the
   triggering avatar's `OnlineCreature` (position, resource activation) stays consistent
   without forcing their own `OverWorld` swap. Depends on subtask 1 being done (there must be a
   correct local transfer to broadcast) but not on subtask 2.

Subtasks 2 and 3 can be reviewed independently of each other; subtask 1 should land first since
2 and 3 both assume it.

## Note for next session

[[phase2-01-avatar-carry-worldloaded]] implemented 2026-08-26 as `src/WorldLoadedHooks.cs` — see
that file's own Note for next session for details. [[phase2-02-performwarp-strip]] implemented
2026-08-26 as `src/PerformWarpStripHooks.cs` — see that plan doc's Note for next session for
details. Subtask 3 ([[phase2-03-cross-client-rpc]]) investigated 2026-08-26: resolved as "no new
RPC needed" — Meadow's existing generic `OnlineResource` membership sync already covers this case
for free, since the warp goes through the same vanilla engine calls (`AbstractRoom.AddEntity`/
`RemoveEntity`, `AbstractPhysicalObject.Move`) that drive it for ordinary gate crossings. See that
plan doc's "Investigation result" section for the full evidence chain. All three Phase 2 subtasks
are now code-complete (2 and 3 requiring no new code beyond what 1 already added) but unverified.

Neither Phase 1 nor Phase 2.1/2.2/2.3 has been verified in-game yet — the user explicitly chose to
proceed with implementation ahead of in-game verification (asked and answered 2026-08-26 for
Phase 2.1; Phase 2.2 followed the same precedent without re-asking), so treat all as
code-complete-but-unplaytested. Next session's entire remaining scope for Phase 2 is the in-game
playtest: a non-Player avatar reaching `PerformWarp()` (Phase 1), carrying a grasped object
through a placed `WarpPoint` into the destination room (Phase 2.1), non-owned entities being
stripped from the source room (Phase 2.2), and — with a second client in the lobby — confirming
that second client's view of the warping avatar's `OnlineCreature` updates correctly without its
own `OverWorld` reloading (Phase 2.3) — ideally as one combined two-client test per Phase 2.2's
Gotchas section — plus a re-test of the vanilla `Player` avatar warp path and vanilla Story mode
for regressions. If the Phase 2.3 half of that test fails, see that plan doc's Note for next
session for the most likely cause before writing new RPC code.
