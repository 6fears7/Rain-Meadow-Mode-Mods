using System;
using System.Security.Permissions;
using BepInEx;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace WatcherWarps
{
    [BepInPlugin(ModId, "Watcher Warps", Version)]
    [BepInDependency("henpemaz.rainmeadow", BepInDependency.DependencyFlags.SoftDependency)]
    public class WatcherWarpsPlugin : BaseUnityPlugin
    {
        public const string ModId = "uo.watcherwarps";
        public const string Version = "0.1.0";

        private bool applied;

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            if (applied) return;
            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("henpemaz.rainmeadow"))
                {
                    Logger.LogWarning("Rain Meadow is not loaded; Watcher Warps is inactive");
                    applied = true;
                    return;
                }

                Warps.Apply(Logger);
                applied = true;
                Logger.LogInfo("Watcher Warps: hooks applied");
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}
