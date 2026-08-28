using System.Runtime.CompilerServices;
using RainMeadow;
using RWCustom;

namespace WatcherWarps
{
    // Phase 7-05-04 fix for Face 2 ("first warper can't see a later warper until a
    // room change"). Root cause (7-05-00 log + decompile): when the later warper's
    // avatar OnlineCreature joins the destination region, RainMeadow's
    // OnlinePhysicalObject.JoinImpl runs its "early creature" branch because
    // AbstractCreature.AllowedToExistInRoom(realizedRoom) is false - for an AI
    // creature that predicate is only true once room.readyForAI, and the join lands
    // in the window where the realized room is shortCutsReady but NOT yet readyForAI.
    // The avatar is Abstractized (realizedCreature -> null) and RainMeadow never
    // re-checks once readyForAI flips (RoomSession.ActivateImpl's ApoEnteringRoom
    // sweep is one-shot), so it stays invisible until the viewer physically changes
    // rooms and a fresh ApoEnteringRoom runs.
    //
    // This nudge: once a realized room reaches readyForAI, realize any non-owned
    // avatar that is registered to this room's session but has no realizedCreature.
    // Cheap guard rails keep it a no-op in the normal case (creature already
    // realized, or not readyForAI yet, or the abstract creature's coordinate doesn't
    // resolve to this room - avoids the RealizeInRoom NRE seen on a stale coord).
    public static class RemoteAvatarRealizeHooks
    {
        // Per-room latch so the sweep runs once per room-load, on the tick readyForAI
        // first becomes true, not every frame afterward.
        private static readonly ConditionalWeakTable<Room, object> swept = new();

        public static void Apply()
        {
            On.Room.Update += Room_Update;
        }

        private static void Room_Update(On.Room.orig_Update orig, Room self)
        {
            orig(self);

            if (!Warps.IsMeadowWatcher()) return;
            if (self.abstractRoom == null || !self.readyForAI) return;
            if (swept.TryGetValue(self, out _)) return;
            swept.Add(self, new object());

            var lobby = OnlineManager.lobby;
            if (lobby == null) return;
            if (!RoomSession.map.TryGetValue(self.abstractRoom, out var roomSession)) return;
            if (!roomSession.isActive || !roomSession.isAvailable) return;

            foreach (var kv in lobby.playerAvatars)
            {
                if (kv.Value.FindEntity(true) is not OnlineCreature oc) continue;
                if (oc.isMine) continue;
                if (oc.roomSession != roomSession) continue;

                AbstractCreature ac = oc.abstractCreature;
                if (ac == null || ac.realizedCreature != null) continue;
                if (ac.slatedForDeletion || ac.state?.dead == true) continue;
                if (!ac.AllowedToExistInRoom(self)) continue;

                // The owner often serializes a stale coordinate (the SOURCE room index)
                // for a freshly warped avatar; RealizeInRoom NREs on a coord that does
                // not resolve to this room. Rewrite it to this room before realizing.
                if (ac.pos.room != self.abstractRoom.index || !ac.pos.TileDefined)
                {
                    ac.pos = new WorldCoordinate(self.abstractRoom.index,
                        self.TileWidth / 2, self.TileHeight / 2, -1);
                }

                ac.RealizeInRoom();
                Warps.Log?.LogInfo(
                    $"Watcher Warps: phase 7-05-04 - realized late-arriving remote avatar {ac.ID} " +
                    $"in {self.abstractRoom.name} after readyForAI (was left abstract by RainMeadow's " +
                    "early-creature branch)");
            }
        }
    }
}
