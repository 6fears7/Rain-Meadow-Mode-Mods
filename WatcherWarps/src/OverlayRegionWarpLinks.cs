namespace WatcherWarps
{
    // Phase 5.5 (plan/phase5-05-optional-region-warp-points.md): reachable warp points
    // bridging the vanilla overlay regions Watcher establishes worlds for (CC / SH / LF)
    // to Watcher-native story regions. In vanilla these crossings are spinning tops /
    // Echoes; a Meadow sandbox has no spinning top, so without an injected warp point
    // nothing traverses them even though EstablishWorldsHooks makes the worlds exist.
    //
    // Destinations point at Watcher story regions (WRFA / WSKA), not the vanilla overlay
    // regions themselves - the user chose this to sidestep the mid-sequence-state risk
    // the plan flagged for arriving cold into a scripted overlay area.
    //
    // Links are two-way (user decision 2026-08-27): the source room gets an injected warp
    // to the destination, and the destination room gets a separate warp back to that
    // specific source - identical mechanics to BadWarpLinks.
    //
    // Consumed by CorruptedWarpInjectionHooks.Room_Loaded alongside BadWarpLinks.All.
    public static class OverlayRegionWarpLinks
    {
        // User-curated 2026-08-27. Each source room hosts a vanilla spinning top / Echo
        // that in the Watcher campaign is the front door into the Watcher region cluster
        // ("swap the echo location with the warp"). Each destination is an already-authored
        // crossing room reused verbatim rather than an arbitrary pick:
        //   - wrfa_sk04 is the target of the vanilla lf_b01w spinning top (watcher-warp-
        //     links.tsv, kind=Echo).
        //   - wska_d13 is the target of the authored warp_a06 -> WSKA WarpPoint pair
        //     (watcher-warp-links.tsv, kind=WarpPoint).
        // HI and SU were explicitly dropped by the user; WSSR left for later.
        public static readonly BadWarpLinks.Link[] All =
        {
            new BadWarpLinks.Link("CC", "CC_C12", "WRFA", "wrfa_sk04"),     // Chimney Canopy -> Coral Caves
            new BadWarpLinks.Link("SH", "SH_A08", "WSKA", "wska_d13"),      // Shaded Citadel -> Torrential Railways
            new BadWarpLinks.Link("LF", "LF_B01W", "WRFA", "wrfa_sk04"),    // Farm Arrays -> Coral Caves (vanilla-authored crossing)
        };
    }
}
