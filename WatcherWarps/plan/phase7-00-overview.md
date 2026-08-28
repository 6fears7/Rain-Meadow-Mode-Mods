# Phase 7 — Convert every remaining vanilla Echo into a real WarpPoint — Overview

Parent plan: [watcher-warping-support.md](watcher-warping-support.md) §2b (Echoes are the same
mechanism as WarpPoints, not a separate one).

## User decision (2026-08-28)

Standing requirement for this mod: **anywhere vanilla places an Echo/SpinningTopSpot sighting, a
Meadow lobby should get a real, walk-into WarpPoint there instead**, using the Echo's own authored
`destRegion`/`destRoom`. Motivated by the user noticing `WARE_I14` has no portal in a Meadow
Watcher lobby — it's vanilla-authored as an Echo (`ware_i14` → `WSKC`/`wskc_a03`,
[plan/vanilla-warp-links-table.md:106](vanilla-warp-links-table.md#L106)), and a Meadow sandbox has
no spinning-top/panel interaction to trigger it, so today nothing there ever transitions.

This generalizes what Phase 5.5 already did for exactly one Echo (`lf_b01w`, in
[src/OverlayRegionWarpLinks.cs](../src/OverlayRegionWarpLinks.cs), comment: "swap the echo location
with the warp") to **all 16** vanilla Echo rows in
[plan/vanilla-warp-links-table.md](vanilla-warp-links-table.md#L102-L117).

## Scope decisions (user-confirmed 2026-08-28)

1. **Directionality: one-way per Echo**, not two-way like `BadWarpLinks`/`OverlayRegionWarpLinks`.
   Vanilla Echoes are inherently one-directional prompts — a room hosts a spinning top that offers
   one destination; the return trip (if any) is vanilla's own separate Echo elsewhere (e.g.
   `wskc_a23` → `WPTA`/`WPTA_B10` is its own row, not the reverse of `ware_i14`). Inventing a return
   leg at every destination room would add connectivity vanilla never authored. So: inject the
   forward leg only, at the Echo's source room.
2. **The 2 dynamic (`NULL`/`NULL`) Echoes — `wara_p09` and `waua_toys` — redirect to Daemon**
   (`WRSA`/`wrsa_c01`), matching how dynamic vanilla WarpPoints are already handled
   ([[phase5-06-null-warps-to-daemon]]). Rather than depend on that existing runtime-redirect hook
   (which phase5-06 left unconfirmed for Echo-backed points specifically — Echoes are a
   `SpinningTopSpot` placed object, not a `WarpPoint`, so it's unverified whether
   `DaemonWarpRedirectHooks` even sees them), these two get a literal hardcoded
   `WRSA`/`wrsa_c01` destination in the same curated list as the other 13, sidestepping the open
   question entirely.

## Not in scope / already handled

- `lf_b01w` → `wrfa_sk04` — already injected via `OverlayRegionWarpLinks.All`. Do not duplicate.
- Region-graph access for the destination regions (`WARA`, `WSKC`, `WTDA`, `WBLA`, `WVWB`, `WARD`,
  `WRFB`, `WPTA`, `WARC`, `SB`, `WRSA`) is **not a new concern** — phase5-07 disassembly proved
  `OnlineGameMode.EstablishWorlds` iterates the mod-merged `World/regions.txt` unconditionally (no
  story/optional filter), so every region already in that file gets a `WorldSession` regardless of
  curation. `SB` (Subterranean) is an ordinary always-loaded vanilla region, not a Watcher optional
  one, so it needs no `EstablishWorldsHooks` entry either.

## Flagged for follow-up verification, not blocking implementation

- `waua_bath` → `sb_d07`: [[phase5-05-optional-region-warp-points]] flagged Ancient Urban (`WAUA`)
  as a scripted dream-sequence area whose specific rooms might assume mid-sequence state a cold
  sandbox arrival would break. Converting the Echo there is in scope per the user's blanket
  instruction, but this is the one entry in the new table that most needs an in-game check before
  calling Phase 7 done.

## Subtask sequence

1. [[phase7-01-echo-warp-links]] — curate the 15-row link table (13 authored destinations + 2
   Daemon redirects) as `src/EchoWarpLinks.cs`, mirroring `BadWarpLinks.Link`'s shape.
2. Wire a one-way-only injection path into
   [src/CorruptedWarpInjectionHooks.cs](../src/CorruptedWarpInjectionHooks.cs) — the existing
   `InjectLinks` always does forward + return legs, which is wrong for this table; needs a
   forward-only sibling method.
3. In-game verification (once buildable): confirm `WARE_I14` now spawns a real WarpPoint to
   `WSKC`/`wskc_a03`, and flag `waua_bath` specifically for the mid-sequence-state check above.

## Note for next session

Created 2026-08-28, same session that answered the user's original "no portal in WARE_I14"
question by finding it's vanilla Echo content, not a missing WarpPoint. User then stated the
standing policy this phase implements. Both scope questions (directionality, dynamic-Echo handling)
were asked and answered before any code was written — see this file's "Scope decisions" section.

Updated 2026-08-28 (later): [[phase7-01-echo-warp-links]] implemented and builds clean. Only
remaining item is subtask 3 — in-game verification that `WARE_I14` now warps to `WSKC`/`wskc_a03`,
and the specific `waua_bath` mid-sequence-state check flagged above. Phase 7 is code-complete but
not yet playtested.
