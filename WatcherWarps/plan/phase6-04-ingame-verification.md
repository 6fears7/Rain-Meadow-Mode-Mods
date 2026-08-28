# Phase 6.4 — In-game verification

Parent: [[phase6-00-timeline-region-mismatch-spawn-guard]]
Depends on: [[phase6-03-spawn-guard-hook]]

## Steps

1. Join/create a Meadow lobby on the **Watcher** timeline. Warp or travel into a Watcher region
   (e.g. `WARA`). Quit to menu (writes `saveLocation` into `meadow.json`).
2. Create/join a Meadow lobby on the **White** timeline. Press start.
3. Expect: no crash; you wake in Outskirts (`SU_C04`); BepInEx log shows the
   "redirecting to Outskirts" warning.
4. Inspect `meadow.json` (`Kittehface UserData persistentDataPath/meadow.json`) — the character's
   `saveLocation` should now be `SU_C04`, so a second White-timeline launch produces no warning.
5. Regression: launch a Watcher-timeline lobby with a saved Watcher spot — must still load into
   that Watcher region (helper returns true, no redirect).
6. Regression: normal White-timeline save + White-timeline launch — no redirect, no warning.

## If the FastTravel path was also patched (per 6.1)

Repeat 1-3 using pause-menu → Passage → pick a region, confirming the fast-travel screen can't
strand you in a cross-timeline region either.
