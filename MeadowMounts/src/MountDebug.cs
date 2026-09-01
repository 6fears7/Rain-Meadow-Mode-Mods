using System.Collections.Generic;
using System.Linq;
using System.Text;
using RainMeadow;
using UnityEngine;

namespace MeadowMounts
{
    public static class MountDebug
    {
        public const bool Verbose = false;

        private const KeyCode Key = KeyCode.M;

        private static int lastDumpFrame = -1;

        private static readonly Dictionary<string, int> throttleUntil = new();

        public static void Log(string message)
        {
            if (!Verbose) return;
            Mounts.Log?.LogInfo("[MeadowMounts] " + message);
        }

        public static bool Throttle(string key, int frames = 30)
        {
            if (throttleUntil.TryGetValue(key, out var until) && Time.frameCount < until) return false;
            throttleUntil[key] = Time.frameCount + frames;
            return true;
        }

        public static string Name(PhysicalObject? obj)
        {
            if (obj == null) return "null";

            var apo = obj.abstractPhysicalObject;
            var owner = "?";
            try
            {
                if (OnlineManager.lobby != null && apo?.GetOnlineObject() is { } online)
                {
                    owner = online.isMine ? "mine" : "remote";
                }
            }
            catch
            {
                owner = "err";
            }

            return $"{obj.GetType().Name}#{apo?.ID.number ?? -1}({owner})";
        }

        public static string Slots(Creature c)
        {
            if (c.grasps == null) return "(no grasp array)";
            return string.Join(", ", c.grasps.Select((g, i) =>
                g == null ? $"{i}:-" : $"{i}:{Name(g.grabbed)}{(g.discontinued ? "!DISCONTINUED" : "")}"));
        }


        public static string Caller(int frames = 8)
        {
            var trace = new System.Diagnostics.StackTrace(1, false);
            var sb = new StringBuilder();
            for (int i = 0; i < frames && i < trace.FrameCount; i++)
            {
                var m = trace.GetFrame(i)?.GetMethod();
                if (m == null) continue;
                sb.Append("\n      <- ").Append(m.DeclaringType?.Name ?? "?").Append('.').Append(m.Name);
            }
            return sb.ToString();
        }

        public static void MaybeDump(Room? room)
        {
            if (room == null) return;
            if (!Input.GetKeyDown(Key)) return;
            if (Time.frameCount == lastDumpFrame) return;
            lastDumpFrame = Time.frameCount;

            Mounts.Log?.LogInfo("=== MeadowMounts body/mass dump ===");
            foreach (var list in room.physicalObjects)
            {
                foreach (var obj in list)
                {
                    if (obj is not Creature c) continue;
                    if (!CreatureController.creatureControllers.TryGetValue(c, out _)) continue;

                    var chunkRads = string.Join(", ", c.bodyChunks.Select(bc => bc.rad.ToString("0.00")));
                    var connDists = c.bodyChunkConnections != null
                        ? string.Join(", ", c.bodyChunkConnections.Select(cc => cc.distance.ToString("0.00")))
                        : "(none)";
                    var length = MountRules.BodyLength(c);

                    Mounts.Log?.LogInfo(
                        $"{c.GetType().Name,-14} len={length,6:0.00}  mass={c.TotalMass,5:0.00}  " +
                        $"chunks=[{chunkRads}]  conns=[{connDists}]  grasps={c.grasps?.Length ?? 0}  " +
                        $"slots=[{Slots(c)}]");
                }
            }
        }
    }
}
