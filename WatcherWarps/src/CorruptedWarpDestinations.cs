namespace WatcherWarps
{
    // Curated "corrupted destination" rooms - Phase 4.3
    // (plan/phase4-03-destination-curation-followup.md), item 1.
    //
    // This is vanilla's own real levelBadWarpTargets list, not a guess: every
    // DynamicWarpTarget placed object with badWarp=true across the whole Watcher
    // room set (grepped mods/watcher/world/*-rooms/*_settings.txt), excluding
    // wora-prefixed rooms - vanilla's own ChooseDynamicWarpTarget explicitly
    // filters those out (Watcher.WarpPoint.decompiled.cs:1393: "!StartsWith('wora')").
    // These 7 rooms all fall in the four zero-fixed-connectivity gap regions
    // (wdsr, wgwr, whir, wsur) identified in plan/watcher-warp-links.tsv, which
    // matches the "off-the-beaten-path / narratively wrong" theming a bad warp
    // is meant to have.
    public static class CorruptedWarpDestinations
    {
        public static readonly (string Region, string Room)[] VanillaBadWarpTargets =
        {
            ("WDSR", "wdsr_a07"),
            ("WDSR", "wdsr_a19"),
            ("WGWR", "wgwr_a08"),
            ("WHIR", "whir_a18"),
            ("WHIR", "whir_b07"),
            ("WHIR", "whir_c04"),
            ("WSUR", "wsur_a40"),
        };
    }
}
