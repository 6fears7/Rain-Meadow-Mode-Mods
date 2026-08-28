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

            if (self.currentState == WarpPoint.State.EnterWarp)
            {
                // Species-agnostic safety net: unlike the ReadyForWarp precast kick below
                // (which only runs for a non-Player local avatar), this covers a Player
                // (Slugcat) non-owner client too. RainMeadow's Watcher_WarpPoint_WarpPrecast
                // hook (Story/StoryHooks.cs) makes self.WarpPrecast() a no-op for every
                // non-owner client regardless of avatar species (unless it's an echo warp),
                // so vanilla's own Player-driven Update - which calls self.WarpPrecast()
                // once triggerTime crosses the threshold and then transitions straight to
                // EnterWarp - gets silently eaten on a Slugcat client. EnterWarp's own state
                // body then waits forever for warpWorldLoader.Finished, which never happens
                // because InitiateSpecialWarp_WarpPoint was never called. Drive it here.
                HandleEnterWarp(self);
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
                // Kick off the destination-region load once we're in range.
                //
                // Rain Meadow's own On.Watcher.WarpPoint.WarpPrecast hook
                // (Story/StoryHooks.cs Watcher_WarpPoint_WarpPrecast) early-returns
                // *without calling orig* for every non-owner client
                // (`OnlineManager.lobby != null && !isOwner`, not story-gated) to avoid
                // racing the host's NormalExecuteWatcherRiftWarp RPC. That RPC only ever
                // fires in Story mode, so in a Meadow sandbox this just silently
                // suppresses the client's warp precast and nothing replaces it — the
                // avatar then rides triggerTime into EnterWarp and waits forever for a
                // warpWorldLoader that was never created.
                //
                // So drive the precast ourselves for non-owners: call
                // OverWorld.InitiateSpecialWarp_WarpPoint directly (its Meadow hook has
                // no owner gate). This also deliberately skips vanilla WarpPrecast's
                // bad-warp destination re-roll, which we don't want for our curated
                // fixed destinations anyway (plan/phase4). Owners keep using the real
                // WarpPrecast. DestinationLoadStarted() below gates the EnterWarp commit.
                if (!self.Data.rippleEggWarpPoint && Region.RegionReadyToWarp && !DestinationLoadStarted(self)
                    && self.room.game.overWorld.activeWorld == self.room.world)
                {
                    if (OnlineManager.lobby != null && !OnlineManager.lobby.isOwner)
                    {
                        DriveWarpPrecastForClient(self);
                    }
                    else
                    {
                        self.canPreCast = true;
                        self.WarpPrecast();
                    }
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

        // Replicates the essential tail of Watcher.WarpPoint.WarpPrecast (decompile
        // lines 1985-2020) for a non-owner client, whose real WarpPrecast is a no-op
        // (see caller). Only the non-rippleEgg / non-voidWeaver / non-badWarp-reroll
        // path: our injected points carry an explicit curated destRegion/destRoom, so
        // we pass Data straight through instead of rolling ChooseDynamicWarpTarget.
        // The music GateEvent and the levitation bookkeeping are cosmetic and skipped.
        private static void DriveWarpPrecastForClient(WarpPoint self)
        {
            OverWorld ow = self.room.game.overWorld;

            // WarpPrecast's own guard: only fire when no loader is already running for
            // this world (decompile lines 1985-1990).
            if (ow.warpWorldLoader != null && !ow.warpWorldLoader.Finished)
                return;

            self.canPreCast = false;
            self.canWarpToVoidWeaverEnding = self.CheckCanWarpToVoidWeaverEnding();

            WarpPoint.WarpPointData data = self.overrideData ?? self.Data;
            ow.InitiateSpecialWarp_WarpPoint(self, data, useNormalWarpLoader: false);

            if (self.room.game.cameras.Length > 0)
            {
                self.room.game.cameras[0].WarpMoveCameraPrecast(data.destRoom, data.destCam);
            }

            Warps.Log?.LogInfo(
                $"WatcherWarps: drove client-side warp precast for {data.RegionString}/{data.destRoom} " +
                $"(warpingPreload={ow.warpingPreload})");
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

        // Species-agnostic precast safety net for the EnterWarp state (see call site
        // comment above). Runs for every local avatar - Player or not - because
        // RainMeadow's precast suppression for non-owner clients isn't species-gated
        // either. A no-op once the destination load is already under way (the common
        // case for the owner, and for a non-Player client whose ReadyForWarp-state
        // precast kick already ran DriveWarpPrecastForClient below).
        private static void HandleEnterWarp(WarpPoint self)
        {
            if (self.Data.rippleEggWarpPoint)
                return;

            if (OnlineManager.lobby == null || OnlineManager.lobby.isOwner)
                return;

            if (!Region.RegionReadyToWarp || DestinationLoadStarted(self))
                return;

            if (self.room?.game?.overWorld == null || self.room.game.overWorld.activeWorld != self.room.world)
                return;

            Warps.Log?.LogInfo(
                $"WatcherWarps: EnterWarp reached on non-owner client with no destination load " +
                $"queued (RainMeadow's WarpPrecast suppression) - driving precast for " +
                $"{self.Data.RegionString}/{self.Data.destRoom}");

            DriveWarpPrecastForClient(self);
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
