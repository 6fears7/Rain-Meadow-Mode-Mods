namespace WatcherWarps
{
    public static class GhostSedaterNeuterHooks
    {
        public static void Apply()
        {
            On.GhostCreatureSedater.Update += GhostCreatureSedater_Update;
        }

        private static void GhostCreatureSedater_Update(On.GhostCreatureSedater.orig_Update orig, GhostCreatureSedater self, bool eu)
        {
            if (Warps.IsMeadowWatcher())
            {
                // Skip vanilla entirely: no restun, no destroy-and-den branch.
                // The sedater object just sits inert until the room unloads.
                return;
            }
            orig(self, eu);
        }
    }
}
