# Phase 7.1 — Curate `EchoWarpLinks` and inject one-way

Parent: [[phase7-00-overview]]. Depends on nothing — the source data
([plan/vanilla-warp-links-table.md](vanilla-warp-links-table.md#L102-L117)) is already parsed and
verified from the previous phase's `watcher-warp-links.tsv` extraction.

## The 15 rows (16 vanilla Echoes minus `lf_b01w`, already in `OverlayRegionWarpLinks`)

| Source Region | Source Room | Dest Region | Dest Room | Note |
|---|---|---|---|---|
| WARA | wara_p09 | WRSA | wrsa_c01 | dynamic in vanilla — Daemon redirect, see phase7-00 §2 |
| WARB | warb_j01 | WARA | wara_p05 | |
| WARC | warc_f01 | WARA | wara_e08 | |
| WARE | ware_i14 | WSKC | wskc_a03 | the room that started this phase |
| WARF | warf_b33 | WTDA | wtda_b12 | |
| WAUA | waua_bath | SB | sb_d07 | flagged — see phase7-00 "follow-up verification" |
| WAUA | waua_toys | WRSA | wrsa_c01 | dynamic in vanilla — Daemon redirect, see phase7-00 §2 |
| WBLA | wbla_d03 | WVWB | wvwb_b05 | |
| WPTA | wpta_f03 | WARA | wara_p08 | |
| WRFB | wrfb_a22 | WARE | ware_i01x | |
| WSKC | wskc_a23 | WPTA | WPTA_B10 | dest room casing preserved from source table |
| WTDA | wtda_z14 | WBLA | wbla_c01 | |
| WTDB | wtdb_a26 | WRFB | wrfb_d09 | |
| WVWA | wvwa_f03 | WARC | warc_c12 | |
| WVWB | wvwb_a04 | WARD | ward_r15 | |

## Implementation

1. New file `src/EchoWarpLinks.cs`, same shape as `BadWarpLinks.Link` (reuse the struct — it's
   already public and region-agnostic, no need for a second identical type). Table above becomes
   the `readonly Link[] All` body, with a doc comment pointing at this file and phase7-00 for the
   directionality/dynamic-echo rationale (don't re-litigate it inline, just cite it).
2. `CorruptedWarpInjectionHooks.Room_Loaded` currently calls a shared `InjectLinks` that always
   spawns both the forward leg (`roomName == link.SourceRoom`) and the return leg
   (`roomName == link.DestRoom`). That's correct for `BadWarpLinks`/`OverlayRegionWarpLinks` but
   wrong here — add a forward-only sibling (e.g. `InjectOneWayLinks`) that drops the
   `roomName == link.DestRoom` branch, and call it with `EchoWarpLinks.All` alongside the two
   existing `InjectLinks` calls. Keep using `Warps.BuildCorruptedWarpPlacedObject` (same
   `oneWay=true` Data flag for the `TrySpawnWarpPoint` dedup-gate workaround, same
   `BadWarpCosmetics()` — `OverlayRegionWarpLinks` already reused that builder for legitimate,
   non-corrupted crossings, so there's precedent for not needing a separate cosmetic here).
3. No `EstablishWorldsHooks` changes needed (phase7-00's "not in scope" section).

## Verification

- Build clean.
- In-game (Meadow Watcher lobby): confirm a WarpPoint now exists in `ware_i14` targeting
  `wskc_a03`, and that no destination room in the table above unexpectedly grew a *second*,
  uninvited return-leg warp (would indicate `InjectOneWayLinks` accidentally reused the two-way
  path).
- Separately flag `waua_bath` for a human playthrough check per phase7-00's mid-sequence-state
  caveat — not a build-time verification.

## Status

Implemented 2026-08-28: `src/EchoWarpLinks.cs` (15-row table above) and
`CorruptedWarpInjectionHooks.InjectOneWayLinks` (forward-leg-only sibling of `InjectLinks`, wired
in `Room_Loaded` alongside the two existing `InjectLinks` calls). Build succeeds clean (pre-existing
nullable warnings in unrelated files only).

Fix 2026-08-28 (later): user reported no portal in `WARE_I14` in-game. Cause: the curated
tables carry offline-extracted lowercase room names (`ware_i14`) but `AbstractRoom.name` is
world-file casing (`WARE_I14`), so `roomName == link.SourceRoom` never matched. Added
`CorruptedWarpInjectionHooks.SameRoom` (OrdinalIgnoreCase) and routed all three comparisons
in `InjectLinks` + `InjectOneWayLinks` through it — this also fixes `BadWarpLinks` /
`OverlayRegionWarpLinks`, which had the same latent mismatch. Rebuilt clean. Still needs the
in-game re-check per phase7-00's subtask 3.
