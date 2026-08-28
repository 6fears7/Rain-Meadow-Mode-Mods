# Phase 5.3 — Region access verification against curated destinations

Parent: [phase5-00-overview.md](phase5-00-overview.md).

**Blocked**: do not start until Phase 6 / [[phase4-03-destination-curation-followup]]'s curated
destination list (both the four-gap-region hand-authored links and the reused
`watcher-warp-links.tsv` pairs) is finalized. Verifying against a placeholder or partial list
just means redoing this later.

## Question to answer

The parent plan's Phase 5 section argues that gating on the Watcher timeline (rather than force-
opening every region via `EstablishWorlds`) should be sufficient, "since Watcher's own campaign
already has broad region access (including rot/corrupted-adjacent areas relevant to failed
warps)" — but flags this as an assumption to verify per curated room, not a guarantee. This
subtask is that verification, once there's an actual list to check.

For every region that ends up as a `destRegion` in the final curated set (Phase 4's bad-warp
links in `src/BadWarpLinks.cs`, plus whatever Phase 6 lands on for the four gap regions `wdsr`/
`wgwr`/`whir`/`wsur` and the `waua`/`wara` special cases), confirm the region is actually reachable
under Watcher's `SlugcatStats` region-access rules — i.e. that it shows up in
`overworldSession.overWorld.regions` for a lobby created with the Watcher timeline. The regions in
`watcher-warp-links.tsv` are themselves already Watcher-native content, so the highest-risk rows
are the base-game overlay regions and the two special-case Echo rooms (`lf_b01w` in `lf`,
`sb_d07`'s region for the `waua_bath` link), since those aren't purely Watcher-owned regions.

## How to check

Look at how `OnlineGameMode.EstablishWorlds` ([GameModes/OnlineGameMode.cs:291-297] in the sibling
Rain-Meadow repo) derives `overworldSession.overWorld.regions` from the selected timeline, and
either:
- Read `SlugcatStats`'s region-access table for the Watcher timeline directly (likely in the game
  install's `StreamingAssets` data or a corresponding decompiled table), or
- The more direct/empirical check: actually load a Meadow lobby with Watcher selected as the
  timeline and confirm each curated destination region loads without error when warped to
  (overlaps with this plan's own top-level Verification step 7 in
  [watcher-warping-support.md](watcher-warping-support.md)).

## What "done" looks like

A per-region pass/fail list. Any region that fails needs `EstablishWorlds` revisited for that
specific region (per the parent plan's explicit instruction not to force-open every region
preemptively) — write that up as a new subtask if it happens, rather than guessing a fix here.

## Verification result (2026-08-26)

Unblocked: `phase4-03`'s curated list is final (`src/BadWarpLinks.cs`, `src/CorruptedWarpDestinations.cs`).
Checked directly against the decompiled `SlugcatStats.SlugcatStoryRegions`/`SlugcatOptionalRegions`
methods in the actual game install (`ikdasm` against
`RainWorld_Data/Managed/Assembly-CSharp.dll`, not guessed), specifically the `ModManager.Watcher`
branch of each:

- `SlugcatStoryRegions(Watcher)` returns 24 regions: `WSKA WSKB WRFA WRRA WPGA WARF WSKD WMPA WTDA
  WTDB WARG WARD WBLA WARE WRFB WSKC WVWA WVWB WARB WARC WPTA WARA WAUA WORA`.
- `SlugcatOptionalRegions(Watcher)` returns 9 regions **not** in the story list: `HI SU CC SH WHIR
  WSUR WDSR WGWR WSSR`. These are the rot-mirror/overlay regions, populated only when something
  (normally the rot-progression state machine) explicitly loads them — not part of the base region
  set `OverWorld` starts with, which is what `OnlineGameMode.EstablishWorlds`
  ([GameModes/OnlineGameMode.cs:291-297](OnlineGameMode.cs#L291-L297)) iterates
  (`overworldSession.overWorld.regions`).

Cross-referencing every region actually used in `BadWarpLinks.All` (source and dest, since every
link is two-way) against the story list:

| Region | In `SlugcatStoryRegions(Watcher)`? | Verdict |
|---|---|---|
| WSKA, WTDA, WMPA, WTDB, WARD, WRFA, WARC, WPGA, WPTA, WSKD, WARG | yes | **PASS** |
| WHIR, WDSR, WGWR, WSUR | no — optional-only | **FAIL** |

**Result: the four gap regions (`WDSR`/`WGWR`/`WHIR`/`WSUR`) — which are exactly
`CorruptedWarpDestinations`' region set and therefore every bad-warp link's destination side — are
not in Watcher's base story-region list.** A Meadow sandbox lobby has no rot-progression state
machine to ever add them to `overworldSession.overWorld.regions`, so `EstablishWorlds` as written
today will never establish a `WorldSession` for any of them, regardless of the Watcher-timeline
gate. This is not a hypothetical: it's the specific failure the parent plan flagged as the thing to
check before assuming the timeline gate alone was sufficient.

`WARA`/`WAUA` (the two special-case Echo regions) both *are* in the story list, so they'd pass —
but moot for now since no code currently injects an entry warp into either (Phase 6's "entry into
the Watcher cluster" piece was never implemented; only `BadWarpLinks`/`CorruptedWarpDestinations`
exist in `src/`).

Per this subtask's "what done looks like" — write the fix as a new subtask rather than guessing it
here: see [[phase5-04-establishworlds-gap-region-override]].

## Note for next session

Done as of 2026-08-26. Verified empirically against the decompiled assembly, not assumed. Follow-up
fix (`EstablishWorlds` override for `WDSR`/`WGWR`/`WHIR`/`WSUR` specifically) is split into
[[phase5-04-establishworlds-gap-region-override]] since implementing it wasn't this subtask's job.
