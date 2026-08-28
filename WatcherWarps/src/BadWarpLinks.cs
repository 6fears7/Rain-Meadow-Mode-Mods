namespace WatcherWarps
{
    // Which real WarpPoint rooms host an injected "bad warp" companion, and which
    // curated CorruptedWarpDestinations room each one links to - Phase 4.3 item 2
    // (plan/phase4-03-destination-curation-followup.md), resolved as: 20% of the 84
    // real WarpPoint source rooms in plan/watcher-warp-links.tsv, deterministically
    // selected (not runtime RNG - a Meadow lobby has no per-client RNG-seed sync for
    // this, per plan/phase4-02-corrupted-warpdata-preset.md's reasoning for avoiding
    // ChooseDynamicWarpTarget). Selection is reproducible: sort all 84 (region, room)
    // pairs by md5("REGION_room"), take the first 17 (round(84 * 0.2)), and assign
    // each one a CorruptedWarpDestinations entry round-robin in that sorted order.
    // Regenerate with:
    //   awk -F'\t' 'NR>1 && $1=="WarpPoint" {print toupper($2)"\t"$3}' watcher-warp-links.tsv \
    //     | while IFS=$'\t' read -r r n; do echo -e "$(printf '%s' "${r}_${n}" | md5sum | cut -d' ' -f1)\t$r\t$n"; done \
    //     | sort | head -17
    //
    // Dedicated source rooms (user decision 2026-08-27): a corrupted warp must NOT
    // share a room with a real vanilla WarpPoint. Each region that originally hosted a
    // corrupted forward leg now has ONE dedicated room - a quiet in-region dead-end (or
    // near-dead-end where no true dead-end was available), never a WarpPoint room, an
    // Echo/SpinningTopSpot room, or a merchant/collectible room. Picked from the installed
    // Watcher world files (mods/watcher/world/<reg>-rooms + world/<reg>/map_<reg>.txt):
    //   WSKA -> wska_d27   WHIR -> whir_a27   WTDA -> wtda_b13   WMPA -> wmpa_d07
    //   WGWR -> wgwr_a14    WTDB -> wtdb_a11   WARD -> ward_r21   WRFA -> wrfa_a05
    //   WARC -> warc_e07    WDSR -> wdsr_a11   WPGA -> wpga_a09   WPTA -> wpta_b10
    //   WSKD -> wskd_b05    WARG -> warg_a01_future
    // Regions with corruption theming (whir/wgwr/wdsr/ward) got a PassiveCorruption /
    // PrinceFilter / CorruptionTube room; the rest got the quietest available dead-end.
    // WHIR (3 links) and WMPA (2 links) share one dedicated room each - co-located
    // injections are fanned out horizontally by CorruptedWarpInjectionHooks so the
    // trigger volumes don't stack.
    //
    // Each link is two-way: the source room gets an injected warp to the destination,
    // and the destination room gets a separate injected warp back to that specific
    // source (CorruptedWarpInjectionHooks fires for both SourceRoom and DestRoom).
    // Both directions use Warps.BuildCorruptedWarpPlacedObject's oneWay=true (i.e.
    // Data.oneWayExit=true) - not because either leg is meant to be one-way, but
    // because it's what bypasses TrySpawnWarpPoint's non-dynamic destRoom-must-be-
    // already-referenced gate on both legs (see plan/phase4-01-injection-mechanism-
    // investigation.md) - there's no authored room content on either side of an
    // injected link for that gate to match against. A destination room can be the
    // target of more than one source (round-robin only cycles through 7 curated
    // destinations for 17 sources); TrySpawnWarpPoint dedups by destRoom, not by
    // room, so each distinct return leg still spawns as its own WarpPoint.
    public static class BadWarpLinks
    {
        public readonly struct Link
        {
            public readonly string SourceRegion;
            public readonly string SourceRoom;
            public readonly string DestRegion;
            public readonly string DestRoom;

            public Link(string sourceRegion, string sourceRoom, string destRegion, string destRoom)
            {
                SourceRegion = sourceRegion;
                SourceRoom = sourceRoom;
                DestRegion = destRegion;
                DestRoom = destRoom;
            }
        }

        public static readonly Link[] All =
        {
            new Link("WSKA", "wska_d27", "WDSR", "wdsr_a07"),
            new Link("WHIR", "whir_a27", "WDSR", "wdsr_a19"),
            new Link("WTDA", "wtda_b13", "WGWR", "wgwr_a08"),
            new Link("WMPA", "wmpa_d07", "WHIR", "whir_a18"),
            new Link("WGWR", "wgwr_a14", "WHIR", "whir_b07"),
            new Link("WHIR", "whir_a27", "WHIR", "whir_c04"),
            new Link("WTDB", "wtdb_a11", "WSUR", "wsur_a40"),
            new Link("WARD", "ward_r21", "WDSR", "wdsr_a07"),
            new Link("WRFA", "wrfa_a05", "WDSR", "wdsr_a19"),
            new Link("WARC", "warc_e07", "WGWR", "wgwr_a08"),
            new Link("WDSR", "wdsr_a11", "WHIR", "whir_a18"),
            new Link("WHIR", "whir_a27", "WHIR", "whir_b07"),
            new Link("WPGA", "wpga_a09", "WHIR", "whir_c04"),
            new Link("WPTA", "wpta_b10", "WSUR", "wsur_a40"),
            new Link("WSKD", "wskd_b05", "WDSR", "wdsr_a07"),
            new Link("WMPA", "wmpa_d07", "WDSR", "wdsr_a19"),
            new Link("WARG", "warg_a01_future", "WGWR", "wgwr_a08"),
        };
    }
}
