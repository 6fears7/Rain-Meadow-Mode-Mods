# Phase 5.6 — Route every dynamic (`NULL`-dest) warp point to Daemon (`WRSA`)

Parent: [phase5-00-overview.md](phase5-00-overview.md). Spun off 2026-08-27 from the
two-way-coverage audit (see note at bottom).

## Decision (user, 2026-08-27)

The `NULL`/`NULL` rows in [watcher-warp-links.tsv](watcher-warp-links.tsv) are vanilla's
**dynamic / "unstable" warp points** — at trigger time vanilla calls
`Watcher.WarpPoint.ChooseDynamicWarpTarget(...)`, which reads
`world.game.GetStorySession.saveState.miscWorldSaveData.*` counters and rolls
`Random.InitState(watcherCampaignSeed + n)` (decompiled `Watcher.WarpPoint.decompiled.cs:1133-1223`).
A Meadow sandbox has no campaign seed / per-save counter sync, so these points can never
resolve a destination — exactly why Phase 4 hand-authored `BadWarpLinks` instead of calling
`ChooseDynamicWarpTarget`.

`NULL` is **not** a hidden pointer to Daemon. Daemon is region acronym `WRSA`
("`wrsa` Daemon", `watcher-warping-support.md:52`) and already appears non-`NULL` in the tsv
(`wora_starcatcher03 ↔ wrsa_c01`, `wrsa_d01 → WARA_P17`).

**User's call:** give every one of these dynamic points a fixed destination of `WRSA` /
`wrsa_c01` (the authored arrival room for the vanilla `wora_starcatcher03 → WRSA` crossing).

- **Direction:** one-way *into* Daemon. Daemon keeps its authored exits
  (`wrsa_c01 → wora_starcatcher03`, `wrsa_d01 → WARA_P17`), so it stays escapable; no pile-up
  of return triggers in one room.
- **Scope:** the 29 `kind=WarpPoint` `NULL/NULL` rows **plus** the 2 `kind=Echo` `NULL/NULL`
  rows (`wara_p09`, `waua_toys`) — user asked to include the Echoes so `WARA` / `WAUA` finally
  get an outbound link. Caveat: Echo triggers need a spinning-top interaction a Meadow sandbox
  lacks; redirecting their `WarpPointData` is necessary but may not be sufficient — flagged as
  follow-up.

## Source rooms (from `watcher-warp-links.tsv`, `NULL/NULL`)

WarpPoint: warb_j08, warc_b07, ward_e09, ware_g15, warf_d15, warg_h20, wbla_j01, wdsr_a25,
wgwr_c09, wgwr_disposal, whir_a06, whir_a22, whir_b13, wmpa_c06, wpga_a02, wpta_c05, wrfa_b09,
wrfb_c07, wrra_l01, wska_d07, wskb_n01, wskc_a25, wskd_b34, wsur_b09, wtda_z07, wtdb_a03,
wvwa_a09, wvwb_b02, and wssr_lab6 (row 73: `WORA`/`NULL` — dest region set, dest room dynamic).

Echo: wara_p09, waua_toys.

Note several of these rooms are *also* `BadWarpLinks` / `OverlayRegionWarpLinks` sources
(`warc_b07`, `wgwr_c09`, `whir_a06`, `whir_a22`, `whir_b13`, `wmpa_c06`, `wpta_c05`,
`wska_d07`? no — `wska_n04`). Those rooms will then host *both* an injected bad-warp point and
a redirected-to-Daemon point. Acceptable (distinct `destRoom`s → distinct `WarpPoint`s), but
worth an eyeball in-game for trigger overlap.

## Mechanism — redirect, don't inject

Unlike `BadWarpLinks` (which injects a brand-new `PlacedObject` and has the latent
`po.pos == Vector2.zero` placement bug `TestWarpHook` calls out), the dynamic points **already
exist** as real placed objects at hand-authored positions in their rooms. `Room.AddObject`
puts every `WarpPoint` into `room.warpPoints` (`phase4-01` notes 3). So:

`src/DaemonWarpRedirectHooks.cs` — `On.Room.Loaded` postfix, gated on `Warps.IsMeadowWatcher()`:
iterate `self.warpPoints`; for any whose `Data.destRegion == null && Data.destRoom == null`,
rewrite the `WarpPointData` in place to mirror `Warps.BuildCorruptedWarpPlacedObject`:

```
Data.destRegion   = "WRSA";
Data.destRoom     = "wrsa_c01";
Data.destPos      = null;
Data.cycleExpiry  = 0;      // nonDynamicWarpPoint => true; never enters ChooseDynamicWarpTarget
Data.oneWay          = true;
Data.oneWayEntrance  = false;   // => Data.oneWayExit == true (one-way out, matches bad-warp theming)
Data.effectSettings  = WarpPoint.WarpPointData.EffectSettings.BadWarpCosmetics();
Data.destCam      = WarpPoint.GetDestCam(Data);   // recompute; ctor computed it against a null dest
```

Keeps the original trigger position. Idempotent (after the first pass `destRegion != null`, so
re-entry / re-load is a no-op — and `room.warpPoints` is rebuilt fresh per load anyway).

## Blocking verification — is `WRSA` established?

`WRSA` is **absent from both** lists `phase5-03` pulled with `ikdasm`
(`SlugcatStats.SlugcatStoryRegions(Watcher)` = 24 regions, `SlugcatOptionalRegions(Watcher)` =
9). If `Region.WarpRegions` has no `WRSA` entry in a Watcher-timeline Meadow lobby,
`EstablishWorldsHooks` logs a warning and skips it, and every redirected warp dead-ends.

- [x] **Subtask 5.6.1** — re-run the `phase5-03` check for `WRSA` specifically: does it appear
      in `SlugcatStats.SlugcatStoryRegions` / `SlugcatOptionalRegions` under any branch, or in
      `World/regions.txt` / `Region.WarpRegions`? Daemon may be a special-case region only
      loaded by the Void Weaver ending (`WRSA_WEAVER`, decompiled `:1999`).
      **Done 2026-08-27 — see "Subtask 5.6.1 result" below.**
- [x] Interim mitigation applied: `"WRSA"` added to `EstablishWorldsHooks.ForceEstablishRegions`.
      Harmless if unresolved (logs warning, skips) — same contract as the `LF` entry.
      **5.6.1 update:** it is currently *always* unresolved (`Region.WarpRegions` is `null` at
      `EstablishWorlds` time) — the mitigation does nothing until [[phase5-07-establishworlds-warpregions-null-fix]] lands.

## Subtask 5.6.1 result (2026-08-27)

Checked directly against the installed assembly (`ikdasm` on
`RainWorld_Data/Managed/Assembly-CSharp.dll`, scratch dump `/tmp/acsharp.il`) and the installed
Watcher world data, not guessed.

### Where `WRSA` is / isn't

| Source | `WRSA` present? | Detail |
|---|---|---|
| `SlugcatStats.SlugcatStoryRegions` — **every** branch (White/Yellow/Red/Gourmand/Artificer/Saint/Spear/**Watcher**/default) | **NO** | `Watcher` branch returns 24 regions: `WSKA WSKB WRFA WRRA WPGA WARF WSKD WMPA WTDA WTDB WARG WARD WBLA WARE WRFB WSKC WVWA WVWB WARB WARC WPTA WARA WAUA WORA`. |
| `SlugcatStats.SlugcatOptionalRegions` — every branch | **NO** | `Watcher` branch: `HI SU CC SH WHIR WSUR WDSR WGWR WSSR`. MSC branches: `OE/MS` or `MS`. |
| `World/regions.txt` (Watcher timeline / Watcher mod active) | **YES** | `consolefiles/watcher/world/regions.txt:30`; `mods/watcher/modify/world/regions.txt:18` = `[ADD]WRSA`; `mergedmods/world/regions.txt:40`. The region line is **unconditional** — only rooms `WRSA_WEAVER02/03` are `{!WeaverEnding}` HIDEROOM-gated; `WRSA_C01` (our redirect target) is a plain always-present room. The `overrideData.destRoom = "WRSA_WEAVER"` path (decompiled `:1999`) is Void-Weaver-ending-specific and irrelevant to establishing the region. |
| `Region.WarpRegions` (static) | **conditionally** — see below | Built from the same `regions.txt`, so it *would* contain `WRSA` **once populated**. |

**So `WRSA` is in the same bucket as the four gap regions w.r.t. the `SlugcatStats` lists —
actually one notch worse: the gap regions are at least in `SlugcatOptionalRegions(Watcher)`,
`WRSA` is in neither list.** It is in `regions.txt` like (almost) every region. Under
`phase5-03`'s model (`EstablishWorlds` only establishes what the timeline grants) `WRSA` gets no
`WorldSession` without the `ForceEstablishRegions` override. Daemon is **not** Void-Weaver-only
as a *region* — its default rooms (`WRSA_L01/C01/D01/J01`) load unconditionally.

### New blocker found — the override reads a static that is `null` at `EstablishWorlds` time

`EstablishWorldsHooks.EstablishWorldsPostfix` resolves each acronym via
`Region.WarpRegions?.FirstOrDefault(...)`. Tracing the disassembly:

- `Region.WarpRegions` starts `null`; `Region.RegionReadyToWarp` starts `false` (static `.cctor`).
- The **only** things that populate `Region.WarpRegions` are `Region.LoadAllRegionsCoroutine`
  (`d__36`) and it is kicked **only** from `OverWorld.InitiateSpecialWarp_WarpPoint` (i.e.
  *during an in-progress warp*) and the vanilla story warp-preload flow. `OverWorld.Update` does
  `regions = Region.WarpRegions` but only when `warpingPreload && Region.RegionReadyToWarp`.
- `OverWorld..ctor` builds `OverWorld.regions` from a **separate** non-coroutine
  `Region.LoadAllRegions(timeline, game)` call that does **not** touch the static
  `Region.WarpRegions`.
- Rain-Meadow never references `WarpRegions` / `LoadAllRegions` / `RegionReadyToWarp` (grep: 0 hits).
- `OnlineGameMode.EstablishWorlds` runs once, from `OverworldSession.ActivateImpl` — at world
  activation, **before any warp**.

⇒ In a fresh Meadow lobby, `Region.WarpRegions == null` when `EstablishWorldsPostfix` runs, so
the postfix logs `region WRSA not found in Region.WarpRegions` (**and the same for all 11
entries — `WHIR/WSUR/WDSR/WGWR/WSSR/HI/SU/CC/SH/LF/WRSA`**) and establishes nothing. This makes
not just 5.6 but the whole of `phase5-04` a **no-op as currently wired**. This directly
contradicts `phase5-04`'s claim that "`Region.WarpRegions` is fully populated at game-load time,
long before an OverworldSession activates" — no game-load populator was found in this pass.

**Recommended fix (write up, don't guess-implement here per plan norms):** resolve
`ForceEstablishRegions` against `overworldSession.overWorld.regions` — the exact array the base
`EstablishWorlds` loop iterates and which *is* populated by `OverWorld..ctor` at that point —
instead of `Region.WarpRegions`. Open question that fix must settle: `OverWorld..ctor`'s
`Region.LoadAllRegions` builds one `Region` per line of the active `regions.txt` with **no
visible timeline filter**, so `overWorld.regions` may *already* contain `WRSA` + the gap regions
(in which case `phase5-03`/`phase5-04`'s "not in `overWorld.regions`" premise needs re-checking
at runtime, and the base loop may already establish them). Spun off as
[[phase5-07-establishworlds-warpregions-null-fix]].

## Status (2026-08-27)

- Code written: `src/DaemonWarpRedirectHooks.cs`, registered in `Warps.Apply()`.
- `"WRSA"` added to `ForceEstablishRegions`.
- Builds clean: yes (2026-08-27, `dotnet build` — 0 errors, 0 new warnings).

## Note for next session

1. ~~Do **Subtask 5.6.1**~~ — done 2026-08-27. It *did* find the no-op: `WRSA` is in neither
   `SlugcatStats` list (worse than the gap regions), and separately the `EstablishWorldsHooks`
   override reads `Region.WarpRegions` which is `null` when `EstablishWorlds` runs, so nothing
   gets established. Fix tracked in [[phase5-07-establishworlds-warpregions-null-fix]] — do that
   next; until it lands, 5.6 (and all of phase5-04) is inert.
2. In-game: load a Watcher Meadow lobby, walk into e.g. `wska_d07`'s warp point, confirm arrival
   in `wrsa_c01` with no `WorldSession` error and no `ChooseDynamicWarpTarget` NRE in the log.
3. Confirm the 2 Echo-backed points (`wara_p09`, `waua_toys`) are actually in `room.warpPoints`
   and actually triggerable without a spinning top — if not, they need their own approach
   (inject a plain `WarpPoint` at the Echo's `panelPos`, or hook the Echo activation).
4. `TestWarpHook` is still in `Warps.Apply()` — unrelated, but remove it before release.
