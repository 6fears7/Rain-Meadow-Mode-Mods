using System;
using HarmonyLib;
using RainMeadow;

namespace WatcherWarps
{
    // Rain Meadow's OnlineGameMode.cosmeticItems (GameModes/OnlineGameModeHelpers.cs) is
    // the whitelist OnlineGameMode.AllowedInMode / FilterItems use to decide which placed
    // objects survive into a Meadow lobby. As shipped it does NOT contain
    // PlacedObject.Type.WarpPoint, so every WarpPoint in a room - including the ones this
    // mod's curated routes rely on - gets item.active = false during FilterItems and never
    // spawns.
    //
    // Rather than mutate the shipped HashSet (version-fragile, and it's a field initializer
    // that also feeds non-Watcher lobbies), postfix AllowedInMode and force-allow WarpPoint
    // only under the Meadow+Watcher guard. MeadowGameMode.AllowedInMode calls
    // base.AllowedInMode, which is this same method, so the override still sees the true.
    public static class CosmeticWhitelistHooks
    {
        public static void Apply()
        {
            var target = AccessTools.Method(typeof(OnlineGameMode), nameof(OnlineGameMode.AllowedInMode));
            if (target == null) throw new MissingMethodException("OnlineGameMode.AllowedInMode not found - Rain Meadow version mismatch?");

            new Harmony(WatcherWarpsPlugin.ModId).Patch(
                target,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(CosmeticWhitelistHooks), nameof(AllowedInModePostfix))));
        }

        private static void AllowedInModePostfix(PlacedObject item, ref bool __result)
        {
            if (__result) return;
            if (item?.type != PlacedObject.Type.WarpPoint) return;
            if (!Warps.IsMeadowWatcher()) return;

            __result = true;
        }
    }
}
