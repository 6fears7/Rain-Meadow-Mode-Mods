# Phase 2.3 — Broadcast the warp to other clients without forcing their own `OverWorld` swap

Parent: [phase2-00-overview.md](phase2-00-overview.md). Depends on
[[phase2-01-avatar-carry-worldloaded]] (there must be a correct local transfer before
broadcasting it). Independent of [[phase2-02-performwarp-strip]].

## Goal

When the triggering client completes a warp, remote clients should keep their view of that
avatar's `OnlineCreature` (position, resource activation) consistent — **without** each remote
client reloading its own `OverWorld`/region. Per the parent plan
([watcher-warping-support.md:32](watcher-warping-support.md#L32)): "each client's own `OverWorld`
swap is local, matching how independent region-gate crossings already work today without
forcing every client to reload."

## Reference pattern (Story mode)

Story mode's version of this is `WarpPoint_NewWorldLoaded_Room`
(`Game/RainMeadow.EntityHooks.cs`, the `On.Watcher.WarpPoint.orig_NewWorldLoaded_Room` hook):
after `orig(self, newRoom)` runs and some cleanup, if `OnlineManager.lobby.isOwner &&
!isEchoWarp`, it loops `OnlineManager.players` and calls
`player.InvokeOnceRPC(StoryRPCs.NormalExecuteWatcherRiftWarp, sourceRoomName, warpData, false)`
for every non-`me` player. That RPC method (`Story/StoryRPCs.cs:247-252`):

```csharp
[RPCMethod]
public static void NormalExecuteWatcherRiftWarp(RPCEvent rpc, string? sourceRoomName, string warpData, bool useNormalWarpLoader)
{
    if (rpc != null && OnlineManager.lobby.owner != rpc.from) return;
    Watcher.WarpPoint? warpPoint = StoryHelpers.PerformWarpHelper(sourceRoomName, warpData, useNormalWarpLoader, false);
}
```

`StoryHelpers.PerformWarpHelper` (`Story/StoryHelpers.cs:69-123`) builds a synthetic local
`WarpPoint` from the serialized `WarpPointData` string and, if the receiving client isn't the
lobby owner, calls `ForceLoadDesiredWarp` — which **does** call
`overWorld.InitiateSpecialWarp_WarpPoint(...)`, i.e. it forces that receiving client's own
`OverWorld` to warp too. This is the opposite of what Phase 2 wants: Story mode's single shared
campaign world means every client's world genuinely must follow the same warp, but Meadow's
sandbox lets clients be in different rooms/regions independently (per the "matching how
independent region-gate crossings already work" note above), so copying `PerformWarpHelper`
verbatim would over-synchronize.

## What to build

A new, Meadow-specific RPC — do not call `StoryRPCs.NormalExecuteWatcherRiftWarp` or
`StoryHelpers.PerformWarpHelper` — that updates the remote clients' bookkeeping about the
triggering avatar's `OnlineCreature` without touching their local `OverWorld`:

1. Fire point: from the triggering client itself once its local warp has completed (i.e. after
   [[phase2-01-avatar-carry-worldloaded]]'s carry-over has run and the avatar's `AbstractCreature`
   is confirmed in the destination room/world) — not from
   `WarpPoint_NewWorldLoaded_Room`'s owner-only broadcast pattern, since Meadow warps aren't
   necessarily owner-initiated the way Story's `myLastWarp` forced-warp flow is. Confirm against
   how Meadow's existing region-gate crossing already announces "my avatar moved to a new
   room/world" to other clients — grep `OnlineManager` avatar/room-change notification calls
   used by ordinary (non-warp) gate crossings first, since a warp's cross-client announcement
   should ideally reuse that exact mechanism rather than invent a parallel one. If ordinary gate
   crossings already keep `OnlineCreature`/`RoomSession`/`WorldSession` membership in sync
   automatically (likely, since Meadow already supports independent per-client region gates),
   this subtask may reduce to "confirm the warp path exercises the same automatic sync a gate
   crossing does" rather than requiring a hand-written RPC at all — verify this before building
   new RPC machinery, since a new RPC modeled on Story's is explicitly the parent plan's
   fallback framing ("modeled on ... so remote clients keep that `OnlineCreature`'s resource
   activation/position consistent"), not a confirmed requirement.
2. If a new RPC does turn out to be necessary (existing gate-crossing sync doesn't cover the
   warp case): define an `[RPCMethod]` in the `WatcherWarps` mod, payload = whatever identifies
   the avatar (`OnlineCreature`/`EntityId`) + destination room/region + `destPos`, invoked via
   `player.InvokeOnceRPC(...)` to every other lobby member the same way
   `NormalExecuteWatcherRiftWarp` does, but whose handler **only** updates the remote
   `OnlineCreature`'s known position/resource-activation state (mirroring whatever
   `RoomSession`/`WorldSession` bookkeeping ordinary remote-avatar tracking already does for
   avatars in rooms the local client hasn't loaded) — explicitly must NOT call
   `InitiateSpecialWarp_WarpPoint`, `ForceLoadDesiredWarp`, or anything else that swaps the
   receiving client's own `OverWorld`.

## Gotchas

- This is the subtask most likely to collapse in scope once investigated — Meadow's core
  premise (independent per-client world sessions) probably already has *some* answer for "how
  does client B know client A's avatar is now in a room client B hasn't loaded," since that's
  not warp-specific. Spend investigation time up front confirming what already exists before
  writing new RPC/broadcast code.
- If a new RPC is needed, keep its guard consistent with the rest of Phase 1/2: only fires under
  `MeadowGameMode` + `meadowTimeline == Watcher`.
- Don't reuse `StoryRPCs`/`StoryHelpers` types directly even if a superficially-matching method
  exists — they're written around Story mode's single-shared-world assumption
  (`ForceLoadDesiredWarp` forcing every receiving client's `OverWorld`), which is the opposite
  of Meadow's per-client independence this subtask needs to preserve.

## Done when

- Two clients in a Meadow/Watcher lobby: client A warps to a different region; client B's view
  of client A's avatar (queried however Meadow already exposes remote-avatar state — position,
  resource activation) reflects the new room/region without client B's own `OverWorld`
  reloading or its camera/room changing.
- Client B can still independently walk into the same or a different `WarpPoint` afterward
  without interference from A's warp having touched B's local world state.

## Investigation result (2026-08-26)

Investigated per the "spend investigation time up front" gotcha above, before writing any RPC
code. Conclusion: **no new RPC is needed.** Evidence, all in the sibling `/home/preston/repos/Rain-Meadow`
repo:

- `Game/RainMeadow.EntityHooks.cs`'s `AbstractRoom_AddEntity`/`AbstractRoom_RemoveEntity`
  (~L298-320) and `AbstractPhysicalObject_Move` (~L176-188) are generic `On.*` hooks on stock RW
  engine methods — they call `WorldSession.ApoEnteringWorld`/`RoomSession.ApoEnteringRoom` /
  `ApoLeavingRoom` for *any* `AbstractPhysicalObject` whose room/world membership changes, with no
  Player-specific or warp-specific branching.
- Those calls run into `OnlineEntity.EnterResource` (`Online/Entity/OnlineEntity.cs:98-126`),
  which updates resource membership and (`JoinOrLeavePending`, ~L142-197) calls
  `OnlineResource.LocalEntityEntered`/`LocalEntityLeft` (`Online/Resource/OnlineResource.Entities.cs`)
  to register/join the entity in its new resource tree.
- Other clients learn about this via the resource's normal periodic `ResourceState` broadcast
  (`Online/Resource/OnlineResource.State.cs:82-199`), which diffs joined/registered entities and
  reconciles generically — not a bespoke per-event RPC.
- `OnlineCreature` doesn't override any of this — it inherits `OnlinePhysicalObject.JoinImpl`
  (`Online/Entity/OnlinePhysicalObject.cs:269-398`) verbatim.
- The scope boundary (only clients actually subscribed to the source/destination
  `WorldSession`/`RoomSession` get the detailed update) is identical for ordinary gate crossings
  today, so it's not a gap Phase 2.3 needs to patch — it's the existing, intentional per-client
  independence the parent plan explicitly wants preserved.

Since [[phase2-01-avatar-carry-worldloaded]] only patches position fixup and grasp carry-over on
top of the *vanilla* `WarpPoint`/`OverWorld` engine calls that actually perform the room/world
reassignment (it doesn't hand-patch `apo.world`/room membership directly — that already happens
through the same `AddEntity`/`RemoveEntity`/`Move` calls a normal gate crossing uses), the generic
hooks above already fire correctly for a Watcher-warping non-Player avatar with no extra code.

**This subtask is resolved as "no code needed," pending the in-game two-client verification pass
described below**, which is still outstanding (blocked on the general Phase 1/2.1/2.2 playtest
noted in [[phase2-00-overview]]).

## Note for next session

No RPC/broadcast code was written for this subtask — investigation (2026-08-26) found Meadow's
existing generic resource-membership sync (`ApoEnteringWorld`/`ApoEnteringRoom` →
`OnlineEntity.EnterResource` → periodic `ResourceState` broadcast) already covers keeping remote
clients' view of a warped avatar's `OnlineCreature` in sync, since it's driven by the same vanilla
engine calls (`AbstractRoom.AddEntity`/`RemoveEntity`, `AbstractPhysicalObject.Move`) that any
ordinary region-gate crossing uses, not by Player-specific or warp-specific logic. See
"Investigation result" above for the full evidence chain.

Next session (and the rest of Phase 2) is now entirely blocked on the in-game two-client playtest
called out in [[phase2-00-overview]]'s note — Phase 1, 2.1, and 2.2 are code-complete but
unverified, and this subtask's "Done when" criteria (remote client B sees A's warped avatar
without B's own `OverWorld` reloading) can only be confirmed by that playtest, not by further
code changes. If the playtest reveals B's view of A's avatar does NOT update correctly, the most
likely cause per the evidence above would be Phase 2.1's carry-over bypassing one of the hooked
engine methods (e.g. mutating `apo.pos`/`apo.world` directly instead of through
`AddEntity`/`RemoveEntity`/`Move`) rather than a missing RPC — re-check
[[phase2-01-avatar-carry-worldloaded]]'s implementation (`src/WorldLoadedHooks.cs`) first before
reconsidering a hand-written RPC.
