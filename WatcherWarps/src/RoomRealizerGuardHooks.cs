namespace WatcherWarps
{
    // phase 7-06 A-2: belt-and-braces guard against a permanent client hang.
    //
    // RoomRealizer.Update()'s follow-creature block (decompiled) is:
    //
    //     if (lastFrameFollowCreatureRoom != followCreature.pos.room
    //         && !followCreature.Room.offScreenDen           // <-- NRE, no null check
    //         && !IsRoomRecentlyAbstracted(followCreature.Room))
    //     { ... }
    //     lastFrameFollowCreatureRoom = followCreature.pos.room;   // <-- never reached on throw
    //
    // AbstractCreature.Room resolves world.GetAbstractRoom(pos.room) against the
    // creature's OWN world field, and World.GetAbstractRoom returns null (no throw)
    // for an out-of-range index. A fresh RoomRealizer has lastFrameFollowCreatureRoom
    // == 0, so the guard fires on the first Update after every warp; if followCreature
    // .Room is null it throws, never advances lastFrameFollowCreatureRoom, and throws
    // again every frame forever - the exception unwinds RainWorldGame.Update so every
    // subsystem after roomRealizer.Update() (shortcutHandler, room activation, our own
    // OverWorld.Update poll) stops running. A hard hang.
    //
    // A-1 fixes the specific cause (non-Player avatar world never migrated). This is
    // insurance for any other path that can desync followCreature.world (deaths,
    // spectator, another mod). If it ever fires, A-1 is incomplete - it is not the fix.
    //
    // Do NOT reassign self.followCreature here: RainMeadow's pin hook
    // (RainMeadow.GameplayHooks.cs:1177) rewrites cameras[0].followAbstractCreature
    // from self.followCreature every tick, so fighting over it desyncs the camera.
    // Repair the creature's world, or skip the tick.
    public static class RoomRealizerGuardHooks
    {
        public static void Apply()
        {
            On.RoomRealizer.Update += RoomRealizer_Update;
        }

        private static void RoomRealizer_Update(On.RoomRealizer.orig_Update orig, RoomRealizer self)
        {
            if (Warps.IsMeadowWatcher() && self.followCreature is AbstractCreature fc && fc.Room == null)
            {
                if (fc.world != self.world && self.world?.GetAbstractRoom(fc.pos.room) != null)
                {
                    // pos was already right for the new world; only the world pointer lagged.
                    fc.world = self.world;
                }
                else
                {
                    Warps.Log?.LogError(
                        $"Watcher Warps: RoomRealizer guard - followCreature {fc.ID} pos.room {fc.pos.room} " +
                        $"unresolvable in world {self.world?.name ?? "null"}; skipping realizer tick to avoid a hard hang");
                    return;
                }
            }

            orig(self);
        }
    }
}
