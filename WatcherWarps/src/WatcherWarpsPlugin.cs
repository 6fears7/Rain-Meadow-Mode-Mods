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

        // Deliberately different from ModId: this is Mod/modinfo.json's "id" field, which is
        // what MachineConnector.SetRegisteredOI keys on (via ModManager.InstalledMods[i].id).
        // ModId above is the BepInPlugin GUID, used for BepInEx's own plugin registry and the
        // Rain Meadow soft-dependency check. SetRegisteredOI silently fails (no exception, no
        // tab) if handed the wrong one, so do not "simplify" these into a single constant.
        public const string RemixModId = "uo_watcherwarps";

        // Keep in sync with Mod/modinfo.json's "version". Rain Meadow's sync_required_mods check
        // compares the modinfo version across clients, so a lobby whose members run different
        // builds under the same version number silently diverges instead of being rejected.
        public const string Version = "0.1.2";

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
                WatcherWarpsOptions.Instance = new WatcherWarpsOptions();
                if (MachineConnector.SetRegisteredOI(RemixModId, WatcherWarpsOptions.Instance))
                    MachineConnector.ReloadConfig(WatcherWarpsOptions.Instance);   // pull the saved value now, not on first menu open
                else
                    Logger.LogWarning($"Watcher Warps: SetRegisteredOI('{RemixModId}') returned false; Remix tab unavailable");

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
