using Watcher;

namespace WatcherWarps
{
    // TEMPORARY test scaffold (user request 2026-08-27): drops a single injected warp
    // point into SU_C04 so the end-to-end path (inject -> trigger -> PerformWarp ->
    // arrive in a force-established Watcher region) can be exercised without needing a
    // curated route. Destination is an arbitrary real Watcher story room.
    //
    // Unlike CorruptedWarpInjectionHooks, this sets PlacedObject.pos explicitly - a
    // WarpPoint takes its world position straight from placedObject.pos
    // (Watcher.WarpPoint ctor, decompiled line 1097), so the default Vector2.zero would
    // put the trigger in the bottom-left corner (usually solid geometry) and nothing
    // could ever walk into it. Placement here targets a room-exit shortcut mouth, or
    // failing that the nearest non-solid tile to room centre.
    //
    // Remove this file (and its Warps.Apply() line) once real routes are curated.
    public static class TestWarpHook
    {
        private const string TestRoom = "SU_C04";
        private const string DestRegion = "WARE";
        private const string DestRoom = "ware_h21";

        public static void Apply()
        {
            On.Room.Loaded += Room_Loaded;
        }

        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);

            if (!Warps.IsMeadowWatcher()) return;
            if (self.abstractRoom?.name != TestRoom) return;

            var po = Warps.BuildCorruptedWarpPlacedObject(DestRegion, DestRoom);
            po.pos = Warps.ReachablePos(self);
            self.TrySpawnWarpPoint(po, saveInRegionState: false);
            Warps.Log?.LogInfo($"Watcher Warps: TEST warp spawned in {TestRoom} at {po.pos} -> {DestRegion}/{DestRoom}");
        }
    }
}
