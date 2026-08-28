namespace WatcherWarps
{
    // Phase 7 (plan/phase7-00-overview.md, plan/phase7-01-echo-warp-links.md): standing user
    // requirement that anywhere vanilla places an Echo/SpinningTopSpot sighting, a Meadow lobby
    // gets a real, walk-into WarpPoint there instead - a Meadow sandbox has no spinning-top/panel
    // interaction, so without this the Echo's destination is unreachable.
    //
    // These are the 15 vanilla Echo rows (plan/vanilla-warp-links-table.md, kind=Echo) not already
    // covered by OverlayRegionWarpLinks (which handles lf_b01w -> wrfa_sk04). Unlike
    // BadWarpLinks/OverlayRegionWarpLinks, these are ONE-WAY (user decision 2026-08-28): vanilla
    // Echoes are inherently one-directional prompts, and inventing a return leg at every
    // destination room would add connectivity vanilla never authored. Consumed by
    // CorruptedWarpInjectionHooks.Room_Loaded via InjectOneWayLinks (forward leg only, no return
    // leg at DestRoom).
    //
    // wara_p09 and waua_toys are vanilla-dynamic (NULL/NULL) Echoes - redirected here to a literal
    // WRSA/wrsa_c01 (Daemon), matching how dynamic vanilla WarpPoints are redirected elsewhere
    // (DaemonWarpRedirectHooks / phase5-06-null-warps-to-daemon.md), rather than depending on that
    // hook actually catching an Echo-backed SpinningTopSpot (left unconfirmed there).
    //
    // waua_bath -> sb_d07 is flagged in phase7-00 for a follow-up in-game check: Ancient Urban is a
    // scripted dream-sequence area whose rooms may assume mid-sequence state a cold arrival breaks.
    public static class EchoWarpLinks
    {
        public static readonly BadWarpLinks.Link[] All =
        {
            new BadWarpLinks.Link("WARA", "wara_p09", "WRSA", "wrsa_c01"),
            new BadWarpLinks.Link("WARB", "warb_j01", "WARA", "wara_p05"),
            new BadWarpLinks.Link("WARC", "warc_f01", "WARA", "wara_e08"),
            new BadWarpLinks.Link("WARE", "ware_i14", "WSKC", "wskc_a03"),
            new BadWarpLinks.Link("WARF", "warf_b33", "WTDA", "wtda_b12"),
            new BadWarpLinks.Link("WAUA", "waua_bath", "SB", "sb_d07"),
            new BadWarpLinks.Link("WAUA", "waua_toys", "WRSA", "wrsa_c01"),
            new BadWarpLinks.Link("WBLA", "wbla_d03", "WVWB", "wvwb_b05"),
            new BadWarpLinks.Link("WPTA", "wpta_f03", "WARA", "wara_p08"),
            new BadWarpLinks.Link("WRFB", "wrfb_a22", "WARE", "ware_i01x"),
            new BadWarpLinks.Link("WSKC", "wskc_a23", "WPTA", "WPTA_B10"),
            new BadWarpLinks.Link("WTDA", "wtda_z14", "WBLA", "wbla_c01"),
            new BadWarpLinks.Link("WTDB", "wtdb_a26", "WRFB", "wrfb_d09"),
            new BadWarpLinks.Link("WVWA", "wvwa_f03", "WARC", "warc_c12"),
            new BadWarpLinks.Link("WVWB", "wvwb_a04", "WARD", "ward_r15"),
        };
    }
}
