using RainMeadow;
using RWCustom;
using UnityEngine;
using Watcher;

namespace WatcherWarps
{
    // Generalizes Watcher.WarpPoint.Update's ReadyForWarp trigger-selection loop,
    // ExitWarp placement, and CoolDown timer scaling (vanilla drives all three off
    // room.game.Players, a Player-only list) to also cover the local Meadow avatar
    // when it isn't a Player. One hook on Update covers all three states per
    // phase1-06-exit-placement's instruction not to add a second hook on the same
    // method.
    public static class WarpPointTriggerHooks
    {
        public static void Apply()
        {
            On.Watcher.WarpPoint.Update += WarpPoint_Update;
        }

        private static void WarpPoint_Update(On.Watcher.WarpPoint.orig_Update orig, WarpPoint self, bool eu)
        {
            orig(self, eu);

            if (self.currentState == WarpPoint.State.ExitWarp)
            {
                HandleExitWarp(self);
                return;
            }

            if (self.currentState == WarpPoint.State.CoolDown)
            {
                HandleCoolDown(self);
                return;
            }

            // Invariant: a Meadow client's local avatar is either a Player (vanilla path
            // above already handled it) or it isn't (this path) — never both in the same
            // frame, since a client only has one local avatar. So we only need to check
            // that `orig` didn't already push this WarpPoint into EnterWarp for a real
            // Player before running our own parallel selection below.
            if (self.currentState != WarpPoint.State.ReadyForWarp || self.warpLocked || !self.transportable)
                return;

            if (!CreatureWarpState.TryGetLocalNonPlayerAvatar(out Creature avatar))
                return;

            if (avatar.dead || avatar.room?.abstractRoom == null || avatar.room.abstractRoom.name != self.room.abstractRoom.name)
                return;

            // Mirror vanilla's Player-selection loop just to confirm no real Player in the
            // room already claimed triggerTime/state this tick (see invariant above).
            for (int k = 0; k < self.room.game.Players.Count; k++)
            {
                AbstractCreature abstractPlayer = self.room.game.Players[k];
                if (abstractPlayer.realizedCreature is not Player player || abstractPlayer.Room.name != self.room.abstractRoom.name || player.dead)
                    continue;
                if (player.warpPointCooldown <= 0 && player.warpExhausionTime <= 0)
                    return;
            }

            CreatureWarpState state = CreatureWarpState.Get(avatar);
            if (state.warpPointCooldown > 0 || state.warpExhausionTime > 0)
                return;

            float dist = Vector2.Distance(self.pos, avatar.mainBodyChunk.pos);
            float pullRadius = self.PullRadius;
            bool inRoom = avatar.room != null && !avatar.inShortcut;

            if (inRoom && dist < pullRadius)
            {
                state.standingInWarpPointProtectionTime = 10;
            }

            // Vanilla drives warpTearOpenTriggered (which opens the warp-tear visuals and
            // the screen distortion shader) off *any* Player physically in the room, using
            // the PullRadius*4.5 / *3 range bands (decompile lines 2351-2373). Only the
            // triggerTime accrual that actually charges the warp is gated on the tight
            // `< PullRadius` proximity (decompile line 2374). The old code returned early at
            // `dist >= pullRadius`, so a non-Player avatar never opened the visuals until it
            // was already inside PullRadius ("extremely close") — a slugcat in the same spot
            // sees them from a third of a screen away. Mirror vanilla: run the open/close
            // band logic whenever the avatar is in the room, gate only charging on proximity.
            bool nonPlayerRequestingOpen = self.nonPlayerRequestOpen != null && self.nonPlayerRequestOpen.Entering();
            if (self.closesEarly && dist > pullRadius * 6f)
            {
                self.closesEarly = false;
            }
            if (!nonPlayerRequestingOpen && !(dist < pullRadius * (self.closesEarly ? 2f : 4.5f)))
            {
                if (self.warpTearOpenTriggered)
                {
                    self.room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Close, self.pos);
                }
                self.warpTearOpenTriggered = false;
            }
            else if (dist < pullRadius * (self.closesEarly ? 1.5f : 3f))
            {
                if (!self.warpTearOpenTriggered)
                {
                    self.playOpenSoundAfterDelay = 80;
                }
                self.warpTearOpenTriggered = true;
            }

            // triggerTime accrual (within PullRadius) and decay (outside it) — mirrors
            // decompile lines 2374-2410.
            if (inRoom && dist < pullRadius && (self.visualOpenness > 0.5f || self.guaranteeTrigger))
            {
                // Kick off the destination-region load. Vanilla WarpPoint.WarpPrecast
                // clears canPreCast as its very first statement (decompile line 1978),
                // *before* its own early-return guards — `warpWorldLoader` still in
                // flight, or `overWorld.activeWorld != room.world`. On a Meadow client
                // whose `activeWorld` doesn't reference the same World instance as the
                // warp point's room (observed on non-host clients: the warp never calls
                // OverWorld.InitiateSpecialWarp_WarpPoint at all), that guard trips, so
                // WarpPrecast latches canPreCast=false and permanently disables precast
                // for this point — the avatar then rides triggerTime into EnterWarp,
                // which waits forever for a warpWorldLoader that was never created.
                //
                // So: keep retrying (re-arm canPreCast every tick until a load is
                // actually queued), and only call WarpPrecast when its activeWorld guard
                // will pass. DestinationLoadStarted() below gates the EnterWarp commit
                // on this having succeeded.
                if (!self.Data.rippleEggWarpPoint && Region.RegionReadyToWarp && !DestinationLoadStarted(self)
                    && self.room.game.overWorld.activeWorld == self.room.world)
                {
                    self.canPreCast = true;
                    self.WarpPrecast();
                }

                int touchedNoInputCounter = CreatureController.creatureControllers.TryGetValue(avatar, out CreatureController controller)
                    ? controller.touchedNoInputCounter
                    : 0;

                float rate = 1f + Mathf.Pow(Mathf.InverseLerp(pullRadius, 10f, dist), 0.5f) * 3f;
                rate = self.guaranteeTrigger
                    ? 1f
                    : (touchedNoInputCounter > 0
                        ? (touchedNoInputCounter >= 20
                            ? rate + Custom.LerpMap(touchedNoInputCounter, 20f, 120f, 0f, 3f)
                            : rate * Custom.LerpMap(touchedNoInputCounter, 1f, 20f, 0.5f, 1f))
                        : (!(self.triggerTime < self.triggerActivationTime / 2f)
                            ? Custom.LerpMap(self.triggerTime, self.triggerActivationTime / 2f, self.triggerActivationTime, -0.5f, -4f)
                            : rate * 0.5f));
                self.triggerTime += rate;
            }
            else if (dist >= pullRadius)
            {
                self.triggerTime = self.triggerTime > 0f ? self.triggerTime - 2f : 0f;
                if (self.triggerTime == 0f)
                {
                    self.canPreCast = true;
                    self.strongPull = false;
                }
            }

            if (self.triggerTime >= self.triggerActivationTime)
            {
                // Never enter EnterWarp unless the destination-region load is actually
                // under way. EnterWarp's state body only advances to ExitWarp once
                // `warpWorldLoader.Finished` (decompile line 2419) — committing without a
                // loader is a permanent, silent softlock (avatar frozen mid-warp, no
                // error). Hold at the threshold and let the precast retry above run.
                if (!DestinationLoadStarted(self))
                {
                    self.triggerTime = self.triggerActivationTime;
                    if (self.room.game.clock % 40 == 0)
                    {
                        OverWorld ow = self.room.game.overWorld;
                        Warps.Log?.LogWarning(
                            $"WatcherWarps: warp to {self.Data.RegionString}/{self.Data.destRoom} stalled at trigger threshold — " +
                            $"no destination load queued. activeWorld={ow.activeWorld?.name ?? "NULL"}, " +
                            $"room.world={self.room.world?.name ?? "NULL"}, sameRef={(ow.activeWorld == self.room.world)}, " +
                            $"canPreCast={self.canPreCast}, RegionReadyToWarp={Region.RegionReadyToWarp}, warpWorldLoader={(ow.warpWorldLoader != null)}");
                    }
                    return;
                }

                state.triggeredWarpPoint = self;
                self.canWarpToVoidWeaverEnding = self.CheckCanWarpToVoidWeaverEnding();
                Warps.Log?.LogInfo($"WatcherWarps: non-Player avatar triggered warp point, entering EnterWarp (triggerTime={self.triggerTime})");
                self.ChangeState(WarpPoint.State.EnterWarp);
            }
        }

        // True once OverWorld has begun (or finished) loading the warp's destination
        // region — i.e. WarpPrecast successfully reached
        // OverWorld.InitiateSpecialWarp_WarpPoint. Mirrors the readiness checks in
        // WarpPoint.Update's EnterWarp body (decompile line 2419).
        private static bool DestinationLoadStarted(WarpPoint self)
        {
            OverWorld ow = self.room.game.overWorld;
            return ow.warpWorldLoader != null
                || ow.warpingPreload
                || string.Equals(ow.activeWorld?.name, self.Data.RegionString, System.StringComparison.OrdinalIgnoreCase);
        }

        // Generalizes the ExitWarp state body (decompile lines 2433-2447), which
        // loops room.game.Players and calls the Player-only OneWayPlacement/
        // TickLevitation/eyesClosedTime on each. Levitation and eyesClosedTime are
        // Player-only cosmetic polish and are skipped; the placement itself (the part
        // that matters for correctness) is replicated generically against
        // PhysicalObject.bodyChunks.
        //
        // Matches vanilla's "any Player physically in the room" loop rather than
        // filtering to WarpState.triggeredWarpPoint == self: the warp point running
        // ExitWarp on arrival is the freshly spawned *destination-side* point (a
        // different instance from the one the avatar walked into on the source side),
        // so a triggeredWarpPoint == self check never matches after a warp and would
        // leave the avatar stranded at its pre-warp abstract tile — i.e. the
        // bottom-left corner (0,0). This placement is what actually lands the avatar
        // at the destination, so it must run for the arrival point.
        private static void HandleExitWarp(WarpPoint self)
        {
            if (!CreatureWarpState.TryGetLocalNonPlayerAvatar(out Creature avatar))
                return;

            if (avatar.dead || avatar.room != self.room)
                return;

            PlaceAtWarpPoint(avatar, self.placedObject.pos);
            avatar.repelLocusts = Mathf.Max(avatar.repelLocusts, 240);

            CreatureWarpState state = CreatureWarpState.Get(avatar);
            state.warpPointCooldown = 80;
            state.triggeredWarpPoint = self;
        }

        // Mirrors the placement half of Player.OneWayPlacement (chunk 0 to the
        // target position, chunk 1 offset below it, velocities zeroed) without the
        // Player-only oneWayPlacementCooldown/camo bookkeeping. playerIndex-based
        // spawn-spread offsetting is dropped since only one local non-Player avatar
        // can ever be placed here.
        private static void PlaceAtWarpPoint(Creature avatar, Vector2 pos)
        {
            BodyChunk[] chunks = avatar.bodyChunks;
            if (chunks.Length > 0)
            {
                chunks[0].pos = pos;
                chunks[0].vel = Vector2.zero;
            }
            if (chunks.Length > 1)
            {
                chunks[1].pos = pos + new Vector2(0f, -20f);
                chunks[1].vel = Vector2.zero;
            }
        }

        // Generalizes the CoolDown state body (decompile lines 2485-2506), which
        // scales successfullWarpCooldownTimer's tick-down rate by the farthest
        // Player's distance from the warp point. orig() has already applied its
        // Player-only decrement by the time this runs; if the local non-Player
        // avatar is farther away than every real Player (or there are no real
        // Players at all, in which case orig used a flat fast-tick rate of 5), the
        // decrement orig applied was too fast, so it's undone and reapplied using
        // the correct farthest distance.
        private static void HandleCoolDown(WarpPoint self)
        {
            if (!CreatureWarpState.TryGetLocalNonPlayerAvatar(out Creature avatar))
                return;

            if (avatar.room != self.room)
                return;

            float? playersFarthest = null;
            foreach (AbstractCreature abstractPlayer in self.room.game.Players)
            {
                if (abstractPlayer.Room == self.room.abstractRoom && abstractPlayer.realizedCreature is Player player)
                {
                    float d = Vector2.Distance(self.pos, player.firstChunk.pos);
                    if (!playersFarthest.HasValue || playersFarthest < d)
                    {
                        playersFarthest = d;
                    }
                }
            }

            float avatarDist = Vector2.Distance(self.pos, avatar.mainBodyChunk.pos);
            float trueFarthest = playersFarthest.HasValue ? Mathf.Max(playersFarthest.Value, avatarDist) : avatarDist;

            if (playersFarthest.HasValue && trueFarthest <= playersFarthest.Value)
                return;

            float wrongRate = playersFarthest.HasValue
                ? Custom.LerpMap(playersFarthest.Value, self.activationRadius * 2f, 1000f, 1f, 5f)
                : 5f;
            float correctRate = Custom.LerpMap(trueFarthest, self.activationRadius * 2f, 1000f, 1f, 5f);

            self.successfullWarpCooldownTimer += wrongRate;
            self.successfullWarpCooldownTimer -= correctRate;
            if (self.successfullWarpCooldownTimer <= 0f)
            {
                self.ChangeState(WarpPoint.State.ReadyForWarp);
            }
        }
    }
}
