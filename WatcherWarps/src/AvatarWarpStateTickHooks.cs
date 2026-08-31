using RainMeadow;
using Watcher;

namespace WatcherWarps
{
    public static class AvatarWarpStateTickHooks
    {
        public static void Apply()
        {
            On.Creature.Update += Creature_Update;
        }

        private static void Creature_Update(On.Creature.orig_Update orig, Creature self, bool eu)
        {
            orig(self, eu);

            if (!CreatureWarpState.TryGetLocalNonPlayerAvatar(out Creature avatar) || self != avatar)
                return;

            CreatureWarpState.Get(avatar).Tick();
        }
    }
}
