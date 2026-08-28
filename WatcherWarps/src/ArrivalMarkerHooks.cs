using Watcher;

namespace WatcherWarps
{
    // Plants a PlacedObject.Type.DynamicWarpTarget marker (at a real walkable position)
    // into every room this mod could ever route a warp into, so
    // Watcher.WarpPoint.NewWorldLoaded's own destPos fallback (decompiled.cs:3660-3672 -
    // "if the injected warp's destPos is null, look for a DynamicWarpTarget in the
    // destination room and use its pos") always finds one. Vanilla's real bad-warp rooms
    // (CorruptedWarpDestinations) already carry an authored DynamicWarpTarget, which is
    // why only this mod's *other* injected destinations - BadWarpLinks return legs,
    // OverlayRegionWarpLinks (both directions), DaemonWarpRedirectHooks' WRSA target,
    // TestWarpHook's destination - crashed with "Nullable object must have a value"
    // before this hook existed. A DynamicWarpTarget entry carries no data payload beyond
    // .pos (confirmed: every reader only touches placedObject.pos), so a bare
    // `new PlacedObject(Type.DynamicWarpTarget, null)` is sufficient.
    //
    // Runs unconditionally per room (not keyed to specific link tables) so any future
    // curated destination is covered automatically without updating a room list here.
    // Safe: this only affects the local roomSettings.placedObjects list vanilla's warp
    // code reads live off the just-loaded Room instance - it does not touch the
    // boot-time-scanned levelDynamicWarpTargets/regionDynamicWarpTargets caches that
    // drive vanilla's own live RNG dynamic-warp rolls, so it can't skew those.
    public static class ArrivalMarkerHooks
    {
        public static void Apply()
        {
            On.Room.Loaded += Room_Loaded;
        }

        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);

            if (!Warps.IsMeadowWatcher()) return;
            if (self.roomSettings?.placedObjects == null) return;

            foreach (var po in self.roomSettings.placedObjects)
            {
                if (po.type == PlacedObject.Type.DynamicWarpTarget) return;
            }

            var marker = new PlacedObject(PlacedObject.Type.DynamicWarpTarget, null)
            {
                pos = Warps.ReachablePos(self),
            };
            self.roomSettings.placedObjects.Add(marker);
        }
    }
}
