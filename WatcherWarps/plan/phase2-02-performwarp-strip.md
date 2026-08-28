# Phase 2.2 — Enable `WarpPoint_PerformWarp`'s entity-stripping for Meadow lobbies

Parent: [phase2-00-overview.md](phase2-00-overview.md). Independent of
[[phase2-01-avatar-carry-worldloaded]] and [[phase2-03-cross-client-rpc]] — different hooked
method, can be built and reviewed on its own.

## Goal

Get the "remove non-owned remote entities from the source room before the warp completes"
bookkeeping running in Meadow/Watcher lobbies. Today it's not species-gated at all — it's
**entirely disabled outside Story mode**, so this is a Story-vs-Meadow gap, not a
Player-vs-non-Player one.

## Where the gap is

`WarpPoint_PerformWarp` (`Story/StoryHooks.cs:495-539`):

```csharp
public void WarpPoint_PerformWarp(On.Watcher.WarpPoint.orig_PerformWarp orig, Watcher.WarpPoint self)
{
    orig(self);
    if (!isStoryMode(out var storyGameMode)) return;   // <-- entire body below is Story-only
    Room room = self.room;
    AbstractRoom absRoom = room.abstractRoom;

    if (!RoomSession.map.TryGetValue(absRoom, out var roomSession)) return;
    var entities = absRoom.entities;
    for (int i = entities.Count - 1; i >= 0; i--)
    {
        if (entities[i] is AbstractPhysicalObject apo && OnlinePhysicalObject.map.TryGetValue(apo, out var oe))
        {
            oe.apo.LoseAllStuckObjects();
            if (!oe.isMine)
            {
                // not-online-aware removal of remote-owned entities
                ...
                entities.Remove(oe.apo);
                absRoom.creatures.Remove(oe.apo as AbstractCreature);
                if (oe.apo.realizedObject != null) { room.RemoveObject(...); room.CleanOutObjectNotInThisRoom(...); }
            }
            else
            {
                // my own entity: formally exit the room/world resource
                oe.ExitResource(roomSession);
                oe.ExitResource(roomSession.worldSession);
            }
        }
    }
}
```

Per the overview at [phase2-00-overview.md](phase2-00-overview.md#L1), `PerformWarp()` itself
(decompile line 1482, confirmed in [[phase1-00-overview]] point 4) is already species-agnostic
— it only touches room/save-state fields. This hook is the layer *on top* of `orig(self)` that
handles the multiplayer-specific room cleanup, and it happens to live in `StoryHooks.cs` today
because Story mode was the only mode that ever reached a real `WarpPoint.PerformWarp()` call
before this feature existed.

## What to build

Add a new `On.Watcher.WarpPoint.PerformWarp` hook in the `WatcherWarps` mod (does not need to
touch/replace the existing Story hook — MonoMod `On.*` hooks chain) that runs the equivalent
body when `OnlineManager.lobby?.gameMode is MeadowGameMode && meadowTimeline == Watcher`
instead of `isStoryMode(...)`. Concretely:

1. Same room/`RoomSession` lookup (`self.room`, `absRoom`, `RoomSession.map.TryGetValue`).
2. Same iteration over `absRoom.entities`, same `OnlinePhysicalObject.map.TryGetValue` check,
   same `oe.isMine` branch:
   - Non-owned (`!oe.isMine`): strip from `entities`/`creatures`, remove the realized object,
     same as Story's version — this part has no `Player`-vs-avatar distinction, it's about
     *other clients'* entities in general, so it can likely be copied close to verbatim.
   - Owned (`oe.isMine`): `oe.ExitResource(roomSession)` / `oe.ExitResource(roomSession.worldSession)`
     — also species-agnostic (`OnlinePhysicalObject`, not `Player`-typed).
3. Skip anything Story-specific that doesn't apply — re-check the full 495-539 range for any
   Story-only bookkeeping folded into this method beyond what's quoted above (the quoted body
   above is the complete method as read during planning; confirm nothing was truncated before
   treating it as complete).

## Gotchas

- Consider whether to literally duplicate the loop or extract a shared static helper (e.g. on
  `StoryHelpers` or a new `WatcherWarpsHelpers`) called from both the Story hook and this new
  Meadow hook, since the loop bodies should end up identical modulo the guard condition. Don't
  edit `Story/StoryHooks.cs` itself either way — the shared helper, if any, belongs in the new
  `WatcherWarps` mod project (per [[phase1-00-overview]]'s mod-structure decision), called via a
  public static method if `StoryHelpers` needs to expose one, or just duplicated if it doesn't
  export cleanly. Confirm with the user only if extracting a shared helper would require
  changes to the sibling Rain-Meadow repo (it should not — everything is publicized).
- `OnlinePhysicalObject.map`, `RoomSession.map`, `oe.isMine`, `oe.ExitResource` are all Meadow
  networking primitives, not species-specific — no new per-creature state from
  [[phase1-02-creature-warp-state]] is needed here.
- Verify ordering relative to [[phase2-01-avatar-carry-worldloaded]]'s work: `PerformWarp()`
  runs before the world/region actually swaps (it operates on the *old* room), while the
  `OverWorld.WorldLoaded` loops run after. They shouldn't conflict, but confirm during testing
  that stripping remote entities here doesn't remove the triggering avatar's own grasped items
  before subtask 1's carry-over code runs (it shouldn't, since `oe.isMine` protects the
  triggering client's own entities from the strip branch — but this is exactly the kind of
  interaction worth a combined test, not just two isolated ones).

## Done when

- Two clients in a Meadow/Watcher lobby, non-owned entities present in the source room (e.g. a
  remote client's creature or dropped item that isn't the triggering client's): after one
  client warps, the source room's abstracted entity list no longer contains those non-owned
  entities per this hook's removal, and the triggering client's own entities went through
  `ExitResource` cleanly (no leaked resource-session state, checked via existing Meadow
  debug logging).
- Story mode and non-Watcher-timeline Meadow lobbies are unaffected (the original Story hook's
  behavior is unchanged; this new hook's guard keeps it from running there).

## Note for next session

Implemented 2026-08-26 as `src/PerformWarpStripHooks.cs`. Re-verified the quoted method body
against the live `Story/StoryHooks.cs:495-539` before implementing — unchanged, and confirmed it
was the complete method (nothing truncated). Chose to duplicate the loop body rather than extract
a shared helper, since `StoryHooks.cs` lives in the sibling Rain-Meadow repo and the plan said not
to edit it; a shared helper would have meant either exposing a new public static method there (out
of scope) or an awkward one-off in `WatcherWarps` for a ~25-line loop, so plain duplication won via
[[phase1-00-overview]]'s mod-structure decision.

Registered in `src/Warps.cs`'s `Warps.Apply`. Builds clean (`dotnet build`), no errors, no new
warnings. Guard is `OnlineManager.lobby?.gameMode is MeadowGameMode && meadowTimeline == Watcher`
— deliberately does NOT also gate on non-`Player` avatar (unlike `CreatureWarpState`'s
`TryGetLocalNonPlayerAvatar`), since this hook strips *any* non-owned remote entity regardless of
the triggering client's own avatar species, matching the plan's framing that this is a
Story-vs-Meadow gap, not a Player-vs-non-Player one.

Not yet playtested — per [[phase2-00-overview]]'s note, Phase 1 and Phase 2.1 are also still
unverified in-game. Next session should playtest all three together: two clients in a Meadow/
Watcher lobby, non-owned entities in the source room, one client warps via a non-Player avatar —
confirm the source room's entity list drops the non-owned entities, the triggering client's own
entities go through `ExitResource` cleanly, and this doesn't remove the triggering avatar's own
grasped items before [[phase2-01-avatar-carry-worldloaded]]'s carry-over code runs (ordering:
`PerformWarp` runs on the old room before `OverWorld.WorldLoaded`'s carry loop, and `oe.isMine`
should protect the triggering client's own entities from the strip branch, but this needs a
combined test, not just isolated ones). Also re-test vanilla Story mode and vanilla `Player`-avatar
Meadow warps for regressions.
