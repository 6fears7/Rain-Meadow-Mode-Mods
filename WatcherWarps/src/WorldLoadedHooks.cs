using RainMeadow;
using RWCustom;
using UnityEngine;
using Watcher;

namespace WatcherWarps
{
    // Generalizes OverWorld_WorldLoaded's post-orig `game.Players` loop
    // (Rain-Meadow/Game/RainMeadow.EntityHooks.cs ~705-728) to the local
    // non-Player Meadow avatar. That loop only ever reads `absplayer.realizedCreature
    // is Player player`, so a Lizard/Scavenger/etc. avatar's own AbstractCreature rides
    // along with the world swap (driven by OnlineManager.lobby.playerAvatars in the
    // beingMoved-toggle loop above it, already avatar-species-agnostic) but its
    // grasped objects never get registered into the new WorldSession and its position
    // never gets fixed up from the warp's destPos.
    //
    // slugOnBack and objectInStomach are declared directly on Player (confirmed via
    // ilspycmd against PUBLIC-Assembly-CSharp.dll - grasps is Creature-level and
    // inherited, the other two are not), and nothing in Watcher content lets a
    // non-Player creature swallow objects or carry a rescued slugcat, so there's no
    // non-Player analogue to replicate for those two - only the position fixup and
    // grasp carry apply here.
    //
    // Only the warpUsed branch is replicated: the plain-gate branch only carries
    // objectInStomach (Player-only, N/A here) because ordinary gate crossings keep
    // the room's entity list intact and grasped objects already follow along with it;
    // it's only the region-swapping warpUsed path that needs objects manually
    // registered into the new WorldSession.
    public static class WorldLoadedHooks
    {
        // ShelterDoor ctor: openUpTicks = 350f, initialWait = 80f (verified via
        // ilspycmd -t ShelterDoor, lines 1258-1259). Doors are shut for timer < 430.
        private const int MeadowCycleStartTicks = 430;
        // The literal RainMeadow uses in Meadow/RainMeadow.MeadowHooks.cs:364. Do not invent a
        // different one - clients sync rainCycle.timer to each other (WorldSession.WorldState
        // .ReadTo), so a mismatch just churns.
        private const int MeadowCycleTimer = 800;

        public static void Apply()
        {
            On.OverWorld.WorldLoaded += OverWorld_WorldLoaded;
        }

        private static void OverWorld_WorldLoaded(On.OverWorld.orig_WorldLoaded orig, OverWorld self, bool warpUsed)
        {
            // PRE-orig (phase 7-03). This mod's hook is the outermost wrapper (BepInEx
            // loads WatcherWarps after its RainMeadow dependency), so everything here
            // runs before RainMeadow.EntityHooks.OverWorld_WorldLoaded's warpUsed branch
            // and, crucially, before its DeactivateAndWait tears down the source region.
            if (warpUsed && Warps.IsMeadowWatcher())
            {
                PreWarpDestinationHandoff(self);
            }

            orig(self, warpUsed);

            if (!warpUsed) return;

            // phase 7-07: Meadow freezes the rain cycle forever
            // (RainMeadow.MeadowHooks.cs:400 AllowRainCounterToTick => false) and compensates for
            // the freeze in exactly one place: On.OverWorld.LoadFirstWorld sets
            // activeWorld.rainCycle.timer = 800 (MeadowHooks.cs:358-367, comment "timer stuck past
            // cycle start"). A Watcher region warp builds a NEW World with a NEW RainCycle at
            // timer ~0 and never passes through LoadFirstWorld, so every ShelterDoor in the
            // destination region constructs with
            // closedFac = InverseLerp(initialWait + openUpTicks, initialWait, timer)
            //           = InverseLerp(430, 80, 0) = 1 (fully closed),
            // and ShelterDoor.Update re-asserts that same value every frame while timer < 430.
            // The door can never open, which is why shelters only work again after a lobby reload.
            // Mirror Meadow's own fixup here. Idempotent: skip if the destination region is already
            // past cycle start (someone warped in before us and already fixed it, or its owner
            // synced a good value). Placed above every avatar-scoped early-return below because the
            // rain cycle belongs to the world, not the avatar (a host on a Slugcat avatar hits the
            // Player return and would otherwise skip the fix entirely - 7-06 D-1).
            if (Warps.IsMeadowWatcher())
            {
                RainCycle? cycle = self.activeWorld?.rainCycle;
                if (cycle == null)
                {
                    Warps.Log?.LogWarning(
                        $"Watcher Warps: warp into {self.activeWorld?.name ?? "?"} - destination world has no " +
                        "rainCycle; shelter doors will construct closed (ShelterDoor ctor line 1316 branch)");
                }
                else if (cycle.timer < MeadowCycleStartTicks)   // 430 = initialWait 80 + openUpTicks 350
                {
                    Warps.Log?.LogInfo(
                        $"Watcher Warps: warp into {self.activeWorld?.name} - rainCycle.timer was {cycle.timer} " +
                        $"(< {MeadowCycleStartTicks}, shelter doors would stay shut); setting to {MeadowCycleTimer} " +
                        "to match RainMeadow's LoadFirstWorld fixup");
                    cycle.timer = MeadowCycleTimer;             // 800, the exact value Meadow uses
                }
            }

            // phase 7-06 A-1: gate on the ABSTRACT avatar, not a realized non-Player
            // creature. The avatar can be abstractized during the world swap, and - more
            // importantly - vanilla's warpUsed branch only migrates entities in
            // game.Players. A non-Player Meadow avatar (Lizard/Scavenger/...) is never in
            // that list (MeadowGameMode.SpawnAvatar only AddPlayer's Slugcats), so its
            // abstractCreature.world is never advanced to the destination World. We then
            // rewrote its pos.room to a destination index in 7-05-03, leaving
            // world=SOURCE / pos.room=DEST -> AbstractCreature.Room resolves to null ->
            // RoomRealizer.Update dereferences null.offScreenDen every frame forever
            // (hard client hang). Fix: replicate the FULL four-step vanilla migration.
            if (!CreatureWarpState.TryGetLocalAvatar(out AbstractCreature ac)) return;
            if (ac.state?.dead == true || ac.realizedCreature?.slatedForDeletetion == true) return;

            // Players are already migrated by vanilla's warpUsed branch; double-migrating
            // (RemoveEntity from a room that no longer holds it, then a second AddEntity)
            // corrupts the room entity lists. Bail for them.
            if (ac.realizedCreature is Player
                || (ac.state is PlayerState && self.game != null && self.game.Players.Contains(ac)))
            {
                return;
            }

            WorldSession newWorldSession = self.activeWorld.GetResource();
            if (newWorldSession == null) return;

            // Fix the arrival tile the way vanilla's own Player loop does ("do not get
            // stuck on bottom left"). destPos is normally non-null by now - vanilla's
            // WarpPoint.NewWorldLoaded backfills a null one from a PlacedObject.Type.
            // DynamicWarpTarget in the destination room, and ArrivalMarkerHooks guarantees
            // every reachable room has one. If it's still null/zero here (marker chain
            // didn't run yet, or the destination room isn't realized), resolve a safe tile
            // ourselves rather than leaving the avatar at (0,0). WarpPointTriggerHooks'
            // HandleExitWarp then corrects the exact position once the destination warp
            // point runs its ExitWarp state.
            Vector2? destPos = self.warpData?.destPos;
            if (destPos == null || destPos.Value == Vector2.zero)
            {
                destPos = ResolveDestPosFallback(self);
            }

            // phase 7-06 A-1: the FULL vanilla migration, not just the pos rewrite that
            // 7-05-03 added. Vanilla's warpUsed branch does four things together for each
            // Player - RemoveEntity(old room), world = newWorld, pos = newCoord,
            // AddEntity(new room). 7-05-03 only did the pos part, which is what left
            // world=SOURCE / pos.room=DEST and hung the RoomRealizer. The room index must
            // now ALWAYS come from destAbsRoom; if we can't resolve it we bail rather than
            // half-migrate.
            string? warpDestRoom = self.warpData?.destRoom;
            World oldWorld = ac.world;                 // still the SOURCE world here
            World newWorld = self.activeWorld;         // orig() already swapped this
            AbstractRoom? destAbsRoom = warpDestRoom != null
                ? newWorld?.GetAbstractRoom(warpDestRoom)
                : null;
            if (destAbsRoom == null)
            {
                Warps.Log?.LogError(
                    $"Watcher Warps: warp into {warpDestRoom ?? "?"} - destination AbstractRoom did not " +
                    $"resolve in {newWorld?.name ?? "?"}; skipping avatar migration (avatar left in source world)");
                return;
            }

            IntVector2 destTile;
            if (destPos != null && destPos.Value != Vector2.zero)
            {
                destTile = new IntVector2(
                    (int)(destPos.Value.x / 20f),
                    (int)(destPos.Value.y / 20f));
            }
            else
            {
                destTile = new IntVector2(ac.pos.x, ac.pos.y);
                Warps.Log?.LogWarning(
                    $"Watcher Warps: warp into {warpDestRoom} had no resolvable destPos; migrating room " +
                    "index only, relying on the destination warp point's ExitWarp for the tile");
            }

            var destCoord = new WorldCoordinate(destAbsRoom.index, destTile.x, destTile.y, -1);

            oldWorld?.GetAbstractRoom(ac.pos)?.RemoveEntity(ac);
            ac.world = newWorld;                       // THE line 7-05-03 was missing
            ac.pos = destCoord;
            destAbsRoom.AddEntity(ac);

            // Re-home the abstract AI too. log.1:8344 showed abstractAI.denPosition (and
            // its own `world` field - the "world of pathfinder was not the same world as
            // its owner" DANGER) stuck in the source region, which NREs PathFinder every
            // frame. denPosition is a WorldCoordinate? on AbstractCreatureAI (verified via
            // ilspycmd); it also owns `world`, `destination`, and a `path` list.
            if (ac.abstractAI != null)
            {
                ac.abstractAI.world = newWorld;
                ac.abstractAI.path?.Clear();
                ac.abstractAI.destination = destCoord;
                var den = ac.abstractAI.denPosition;
                if (den == null || newWorld?.GetAbstractRoom(den.Value) == null)
                {
                    ac.abstractAI.denPosition = destCoord;
                }
            }

            // Invariant: after a correct migration the avatar resolves a room in the
            // active world. If it doesn't, that is exactly the RoomRealizer-hang
            // precondition - name it in the log instead of discovering 485 stack traces.
            if (ac.world != self.activeWorld || ac.Room == null)
            {
                Warps.Log?.LogError(
                    $"Watcher Warps: avatar migration invariant FAILED - world={ac.world?.name ?? "null"} " +
                    $"activeWorld={self.activeWorld?.name ?? "null"} pos.room={ac.pos.room} Room={ac.Room?.name ?? "null"}; " +
                    "RoomRealizer may hang (A-2 guard should catch it)");
            }

            if (ac.realizedCreature is Creature realizedAvatar)
            {
                RegisterGrasps(realizedAvatar, newWorldSession);
            }

            // Phase 7-01: PerformWarpStripHooks + world deactivation detach this
            // avatar's APO from the online resource system, and nothing re-enters it
            // into the destination sessions, so remote players never learn the avatar
            // exists in the new region. Confirmed on a non-host warp (host couldn't
            // see the warped client); not run for the owner in that log only because
            // the host was stationary. The re-register is idempotent (EnterResource
            // no-ops if already entered, ApoEntering* no-op unless the resource is
            // live), so drive it for everyone rather than assume the host is immune.
            // Defer until the destination World/RoomSession are active - see
            // AvatarWarpReentryHooks.
            string? destRoom = self.warpData?.destRoom ?? ac.Room?.name;
            AvatarWarpReentryHooks.RequestReentry(newWorldSession, destRoom, ac);
        }

        // phase 7-03: the two things that MUST happen before vanilla's warpUsed branch
        // runs orig() + DeactivateAndWait().
        //
        // Stage 1 - enter the local avatar into the destination WorldSession while it is
        //   still in OnlinePhysicalObject.map. ApoEnteringWorld takes the in-map branch
        //   (oe.EnterResource(destWs)) - no ShouldRegisterAPO gate, no NewEntity throw -
        //   so the avatar becomes joined/isPending on the destination region. Then when
        //   DeactivateAndWait deactivates the source region, OnlineEntity.Deactivated's
        //   `primaryResource == null && !isPending` guard is false and the avatar is NOT
        //   deregistered. Room re-entry then happens normally once the destination
        //   RoomSession activates (AvatarWarpReentryHooks poll / ApoEnteringRoom in-map
        //   branch). Without this the avatar's joinedResources is just [source region],
        //   which empties on deactivate -> Deregister -> invisible-until-relobby, and
        //   Meadow cannot re-register a live avatar.
        //
        // Stage 2 - remove every OTHER player's avatar from the source world before
        //   orig() copies the source world's abstract-room entity lists into the
        //   destination world. Vanilla's gate-switch branch guards this
        //   (RainMeadow.EntityHooks ~632: `if (!opo.isMine) opo.RemoveEntityFromGame(false)`);
        //   the warpUsed branch does not, so a copied-but-unsynced remote avatar
        //   realizes in the destination as an uncontrolled ghost NPC. Their real avatar
        //   arrives via online sync if that player also warps here, never via the copy.
        private static void PreWarpDestinationHandoff(OverWorld self)
        {
            World? sourceWorld = self.activeWorld;
            World? destWorld = self.worldLoader?.ReturnWorld() ?? sourceWorld;
            if (destWorld == null || sourceWorld == null) return;
            if (sourceWorld.name == destWorld.name) return; // same-world "warp"; nothing to hand off

            WorldSession? destWs = destWorld.GetResource();
            if (destWs == null)
            {
                Warps.Log?.LogWarning(
                    $"Watcher Warps: pre-warp handoff - no WorldSession for destination {destWorld.name}; " +
                    "avatar may be deregistered on source teardown");
                return;
            }

            // Stage 1
            if (CreatureWarpState.TryGetLocalAvatar(out AbstractCreature localAvatar))
            {
                if (!OnlinePhysicalObject.map.TryGetValue(localAvatar, out _))
                {
                    Warps.Log?.LogWarning(
                        $"Watcher Warps: pre-warp handoff - local avatar {localAvatar.ID} already out of " +
                        "online space before teardown (unexpected); cannot hand it off");
                }
                else if (!destWs.isAvailable)
                {
                    // Both 2026-08-28 logs had the destination region live well before
                    // this point on host AND client. If this fires, phase 7-03 Stage 3
                    // (a mod-side wait for destWs.isAvailable) is needed.
                    Warps.Log?.LogWarning(
                        $"Watcher Warps: pre-warp handoff - destination {destWs} not available yet; " +
                        "avatar handoff skipped, expect invisible-after-warp (needs phase 7-03 Stage 3)");
                }
                else
                {
                    destWs.ApoEnteringWorld(localAvatar);
                    Warps.Log?.LogInfo(
                        $"Watcher Warps: pre-warp handoff - entered local avatar {localAvatar.ID} " +
                        $"into {destWs} before source teardown");
                }
            }

            // Stage 2
            OrphanAvatarDiagnosticHooks.ResetOrphanLog(); // TEMP phase 7-05-00
            RemoveRemoteAvatarsFromSourceWorld(sourceWorld);
        }

        private static void RemoveRemoteAvatarsFromSourceWorld(World sourceWorld)
        {
            var lobby = OnlineManager.lobby;
            if (lobby == null) return;

            foreach (var kv in lobby.playerAvatars)
            {
                if (kv.Value.FindEntity(true) is not OnlinePhysicalObject opo) continue;
                if (opo.isMine) continue;
                if (opo.apo is not AbstractCreature remoteAvatar) continue;
                // Any remote avatar still tied to the world we're leaving - by its
                // abstract world pointer OR by a realized creature sitting in one of the
                // source world's rooms. The realized-room check matters: playtest
                // 2026-08-28 12:15 showed a remote RedLizard whose abstract `world` had
                // already drifted but whose realized creature was still updating in
                // SU_C04 ("DANGER the world of pathfinder inside RedLizard was not the
                // same world as its owner" + PathFinder NRE every frame).
                bool tiedToSource = remoteAvatar.world == sourceWorld
                    || remoteAvatar.realizedCreature?.room?.world == sourceWorld;
                if (!tiedToSource) continue;

                // Option A (phase 7-05-02, playtest 2026-08-28 picked it): the bare
                // RemoveEntityFromGame(false) left this avatar's realized Creature ALIVE
                // with a ticking abstractAI - it then migrated into the destination
                // region as a ghost NPC and its EntityID(-1,seed) collided with the
                // real player's incoming avatar. Fix: despawn the realization the way
                // RainMeadow's own "early creature" branch does
                // (OnlinePhysicalObject.cs:298-315) - Abstractize, NEVER Destroy() (that
                // was phase 7-04's regression). Do it HERE, pre-orig, while the SOURCE
                // room is still readyForAI, so AbstractCreature.Abstractize takes its
                // full despawn path (decompiled: it early-outs to a bare
                // `realizedCreature = null` when the room is not readyForAI, leaving
                // abstractAI running). Abstractize BEFORE RemoveEntityFromGame so
                // realizedCreature is still live for the despawn; let Abstractize pull
                // the creature from the room rather than a manual RemoveFromRoom first
                // (double-removal NRE risk).
                if (remoteAvatar.realizedCreature is Creature rc)
                {
                    rc.slatedForDeletetion = true;
                    rc.RemoveFromShortcuts();
                }
                // Abstractize at a coordinate that actually resolves in the CURRENT
                // world - if the avatar's `world`/`pos` drifted, its own `pos` can name
                // a room this world doesn't have and Abstractize's pathfinding recompute
                // NREs. Prefer the realized creature's live room coord.
                WorldCoordinate abstractizeAt = remoteAvatar.pos;
                if (remoteAvatar.realizedCreature?.room?.abstractRoom is AbstractRoom liveRoom)
                {
                    abstractizeAt = new WorldCoordinate(liveRoom.index, -1, -1, 0);
                }
                try
                {
                    remoteAvatar.Abstractize(abstractizeAt);
                }
                catch (System.Exception e)
                {
                    Warps.Log?.LogWarning($"Watcher Warps: pre-warp handoff - Abstractize threw for {remoteAvatar.ID}: {e.Message}; forcing realized despawn");
                    remoteAvatar.realizedCreature?.RemoveFromRoom();
                }

                opo.RemoveEntityFromGame(false);
                Warps.Log?.LogInfo(
                    $"Watcher Warps: pre-warp handoff - abstractized + removed remote avatar {remoteAvatar.ID} " +
                    $"from source world {sourceWorld.name} so it can't ride in as a ghost NPC");
                OrphanAvatarDiagnosticHooks.LogRemovedAvatarState(remoteAvatar, sourceWorld.name); // TEMP phase 7-05-00
            }
        }

        // Last-resort arrival position when OverWorld.warpData.destPos is null/zero:
        // an authored DynamicWarpTarget in the (realized) destination room, else a
        // standable tile in it. Returns null if the room isn't realized yet.
        private static Vector2? ResolveDestPosFallback(OverWorld self)
        {
            string? destRoom = self.warpData?.destRoom;
            if (destRoom == null) return null;

            Room? room = self.activeWorld?.GetAbstractRoom(destRoom)?.realizedRoom;
            if (room == null) return null;

            if (room.roomSettings?.placedObjects != null)
            {
                foreach (var po in room.roomSettings.placedObjects)
                {
                    if (po.type == PlacedObject.Type.DynamicWarpTarget)
                    {
                        return po.pos;
                    }
                }
            }

            return Warps.ReachablePos(room);
        }

        private static void RegisterGrasps(Creature avatar, WorldSession newWorldSession)
        {
            if (avatar.grasps == null) return;
            for (int k = 0; k < avatar.grasps.Length; k++)
            {
                if (avatar.grasps[k]?.grabbed != null)
                {
                    newWorldSession.ApoEnteringWorld(avatar.grasps[k].grabbed.abstractPhysicalObject);
                }
            }
        }
    }
}
