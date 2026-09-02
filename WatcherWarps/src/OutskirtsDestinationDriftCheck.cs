using System;

namespace WatcherWarps
{

    public static class OutskirtsDestinationDriftCheck
    {
        public static void Run()
        {
            foreach (var d in OutskirtsDestinations.All)
            {
                if (!IsDestination(d.Region, d.Room))
                {
                    Warps.Log?.LogWarning(
                        $"Watcher Warps: OutskirtsDestinations entry '{d.Key}' is not a destination in any of "
                        + "BadWarpLinks/OverlayRegionWarpLinks/EchoWarpLinks/CorruptedWarpDestinations - "
                        + "the Remix picker mirror has gone stale");
                }
            }
        }

        private static bool IsDestination(string region, string room)
        {
            foreach (var link in BadWarpLinks.All)
            {
                if (Matches(link.DestRegion, link.DestRoom, region, room)) return true;
            }

            foreach (var link in OverlayRegionWarpLinks.All)
            {
                if (Matches(link.DestRegion, link.DestRoom, region, room)) return true;
            }

            foreach (var link in EchoWarpLinks.All)
            {
                if (Matches(link.DestRegion, link.DestRoom, region, room)) return true;
            }

            foreach (var target in CorruptedWarpDestinations.VanillaBadWarpTargets)
            {
                if (Matches(target.Region, target.Room, region, room)) return true;
            }

            return false;
        }

        private static bool Matches(string aRegion, string aRoom, string bRegion, string bRoom)
        {
            return string.Equals(aRegion, bRegion, StringComparison.OrdinalIgnoreCase)
                && string.Equals(aRoom, bRoom, StringComparison.OrdinalIgnoreCase);
        }
    }
}
