using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MeadowMounts
{
    public enum MountKind
    {
        Ride,
        Carry,
        Mouth,
        BackRide,
    }

    public static class MountRules
    {
        public const float MaxRideMassRatio = 100f;
        public const float ReachRange = 45f;

        public static bool IsCarrier(Creature c) => c.Template?.canFly == true && c is not SmallNeedleWorm;

        private static bool IsMountable(Creature c) => c is Lizard || c is Scavenger || c is EggBug || c is Player;

        private const float BlueLizardMass = 1.4f;

        public static float CarryMass(Creature c) => c is Lizard ? BlueLizardMass : c.TotalMass;

        public static bool CanRide(Creature rider, Creature mount)
        {
            if (IsCarrier(rider)) return false;
            if (!IsMountable(mount)) return false;

            if (rider is EggBug) return mount is Lizard;

            if (rider is Scavenger) return false;

            return rider is Player || rider is LanternMouse;
        }

        public static bool CanMouthHold(Creature holder, Creature target) => holder is Lizard;

        public static MountKind? Resolve(Creature grabber, Creature grabbed)
        {
            if (grabber == null || grabbed == null || grabber == grabbed) return null;
            if (grabber.dead || grabbed.dead) return null;

            if (IsPacifyingGrasp(grabber, grabbed)) return null;

            if (IsCarrier(grabber))
            {
                return MountKind.Carry;
            }

            if (CanRide(grabber, grabbed) && grabber.TotalMass <= grabbed.TotalMass * MaxRideMassRatio)
            {
                return grabber is Player && grabbed is Player mountPlayer && mountPlayer.slugOnBack != null
                    ? MountKind.BackRide
                    : MountKind.Ride;
            }

            if (CanMouthHold(grabber, grabbed))
            {
                return MountKind.Mouth;
            }

            return null;
        }

        private static readonly ConditionalWeakTable<Creature, HashSet<Creature>> mountPairs = new();

        public static void MarkMountPair(Creature grabber, Creature grabbed) =>
            mountPairs.GetOrCreateValue(grabber).Add(grabbed);

        public static void UnmarkMountPair(Creature grabber, Creature grabbed)
        {
            if (mountPairs.TryGetValue(grabber, out var pairs)) pairs.Remove(grabbed);
        }

        private static bool IsPacifyingGrasp(Creature grabber, Creature grabbed)
        {
            if (mountPairs.TryGetValue(grabber, out var pairs) && pairs.Contains(grabbed)) return false;

            if (grabber.grasps == null) return false;
            foreach (var g in grabber.grasps)
            {
                if (g?.grabbed == grabbed) return g.pacifying;
            }
            return false;
        }

        public static int SeatCapacity(Creature mount) =>
            mount is Lizard lizard && (lizard.Template.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard || lizard.Template.type == CreatureTemplate.Type.RedLizard) ? 3 : 1;

        public static BodyChunk HoldPoint(MountKind kind, Creature grabbed, Vector2 from)
        {
            if (kind is MountKind.Ride or MountKind.BackRide)
            {
                return grabbed.bodyChunks[Mathf.Min(1, grabbed.bodyChunks.Length - 1)];
            }

            var best = grabbed.bodyChunks[0];
            var bestDist = float.MaxValue;
            foreach (var chunk in grabbed.bodyChunks)
            {
                var dist = Vector2.Distance(chunk.pos, from);
                if (dist >= bestDist) continue;
                best = chunk;
                bestDist = dist;
            }
            return best;
        }

        private const float SeatStart = 0.35f;
        private const float SeatEnd = 0.95f;

        public static void AttachPoint(MountLink link, out Vector2 pos, out Vector2 vel)
        {
            var anchor = link.anchor;

            if (link.kind == MountKind.Ride)
            {
                var (index, total) = MountTopology.SeatIndexOf(link);
                var t = total <= 1 ? (SeatStart + SeatEnd) * 0.5f
                    : Mathf.Lerp(SeatStart, SeatEnd, index / (float)(total - 1));

                SpineSample(anchor, t, out var spinePos, out var spineVel, out var spineRad);
                var offset = SurfaceUp(anchor) * (spineRad + link.passenger.bodyChunks[0].rad * 0.8f);
                pos = spinePos + offset;
                vel = spineVel;
                return;
            }

            if (link.kind == MountKind.Mouth)
            {
                var head = anchor.bodyChunks[0];
                var facing = FacingDirection(anchor);
                pos = head.pos + facing * (head.rad + link.SeatedChunk.rad);
                vel = head.vel;
                return;
            }

            var seat = anchor.mainBodyChunk;
            pos = seat.pos + Vector2.down * (seat.rad + link.SeatedChunk.rad);
            vel = seat.vel;
        }

        private static void SpineSample(Creature anchor, float t, out Vector2 pos, out Vector2 vel, out float rad)
        {
            var chunks = anchor.bodyChunks;
            var n = chunks.Length;
            if (n == 1)
            {
                pos = chunks[0].pos;
                vel = chunks[0].vel;
                rad = chunks[0].rad;
                return;
            }

            var total = 0f;
            for (int i = 0; i < n - 1; i++) total += ChunkGap(anchor, i, i + 1);

            if (total <= 0f)
            {
                pos = chunks[0].pos;
                vel = chunks[0].vel;
                rad = chunks[0].rad;
                return;
            }

            var target = Mathf.Clamp01(t) * total;
            var accum = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                var segLen = ChunkGap(anchor, i, i + 1);
                if (target <= accum + segLen || i == n - 2)
                {
                    var localT = segLen > 0f ? Mathf.Clamp01((target - accum) / segLen) : 0f;
                    var a = chunks[i];
                    var b = chunks[i + 1];
                    pos = Vector2.Lerp(a.pos, b.pos, localT);
                    vel = Vector2.Lerp(a.vel, b.vel, localT);
                    rad = Mathf.Lerp(a.rad, b.rad, localT);
                    return;
                }
                accum += segLen;
            }

            var last = chunks[n - 1];
            pos = last.pos;
            vel = last.vel;
            rad = last.rad;
        }

        private static float ChunkGap(Creature c, int indexA, int indexB)
        {
            var a = c.bodyChunks[indexA];
            var b = c.bodyChunks[indexB];
            if (c.bodyChunkConnections != null)
            {
                foreach (var conn in c.bodyChunkConnections)
                {
                    if ((conn.chunk1 == a && conn.chunk2 == b) || (conn.chunk1 == b && conn.chunk2 == a))
                    {
                        return conn.distance;
                    }
                }
            }
            return Vector2.Distance(a.pos, b.pos);
        }

        private static Vector2 FacingDirection(Creature c)
        {
            if (c.bodyChunks.Length < 2) return Vector2.right;
            var dir = c.bodyChunks[0].pos - c.bodyChunks[1].pos;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        }

        public static float BodyLength(Creature c)
        {
            var chunks = c.bodyChunks;
            if (chunks == null || chunks.Length == 0) return 0f;
            if (chunks.Length == 1) return chunks[0].rad * 2f;

            var length = chunks[0].rad + chunks[chunks.Length - 1].rad;
            for (int i = 0; i < chunks.Length - 1; i++)
            {
                length += ChunkGap(c, i, i + 1);
            }
            return length;
        }

        private static Vector2 SurfaceUp(Creature c)
        {
            foreach (var chunk in c.bodyChunks)
            {
                var contact = chunk.contactPoint;
                if (contact.y < 0) return Vector2.up;
                if (contact.y > 0) return Vector2.down;
                if (contact.x != 0) return new Vector2(-contact.x, 0f);
            }
            return Vector2.up;
        }
    }
}
