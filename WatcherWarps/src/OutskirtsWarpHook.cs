using Watcher;

namespace WatcherWarps
{

    public static class OutskirtsWarpHook
    {
        private const string OutskirtsRoom = "SU_C04";

        public static void Apply()
        {
            On.Room.Loaded += Room_Loaded;
        }

        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);

            if (!Warps.IsMeadowWatcher()) return;
            if (self.abstractRoom?.name != OutskirtsRoom) return;

            string? configuredKey = WatcherWarpsOptions.Instance?.OutskirtsDestination?.Value;


            bool usedFallback;
            OutskirtsDestinations.Dest dest;
            if (OutskirtsDestinations.TryGet(configuredKey, out dest))
            {
                usedFallback = false;
            }
            else
            {
                if (string.IsNullOrEmpty(configuredKey))
                {
                    Warps.Log?.LogWarning(
                        "Watcher Warps: no Outskirts portal destination configured (Remix options not registered?); "
                        + $"using {OutskirtsDestinations.DefaultKey}");
                }
                else
                {
                    Warps.Log?.LogWarning(
                        $"Watcher Warps: configured Outskirts portal destination '{configuredKey}' is not a known "
                        + $"destination; falling back to {OutskirtsDestinations.DefaultKey}");
                }

                OutskirtsDestinations.TryGet(OutskirtsDestinations.DefaultKey, out dest);
                usedFallback = true;
            }

            var po = Warps.BuildCorruptedWarpPlacedObject(dest.Region, dest.Room);
            po.pos = Warps.ReachablePos(self);
            self.TrySpawnWarpPoint(po, saveInRegionState: false);
            Warps.Log?.LogInfo(
                $"Watcher Warps: Outskirts warp spawned in {OutskirtsRoom} at {po.pos} -> {dest.Region}/{dest.Room} "
                + $"({(usedFallback ? "fallback" : "config")})");
        }
    }
}
