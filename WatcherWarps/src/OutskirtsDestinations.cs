namespace WatcherWarps
{
    // Phase 12.1 (plan/phase12-01-remix-outskirts-destination.md): the curated list of
    // destinations a player can pick, via the mod's Remix options tab, for the injected
    // Outskirts (SU_C04) warp portal from OutskirtsWarpHook.
    //
    // This table is a MANUAL MIRROR of the destination columns of
    // OverlayRegionWarpLinks.All, EchoWarpLinks.All, and CorruptedWarpDestinations.
    // VanillaBadWarpTargets - it is deliberately NOT derived from those tables at
    // runtime, so display order and labels stay under authorial control (a runtime
    // union would sort however the source tables happen to be ordered and would have
    // no good place to hang a human-readable Label). Subtask 12-01-D adds an
    // unconditional drift check (in Warps.Apply) that confirms every (Region, Room)
    // pair here still appears in one of those three tables, so this mirror going stale
    // - a renamed destination, a table entry removed - gets caught instead of silently
    // pointing the picker at a room the rest of the mod no longer supports.
    //
    // WARE/ware_h21 - the portal's old hardcoded destination, from before this phase -
    // is deliberately ABSENT (user decision D2, 2026-09-01): it never appeared as a
    // destination in any of the three link tables, so keeping it in a user-facing
    // picker would contradict "every option is a destination the mod already
    // supports." Do not add it back as an oversight fix; see the phase12-01 plan for
    // the full reasoning.
    public static class OutskirtsDestinations
    {
        public readonly struct Dest
        {
            public readonly string Key;
            public readonly string Region;
            public readonly string Room;
            public readonly string Label;
            public readonly string Desc;

            public Dest(string key, string region, string room, string label, string desc)
            {
                Key = key;
                Region = region;
                Room = room;
                Label = label;
                Desc = desc;
            }
        }


        public static readonly Dest[] All =
        {
            // Overlay crossings - reuse of the mod's existing vanilla-overlay-region routes.
            new Dest("WRFA/wrfa_sk04", "WRFA", "wrfa_sk04", "Coral Caves — wrfa_sk04", ""),
            new Dest("WSKA/wska_d13", "WSKA", "wska_d13", "Torrential Railways — wska_d13", ""),

            // Echo destinations (from EchoWarpLinks.All, de-duplicated; wrsa_c01 appears twice there).
            new Dest("WRSA/wrsa_c01", "WRSA", "wrsa_c01", "Daemon — wrsa_c01", ""),
            new Dest("WARA/wara_p05", "WARA", "wara_p05", "Shattered Terrace — wara_p05", ""),
            new Dest("WARA/wara_p08", "WARA", "wara_p08", "Shattered Terrace — wara_p08", ""),
            new Dest("WARA/wara_e08", "WARA", "wara_e08", "Shattered Terrace — wara_e08", ""),
            new Dest("WARC/warc_c12", "WARC", "warc_c12", "Fetid Glen — warc_c12", ""),
            new Dest("WARD/ward_r15", "WARD", "ward_r15", "Cold Storage — ward_r15", ""),
            new Dest("WARE/ware_i01x", "WARE", "ware_i01x", "Heat Ducts — ware_i01x", ""),
            new Dest("WBLA/wbla_c01", "WBLA", "wbla_c01", "Badlands — wbla_c01", ""),
            new Dest("WPTA/wpta_b10", "WPTA", "wpta_b10", "Signal Spires — wpta_b10", ""),
            new Dest("WRFB/wrfb_d09", "WRFB", "wrfb_d09", "Turbulent Pump — wrfb_d09", ""),
            new Dest("WSKC/wskc_a03", "WSKC", "wskc_a03", "Stormy Coast — wskc_a03", ""),
            new Dest("WTDA/wtda_b12", "WTDA", "wtda_b12", "Torrid Desert — wtda_b12", ""),
            new Dest("WVWB/wvwb_b05", "WVWB", "wvwb_b05", "Fractured Gateways — wvwb_b05", ""),

            // Corrupted / bad-warp targets (CorruptedWarpDestinations.VanillaBadWarpTargets,
            // i.e. vanilla's own levelBadWarpTargets).
            new Dest("WDSR/wdsr_a07", "WDSR", "wdsr_a07", "Decaying Tunnels — wdsr_a07", "Vanilla bad-warp target"),
            new Dest("WDSR/wdsr_a19", "WDSR", "wdsr_a19", "Decaying Tunnels — wdsr_a19", "Vanilla bad-warp target"),
            new Dest("WGWR/wgwr_a08", "WGWR", "wgwr_a08", "Infested Wastes — wgwr_a08", "Vanilla bad-warp target"),
            new Dest("WHIR/whir_a18", "WHIR", "whir_a18", "Corrupted Factories — whir_a18", "Vanilla bad-warp target"),
            new Dest("WHIR/whir_b07", "WHIR", "whir_b07", "Corrupted Factories — whir_b07", "Vanilla bad-warp target"),
            new Dest("WHIR/whir_c04", "WHIR", "whir_c04", "Corrupted Factories — whir_c04", "Vanilla bad-warp target"),
            new Dest("WSUR/wsur_a40", "WSUR", "wsur_a40", "Crumbling Fringes — wsur_a40", "Vanilla bad-warp target"),


            new Dest("SB/sb_d07", "SB", "sb_d07", "Subterranean — sb_d07", "scripted area, may misbehave"),
        };

        public const string DefaultKey = "WRFA/wrfa_sk04";


        public static bool TryGet(string? key, out Dest dest)
        {
            foreach (var d in All)
            {
                if (string.Equals(d.Key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    dest = d;
                    return true;
                }
            }

            dest = default;
            return false;
        }
    }
}
