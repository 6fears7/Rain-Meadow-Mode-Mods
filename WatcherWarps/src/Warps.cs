using System;
using BepInEx.Logging;
using RainMeadow;
using RWCustom;
using UnityEngine;
using Watcher;

namespace WatcherWarps
{
    public static class Warps
    {
        internal static ManualLogSource? Log;

        public static bool IsMeadowWatcher()
        {
            return OnlineManager.lobby?.gameMode is MeadowGameMode
                && OnlineManager.lobby.meadowTimeline == WatcherEnums.SlugcatStatsName.Watcher.value;
        }

        public static void Apply(ManualLogSource log)
        {
            Log = log;
            WarpPointTriggerHooks.Apply();
            AvatarWarpStateTickHooks.Apply(); // phase 8-01: tick down CWT warp cooldowns
            SuckInCreaturesHooks.Apply();
            ChangeStateHooks.Apply();
            WorldLoadedHooks.Apply();
            RoomRealizerGuardHooks.Apply(); // phase 7-06 A-2
            RainCycleGuardHooks.Apply(); // phase 7-07 B
            GhostSedaterNeuterHooks.Apply(); // phase11-01: stop GhostCreatureSedater stunlocking non-Slugcat avatars
            AvatarWarpReentryHooks.Apply();
            OrphanAvatarDiagnosticHooks.Apply(); // TEMP phase 7-05-00; remove after 7-05-03
            RemoteAvatarRealizeHooks.Apply();
            PerformWarpStripHooks.Apply();
            ProgressionFilterGuardHooks.Apply();
            CorruptedWarpInjectionHooks.Apply();
            ArrivalMarkerHooks.Apply();
            DaemonWarpRedirectHooks.Apply();
            EstablishWorldsHooks.Apply();
            TimelineRegionGuardHooks.Apply();
            CosmeticWhitelistHooks.Apply();
            ShortcutGraphicsGuardHooks.Log = log;
            ShortcutGraphicsGuardHooks.Apply();
            TestWarpHook.Apply(); // TEMP: remove once real routes are curated (see TestWarpHook.cs)
            Log.LogInfo("Watcher Warps: trigger-detection, suck-in, change-state, world-loaded, performwarp-strip, progression-filter-guard, corrupted-warp-injection, arrival-marker, daemon-warp-redirect, establish-worlds, timeline-region-guard, cosmetic-whitelist, and shortcut-graphics-guard hooks applied");
        }

        public static PlacedObject BuildCorruptedWarpPlacedObject(string destRegion, string destRoom)
        {
            var po = new PlacedObject(PlacedObject.Type.WarpPoint, null);
            var data = new WarpPoint.WarpPointData(po)
            {
                effectSettings = WarpPoint.WarpPointData.EffectSettings.BadWarpCosmetics(),
                destRegion = destRegion,
                destRoom = destRoom,
                destPos = null,
                oneWay = true,
                oneWayEntrance = false,
            };
            po.data = data;
            return po;
        }


        public static Vector2 ReachablePos(Room room)
        {
            var centre = new IntVector2(room.TileWidth / 2, room.TileHeight / 2);

            IntVector2? best = null;
            int bestDist = int.MaxValue;
            IntVector2? loose = null;
            int looseDist = int.MaxValue;

            for (int x = 1; x < room.TileWidth - 1; x++)
            {
                for (int y = 1; y < room.TileHeight - 1; y++)
                {
                    var t = new IntVector2(x, y);
                    int d = (x - centre.x) * (x - centre.x) + (y - centre.y) * (y - centre.y);

                    var terrain = room.GetTile(t).Terrain;
                    if (terrain != Room.Tile.TerrainType.Solid
                        && terrain != Room.Tile.TerrainType.ShortcutEntrance
                        && d < looseDist)
                    {
                        looseDist = d;
                        loose = t;
                    }

                    if (IsStandable(room, t) && d < bestDist)
                    {
                        bestDist = d;
                        best = t;
                    }
                }
            }

            if (best.HasValue) return room.MiddleOfTile(best.Value);
            if (loose.HasValue) return room.MiddleOfTile(loose.Value);

            if (room.shortcuts != null)
            {
                foreach (var sc in room.shortcuts)
                {
                    if (sc.shortCutType == ShortcutData.Type.RoomExit)
                    {
                        return room.MiddleOfTile(sc.StartTile);
                    }
                }
            }

            return room.MiddleOfTile(centre);
        }

        private static bool IsStandable(Room room, IntVector2 t)
        {
            var terrain = room.GetTile(t).Terrain;
            if (terrain == Room.Tile.TerrainType.Solid
                || terrain == Room.Tile.TerrainType.Slope
                || terrain == Room.Tile.TerrainType.ShortcutEntrance)
            {
                return false;
            }

            var below = room.GetTile(new IntVector2(t.x, t.y - 1)).Terrain;
            var above = room.GetTile(new IntVector2(t.x, t.y + 1)).Terrain;

            return Blocky(below) && !Blocky(above);
        }

        private static bool Blocky(Room.Tile.TerrainType tt)
        {
            return tt == Room.Tile.TerrainType.Solid
                || tt == Room.Tile.TerrainType.Slope
                || tt == Room.Tile.TerrainType.ShortcutEntrance;
        }
    }
}
