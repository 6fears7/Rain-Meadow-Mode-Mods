using Watcher;

namespace WatcherWarps
{
    // RippleLevelFilterData.Active, PrinceFilterData.Active, and RippleEggFilterData.Active
    // (decompiled PlacedObject.cs:1237-1358) all start with
    // `if (roomSettings.game == null || !roomSettings.game.IsStorySession) return false;` -
    // a Meadow lobby is never a story session, so every placed object within range of one of
    // these three filters is unconditionally force-deactivated in every Meadow lobby today,
    // regardless of species or timeline. Under the Meadow+Watcher guard, report the
    // requirement as met instead of running the real save-state check; outside that guard,
    // fall through to orig so story-mode and non-Watcher-timeline Meadow lobbies keep vanilla
    // behavior. See plan/phase3-02-progression-filter-guard.md.
    public static class ProgressionFilterGuardHooks
    {
        public static void Apply()
        {
            On.PlacedObject.RippleLevelFilterData.Active += RippleLevelFilterData_Active;
            On.PlacedObject.PrinceFilterData.Active += PrinceFilterData_Active;
            On.PlacedObject.RippleEggFilterData.Active += RippleEggFilterData_Active;
        }

        private static bool RippleLevelFilterData_Active(On.PlacedObject.RippleLevelFilterData.orig_Active orig, PlacedObject.RippleLevelFilterData self, RoomSettings roomSettings, SlugcatStats.Timeline timelinePoint)
        {
            if (Warps.IsMeadowWatcher()) return true;
            return orig(self, roomSettings, timelinePoint);
        }

        private static bool PrinceFilterData_Active(On.PlacedObject.PrinceFilterData.orig_Active orig, PlacedObject.PrinceFilterData self, RoomSettings roomSettings, SlugcatStats.Timeline timelinePoint)
        {
            if (Warps.IsMeadowWatcher()) return true;
            return orig(self, roomSettings, timelinePoint);
        }

        private static bool RippleEggFilterData_Active(On.PlacedObject.RippleEggFilterData.orig_Active orig, PlacedObject.RippleEggFilterData self, RoomSettings roomSettings, SlugcatStats.Timeline timelinePoint)
        {
            if (Warps.IsMeadowWatcher()) return true;
            return orig(self, roomSettings, timelinePoint);
        }
    }
}
