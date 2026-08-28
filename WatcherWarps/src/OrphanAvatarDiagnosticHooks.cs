using System.Collections.Generic;
using RainMeadow;

namespace WatcherWarps
{
    // TEMP diagnostics for phase 7-05-00. Delete after the orphan-teardown gate
    // (7-05-03) passes. Goal: prove whether the remote avatar this mod strips from
    // the source world in WorldLoadedHooks.RemoveRemoteAvatarsFromSourceWorld leaves
    // a still-realized / still-abstract-updating AbstractCreature behind, and in
    // which world that orphan ends up.
    //
    // Two probes:
    //   1. On.AbstractCreature.Update - for any AbstractCreature whose ID.RandomSeed
    //      matches a known online avatar seed but which is NOT in
    //      OnlinePhysicalObject.map, log once per id where it is abstract-updating.
    //   2. A public snapshot helper used by WorldLoadedHooks right after
    //      RemoveEntityFromGame(false) to dump the removed avatar's post-strip state.
    public static class OrphanAvatarDiagnosticHooks
    {
        private static readonly HashSet<string> loggedOrphans = new();
        private static readonly HashSet<string> loggedUnrealized = new();

        public static void Apply()
        {
            On.AbstractCreature.Update += AbstractCreature_Update;
        }

        // Reset per warp so an orphan that reappears in a new region logs again.
        public static void ResetOrphanLog()
        {
            loggedOrphans.Clear();
            loggedUnrealized.Clear();
        }

        // Seeds of every avatar currently registered in the lobby. Recomputed each
        // call - cheap enough for a temporary diagnostic and always current.
        private static HashSet<int> KnownAvatarSeeds()
        {
            var seeds = new HashSet<int>();
            var lobby = OnlineManager.lobby;
            if (lobby == null) return seeds;
            foreach (var kv in lobby.playerAvatars)
            {
                if (kv.Value.FindEntity(true) is OnlinePhysicalObject opo && opo.apo != null)
                {
                    seeds.Add(opo.apo.ID.RandomSeed);
                }
            }
            return seeds;
        }

        private static void AbstractCreature_Update(On.AbstractCreature.orig_Update orig, AbstractCreature self, int time)
        {
            orig(self, time);

            if (self?.ID == null) return;

            // Probe 3 (7-05-00 (c)): a registered avatar AbstractCreature that has an
            // active realized room but no realized creature - the "received but not
            // realized until a room change" symptom of Face 2. Also dump whether a
            // colliding-id AbstractCreature shares its abstract room.
            if (OnlinePhysicalObject.map.TryGetValue(self, out var selfOe)
                && selfOe is OnlineCreature { isAvatar: true, isMine: false }
                && self.realizedCreature == null
                && self.Room?.realizedRoom is { shortCutsReady: true } rr)
            {
                string uid = self.ID.ToString();
                if (loggedUnrealized.Add(uid))
                {
                    int collisions = 0;
                    if (self.Room.creatures != null)
                    {
                        foreach (var ac in self.Room.creatures)
                        {
                            if (ac != self && ac.ID.RandomSeed == self.ID.RandomSeed) collisions++;
                        }
                    }
                    Warps.Log?.LogWarning(
                        $"Watcher Warps: [7-05-00c] remote avatar {uid} joined room {rr.abstractRoom.name} " +
                        $"but realizedCreature==null; colliding-seed AbstractCreatures in room = {collisions}");
                }
            }

            if (OnlinePhysicalObject.map.TryGetValue(self, out _)) return;

            string idStr = self.ID.ToString();
            if (loggedOrphans.Contains(idStr)) return;
            if (!KnownAvatarSeeds().Contains(self.ID.RandomSeed)) return;

            loggedOrphans.Add(idStr);
            Warps.Log?.LogWarning(
                $"Watcher Warps: orphan avatar {idStr} abstract-updating in world " +
                $"{self.world?.name ?? "?"} room {self.pos.room} ({self.Room?.name ?? "?"}); " +
                $"realizedCreature={(self.realizedCreature != null)} " +
                $"realizedRoom={self.realizedCreature?.room?.abstractRoom?.name ?? "-"} " +
                $"slatedForDeletion={self.slatedForDeletion} alive={self.state?.alive}");
        }

        // Called from RemoveRemoteAvatarsFromSourceWorld right after
        // RemoveEntityFromGame(false). Answers 7-05-00 (a) and (b) directly.
        public static void LogRemovedAvatarState(AbstractCreature remoteAvatar, string sourceWorldName)
        {
            Warps.Log?.LogInfo(
                $"Watcher Warps: [7-05-00] removed remote avatar {remoteAvatar.ID} from {sourceWorldName}: " +
                $"world={remoteAvatar.world?.name ?? "?"} pos.room={remoteAvatar.pos.room} " +
                $"room={remoteAvatar.Room?.name ?? "-"} tile={remoteAvatar.pos.Tile} " +
                $"realizedCreature={(remoteAvatar.realizedCreature != null)} " +
                $"realizedRoom={remoteAvatar.realizedCreature?.room?.abstractRoom?.name ?? "-"} " +
                $"realizedSlated={remoteAvatar.realizedCreature?.slatedForDeletetion} " +
                $"inMap={OnlinePhysicalObject.map.TryGetValue(remoteAvatar, out _)} " +
                $"slatedForDeletion={remoteAvatar.slatedForDeletion} alive={remoteAvatar.state?.alive}");
        }
    }
}
