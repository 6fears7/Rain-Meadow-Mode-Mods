using System.Collections.Generic;
using RainMeadow;
using UnityEngine;

namespace MeadowMounts
{
    public readonly struct MountLink
    {
        public readonly Creature passenger;
        public readonly Creature anchor;
        public readonly Creature.Grasp grasp;
        public readonly MountKind kind;

        public MountLink(Creature passenger, Creature anchor, Creature.Grasp grasp, MountKind kind)
        {
            this.passenger = passenger;
            this.anchor = anchor;
            this.grasp = grasp;
            this.kind = kind;
        }

        public static MountLink? ParentOf(Creature self)
        {
            if (self is Player selfPlayer && selfPlayer.onBack is Player backCarrier)
            {
                return new MountLink(selfPlayer, backCarrier, null!, MountKind.BackRide);

            }

            if (self.grasps != null)
            {
                foreach (var g in self.grasps)
                {
                    if (g?.grabbed is Creature mount && MountRules.Resolve(self, mount) == MountKind.Ride)
                    {
                        return new MountLink(self, mount, g, MountKind.Ride);
                    }
                }
            }

            if (self.grabbedBy != null)
            {
                foreach (var g in self.grabbedBy)
                {
                    if (g?.grabber is not Creature holder) continue;
                    var kind = MountRules.Resolve(holder, self);
                    if (kind is MountKind.Carry or MountKind.Mouth)
                    {
                        return new MountLink(self, holder, g, kind.Value);
                    }
                }
            }

            return null;
        }

        public BodyChunk SeatedChunk =>
            kind != MountKind.Ride && grasp?.grabbedChunk is BodyChunk held
                ? held
                : passenger.mainBodyChunk;

        public bool StillValid =>
            passenger?.room != null
            && anchor?.room == passenger.room
            && !passenger.dead && !anchor.dead
            && (kind == MountKind.BackRide
                ? passenger is Player p && anchor is Player a && p.onBack == a && a.slugOnBack?.slugcat == p
                : grasp?.grabbed != null);
    }

    public static class MountTopology
    {
        private const int MaxDepth = 8;

        public static void ChildrenOf(Creature self, List<MountLink> into)
        {
            into.Clear();

            if (self.grasps != null)
            {
                foreach (var g in self.grasps)
                {
                    if (g?.grabbed is not Creature held) continue;
                    var kind = MountRules.Resolve(self, held);
                    if (kind is MountKind.Carry or MountKind.Mouth)
                    {
                        into.Add(new MountLink(held, self, g, kind.Value));
                    }
                }
            }

            if (self.grabbedBy != null)
            {
                foreach (var g in self.grabbedBy)
                {
                    if (g?.grabber is Creature rider && MountRules.Resolve(rider, self) == MountKind.Ride)
                    {
                        into.Add(new MountLink(rider, self, g, MountKind.Ride));
                    }
                }
            }

            if (self is Player selfPlayer && selfPlayer.slugOnBack?.slugcat is Player backRider)
            {
                into.Add(new MountLink(backRider, selfPlayer, null!, MountKind.BackRide));
            }
        }

        public static void GraspsOwnedBy(Creature self, List<MountLink> into)
        {
            into.Clear();

            if (self.grasps != null)
            {
                foreach (var g in self.grasps)
                {
                    if (g?.grabbed is not Creature other) continue;
                    var kind = MountRules.Resolve(self, other);
                    if (kind == null) continue;

                    into.Add(kind == MountKind.Ride
                        ? new MountLink(self, other, g, MountKind.Ride)
                        : new MountLink(other, self, g, kind.Value));
                }
            }

            if (self is Player selfPlayer)
            {
                if (selfPlayer.onBack is Player carrier)
                {
                    into.Add(new MountLink(selfPlayer, carrier, null!, MountKind.BackRide));
                }
                if (selfPlayer.slugOnBack?.slugcat is Player rider)
                {
                    into.Add(new MountLink(rider, selfPlayer, null!, MountKind.BackRide));
                }
            }
        }

        public static bool IsAncestor(Creature maybeAncestor, Creature of)
        {
            var current = of;
            for (int i = 0; i < MaxDepth && current != null; i++)
            {
                if (current == maybeAncestor) return true;
                current = MountLink.ParentOf(current)?.anchor;
            }
            return false;
        }

        private static readonly List<MountLink>[] scratchByDepth = BuildScratch();

        private static List<MountLink>[] BuildScratch()
        {
            var arr = new List<MountLink>[MaxDepth + 1];
            for (int i = 0; i < arr.Length; i++) arr[i] = new List<MountLink>();
            return arr;
        }

        public static float SubtreeCarryMass(Creature self) => SubtreeCarryMass(self, 0);

        private static float SubtreeCarryMass(Creature self, int depth)
        {
            var total = MountRules.CarryMass(self);
            if (depth >= MaxDepth) return total;

            var children = scratchByDepth[depth];
            ChildrenOf(self, children);
            foreach (var child in children)
            {
                total += SubtreeCarryMass(child.passenger, depth + 1);
            }
            return total;
        }

        public static (int index, int total) SeatIndexOf(MountLink rideLink)
        {
            var riders = new List<MountLink>();
            ChildrenOf(rideLink.anchor, riders);
            riders.RemoveAll(l => l.kind != MountKind.Ride);
            riders.Sort((a, b) => a.passenger.abstractCreature.ID.number
                .CompareTo(b.passenger.abstractCreature.ID.number));

            var index = riders.FindIndex(l => l.passenger == rideLink.passenger);
            return (Mathf.Max(index, 0), Mathf.Max(riders.Count, 1));
        }
    }

    public static class MountPhysics
    {
        private const float Stiffness = 0.45f;
        private const float VelocityBlend = 0.6f;
        private const float MaxLoad = 3.8f;
        private const float ReferenceLoad = 0.2f;
        private const float SagPerLoad = 0.07f;
        private const float LagPullPerLoad = 0.0035f;
        private const float MaxReaction = 0.12f;
        private const float MaxTether = 32f;
        private const float SlackPerLoad = 0.4f;

        public static float Load(MountLink link)
        {
            var raw = MountTopology.SubtreeCarryMass(link.passenger) / Mathf.Max(link.anchor.TotalMass, 0.01f);
            return Mathf.Min(Mathf.Sqrt(ReferenceLoad * raw) * 0.01f, MaxLoad);
        }

        public static void Apply(MountLink link)
        {
            MountRules.AttachPoint(link, out var seatPos, out var seatVel);

            var seated = link.SeatedChunk;

            var slack = link.kind == MountKind.Carry ? 1f + Load(link) * SlackPerLoad : 1f;
            var pull = (seatPos - seated.pos) * (Stiffness / slack);

            var want = seatVel + pull;
            var blend = VelocityBlend / slack;

            if (link.kind != MountKind.Ride)
            {
                seated.vel = Vector2.Lerp(seated.vel, want, blend);
                Tether(link);
                return;
            }

            foreach (var chunk in link.passenger.bodyChunks)
            {
                chunk.vel = Vector2.Lerp(chunk.vel, want, blend);
            }
        }

        private static void Tether(MountLink link)
        {
            var away = link.SeatedChunk.pos - link.anchor.mainBodyChunk.pos;

            var slip = away.magnitude - MaxTether;
            if (slip <= 0f) return;

            var correction = -away.normalized * slip;
            ShiftSubtree(link.passenger, correction, 0);
        }

        private const int MaxSubtreeShiftDepth = 8;

        private static void ShiftSubtree(Creature root, Vector2 correction, int depth)
        {
            foreach (var chunk in root.bodyChunks)
            {
                chunk.pos += correction;
                chunk.lastPos += correction;
            }

            if (depth >= MaxSubtreeShiftDepth) return;

            var children = new List<MountLink>();
            MountTopology.ChildrenOf(root, children);
            foreach (var child in children)
            {
                ShiftSubtree(child.passenger, correction, depth + 1);
            }
        }

        public static void ApplyLoad(MountLink link)
        {
            if (link.kind != MountKind.Carry) return;

            var carrier = link.anchor;
            var load = Load(link);
            if (load <= 0f) return;

            MountRules.AttachPoint(link, out var seatPos, out _);

            var sag = new Vector2(0f, -SagPerLoad * load);
            var lag = (link.SeatedChunk.pos - seatPos) * (LagPullPerLoad * load);

            var reaction = Vector2.ClampMagnitude(sag + lag, MaxReaction) / carrier.bodyChunks.Length;
            foreach (var chunk in carrier.bodyChunks)
            {
                chunk.vel += reaction;
            }
        }

        public static void SetAttachedCollision(Creature passenger, bool attached)
        {
            passenger.CollideWithObjects = !attached;
        }

        public static void SuppressSelfDrive(CreatureController cc)
        {
            cc.lockInPlace = true;

            var creature = cc.creature;
            if (creature.Template?.AI == true)
            {
                cc.ForceAIDestination(cc.CurrentPathfindingPosition);
            }

            if (creature is Lizard lizard)
            {
                lizard.gripPoint = null;
            }
        }
    }
}
