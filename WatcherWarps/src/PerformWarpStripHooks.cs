using RainMeadow;
using Watcher;

namespace WatcherWarps
{
    // Story/StoryHooks.cs's WarpPoint_PerformWarp hook (lines 495-539) strips
    // non-owned OnlinePhysicalObjects from the source room's abstracted entity
    // list and formally exits the triggering client's own entities from the
    // room/world resource - but the whole method body is gated behind
    // `if (!isStoryMode(...)) return;`, so none of this runs in a Meadow lobby
    // today, regardless of avatar species. This is a Story-vs-Meadow gap, not
    // a Player-vs-non-Player one: OnlinePhysicalObject.map, RoomSession.map,
    // oe.isMine, and oe.ExitResource are all Meadow networking primitives with
    // no per-creature-species distinction.
    //
    // MonoMod On.* hooks chain, so this runs alongside (not instead of) the
    // Story hook - each early-returns in the other's mode, and both call
    // orig() themselves, so orig only ever runs once per real call.
    public static class PerformWarpStripHooks
    {
        public static void Apply()
        {
            On.Watcher.WarpPoint.PerformWarp += WarpPoint_PerformWarp;
        }

        private static void WarpPoint_PerformWarp(On.Watcher.WarpPoint.orig_PerformWarp orig, WarpPoint self)
        {
            orig(self);

            if (!Warps.IsMeadowWatcher())
            {
                return;
            }

            Room room = self.room;
            AbstractRoom absRoom = room.abstractRoom;

            if (!RoomSession.map.TryGetValue(absRoom, out var roomSession)) return; // deactivating world session is evil
            var entities = absRoom.entities;
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                if (entities[i] is AbstractPhysicalObject apo && OnlinePhysicalObject.map.TryGetValue(apo, out var oe))
                {
                    oe.apo.LoseAllStuckObjects();
                    if (!oe.isMine)
                    {
                        oe.beingMoved = true;

                        if (oe.apo.realizedObject is Creature c && c.inShortcut)
                        {
                            if (c.RemoveFromShortcuts()) c.inShortcut = false;
                        }

                        entities.Remove(oe.apo);

                        absRoom.creatures.Remove(oe.apo as AbstractCreature);
                        if (oe.apo.realizedObject != null)
                        {
                            room.RemoveObject(oe.apo.realizedObject);
                            room.CleanOutObjectNotInThisRoom(oe.apo.realizedObject);
                        }
                        oe.beingMoved = false;
                    }
                    else if (oe is OnlineCreature { isAvatar: true })
                    {
                        // Phase 7-02: do NOT ExitResource our own avatar. Meadow forbids
                        // re-registering a live avatar (MeadowGameMode.ShouldRegisterAPO
                        // gates Creatures behind sSpawningAvatar, and NewEntity throws
                        // "entity re-register" for a realized one). ExitResource here
                        // empties joinedResources -> OnlineEntity.OnLeftResource sees
                        // primaryResource == null and Deregisters the avatar for good,
                        // which is exactly the phase 7-01 "invisible after warp" bug.
                        // Leave it registered and let OverWorld_WorldLoaded's warpUsed
                        // branch (beingMoved loop + orig + AbstractRoom_AddEntity ->
                        // ApoEnteringWorld's in-map branch) carry it to the destination
                        // sessions with its identity intact, the same way a whole-lobby
                        // watcher warp does. Story can strip-and-re-register; Meadow can't.
                        oe.beingMoved = true;
                    }
                    else
                    {
                        oe.ExitResource(roomSession);
                        oe.ExitResource(roomSession.worldSession);
                    }
                }
            }
        }
    }
}
