# Phase 5.5 — Warp points into the optional overlay regions (CC etc.)

Parent: [phase5-00-overview.md](phase5-00-overview.md). Spun off from
[[phase5-04-establishworlds-gap-region-override]] on 2026-08-27.

## The gap

[[phase5-04-establishworlds-gap-region-override]] made the game *establish a synced `WorldSession`*
for all 9 Watcher optional regions (`WHIR WSUR WDSR WGWR WSSR HI SU CC SH`) in a Watcher-timeline
Meadow lobby. That's necessary but not sufficient: a player still needs a *reachable warp point*
whose `destRegion`/`destRoom` lands them in those regions.

- `WHIR WSUR WDSR WGWR` — already covered: every `BadWarpLinks` entry targets one of these, and
  `CorruptedWarpInjectionHooks` injects a warp point into each link's source room.
- `CC HI SU SH WSSR` — **not covered.** Per `watcher-warping-support.md` Phase 6 research, these
  base-game overlay regions contain zero placed `WarpPoint` objects. In vanilla they're reached via
  a spinning top / Echo (`Watcher.SpinningTopData.CreateWarpPointData` — an Echo is a `WarpPoint`
  with a conversation panel bolted on), or by normal Watcher-campaign traversal. A Meadow sandbox
  has neither, so right now nothing warps a player into them even though their worlds now exist.

## What needs deciding (content, not code)

For each of `CC`, `HI`, `SU`, `SH`, `WSSR` (confirm this is the right set — `WSSR` "Unfortunate
Evolution" may be a scripted-sequence area like `waua`/`wara` and want the same individual
verification Phase 6 flagged for those):

1. **A reachable source room** to host the injected warp point — a room already in the Watcher
   story-region set (so it loads without this whole problem recursing). Candidates: reuse the
   already-authored vanilla-crossing Echo rooms Phase 6 identified (`lf_b01w` → `wrfa_sk04` is
   `LF`→Watcher, not helpful here; check `watcher-warp-links.tsv` `kind=Echo` rows for any
   Watcher-region → `CC`/`HI`/`SU`/`SH` spinning tops to reuse their source/dest verbatim).
2. **A destination room** in each target region — its entrance / a safe spawn room, hand-verified
   not to assume mid-sequence state.
3. Whether these should be two-way (like `BadWarpLinks`) or one-way in.

## Implementation once curated

Mechanically identical to `CorruptedWarpInjectionHooks` — add the curated links to a data table
(mirror `BadWarpLinks.cs`) and either extend that hook or add a sibling one that calls
`self.TrySpawnWarpPoint(Warps.BuildCorruptedWarpPlacedObject(destRegion, destRoom), saveInRegionState: false)`
when `roomName` matches a source room. Note the current `CorruptedWarpInjectionHooks.Room_Loaded`
body still has placeholder hardcoded `"WARE"/"ware_g15"` / `"SU"/"SU_C04"` values instead of
iterating `link.DestRoom`/`link.DestRegion` — fix that at the same time.

## Status (2026-08-27)

- **Code path done.** `src/OverlayRegionWarpLinks.cs` added (mirrors `BadWarpLinks`, reuses
  `BadWarpLinks.Link`). `CorruptedWarpInjectionHooks.Room_Loaded` refactored into a shared
  `InjectLinks(self, roomName, links)` helper called for both `BadWarpLinks.All` and
  `OverlayRegionWarpLinks.All`. Builds clean.
- **Placeholder bug fixed** at the same time: `Room_Loaded` no longer hardcodes
  `"WARE"/"ware_g15"` / `"SU"/"SU_C04"` — it now iterates `link.DestRegion`/`link.DestRoom`
  (forward leg) and `link.SourceRegion`/`link.SourceRoom` (return leg).
- Directionality decided: **two-way** (user, 2026-08-27).

## Curated content (user, 2026-08-27)

`OverlayRegionWarpLinks.All` is now populated, two-way, builds clean:

| Source (spinning-top room) | Destination (reused authored crossing) |
|---|---|
| `CC` / `CC_C12` (Chimney Canopy) | `WRFA` / `wrfa_sk04` (Coral Caves) |
| `SH` / `SH_A08` (Shaded Citadel) | `WSKA` / `wska_d13` (Torrential Railways) |
| `LF` / `LF_B01W` (Farm Arrays) | `WRFA` / `wrfa_sk04` (Coral Caves) |

Design change vs. the original framing: destinations are Watcher **story** regions, not the
vanilla overlays — the user chose this to avoid arriving cold into a scripted overlay area.
`HI` and `SU` were explicitly dropped. `WSSR` deferred.

## Remaining verification

- `CC` and `SH` are in [[phase5-04-establishworlds-gap-region-override]]'s established list.
  `LF` was added to that list too (2026-08-27, user: "LF should be reachable so add it") — the
  array in `src/EstablishWorldsHooks.cs` is now `ForceEstablishRegions` and includes `"LF"`.
  Still worth a runtime log check that `LF` resolves in `Region.WarpRegions` (if the Watcher
  timeline omits it entirely the hook logs a warning and skips).
- One in-game check that a warp completes into `wrfa_sk04` / `wska_d13` from these rooms.
