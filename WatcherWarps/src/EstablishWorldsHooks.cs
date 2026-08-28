using System;
using System.Linq;
using HarmonyLib;
using RainMeadow;

namespace WatcherWarps
{
    // Phase 5.7 (plan/phase5-07-establishworlds-warpregions-null-fix.md).
    //
    // History: phase5-03 concluded, from ikdasm against SlugcatStats.SlugcatOptionalRegions,
    // that Watcher's optional regions (WHIR WSUR WDSR WGWR WSSR HI SU CC SH) plus WRSA are
    // never given a WorldSession by OnlineGameMode.EstablishWorlds in a Meadow sandbox, and
    // phase5-04 built this hook to force-establish them off Region.WarpRegions. phase5-06
    // then found Region.WarpRegions is null at EstablishWorlds time, so the hook established
    // nothing.
    //
    // phase5-07 settled the blocking open question by disassembly (ikdasm, 2026-08-27):
    //
    //   * OnlineGameMode.EstablishWorlds iterates overworldSession.overWorld.regions and
    //     nothing else. It never consults SlugcatStats access lists.
    //   * OverWorld..ctor sets regions = Region.LoadAllRegions(timeline, game). That method
    //     reads World/regions.txt (AssetManager.ResolveFilePath - mod-merged, not timeline-
    //     filtered) and constructs one Region per line, regionNumber = line index. There is
    //     no story/optional/slugcat filter anywhere in it or in Region..ctor (the only
    //     Watcher branch in Region..ctor just tints corruption colors).
    //   * The active merged World/regions.txt with the Watcher mod enabled lists every one
    //     of WHIR WSUR WDSR WGWR WSSR HI SU CC SH LF WRSA.
    //
    // => overWorld.regions already contains all of them, so the base EstablishWorlds loop
    // already establishes a WorldSession for each. phase5-04's premise ("optional regions
    // absent from overWorld.regions in a sandbox") was a misdiagnosis - the optional/story
    // split matters for region-graph access, not for EstablishWorlds.
    //
    // This postfix is therefore a defensive safety net, not load-bearing: it re-scans
    // overWorld.regions for the curated set and establishes any that the base loop somehow
    // missed (skipping every one already present via the ContainsKey guard - base
    // EstablishWorld throws on a duplicate key). In the expected case it establishes nothing
    // and just logs the region list once, which doubles as the phase5-07 step 1
    // instrumentation for the in-game verification.
    public static class EstablishWorldsHooks
    {
        // Regions every curated warp route needs a synced WorldSession for. All of these are
        // lines in the Watcher-merged World/regions.txt, so overWorld.regions (hence the base
        // EstablishWorlds loop) already covers them - see the class comment.
        private static readonly string[] ForceEstablishRegions =
        {
            "WHIR", "WSUR", "WDSR", "WGWR", "WSSR", "HI", "SU", "CC", "SH", "LF", "WRSA",
        };

        public static void Apply()
        {
            var target = AccessTools.Method(typeof(OnlineGameMode), nameof(OnlineGameMode.EstablishWorlds));
            if (target == null) throw new MissingMethodException("OnlineGameMode.EstablishWorlds not found - Rain Meadow version mismatch?");

            new Harmony(WatcherWarpsPlugin.ModId).Patch(
                target,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(EstablishWorldsHooks), nameof(EstablishWorldsPostfix))));
        }

        private static void EstablishWorldsPostfix(OverworldSession overworldSession)
        {
            if (!Warps.IsMeadowWatcher()) return;

            var regions = overworldSession.overWorld?.regions;
            if (regions == null)
            {
                Warps.Log?.LogWarning("Watcher Warps: overworldSession.overWorld.regions is null in EstablishWorlds postfix; cannot verify curated regions");
                return;
            }

            // phase5-07 step 1 instrumentation: dump what EstablishWorlds actually iterated,
            // so an in-game run confirms the curated regions (and WRSA) are present.
            Warps.Log?.LogInfo($"Watcher Warps: overWorld.regions = [{string.Join(", ", regions.Where(r => r != null).Select(r => $"{r.name}#{r.regionNumber}"))}]");

            foreach (string acronym in ForceEstablishRegions)
            {
                Region? region = regions
                    .FirstOrDefault(r => r != null && string.Equals(r.name, acronym, StringComparison.OrdinalIgnoreCase));

                if (region == null)
                {
                    Warps.Log?.LogWarning($"Watcher Warps: region {acronym} not in overWorld.regions; its WorldSession will not be established");
                    continue;
                }

                if (overworldSession.worldSessions.ContainsKey(region.name))
                {
                    continue;
                }

                overworldSession.EstablishWorld(region.name, checked((ushort)region.regionNumber));
                Warps.Log?.LogInfo($"Watcher Warps: established WorldSession for region {region.name} (#{region.regionNumber}) - base EstablishWorlds loop had skipped it");
            }
        }
    }
}
