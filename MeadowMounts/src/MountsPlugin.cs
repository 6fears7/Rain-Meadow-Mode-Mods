using System;
using System.Security.Permissions;
using BepInEx;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace MeadowMounts
{
    [BepInPlugin(ModId, "Meadow Mounts", Version)]
    [BepInDependency("henpemaz.rainmeadow", BepInDependency.DependencyFlags.SoftDependency)]
    public class MountsPlugin : BaseUnityPlugin
    {
        public const string ModId = "uo.meadowmounts";
        public const string Version = "0.1.1";

        private const string ModInfoId = "uo_meadowmounts";

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
                    Logger.LogWarning("Rain Meadow is not loaded; Meadow Mounts is inactive");
                    applied = true;
                    return;
                }

                Mounts.Apply(Logger);
                MachineConnector.SetRegisteredOI(ModInfoId, MountOptions.Instance);
                applied = true;
                Logger.LogInfo("Meadow Mounts: hooks applied");
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}
