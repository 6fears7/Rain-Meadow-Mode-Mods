using Watcher;

namespace WatcherWarps
{
    // Wires plan/phase4-01-injection-mechanism-investigation.md's chosen mechanism:
    // an On.Room.Loaded postfix building a throwaway PlacedObject via
    // Warps.BuildCorruptedWarpPlacedObject and handing it to Room.TrySpawnWarpPoint
    // (saveInRegionState: false), the same save-state-driven-not-room-content-driven
    // pattern vanilla's own Room.LoadWarpsFromSaveState uses. TrySpawnWarpPoint handles
    // dedup, WarpPoint construction, and room.warpPoints/updateList registration itself
    // - no manual list manipulation needed (see phase4-01's "Deliverable" section).
    //
    // Destination data (CorruptedWarpDestinations - vanilla's own levelBadWarpTargets)
    // and source-room selection (BadWarpLinks - a deterministic 20% sample of the real
    // WarpPoint rooms in plan/watcher-warp-links.tsv) are both real curated data now,
    // per plan/phase4-03-destination-curation-followup.md items 1 and 2. BadWarpLinks and
    // OverlayRegionWarpLinks are injected both ways: entering the source room's warp lands
    // at the destination, and entering the destination room's (separately injected) warp
    // lands back at that specific source - see BadWarpLinks for why each leg still uses
    // oneWay=true (Data.oneWayExit=true) internally despite the pair being functionally
    // two-way. EchoWarpLinks (Phase 7) is forward-only - see InjectOneWayLinks.
    public static class CorruptedWarpInjectionHooks
    {
        public static void Apply()
        {
            On.Room.Loaded += Room_Loaded;
        }

        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);

            if (!Warps.IsMeadowWatcher())
            {
                return;
            }

            string? roomName = self.abstractRoom?.name;
            if (roomName == null) return;

            // A dedicated source room can host more than one corrupted warp (WHIR and
            // WMPA each collapse multiple forward legs onto one room), and a destination
            // room can receive several return legs. Fan co-located injections out
            // horizontally from Warps.ReachablePos so their trigger volumes don't stack.
            // Room names in the link tables come from an offline TSV extraction and are
            // mostly lowercase, but AbstractRoom.name is the world-file casing (uppercase,
            // e.g. "WARE_I14"). Compare case-insensitively so a curated forward leg still
            // fires - a lowercase "ware_i14" would otherwise never match.
            int spawned = 0;
            InjectLinks(self, roomName, BadWarpLinks.All, ref spawned);
            InjectLinks(self, roomName, OverlayRegionWarpLinks.All, ref spawned);
            InjectOneWayLinks(self, roomName, EchoWarpLinks.All, ref spawned);
        }

        // Horizontal spacing (world units, ~1.5 tiles) between co-located injected warps.
        private const float SpreadStep = 30f;

        private static void InjectLinks(Room self, string roomName, BadWarpLinks.Link[] links, ref int spawned)
        {
            foreach (var link in links)
            {
                // Forward leg: entering the source room warps to the curated destination.
                if (SameRoom(roomName, link.SourceRoom))
                {
                    var po = Warps.BuildCorruptedWarpPlacedObject(link.DestRegion, link.DestRoom);
                    po.pos = SpreadPos(self, spawned++);
                    self.TrySpawnWarpPoint(po, saveInRegionState: false);
                }

                // Return leg: entering the destination room warps back to that specific source.
                if (SameRoom(roomName, link.DestRoom))
                {
                    var po = Warps.BuildCorruptedWarpPlacedObject(link.SourceRegion, link.SourceRoom);
                    po.pos = SpreadPos(self, spawned++);
                    self.TrySpawnWarpPoint(po, saveInRegionState: false);
                }
            }
        }

        // Phase 7 (plan/phase7-01-echo-warp-links.md): forward leg only, no return leg at
        // DestRoom - unlike InjectLinks' pairs, EchoWarpLinks entries are one-way conversions of
        // vanilla's inherently one-directional Echo/SpinningTopSpot prompts.
        private static void InjectOneWayLinks(Room self, string roomName, BadWarpLinks.Link[] links, ref int spawned)
        {
            foreach (var link in links)
            {
                if (SameRoom(roomName, link.SourceRoom))
                {
                    var po = Warps.BuildCorruptedWarpPlacedObject(link.DestRegion, link.DestRoom);
                    po.pos = SpreadPos(self, spawned++);
                    self.TrySpawnWarpPoint(po, saveInRegionState: false);
                }
            }
        }

        // Curated tables use offline-extracted (mostly lowercase) room names;
        // AbstractRoom.name is world-file casing. Match case-insensitively.
        private static bool SameRoom(string a, string b)
        {
            return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
        }

        // The Nth injected warp for this room, offset from the standable anchor in an
        // alternating fan (0, +step, -step, +2step, ...) and clamped inside the room.
        private static UnityEngine.Vector2 SpreadPos(Room self, int index)
        {
            var pos = Warps.ReachablePos(self);
            if (index > 0)
            {
                int step = (index + 1) / 2;
                float dir = (index % 2 == 1) ? 1f : -1f;
                pos.x += dir * step * SpreadStep;
                float margin = 20f * 2f; // keep clear of the outermost tiles
                pos.x = UnityEngine.Mathf.Clamp(pos.x, margin, self.TileWidth * 20f - margin);
            }
            return pos;
        }
    }
}
