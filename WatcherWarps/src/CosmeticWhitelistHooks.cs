using System;
using System.Collections.Generic;
using HarmonyLib;
using RainMeadow;
using Watcher;

namespace WatcherWarps
{

    public static class CosmeticWhitelistHooks
    {
        private static readonly HashSet<PlacedObject.Type> extraAllowed = new()
        {
            PlacedObject.Type.WarpPoint,
            WatcherEnums.PlacedObjectType.BigSkyWhaleSpawner,
            WatcherEnums.PlacedObjectType.BigSkyWhaleTrigger,
            PlacedObject.Type.SkyWhalePathfinding, // base enum: the spawner finds no path without it
        };

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
            if (__result || item?.type == null || !extraAllowed.Contains(item.type)) return;
            if (!Warps.IsMeadowWatcher()) return;

            __result = true;
        }
    }
}
