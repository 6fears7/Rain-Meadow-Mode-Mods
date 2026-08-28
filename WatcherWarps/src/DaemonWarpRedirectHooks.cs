using Watcher;

namespace WatcherWarps
{
    // Phase 5.6 (plan/phase5-06-null-warps-to-daemon.md).
    //
    // Vanilla's dynamic / "unstable" warp points ship with destRegion/destRoom == null
    // (the NULL/NULL rows in plan/watcher-warp-links.tsv) and resolve a destination at
    // trigger time via Watcher.WarpPoint.ChooseDynamicWarpTarget, which reads
    // world.game.GetStorySession.saveState campaign counters and rolls the Watcher
    // campaign seed (decompiled Watcher.WarpPoint.decompiled.cs:1133-1223). A Meadow
    // sandbox has neither, so these points can never resolve - the same reason Phase 4
    // hand-authored BadWarpLinks instead of calling ChooseDynamicWarpTarget.
    //
    // User decision 2026-08-27: give every one of them a fixed destination of Daemon
    // (region WRSA, arrival room wrsa_c01 - the authored target of the vanilla
    // wora_starcatcher03 -> WRSA crossing). One-way in; Daemon keeps its own authored
    // exits (wrsa_c01 -> wora_starcatcher03, wrsa_d01 -> WARA_P17).
    //
    // Redirect, not inject: these points already exist as placed objects at authored
    // positions and Room.AddObject registers every WarpPoint into room.warpPoints
    // (phase4-01), so we just rewrite the WarpPointData in place - no Vector2.zero
    // placement guesswork (cf. TestWarpHook's note about CorruptedWarpInjectionHooks).
    public static class DaemonWarpRedirectHooks
    {
        private const string DaemonRegion = "WRSA";
        private const string DaemonRoom = "wrsa_c01";

        public static void Apply()
        {
            On.Room.Loaded += Room_Loaded;
        }

        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);

            if (!Warps.IsMeadowWatcher()) return;
            if (self.warpPoints == null) return;

            foreach (WarpPoint wp in self.warpPoints)
            {
                WarpPoint.WarpPointData data = wp?.Data;
                if (data == null) continue;

                // Only the unauthored dynamic points. Once rewritten destRegion is set,
                // so this is idempotent across re-entry / re-load.
                if (data.destRegion != null || data.destRoom != null) continue;

                data.destRegion = DaemonRegion;
                data.destRoom = DaemonRoom;
                data.destPos = null;
                data.cycleExpiry = 0;          // nonDynamicWarpPoint => true; skips ChooseDynamicWarpTarget
                data.oneWay = true;
                data.oneWayEntrance = false;   // => Data.oneWayExit == true
                data.effectSettings = WarpPoint.WarpPointData.EffectSettings.BadWarpCosmetics();
                data.destCam = WarpPoint.GetDestCam(data); // ctor computed this against a null dest

                Warps.Log?.LogInfo(
                    $"Watcher Warps: redirected dynamic warp point in {self.abstractRoom?.name} -> {DaemonRegion}/{DaemonRoom}");
            }
        }
    }
}
