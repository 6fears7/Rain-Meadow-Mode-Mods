using BepInEx;
using MonoMod.Cil;
using RainMeadow;
using System;
using System.Linq;
using System.Security.Permissions;
using MonoMod.RuntimeDetour;

using System.IO;
using Menu;
using System.Collections.Generic;
using System.Globalization;
using HUD;
using System.Configuration;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Data.SqlTypes;

//#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace RotMeadow
{
    [BepInPlugin("uo.rotmeadow", "Rot Lizards for Meadow", "0.2.0")]
    public partial class RotMeadowMod : BaseUnityPlugin
    {
        public static RotMeadowMod instance;
        private bool init;
        private bool fullyInit;
        public void OnEnable()
        {
            instance = this;
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;

        }


        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            if (init) return;
            init = true;

            try
            {
            On.AbstractCreature.Realize  += AbstractCreature_Realize;
            fullyInit = true;
            Logger.LogInfo("Rot Lizards For Meadow intialized succesfully");
            }
            catch (Exception e)
            {
                Logger.LogError("Rot Meadow failed to initialize:");
                Logger.LogError(e);
                fullyInit = false;
            }
        }


        public static RainMeadow.MeadowProgression.Character Lizard_Rot = new("Lizard_Rot", true, new()
        {
            displayName = "LIZARD_ROT_ONE",
            emotePrefix = "liz_",
            emoteAtlas = "emotes_lizard",
            emoteColor = RainWorld.RippleColor,
            voiceId = Watcher.WatcherEnums.WatcherSoundID.Lizard_Voice_Rot_A,
            selectSpriteIndexes = new int[2] { 1, 2 },
            startingCoords = new WorldCoordinate("DS_A06", 12, 16, -1)
        });
public static RainMeadow.MeadowProgression.Skin Lizard_RotPink_1 = new("Lizard_RotPink_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Pink 1",
            creatureType = CreatureTemplate.Type.PinkLizard,
            randomSeed = 3321,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotBlue_1 = new("Lizard_RotBlue_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Blue 1",
            creatureType = CreatureTemplate.Type.BlueLizard,
            randomSeed = 3125,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotYellow_1 = new("Lizard_RotYellow_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Yellow 1",
            creatureType = CreatureTemplate.Type.YellowLizard,
            randomSeed = 718,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotWhite_1 = new("Lizard_RotWhite_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted White 1",
            creatureType = CreatureTemplate.Type.WhiteLizard,
            randomSeed = 3217,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotRed_1 = new("Lizard_RotRed_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Red 1",
            creatureType = CreatureTemplate.Type.RedLizard,
            randomSeed = 5036,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotBlack_1 = new("Lizard_RotBlack_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Black 1",
            creatureType = CreatureTemplate.Type.BlackLizard,
            randomSeed = 280,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotAxo_1 = new("Lizard_RotAxo_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Axo 1",
            creatureType = CreatureTemplate.Type.Salamander,
            randomSeed = 7621,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotSala_1 = new("Lizard_RotSala_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Sala 1",
            creatureType = CreatureTemplate.Type.Salamander,
            randomSeed = 3107,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotCyan_1 = new("Lizard_RotCyan_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Cyan 1",
            creatureType = CreatureTemplate.Type.CyanLizard,
            randomSeed = 5243,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotFluffers_1 = new("Lizard_RotFluffers_1", true, new()
        {
            character = Lizard_Rot,
            displayName = "Rotted Fluffers 1",
            creatureType = CreatureTemplate.Type.PinkLizard,
            randomSeed = 7713,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        // LEVEL TWO
        public static RainMeadow.MeadowProgression.Character Lizard_Rot2 = new("Lizard_Rot_2", true, new()
        {
            displayName = "LIZARD_ROT_TWO",
            emotePrefix = "liz_",
            emoteAtlas = "emotes_lizard",
            emoteColor = RainWorld.RippleColor,
            voiceId = Watcher.WatcherEnums.WatcherSoundID.Lizard_Voice_Rot_A,
            selectSpriteIndexes = new int[2] { 1, 2 },
            startingCoords = new WorldCoordinate("DS_A06", 12, 16, -1)
        });
        public static RainMeadow.MeadowProgression.Skin Lizard_RotPink_2 = new("Lizard_RotPink_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Pink 2",
            creatureType = CreatureTemplate.Type.PinkLizard,
            randomSeed = 3322,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotBlue_2 = new("Lizard_RotBlue_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Blue 2",
            creatureType = CreatureTemplate.Type.BlueLizard,
            randomSeed = 3126,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotYellow_2 = new("Lizard_RotYellow_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Yellow 2",
            creatureType = CreatureTemplate.Type.YellowLizard,
            randomSeed = 719,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotWhite_2 = new("Lizard_RotWhite_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted White 2",
            creatureType = CreatureTemplate.Type.WhiteLizard,
            randomSeed = 3218,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotRed_2 = new("Lizard_RotRed_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Red 2",
            creatureType = CreatureTemplate.Type.RedLizard,
            randomSeed = 5037,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotBlack_2 = new("Lizard_RotBlack_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Black 2",
            creatureType = CreatureTemplate.Type.BlackLizard,
            randomSeed = 281,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotAxo_2 = new("Lizard_RotAxo_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Axo 2",
            creatureType = CreatureTemplate.Type.Salamander,
            randomSeed = 7622,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotSala_2 = new("Lizard_RotSala_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Sala 2",
            creatureType = CreatureTemplate.Type.Salamander,
            randomSeed = 3108,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotCyan_2 = new("Lizard_RotCyan_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Cyan 2",
            creatureType = CreatureTemplate.Type.CyanLizard,
            randomSeed = 5244,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotFluffers_2 = new("Lizard_RotFluffers_2", true, new()
        {
            character = Lizard_Rot2,
            displayName = "Rotted Fluffers 2",
            creatureType = CreatureTemplate.Type.PinkLizard,
            randomSeed = 7714,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        // LEVEL THREE

        public static RainMeadow.MeadowProgression.Character Lizard_Rot3 = new("Lizard_Rot_3", true, new()
        {
            displayName = "LIZARD_ROT_THREE",
            emotePrefix = "liz_",
            emoteAtlas = "emotes_lizard",
            emoteColor = RainWorld.RippleColor,
            voiceId = Watcher.WatcherEnums.WatcherSoundID.RotLiz_Pain,
            selectSpriteIndexes = new int[2] { 1, 2 },
            startingCoords = new WorldCoordinate("DS_A06", 12, 16, -1)
        });
        public static RainMeadow.MeadowProgression.Skin Lizard_RotPink_3 = new("Lizard_RotPink_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Pink 3",
            creatureType = CreatureTemplate.Type.PinkLizard,
            randomSeed = 3323,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotBlue_3 = new("Lizard_RotBlue_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Blue 3",
            creatureType = CreatureTemplate.Type.BlueLizard,
            randomSeed = 3127,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotYellow_3 = new("Lizard_RotYellow_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Yellow 3",
            creatureType = CreatureTemplate.Type.YellowLizard,
            randomSeed = 720,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotWhite_3 = new("Lizard_RotWhite_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted White 3",
            creatureType = CreatureTemplate.Type.WhiteLizard,
            randomSeed = 3219,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotRed_3 = new("Lizard_RotRed_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Red 3",
            creatureType = CreatureTemplate.Type.RedLizard,
            randomSeed = 5038,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotBlack_3 = new("Lizard_RotBlack_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Black 3",
            creatureType = CreatureTemplate.Type.BlackLizard,
            randomSeed = 282,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotAxo_3 = new("Lizard_RotAxo_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Axo 3",
            creatureType = CreatureTemplate.Type.Salamander,
            randomSeed = 7623,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotSala_3 = new("Lizard_RotSala_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Sala 3",
            creatureType = CreatureTemplate.Type.Salamander,
            randomSeed = 3109,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotCyan_3 = new("Lizard_RotCyan_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Cyan 3",
            creatureType = CreatureTemplate.Type.CyanLizard,
            randomSeed = 5245,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,
        });

        public static RainMeadow.MeadowProgression.Skin Lizard_RotFluffers_3 = new("Lizard_RotFluffers_3", true, new()
        {
            character = Lizard_Rot3,
            displayName = "Rotted Fluffers 3",
            creatureType = CreatureTemplate.Type.PinkLizard,
            randomSeed = 7715,
            previewColor = RainWorld.RippleColor,
            tintFactor = 0.5f,

        });

public void AbstractCreature_Realize(On.AbstractCreature.orig_Realize orig, AbstractCreature self)  {

       if (OnlineManager.lobby != null && OnlineManager.lobby.gameMode is MeadowGameMode mgm && ModManager.Watcher)
        {
            if (self.state is LizardState && self.GetOnlineCreature().TryGetData<MeadowAvatarData>(out var data))
            {
                
                if (data.character == Lizard_Rot) {
                   (self.state as LizardState).SetRotType(LizardState.RotType.Slight);
                } else if (data.character == Lizard_Rot2)
                    {
                      (self.state as LizardState).SetRotType(LizardState.RotType.Opossum);
                    }
                  else if (data.character == Lizard_Rot3)
                    {
                     (self.state as LizardState).SetRotType(LizardState.RotType.Full);

                    }
            }
        }
         orig(self);
    }
}
    
}