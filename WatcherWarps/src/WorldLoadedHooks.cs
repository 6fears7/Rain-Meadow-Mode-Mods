using RainMeadow;
using RWCustom;
using UnityEngine;
using Watcher;

namespace WatcherWarps
{
    // Generalizes OverWorld_WorldLoaded's post-orig `game.Players` loop
    // (Rain-Meadow/Game/RainMeadow.EntityHooks.cs ~705-728) to the local
    // non-Player Meadow avatar. That loop only ever reads `absplayer.realizedCreature
    // is Player player`, so a Lizard/Scavenger/etc. avatar's own AbstractCreature rides
    // along with the world swap (driven by OnlineManager.lobby.playerAvatars in the
    // beingMoved-toggle loop above it, already avatar-species-agnostic) but its
    // grasped objects never get registered into the new WorldSession and its position
    // never gets fixed up from the warp's destPos.
    //
    // slugOnBack and objectInStomach are declared directly on Player (confirmed via
    // ilspycmd against PUBLIC-Assembly-CSharp.dll - grasps is Creature-level and
    // inherited, the other two are not), and nothing in Watcher content lets a
    // non-Player creature swallow objects or carry a rescued slugcat, so there's no
    // non-Player analogue to replicate for those two - only the position fixup and
    // grasp carry apply here.
    //
    // Only the warpUsed branch is replicated: the plain-gate branch only carries
    // objectInStomach (Player-only, N/A here) because ordinary gate crossings keep
    // the room's entity list intact and grasped objects already follow along with it;
    // it's only the region-swapping warpUsed path that needs objects manually
    // registered into the new WorldSession.
    public static class WorldLoadedHooks
    {
        public static void Apply()
        {
            On.OverWorld.WorldLoaded += OverWorld_WorldLoaded;
        }

        private static void OverWorld_WorldLoaded(On.OverWorld.orig_WorldLoaded orig, OverWorld self, bool warpUsed)
        {
            orig(self, warpUsed);

            if (!warpUsed) return;
            if (!CreatureWarpState.TryGetLocalNonPlayerAvatar(out Creature avatar)) return;
            if (avatar.dead || avatar.slatedForDeletetion) return;

            WorldSession newWorldSession = self.activeWorld.GetResource();
            if (newWorldSession == null) return;

            // Fix the arrival tile the way vanilla's own Player loop does ("do not get
            // stuck on bottom left"). destPos is normally non-null by now - vanilla's
            // WarpPoint.NewWorldLoaded backfills a null one from a PlacedObject.Type.
            // DynamicWarpTarget in the destination room, and ArrivalMarkerHooks guarantees
            // every reachable room has one. If it's still null/zero here (marker chain
            // didn't run yet, or the destination room isn't realized), resolve a safe tile
            // ourselves rather than leaving the avatar at (0,0). WarpPointTriggerHooks'
            // HandleExitWarp then corrects the exact position once the destination warp
            // point runs its ExitWarp state.
            Vector2? destPos = self.warpData?.destPos;
            if (destPos == null || destPos.Value == Vector2.zero)
            {
                destPos = ResolveDestPosFallback(self);
            }

            if (destPos != null && destPos.Value != Vector2.zero)
            {
                avatar.abstractCreature.pos.Tile = new IntVector2(
                    (int)(destPos.Value.x / 20f),
                    (int)(destPos.Value.y / 20f));
            }
            else
            {
                Warps.Log?.LogWarning(
                    $"Watcher Warps: warp into {self.warpData?.destRoom ?? "?"} had no resolvable destPos; " +
                    "relying on the destination warp point's ExitWarp placement to position the avatar");
            }

            RegisterGrasps(avatar, newWorldSession);
        }

        // Last-resort arrival position when OverWorld.warpData.destPos is null/zero:
        // an authored DynamicWarpTarget in the (realized) destination room, else a
        // standable tile in it. Returns null if the room isn't realized yet.
        private static Vector2? ResolveDestPosFallback(OverWorld self)
        {
            string? destRoom = self.warpData?.destRoom;
            if (destRoom == null) return null;

            Room? room = self.activeWorld?.GetAbstractRoom(destRoom)?.realizedRoom;
            if (room == null) return null;

            if (room.roomSettings?.placedObjects != null)
            {
                foreach (var po in room.roomSettings.placedObjects)
                {
                    if (po.type == PlacedObject.Type.DynamicWarpTarget)
                    {
                        return po.pos;
                    }
                }
            }

            return Warps.ReachablePos(room);
        }

        private static void RegisterGrasps(Creature avatar, WorldSession newWorldSession)
        {
            if (avatar.grasps == null) return;
            for (int k = 0; k < avatar.grasps.Length; k++)
            {
                if (avatar.grasps[k]?.grabbed != null)
                {
                    newWorldSession.ApoEnteringWorld(avatar.grasps[k].grabbed.abstractPhysicalObject);
                }
            }
        }
    }
}
