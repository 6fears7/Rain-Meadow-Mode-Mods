# Vanilla-Authored Watcher Warp Links (reference table)

Generated from `plan/watcher-warp-links.tsv` (not modified). These are the **vanilla room-content**
warp points and Echoes — the mod does not inject them; its hooks only make them function in a
Meadow Watcher lobby.

Destinations shown are the **mod-effective** destinations:
- Rows where the TSV had `NULL` / `NULL` are vanilla dynamic warp points. In a Meadow sandbox
  `ChooseDynamicWarpTarget` can't resolve, so `DaemonWarpRedirectHooks` (Phase 5.6) rewrites each
  to **Daemon — `WRSA` / `wrsa_c01`**, one-way in. These are marked with `*`.
- `wssr_lab6` → `WORA` / `NULL`: **not** redirected — the hook only fires when *both* destRegion
  and destRoom are null, and this row's destRegion is `WORA`. Left as authored (dest room unset).

Region display names from `plan/watcher-warping-support.md`. 84 `WarpPoint` + 16 `Echo` = 100 rows.

| Kind | Source Region | Src Acronym | Source Room | Dest Region | Dest Acronym | Dest Room |
|---|---|---|---|---|---|---|
| WarpPoint | Salination | WARB | warb_f01 | Heat Ducts | WARE | ware_h21 |
| WarpPoint | Salination | WARB | warb_f18 | Fetid Glen | WARC | WARC_B12 |
| WarpPoint | Salination | WARB | warb_j08 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Fetid Glen | WARC | warc_b07 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Fetid Glen | WARC | warc_b12 | Salination | WARB | warb_f18 |
| WarpPoint | Fetid Glen | WARC | warc_e11 | Fractured Gateways | WVWB | wvwb_e01 |
| WarpPoint | Cold Storage | WARD | ward_b41 | Shrouded Stacks | WSKD | wskd_b42 |
| WarpPoint | Cold Storage | WARD | ward_e01 | The Surface | WARG | warg_d06_future |
| WarpPoint | Cold Storage | WARD | ward_e09 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Cold Storage | WARD | ward_e12 | Migration Path | WMPA | wmpa_c07 |
| WarpPoint | Cold Storage | WARD | ward_e33 | Stormy Coast | WSKC | wskc_a10 |
| WarpPoint | Cold Storage | WARD | ward_r10 | Unfortunate Evolution | WSSR | wssr_cramped |
| WarpPoint | Heat Ducts | WARE | ware_g15 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Heat Ducts | WARE | ware_h16 | Torrid Desert | WTDA | wtda_b16 |
| WarpPoint | Heat Ducts | WARE | ware_h21 | Salination | WARB | warb_f01 |
| WarpPoint | Aether Ridge | WARF | warf_a06 | Torrential Railways | WSKA | wska_d13 |
| WarpPoint | Aether Ridge | WARF | warf_b11 | Coral Caves | WRFA | wrfa_f01 |
| WarpPoint | Aether Ridge | WARF | warf_b14 | Sunbaked Alley | WSKB | wskb_c18 |
| WarpPoint | Aether Ridge | WARF | warf_d06 | Shrouded Stacks | WSKD | wskd_b38 |
| WarpPoint | Aether Ridge | WARF | warf_d15 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Aether Ridge | WARF | warf_d30 | Migration Path | WMPA | wmpa_a09 |
| WarpPoint | The Surface | WARG | warg_a06_future | Desolate Tract | WTDB | wtdb_a04 |
| WarpPoint | The Surface | WARG | warg_b31 | Torrid Desert | WTDA | wtda_a13 |
| WarpPoint | The Surface | WARG | warg_d06_future | Cold Storage | WARD | ward_e01 |
| WarpPoint | The Surface | WARG | warg_h20 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | The Surface | WARG | warg_w11 | Shrouded Stacks | WSKD | wskd_b12 |
| WarpPoint | Badlands | WBLA | wbla_b08 | Migration Path | WMPA | wmpa_d09 |
| WarpPoint | Badlands | WBLA | wbla_j01 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Decaying Tunnels | WDSR | wdsr_a25 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Infested Wastes | WGWR | wgwr_c09 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Infested Wastes | WGWR | wgwr_disposal | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Corrupted Factories | WHIR | whir_a06 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Corrupted Factories | WHIR | whir_a22 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Corrupted Factories | WHIR | whir_b13 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Migration Path | WMPA | wmpa_a09 | Aether Ridge | WARF | warf_d30 |
| WarpPoint | Migration Path | WMPA | wmpa_c06 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Migration Path | WMPA | wmpa_c07 | Cold Storage | WARD | ward_e12 |
| WarpPoint | Migration Path | WMPA | wmpa_d09 | Badlands | WBLA | wbla_b08 |
| WarpPoint | Outer Rim | WORA | wora_egg04 | Outer Rim | WORA | wora_dial |
| WarpPoint | Outer Rim | WORA | wora_starcatcher03 | Daemon | WRSA | WRSA_C01 |
| WarpPoint | Pillar Grove | WPGA | wpga_a02 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Pillar Grove | WPGA | wpga_b08 | Coral Caves | WRFA | wrfa_a21 |
| WarpPoint | Pillar Grove | WPGA | wpga_b10 | Torrential Railways | WSKA | wska_n04 |
| WarpPoint | Pillar Grove | WPGA | wpga_e01 | Shrouded Stacks | WSKD | wskd_b01 |
| WarpPoint | Signal Spires | WPTA | wpta_c05 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Signal Spires | WPTA | wpta_c07 | Verdant Waterways | WVWA | wvwa_h01 |
| WarpPoint | Coral Caves | WRFA | wrfa_a21 | Pillar Grove | WPGA | wpga_b08 |
| WarpPoint | Coral Caves | WRFA | wrfa_b09 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Coral Caves | WRFA | wrfa_d08 | Rusted Wrecks | WRRA | wrra_b01 |
| WarpPoint | Coral Caves | WRFA | wrfa_f01 | Aether Ridge | WARF | warf_b11 |
| WarpPoint | Turbulent Pump | WRFB | wrfb_b12 | Verdant Waterways | WVWA | wvwa_e01 |
| WarpPoint | Turbulent Pump | WRFB | wrfb_c07 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Rusted Wrecks | WRRA | wrra_a07 | Sunbaked Alley | WSKB | wskb_c07 |
| WarpPoint | Rusted Wrecks | WRRA | wrra_a26 | Desolate Tract | WTDB | wtdb_a19 |
| WarpPoint | Rusted Wrecks | WRRA | wrra_b01 | Coral Caves | WRFA | wrfa_d08 |
| WarpPoint | Rusted Wrecks | WRRA | wrra_l01 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Daemon | WRSA | wrsa_c01 | Outer Rim | WORA | wora_starcatcher03 |
| WarpPoint | Daemon | WRSA | wrsa_d01 | Shattered Terrace | WARA | WARA_P17 |
| WarpPoint | Torrential Railways | WSKA | wska_d07 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Torrential Railways | WSKA | wska_d13 | Aether Ridge | WARF | warf_a06 |
| WarpPoint | Torrential Railways | WSKA | wska_n04 | Pillar Grove | WPGA | wpga_b10 |
| WarpPoint | Sunbaked Alley | WSKB | wskb_c07 | Rusted Wrecks | WRRA | wrra_a07 |
| WarpPoint | Sunbaked Alley | WSKB | wskb_c18 | Aether Ridge | WARF | warf_b14 |
| WarpPoint | Sunbaked Alley | WSKB | wskb_n01 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Stormy Coast | WSKC | wskc_a10 | Cold Storage | WARD | ward_e33 |
| WarpPoint | Stormy Coast | WSKC | wskc_a25 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Shrouded Stacks | WSKD | wskd_b01 | Pillar Grove | WPGA | wpga_e01 |
| WarpPoint | Shrouded Stacks | WSKD | wskd_b12 | The Surface | WARG | warg_w11 |
| WarpPoint | Shrouded Stacks | WSKD | wskd_b34 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Shrouded Stacks | WSKD | wskd_b38 | Aether Ridge | WARF | warf_d06 |
| WarpPoint | Shrouded Stacks | WSKD | wskd_b42 | Cold Storage | WARD | ward_b41 |
| WarpPoint | Unfortunate Evolution | WSSR | wssr_lab6 | Outer Rim | WORA | NULL |
| WarpPoint | Crumbling Fringes | WSUR | wsur_b09 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Torrid Desert | WTDA | wtda_a13 | The Surface | WARG | warg_b31 |
| WarpPoint | Torrid Desert | WTDA | wtda_b16 | Heat Ducts | WARE | ware_h16 |
| WarpPoint | Torrid Desert | WTDA | wtda_z07 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Desolate Tract | WTDB | wtdb_a03 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Desolate Tract | WTDB | wtdb_a04 | The Surface | WARG | warg_a06_future |
| WarpPoint | Desolate Tract | WTDB | wtdb_a19 | Rusted Wrecks | WRRA | wrra_a26 |
| WarpPoint | Verdant Waterways | WVWA | wvwa_a09 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Verdant Waterways | WVWA | wvwa_e01 | Turbulent Pump | WRFB | wrfb_b12 |
| WarpPoint | Verdant Waterways | WVWA | wvwa_h01 | Signal Spires | WPTA | wpta_c07 |
| WarpPoint | Fractured Gateways | WVWB | wvwb_b02 | Daemon | WRSA | wrsa_c01 * |
| WarpPoint | Fractured Gateways | WVWB | wvwb_e01 | Fetid Glen | WARC | warc_e11 |
| Echo | Farm Arrays | LF | lf_b01w | Coral Caves | WRFA | wrfa_sk04 |
| Echo | Shattered Terrace | WARA | wara_p09 | Daemon | WRSA | wrsa_c01 * |
| Echo | Salination | WARB | warb_j01 | Shattered Terrace | WARA | wara_p05 |
| Echo | Fetid Glen | WARC | warc_f01 | Shattered Terrace | WARA | wara_e08 |
| Echo | Heat Ducts | WARE | ware_i14 | Stormy Coast | WSKC | wskc_a03 |
| Echo | Aether Ridge | WARF | warf_b33 | Torrid Desert | WTDA | wtda_b12 |
| Echo | Ancient Urban | WAUA | waua_bath | Subterranean | SB | sb_d07 |
| Echo | Ancient Urban | WAUA | waua_toys | Daemon | WRSA | wrsa_c01 * |
| Echo | Badlands | WBLA | wbla_d03 | Fractured Gateways | WVWB | wvwb_b05 |
| Echo | Signal Spires | WPTA | wpta_f03 | Shattered Terrace | WARA | wara_p08 |
| Echo | Turbulent Pump | WRFB | wrfb_a22 | Heat Ducts | WARE | ware_i01x |
| Echo | Stormy Coast | WSKC | wskc_a23 | Signal Spires | WPTA | WPTA_B10 |
| Echo | Torrid Desert | WTDA | wtda_z14 | Badlands | WBLA | wbla_c01 |
| Echo | Desolate Tract | WTDB | wtdb_a26 | Turbulent Pump | WRFB | wrfb_d09 |
| Echo | Verdant Waterways | WVWA | wvwa_f03 | Fetid Glen | WARC | warc_c12 |
| Echo | Fractured Gateways | WVWB | wvwb_a04 | Cold Storage | WARD | ward_r15 |

`*` = destination is not authored; assigned by `DaemonWarpRedirectHooks` (was `NULL`/`NULL` in the TSV). One-way in.

Other notes:
- Room-name casing is verbatim from the TSV (some rows upper-case, e.g. `WARC_B12`, `WRSA_C01`).
- `lf_b01w` (Farm Arrays) and `waua_bath`→`SB` (Subterranean) are the only crossings touching a non-Watcher region.
- `wora_starcatcher03` → `WRSA_C01` is an **authored** Daemon link, not a redirect.
