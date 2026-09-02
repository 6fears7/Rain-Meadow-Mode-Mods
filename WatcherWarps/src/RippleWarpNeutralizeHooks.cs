using Watcher;

namespace WatcherWarps
{
    // Phase 14-01 (plan/phase14-01-daemon-ripple-warp-gate.md).
    //
    // Watcher.WarpPoint.transportable requires
    //   room.game.ActiveRippleLayer == 1 == Data.rippleWarp
    // RainWorldGame.ActiveRippleLayer is Players[0].rippleLayer, which is permanently 0 in a
    // Meadow sandbox (no StorySession, and nothing raises a player's ripple layer there). So
    // every rippleWarp=true point - both authored Daemon (WRSA) exits and 21 of the 28
    // NULL/NULL dynamic points DaemonWarpRedirectHooks retargets - is non-transportable
    // forever. Neutralizing the flag (rather than forcing ActiveRippleLayer to 1) is the fix:
    // forcing the ripple layer is global and would break every ordinary rippleWarp=false warp,
    // and would disable rain-cycle progression (RainWorldGame.ShouldTickCycle-equivalent check
    // requires ActiveRippleLayer == 0).
    //
    // This costs the ripple visual/audio flourish on these points (WarpCircleRipple shader,
    // ripple idle loop) - acceptable; see plan §2 for the full accounting of what rippleWarp
    // otherwise drives.
    public static class RippleWarpNeutralizeHooks
    {
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
                if (data == null || !data.rippleWarp) continue; // idempotent: skip already-cleared points

                data.rippleWarp = false;
                wp.refreshGraphics = true; // forces DrawSprites to re-InitiateSprites with the non-ripple shader

                Warps.Log?.LogInfo(
                    $"Watcher Warps: cleared rippleWarp on point in {self.abstractRoom?.name} -> {data.RegionString}/{data.destRoom}");
            }
        }
    }
}
