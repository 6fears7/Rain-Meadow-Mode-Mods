# All Injected Warp Links (WatcherWarps mod)

Every warp link this mod injects via `CorruptedWarpInjectionHooks` in a Meadow Watcher lobby.
All links are **two-way**: the source room gets an injected warp to the destination, and the
destination room gets a separate injected warp back to that specific source.

Sources of truth:
- `src/BadWarpLinks.cs` — `BadWarpLinks.All` (17 links; sources are dedicated in-region rooms, one per region, never shared with a real WarpPoint — see class comment)
- `src/OverlayRegionWarpLinks.cs` — `OverlayRegionWarpLinks.All` (3 links; vanilla overlay-region spinning-top rooms)

Region display names from `plan/watcher-warping-support.md`.

## BadWarpLinks.All

| Source Region (display name) | Source Acronym | Source Room | Destination Region (display name) | Destination Acronym | Destination Room |
|---|---|---|---|---|---|
| Torrential Railways | WSKA | wska_d27 | Decaying Tunnels | WDSR | wdsr_a07 |
| Corrupted Factories | WHIR | whir_a27 | Decaying Tunnels | WDSR | wdsr_a19 |
| Torrid Desert | WTDA | wtda_b13 | Infested Wastes | WGWR | wgwr_a08 |
| Migration Path | WMPA | wmpa_d07 | Corrupted Factories | WHIR | whir_a18 |
| Infested Wastes | WGWR | wgwr_a14 | Corrupted Factories | WHIR | whir_b07 |
| Corrupted Factories | WHIR | whir_a27 | Corrupted Factories | WHIR | whir_c04 |
| Desolate Tract | WTDB | wtdb_a11 | Crumbling Fringes | WSUR | wsur_a40 |
| Cold Storage | WARD | ward_r21 | Decaying Tunnels | WDSR | wdsr_a07 |
| Coral Caves | WRFA | wrfa_a05 | Decaying Tunnels | WDSR | wdsr_a19 |
| Fetid Glen | WARC | warc_e07 | Infested Wastes | WGWR | wgwr_a08 |
| Decaying Tunnels | WDSR | wdsr_a11 | Corrupted Factories | WHIR | whir_a18 |
| Corrupted Factories | WHIR | whir_a27 | Corrupted Factories | WHIR | whir_b07 |
| Pillar Grove | WPGA | wpga_a09 | Corrupted Factories | WHIR | whir_c04 |
| Signal Spires | WPTA | wpta_b10 | Crumbling Fringes | WSUR | wsur_a40 |
| Shrouded Stacks | WSKD | wskd_b05 | Decaying Tunnels | WDSR | wdsr_a07 |
| Migration Path | WMPA | wmpa_d07 | Decaying Tunnels | WDSR | wdsr_a19 |
| The Surface | WARG | warg_a01_future | Infested Wastes | WGWR | wgwr_a08 |

## OverlayRegionWarpLinks.All

| Source Region (display name) | Source Acronym | Source Room | Destination Region (display name) | Destination Acronym | Destination Room |
|---|---|---|---|---|---|
| Chimney Canopy | CC | CC_C12 | Coral Caves | WRFA | wrfa_sk04 |
| Shaded Citadel | SH | SH_A08 | Torrential Railways | WSKA | wska_d13 |
| Farm Arrays | LF | LF_B01W | Coral Caves | WRFA | wrfa_sk04 |
