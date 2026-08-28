# Phase 3.3 — `WarpFilter` scope decision

Parent: [phase3-00-overview.md](phase3-00-overview.md). Independent of
[[phase3-02-progression-filter-guard]]'s code; depends on
[[phase3-01-hook-target-investigation]] only if the decision below comes out "yes, suppress it."
This subtask may conclude with no code at all — that's a valid outcome, not an unfinished state.

## What `PlacedObject.Type.WarpFilter` actually is

Confirmed by decompiling `RoomSettings.LoadPlacedObjects` (`RoomSettings.cs:1533-1584`, see
[[phase3-00-overview]]'s point 2): a `WarpFilter` placed object is **not** a
`GenericFilterData`-based progression gate. It's collected into a separate list purely by
`PlacedObject.Type.WarpFilter` (line 1553-1556), with **no call to `.Active()` at all** — every
other deactivatable placed object within its radius unconditionally gets
`pObj.deactivatedByWarpFilter = true` (line 1580), regardless of session type, save state, or
timeline. It exists in 18 rooms in the current Watcher room data (vs. 121 rooms using one of the
three real progression filters checked in [[phase3-02-progression-filter-guard]]).

Because it has no progression logic to read, there is nothing to "always report the requirement
met" for — the only lever available is whether to suppress its deactivation effect entirely
under the Meadow + Watcher guard, i.e. treat `WarpFilter`-adjacent objects as if the filter
weren't there.

## Why this needs a decision, not just a fix

The parent design doc's Phase 3 bullet lumped `WarpFilter` in with the three progression filters
under "disable progression gating," but it isn't progression gating — it's an unconditional
level-design tool, presumably placed deliberately by Watcher's room authors to deactivate a
specific `WarpPoint` (or other object) in specific rooms for reasons unrelated to save state
(e.g. narrative sequencing, avoiding a warp point being reachable before its intended point in
the campaign, or preventing a decorative-only object from being usable). Suppressing it
unconditionally in Meadow could re-activate objects the room design intentionally keeps off in
ways the three progression filters don't cover — this needs eyes on what's actually in some of
those 18 rooms before deciding, not an assumption that "more warp points active" is uniformly
correct.

## What to check before deciding

1. Pull the list of the 18 rooms (`grep -l WarpFilter mods/watcher/world/*-rooms/*_settings.txt`)
   and cross-reference against [plan/watcher-warp-links.tsv](watcher-warp-links.tsv) — how many of
   those rooms actually contain a `WarpPoint` (not some other deactivatable object type) within a
   `WarpFilter`'s radius? If few/none do, this subtask may be near-moot for the warp feature
   specifically (the `WarpFilter` might mostly be deactivating unrelated objects like `RippleSpawnEgg`
   markers), which would argue for leaving it alone entirely.
2. For rooms where a `WarpFilter` *does* sit near a `WarpPoint`, sample a couple by eye (via the
   dev tools / room file) to see whether the deactivated `WarpPoint` looks like a "not yet
   reachable in campaign order" case (argues for suppressing in Meadow's sandbox, where campaign
   order doesn't apply) or a "genuinely broken/unfinished/hazardous without other context" case
   (argues for leaving deactivated).

## Decision to make with the user

Once the above is checked, ask: should `PlacedObject.Type.WarpFilter` be suppressed under the
same Meadow + Watcher guard as the three progression filters (full symmetry with
[[phase3-02-progression-filter-guard]]), left untouched (accept that a small number of `WarpPoint`s
stay unreachable in Meadow, matching vanilla level-design intent), or handled per-room via a curated
allowlist (more precise, more manual work, ties into [[phase6]]-style destination curation from
the parent plan)? This is the same kind of content decision the parent plan already deferred for
Phase 6's curated destinations and Phase 4's corrupted-warp rooms — don't infer an answer from
code alone.

## Investigation results (2026-08-26)

Ran the check against the actual Watcher install
(`~/.steam/.../Rain World/RainWorld_Data/StreamingAssets/mods/watcher`, not present anywhere in
this repo/machine's usual paths — needed to be located under the Steam library first).

1. `grep -l WarpFilter world/*-rooms/*_settings.txt` confirms exactly 18 rooms: `warb_f01`,
   `warb_h13`, `warb_j07`, `warc_c12`, `ward_r16`, `warg_g05`, `warg_g21`, `warg_g30`, `wbla_c01`,
   `wpta_b10`, `wrfa_a05`, `wrfa_a07`, `wrfa_sk04`, `wrfb_b11`, `wska_d10`, `wska_d18`,
   `wskb_c15`, `wvwb_b04`.
2. Cross-referencing against [watcher-warp-links.tsv](watcher-warp-links.tsv)'s `sourceRoom`
   column: only **one** of the 18 (`warb_f01`) even contains a `WarpPoint` object at all. The
   other 17 have zero `WarpPoint><` occurrences in their settings file — the `WarpFilter` there is
   deactivating unrelated placed objects (decals, `KarmaFlower`, etc.), never a `WarpPoint`.
3. For `warb_f01`, checked actual coordinates: `WarpFilter><342.1892><419.6759><-1~53` (radius 53)
   vs. `WarpPoint><491.742><244.9561>`. Distance ≈ 230 units — **far outside the filter's radius**.
   The filter in this room is instead adjacent to a `KarmaFlower` at `(328.52, 409.86)`, ≈17 units
   away, well inside radius. So even in the one room where both object types coexist, the
   `WarpFilter` does not touch the `WarpPoint`.

**Conclusion: `PlacedObject.Type.WarpFilter` never deactivates a `WarpPoint` anywhere in the
current Watcher room data.** It exists purely to gate unrelated decorative/pickup objects. This
makes the subtask moot for the warp feature specifically — there is nothing for a Meadow guard to
suppress, because no `WarpPoint` is ever affected in the first place.

## Decision

**Leave `WarpFilter` untouched.** No code needed for this subtask — confirmed no-op outcome per
the investigation above, not a placeholder. If future Watcher room data changes ever place a
`WarpFilter` within radius of a `WarpPoint`, this conclusion would need re-checking, but as of the
current room data (checked 2026-08-26) there's nothing to suppress.

## Note for next session

Phase 3.3 is **complete** — investigated and decided, no code required. `phase3-02` (progression
filter guard) remains the only Phase 3 subtask with actual code to write, gated on `phase3-01`'s
hook-target investigation.
