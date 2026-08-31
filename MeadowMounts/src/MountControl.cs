using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RainMeadow;
using UnityEngine;

namespace MeadowMounts
{
    public static class MountControl
    {
        private class PressWatch
        {
            public bool lastRaw;
            public int latch;
        }

        private class State
        {
            public bool lastPickup;
            public bool lastRelease;
            public bool lastJump;
            public bool lastJawHeld;
            public readonly Dictionary<int, int> regrabGraceUntilFrame = new();
            public readonly Dictionary<int, PressWatch> pressWatches = new();
            public bool collisionSuppressed;
            public readonly List<MountLink> owned = new();
            public readonly List<MountLink> scratchA = new();
            public readonly List<MountLink> scratchB = new();
        }

        private static readonly ConditionalWeakTable<CreatureController, State> states = new();
        private const int RegrabGrace = 20;
        private const int PressLatchFrames = 10;

        private static int RidersOf(Creature mount, List<MountLink> scratch)
        {
            MountTopology.ChildrenOf(mount, scratch);
            var riders = 0;
            foreach (var child in scratch)
            {
                if (child.kind is MountKind.Ride or MountKind.BackRide) riders++;
            }
            return riders;
        }

        public static void Update(CreatureController cc)
        {
            var self = cc.creature;
            if (self?.room == null) return;

            MountDebug.MaybeDump(self.room);

            var state = states.GetValue(cc, _ => new State());

            var parentLink = MountLink.ParentOf(self);
            ApplyPassengerState(cc, state, parentLink);

            if (!cc.onlineCreature.isMine) return;

            var pickup = MountButtons.Held(MountOptions.Instance.grabButton.Value, cc.MapInput);
            var pickupPressed = pickup && !state.lastPickup;
            state.lastPickup = pickup;

            var release = MountButtons.Held(MountOptions.Instance.releaseButton.Value, cc.MapInput);
            var releasePressed = release && !state.lastRelease;
            state.lastRelease = release;

            var jump = cc.MapInput.jmp;
            var jumpPressed = jump && !state.lastJump;
            state.lastJump = jump;

            if (MountDebug.Verbose) DetectSilentDrop(self, state);

            MountTopology.GraspsOwnedBy(self, state.owned);

            // Being carried: jump removes yourself. Self already owns the grasp for Ride, and
            // BackRide is a plain RPC handshake, so either can be released straight from here -
            // no need to wait on the carrier to notice anything.
            if (jumpPressed && parentLink is { kind: MountKind.Ride or MountKind.BackRide } mine)
            {
                Release(cc, state, mine);
                return;
            }

            // Carrying others: the configured release key drops everything you're actively
            // holding (Carry/Mouth grasps, or someone riding on your back) - not a mount you
            // yourself are riding, since that's the passenger-side case above.
            if (releasePressed)
            {
                var releasedAny = false;
                foreach (var link in state.owned)
                {
                    if (link.anchor != self) continue;
                    Release(cc, state, link);
                    releasedAny = true;
                }
                if (releasedAny) return;
            }

            foreach (var link in state.owned)
            {
                if (link.kind is MountKind.Ride or MountKind.BackRide || !link.StillValid) continue;

                MountTopology.GraspsOwnedBy(link.passenger, state.scratchA);
                if (state.scratchA.Count > 0) continue;

                if (CreatureController.creatureControllers.TryGetValue(link.passenger, out var passengerController)
                    && WatchPress(state, link.passenger, passengerController.MapInput.jmp))
                {
                    Release(cc, state, link);
                    return;
                }
            }

            if (parentLink is { kind: MountKind.Ride } ride)
            {
                MountTopology.GraspsOwnedBy(ride.anchor, state.scratchA);
                var anchorHasParent = MountLink.ParentOf(ride.anchor).HasValue;

                // This has to be a fixed vanilla action, not the rider's own configured release
                // button: the mount's remix config isn't visible to us over the network, and the
                // mount is the one deciding to buck its rider off, so the watched signal needs to
                // mean the same thing regardless of whose client is watching.
                if (state.scratchA.Count == 0 && !anchorHasParent
                    && CreatureController.creatureControllers.TryGetValue(ride.anchor, out var anchorController)
                    && WatchPress(state, ride.anchor, anchorController.MapInput.spec))
                {
                    Release(cc, state, ride);
                    return;
                }
            }

            // Lizards don't grab on the button press - the bite is committed when the jaws
            // close again (button release), driven entirely by ApplyJawControl -> TryMouthSnap.
            if (self is Lizard lizard)
            {
                ApplyJawControl(lizard, pickup, state);
            }
            else if (pickupPressed && !TryGrab(cc, state) && MountDebug.Verbose)
            {
                LogGrabFailure(self, state);
            }
        }

        private static void ApplyJawControl(Lizard lizard, bool rawHeld, State state)
        {
            if (lizard.grasps != null && lizard.grasps.Length > 0 && lizard.grasps[0] != null)
            {
                state.lastJawHeld = false;
                return;
            }

            var held = lizard.stun == 0 ? rawHeld : state.lastJawHeld;

            lizard.JawOpen = held ? 1f : 0f;

            if (!held && state.lastJawHeld) TryMouthSnap(lizard, state);
            state.lastJawHeld = held;
        }

        private static void TryMouthSnap(Lizard lizard, State state)
        {
            Creature? best = null;
            BodyChunk? bestHold = null;
            var bestDist = float.MaxValue;

            foreach (var list in lizard.room.physicalObjects)
            {
                foreach (var obj in list)
                {
                    if (obj is not Creature other) continue;
                    if (MountRules.Resolve(lizard, other) != MountKind.Mouth) continue;
                    if (!CreatureController.creatureControllers.TryGetValue(other, out _)) continue;

                    if (MountTopology.IsAncestor(other, lizard) || MountTopology.IsAncestor(lizard, other))
                    {
                        continue;
                    }

                    if (state.regrabGraceUntilFrame.TryGetValue(other.abstractCreature.ID.number, out var until)
                        && Time.frameCount < until)
                    {
                        continue;
                    }

                    if (!MountRules.TryReach(lizard, MountKind.Mouth, other, out var hold, out var reachDist)) continue;
                    if (hold == null || reachDist >= bestDist) continue;

                    best = other;
                    bestHold = hold;
                    bestDist = reachDist;
                }
            }

            if (best == null || bestHold == null)
            {
                PlayMouthSnap(lizard, caught: false);
                return;
            }

            if (lizard.Grab(best, 0, bestHold.index, Creature.Grasp.Shareability.CanOnlyShareWithNonExclusive,
                    dominance: 1f, overrideEquallyDominant: false, pacifying: false))
            {
                MountRules.MarkMountPair(lizard, best);
                PlayMouthSnap(lizard, caught: true);
            }
            else
            {
                PlayMouthSnap(lizard, caught: false);
            }
        }

        private static void PlayMouthSnap(Lizard lizard, bool caught)
        {
            lizard.room.PlaySound(SoundID.Lizard_Jaws_Shut_Miss_Creature, lizard.mainBodyChunk);
            if (caught) lizard.room.PlaySound(SoundID.UI_Multiplayer_Player_Revive, lizard.mainBodyChunk);

            var oc = lizard.abstractCreature.GetOnlineCreature();
            oc?.BroadcastRPCInRoom(MountRPCs.MouthSnap, oc, (byte)(caught ? 1 : 0));
        }

        private static void ApplyPassengerState(CreatureController cc, State state, MountLink? passenger)
        {
            if (passenger is { kind: not MountKind.BackRide } link && link.StillValid)
            {
                cc.preventInput = true;
                if (!state.collisionSuppressed)
                {
                    MountPhysics.SetAttachedCollision(cc.creature, true);
                    state.collisionSuppressed = true;
                }
                if (cc.onlineCreature.isMine) MountPhysics.SuppressSelfDrive(cc);
            }
            else if (state.collisionSuppressed)
            {
                cc.preventInput = false;
                MountPhysics.SetAttachedCollision(cc.creature, false);
                state.collisionSuppressed = false;
            }
        }

        private static readonly List<MountLink> physicsScratch = new();

        public static void ApplyPhysics(Room room)
        {
            foreach (var list in room.physicalObjects)
            {
                foreach (var obj in list)
                {
                    if (obj is not Creature c) continue;
                    if (!CreatureController.creatureControllers.TryGetValue(c, out var cc)) continue;
                    if (!cc.onlineCreature.isMine) continue;

                    if (MountLink.ParentOf(c) is { kind: not MountKind.BackRide } seat && seat.StillValid)
                    {
                        MountPhysics.Apply(seat);
                    }

                    MountTopology.GraspsOwnedBy(c, physicsScratch);
                    foreach (var owned in physicsScratch)
                    {
                        if (owned.kind == MountKind.Carry && owned.StillValid) MountPhysics.ApplyLoad(owned);
                    }
                }
            }
        }

        // The target may be a remote player's creature, so we can only see its input via the
        // vanilla, network-synced Player.InputPackage fields (jmp/spec) - our own custom grab and
        // release keys are purely local Unity.Input reads with no equivalent for creatures we
        // don't own.
        private static bool WatchPress(State state, Creature target, bool raw)
        {
            var key = target.abstractCreature.ID.number;

            if (!state.pressWatches.TryGetValue(key, out var watch))
            {
                watch = new PressWatch();
                state.pressWatches[key] = watch;
            }

            if (raw && !watch.lastRaw) watch.latch = PressLatchFrames;
            watch.lastRaw = raw;

            if (watch.latch <= 0) return false;
            watch.latch--;
            return true;
        }

        private static void RequestHopOffBack(MountLink link)
        {
            if (link.anchor is not Player anchorPlayer || link.passenger is not Player passengerPlayer) return;

            var anchorObj = anchorPlayer.abstractCreature.GetOnlineCreature();
            var passengerObj = passengerPlayer.abstractCreature.GetOnlineCreature();
            if (anchorObj == null || passengerObj == null) return;

            anchorObj.Lock("slugonback", anchorObj.owner.InvokeRPC(anchorObj.HopOffBack, passengerObj));
            anchorPlayer.slugOnBack?.DropSlug();
        }

        private static void RequestHopOnBack(Player rider, Player mountPlayer, Player.SlugOnBack slugOnBack)
        {
            var riderObj = rider.abstractCreature.GetOnlineCreature();
            var mountObj = mountPlayer.abstractCreature.GetOnlineCreature();
            if (riderObj == null || mountObj == null) return;

            mountObj.Lock("slugonback", mountObj.owner.InvokeRPC(mountObj.HopOnBack, riderObj));
            slugOnBack.SlugToBack(rider);
        }

        private static bool releasing;

        private static void Release(CreatureController cc, State state, MountLink link)
        {
            var released = link.passenger;

            if (link.kind == MountKind.BackRide)
            {
                RequestHopOffBack(link);
            }
            else
            {
                if (link.grasp.grabbed is Creature grabbedCreature)
                {
                    MountRules.UnmarkMountPair(link.grasp.grabber, grabbedCreature);
                }

                releasing = true;
                try { cc.creature.ReleaseGrasp(link.grasp.graspUsed); }
                finally { releasing = false; }
            }

            if (released != null)
            {
                state.regrabGraceUntilFrame[released.abstractCreature.ID.number] = Time.frameCount + RegrabGrace;
            }
        }

        internal static bool AllowRelease(Creature self, int graspIndex)
        {
            if (releasing) return true;
            if (OnlineManager.lobby?.gameMode is not MeadowGameMode) return true;

            var grasp = self.grasps != null && graspIndex >= 0 && graspIndex < self.grasps.Length
                ? self.grasps[graspIndex]
                : null;
            if (grasp?.grabbed is not Creature held) return true;
            if (MountRules.Resolve(self, held) == null) return true;

            return !CreatureController.creatureControllers.TryGetValue(self, out var cc)
                || !cc.onlineCreature.isMine;
        }

        private static bool TryGrab(CreatureController cc, State state)
        {
            var self = cc.creature;

            Creature? best = null;
            BodyChunk? bestHold = null;
            MountKind bestKind = default;
            var bestDist = float.MaxValue;

            foreach (var list in self.room.physicalObjects)
            {
                foreach (var obj in list)
                {
                    if (obj is not Creature other) continue;

                    var kind = MountRules.Resolve(self, other);
                    if (kind == null) continue;

                    if (!CreatureController.creatureControllers.TryGetValue(other, out _)) continue;

                    if (MountTopology.IsAncestor(other, self) || MountTopology.IsAncestor(self, other))
                    {
                        continue;
                    }

                    if (state.regrabGraceUntilFrame.TryGetValue(other.abstractCreature.ID.number, out var until)
                        && Time.frameCount < until)
                    {
                        continue;
                    }

                    if (kind is MountKind.Ride or MountKind.BackRide
                        && RidersOf(other, state.scratchB) >= MountRules.SeatCapacity(other))
                    {
                        continue;
                    }

                    if (!MountRules.TryReach(self, kind.Value, other, out var hold, out var reachDist)) continue;
                    if (hold == null || reachDist >= bestDist) continue;

                    best = other;
                    bestHold = hold;
                    bestKind = kind.Value;
                    bestDist = reachDist;
                }
            }

            if (best == null || bestHold == null) return false;

            if (bestKind == MountKind.BackRide)
            {
                if (self is Player rider && best is Player mountPlayer
                    && mountPlayer.slugOnBack is { HasASlug: false } slugOnBack)
                {
                    RequestHopOnBack(rider, mountPlayer, slugOnBack);
                    return true;
                }
                return false;
            }

            var slot = bestKind == MountKind.Mouth
                ? (self.grasps != null && self.grasps.Length > 0 && self.grasps[0] == null ? 0 : -1)
                : FreeGraspSlot(self);
            if (slot < 0) return false;

            var shareability = bestKind == MountKind.Ride
                ? Creature.Grasp.Shareability.NonExclusive
                : Creature.Grasp.Shareability.CanOnlyShareWithNonExclusive;

            if (self.Grab(best, slot, bestHold.index, shareability,
                    dominance: 1f, overrideEquallyDominant: false, pacifying: false))
            {
                MountRules.MarkMountPair(self, best);
                self.room.PlaySound(SoundID.UI_Multiplayer_Player_Revive, self.mainBodyChunk);

                var oc = self.abstractCreature.GetOnlineCreature();
                oc?.BroadcastRPCInRoom(MountRPCs.GrabConfirm, oc);
                return true;
            }

            if (MountDebug.Verbose)
            {
                MountDebug.Log($"{self.GetType().Name} grab failed: Creature.Grab rejected {best.GetType().Name} " +
                               $"(kind={bestKind}, slot={slot}) - dominance conflict?");
            }

            return false;
        }

        private static void LogGrabFailure(Creature self, State state)
        {
            Creature? nearest = null;
            var reason = "no candidate in room";
            var nearestDist = float.MaxValue;

            foreach (var list in self.room.physicalObjects)
            {
                foreach (var obj in list)
                {
                    if (obj is not Creature other || other == self) continue;

                    var kind = MountRules.Resolve(self, other);
                    if (kind == null) continue;

                    var dist = Vector2.Distance(self.mainBodyChunk.pos, other.mainBodyChunk.pos);
                    if (dist >= nearestDist) continue;

                    string candidateReason;
                    if (!CreatureController.creatureControllers.TryGetValue(other, out _))
                    {
                        candidateReason = "candidate has no CreatureController";
                    }
                    else if (MountTopology.IsAncestor(other, self) || MountTopology.IsAncestor(self, other))
                    {
                        candidateReason = "ancestor or descendant (would form a cycle)";
                    }
                    else if (state.regrabGraceUntilFrame.TryGetValue(other.abstractCreature.ID.number, out var until)
                             && Time.frameCount < until)
                    {
                        candidateReason = $"regrab grace ({until - Time.frameCount} frames left)";
                    }
                    else if (kind is MountKind.Ride or MountKind.BackRide
                             && RidersOf(other, state.scratchB) is var riders
                             && riders >= MountRules.SeatCapacity(other))
                    {
                        candidateReason = $"seats full ({riders}/{MountRules.SeatCapacity(other)})";
                    }
                    else if (!MountRules.TryReach(self, kind.Value, other, out _, out var reachDist))
                    {
                        candidateReason = $"out of reach (dist={reachDist:0.0})";
                    }
                    else
                    {
                        candidateReason = "reach/LOS ok - Creature.Grab itself must have rejected it";
                    }

                    nearest = other;
                    reason = candidateReason;
                    nearestDist = dist;
                }
            }

            MountDebug.Log(nearest == null
                ? $"{self.GetType().Name} grab failed: no Resolve-eligible candidate in room"
                : $"{self.GetType().Name} grab failed: nearest candidate {nearest.GetType().Name} - {reason}");
        }

        private static void DetectSilentDrop(Creature self, State state)
        {
            foreach (var link in state.owned)
            {
                if (link.kind == MountKind.BackRide || link.StillValid) continue;

                var id = link.passenger.abstractCreature.ID.number;
                var recentlyReleasedByUs = state.regrabGraceUntilFrame.TryGetValue(id, out var until)
                    && until >= Time.frameCount + RegrabGrace - 2;

                if (!recentlyReleasedByUs)
                {
                    var other = link.kind == MountKind.Ride ? link.anchor : link.passenger;
                    MountDebug.Log($"{self.GetType().Name} lost its {link.kind} grasp on " +
                                   $"{other.GetType().Name} without going through MountControl.Release");
                }
            }
        }

        private static int FreeGraspSlot(Creature c)
        {
            if (c.grasps == null) return -1;
            for (int i = 0; i < c.grasps.Length; i++)
            {
                if (c.grasps[i] == null) return i;
            }
            return -1;
        }
    }
}
