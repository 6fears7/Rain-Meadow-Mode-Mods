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

        // Shared gate for every warp hook in this mod: only run in a Meadow lobby whose
        // selected timeline is Watcher, so story-mode and non-Watcher-timeline Meadow
        // lobbies keep vanilla behavior untouched. See plan/phase5-01-guard-consolidation-decision.md.
        public static bool IsMeadowWatcher()
        {
            return OnlineManager.lobby?.gameMode is MeadowGameMode
                && OnlineManager.lobby.meadowTimeline == WatcherEnums.SlugcatStatsName.Watcher.value;
        }

        public static void Apply(ManualLogSource log)
        {
            Log = log;
            WarpPointTriggerHooks.Apply();
            SuckInCreaturesHooks.Apply();
            ChangeStateHooks.Apply();
            WorldLoadedHooks.Apply();
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

        // Builds a one-off "corrupted destination" WarpPoint's backing PlacedObject,
        // ready for Room.TrySpawnWarpPoint. Mirrors vanilla's own
        // Room.LoadWarpsFromSaveState pattern (a save-state-driven PlacedObject with no
        // room-content backing) rather than authoring a _settings.txt entry, since this
        // mod ships no room content of its own (see plan/phase4-00-overview.md).
        //
        // oneWay/oneWayEntrance=false makes Data.oneWayExit true, matching a bad-warp's
        // one-directional theming (no reciprocal return trip) and, per
        // plan/phase4-01-injection-mechanism-investigation.md, also bypasses
        // TrySpawnWarpPoint's non-dynamic "destRoom must already be referenced by a real
        // placed object in this room" sanity gate.
        //
        // Callers MUST set po.pos (to Warps.ReachablePos(room) or an authored spot) before
        // handing this to Room.TrySpawnWarpPoint - a WarpPoint takes its world position
        // straight from placedObject.pos, and the default Vector2.zero would drop the
        // trigger (and, for a return leg, the arrival) in the bottom-left corner.
        public static PlacedObject BuildCorruptedWarpPlacedObject(string destRegion, string destRoom)
        {
            var po = new PlacedObject(PlacedObject.Type.WarpPoint, null);
            var data = new WarpPoint.WarpPointData(po)
            {
                effectSettings = WarpPoint.WarpPointData.EffectSettings.BadWarpCosmetics(),
                destRegion = destRegion,
                destRoom = destRoom,
                // Left null on purpose: Watcher.WarpPoint.NewWorldLoaded backfills a null
                // destPos from a PlacedObject.Type.DynamicWarpTarget in the destination room
                // (decompiled.cs:3660-3672) - matching vanilla's own real bad-warp rooms,
                // which all carry one. ArrivalMarkerHooks plants that marker (at a real
                // walkable position) in every room this mod could route someone into, so
                // this fallback always finds one instead of NREing on a still-null Nullable.
                destPos = null,
                oneWay = true,
                oneWayEntrance = false,
            };
            po.data = data;
            return po;
        }

        // Finds a real, standable tile in an already-loaded room, for planting an arrival
        // marker (see ArrivalMarkerHooks) or a warp trigger. A WarpPoint/PlacedObject takes
        // its world position straight from placedObject.pos (Watcher.WarpPoint ctor,
        // decompiled line 1097), and OverWorld.WorldLoaded's arrival fixup drops the avatar
        // at the warp's destPos, so an unvetted position risks landing in solid geometry or
        // the bottom-left corner (0,0).
        //
        // "Standable" mirrors Rain Meadow's own placement test in
        // MeadowGameMode.RegisterPlacesInRoom's ValidPlacement: a non-solid, non-shortcut
        // tile with solid/slope footing below and open headroom above. Prefers the standable
        // tile nearest room centre; then any non-solid tile nearest centre; then (last
        // resort, old behavior) a room-exit shortcut mouth; then room centre. Never (0,0).
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

        // Mirrors MeadowGameMode.RegisterPlacesInRoom's ValidPlacement: a non-solid,
        // non-shortcut tile whose tile below is solid/slope/shortcut (footing) and whose
        // tile above is not (headroom).
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
