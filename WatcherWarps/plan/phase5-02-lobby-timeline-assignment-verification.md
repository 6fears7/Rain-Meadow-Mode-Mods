# Phase 5.2 — Lobby timeline assignment verification

Parent: [phase5-00-overview.md](phase5-00-overview.md).

## Question to answer

Every warp hook in this mod gates on `OnlineManager.lobby.meadowTimeline ==
WatcherEnums.SlugcatStatsName.Watcher.value`. This value is not set by `WatcherWarps` — it's
assigned by Rain Meadow's own lobby-creation flow, in the sibling repo
`/home/preston/repos/Rain-Meadow` (read-only reference for this investigation; nothing in that
repo gets edited). The open question: is `OnlineManager.lobby.meadowTimeline` guaranteed to be
populated (synced to every client, not just the host) before any `Room.Loaded` /
`RoomSettings` construction can happen for that lobby — i.e. before this mod's hooks can possibly
fire? If there's a window where a room loads before the timeline value has arrived on a given
client, that client's guard silently evaluates false and the warp feature is inert for them,
which would look like an intermittent bug with no exception or log trace.

## Where to look (per the parent plan's Critical Files list)

- `Menu/LobbyCreateMenu.cs:106-137,200,230-233` — where `meadowTimeline` is chosen from the
  timeline dropdown and set at lobby creation.
- `Online/Resource/Lobby.cs:35,50-61,278,314,323` — `meadowTimeline`/`ActiveTimeline` fields and
  `Lobby.LobbyState.timeline`, the sync mechanism across clients.
- `GameModes/OnlineGameMode.cs:162-176,213-229,291-297` — `LoadWorldAs`/`LoadWorldIn` (which
  consumes `meadowTimeline`) and `EstablishWorlds`, to see the actual sequencing: does
  `meadowTimeline` get read/consumed by Rain Meadow's own world-loading code before any room can
  load, which would mean it's already a hard precondition Rain Meadow itself relies on (and this
  mod's guard is safe by construction), or is there a gap this mod's guard could hit that Rain
  Meadow's own code doesn't?

## What "done" looks like

A short written answer (append to this file's Note section, or fold back into
[phase5-00-overview.md](phase5-00-overview.md)'s note) stating either:

- "Confirmed safe: `meadowTimeline` is consumed by Rain Meadow's own world-loading path
  (cite the specific method/line) before any room can load for any client, so this mod's guard
  can never read a not-yet-assigned value." No code change needed.
- "Found a gap: [describe the window]." In that case, this stops being a verification task and
  becomes a small follow-up subtask (e.g. a defensive null/empty-string check already implicit in
  `meadowTimeline == Watcher.value` failing safe, or a startup-ordering fix) — write that up as
  phase5-04 if it's needed, rather than bolting it onto this file.

## Note for next session

Resolved 2026-08-26 by user confirmation (not independently re-derived from the sibling repo in
this session): `OnlineManager.lobby.meadowTimeline` is guaranteed set before any room can load, so
the existing (and now consolidated, see [[phase5-01-guard-consolidation-decision]]) guard never
reads a not-yet-assigned value. No code change needed. If this ever needs re-verifying from first
principles, see the "Where to look" section above for the sibling-repo files to check.
