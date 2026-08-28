using RainMeadow;
using Watcher;

namespace WatcherWarps
{
    // Phase 7-01 fix. After a non-host client warps through an injected warp point,
    // the host never learns the client's non-Player avatar entity exists in the
    // destination region: PerformWarpStripHooks.ExitResource detaches the avatar APO
    // from the source Room/World sessions (and Deactivate deregisters it from
    // OnlinePhysicalObject.map entirely), and vanilla's Meadow warp branch in
    // RainMeadow.EntityHooks.OverWorld_WorldLoaded only re-enters Player avatars +
    // grasp/stomach APOs, never a non-Player avatar itself. Story papers over this by
    // having the host re-drive every client via StoryRPCs.NormalExecuteWatcherRiftWarp
    // from WarpPoint_NewWorldLoaded_Room; Meadow's else branch just calls orig.
    //
    // Result: the client renders its own always-realized local avatar (so it sees
    // itself), but the host is never sent the avatar entity, so remote players are
    // invisible until a full re-lease (returning to the Meadow lobby).
    //
    // STATUS 2026-08-28: this approach does NOT fix the bug. Two-client playtest
    // showed the warped avatar still "doesn't exist in online space" afterward,
    // players still invisible to each other, plus a ghost NPC copy of the other
    // player left in the destination. Root cause of the no-op: for a Creature APO
    // that fell out of OnlinePhysicalObject.map, MeadowGameMode.ShouldRegisterAPO
    // returns false unless RainMeadow.sSpawningAvatar is set, so ApoEnteringWorld/
    // ApoEnteringRoom silently do nothing; and setting sSpawningAvatar to force it
    // hits MeadowGameMode.NewEntity's `throw new InvalidProgrammerException("entity
    // re-register")` for an already-realized avatar. Rain Meadow deliberately does
    // not support re-registering a live avatar entity. The real fix has to keep the
    // avatar's OnlineCreature ALIVE across the region transition (the vanilla
    // group-warp `beingMoved` path never deregisters avatars) rather than let
    // PerformWarpStripHooks + region Deactivate tear it down and then try to re-add
    // it. See plan/phase7-01-postwarp-invisible-players.md "REDESIGN".
    //
    // Kept for now only as an honest diagnostic (logs whether the avatar APO is in
    // online space after the attempt) and as the scaffold for the redesign's
    // deferred "enter the new sessions once they're active" step, which is still
    // needed once the avatar survives with its identity intact.
    public static class AvatarWarpReentryHooks
    {
        // ~10s at 40 ticks/s. The destination RoomSession is normally active within a
        // few hundred ms; if it never comes up something else is broken and we bail
        // rather than poll forever.
        private const int TimeoutTicks = 400;

        private static WorldSession? pendingWorldSession;
        private static string? pendingDestRoom;
        private static AbstractCreature? pendingAvatar;
        private static int ticksRemaining;

        public static void Apply()
        {
            On.OverWorld.Update += OverWorld_Update;
        }

        // Called from WorldLoadedHooks post-orig on whichever client just warped.
        public static void RequestReentry(WorldSession destWorldSession, string? destRoom, AbstractCreature avatar)
        {
            if (destWorldSession == null || destRoom == null || avatar == null) return;

            pendingWorldSession = destWorldSession;
            pendingDestRoom = destRoom;
            pendingAvatar = avatar;
            ticksRemaining = TimeoutTicks;

            // The sessions are occasionally already up by now (dest room realized fast);
            // try immediately so we don't wait a tick for the common case.
            TryReentry();
        }

        private static void OverWorld_Update(On.OverWorld.orig_Update orig, OverWorld self)
        {
            orig(self);

            if (pendingAvatar == null) return;

            if (--ticksRemaining <= 0)
            {
                Warps.Log?.LogWarning(
                    $"Watcher Warps: gave up re-registering warped avatar into {pendingDestRoom} " +
                    "(destination session never became active); remote players may be invisible until re-lobby");
                Clear();
                return;
            }

            TryReentry();
        }

        private static void TryReentry()
        {
            if (OnlineManager.lobby == null) { Clear(); return; }

            WorldSession? ws = pendingWorldSession;
            AbstractCreature? avatar = pendingAvatar;
            if (ws == null || avatar == null || pendingDestRoom == null) { Clear(); return; }

            if (avatar.realizedCreature == null || avatar.realizedCreature.slatedForDeletetion || avatar.state?.dead == true)
            {
                Clear();
                return;
            }

            if (!ws.isAvailable) return;

            AbstractRoom? absRoom = ws.world?.GetAbstractRoom(pendingDestRoom);
            if (absRoom == null) return;
            if (!RoomSession.map.TryGetValue(absRoom, out var roomSession)) return;
            if (!roomSession.isAvailable || !roomSession.isActive) return;

            ws.ApoEnteringWorld(avatar);
            roomSession.ApoEnteringRoom(avatar, avatar.pos);

            // HONESTY CHECK (phase 7-01, 2026-08-28 playtest): the two calls above are
            // a NO-OP for a Creature avatar that fell out of OnlinePhysicalObject.map.
            // MeadowGameMode.ShouldRegisterAPO returns false for Creature APOs unless
            // RainMeadow.sSpawningAvatar is set, so ApoEntering* never re-registers it;
            // and forcing sSpawningAvatar hits MeadowGameMode.NewEntity's
            // `throw new InvalidProgrammerException("entity re-register")` for an
            // already-realized avatar with an EmoteDisplayer. Confirmed in both logs:
            // `ID.-1.x doesn't exist in online space!` after this "success". Report the
            // real outcome so the next playtest log is trustworthy; see the plan file
            // for the redesign (keep the avatar's OnlineCreature alive through the
            // transition instead of tearing it down and re-adding it).
            if (OnlinePhysicalObject.map.TryGetValue(avatar, out _))
            {
                Warps.Log?.LogInfo(
                    $"Watcher Warps: warped avatar {avatar.ID} is in online space for {pendingDestRoom}");
            }
            else
            {
                Warps.Log?.LogWarning(
                    $"Watcher Warps: warped avatar {avatar.ID} still NOT in online space after " +
                    $"ApoEntering* into {pendingDestRoom} - remote players cannot see it (known: " +
                    "Meadow blocks avatar-APO re-registration; needs teardown redesign)");
            }
            Clear();
        }

        private static void Clear()
        {
            pendingWorldSession = null;
            pendingDestRoom = null;
            pendingAvatar = null;
            ticksRemaining = 0;
        }
    }
}
