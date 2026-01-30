    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using BepInEx;
    using HUD;
    using Menu;
    using MonoMod.RuntimeDetour;
    using UnityEngine;
    using RWCustom;
    using RainMeadow; // Ensure you reference the Rain Meadow DLL
    using Random = UnityEngine.Random;

    namespace RotMeadow
    {
        [BepInPlugin("uo.passagemostwhere", "Passage Mostwhere", "0.2.0")]
        public class PassageMostwhere : BaseUnityPlugin
        {
            // Delegate for the manual property hook
            private delegate Vector2 orig_MapOwnerInRoomPosition(FastTravelScreen self);

            public static PassageMostwhere instance;
            private bool init;
            private bool fullyInit;
            private Hook mapOwnerPosHook;

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
                    // Standard On Hooks
                    On.Menu.FastTravelScreen.ctor += FastTravelScreen_ctor;
                    On.Menu.FastTravelScreen.Singal += FastTravelScreen_Singal;
                    On.HUD.Map.MapData.ShelterMarkerPosOfRoom += MapData_ShelterMarkerPosOfRoom;

                    // Manual Hook for Property Getter: FastTravelScreen.MapOwnerInRoomPosition
                    // We use manual RuntimeDetour here because property getters are sometimes tricky with generated On hooks
                    mapOwnerPosHook = new Hook(
                        typeof(FastTravelScreen).GetProperty("MapOwnerInRoomPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod(true),
                        new Func<orig_MapOwnerInRoomPosition, FastTravelScreen, Vector2>(FastTravelScreen_get_MapOwnerInRoomPosition_Hook)
                    );

                    fullyInit = true;
                    Logger.LogInfo("Passage Mostwhere initialized successfully.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Passage Mostwhere failed to initialize:");
                    Logger.LogError(ex);
                    fullyInit = false;
                }
            }

            // Hook for FastTravelScreen.MapOwnerInRoomPosition (Property Getter)
            private Vector2 FastTravelScreen_get_MapOwnerInRoomPosition_Hook(orig_MapOwnerInRoomPosition orig, FastTravelScreen self)
            {
                if (OnlineManager.lobby != null && !string.IsNullOrEmpty(OnlineManager.lobby.meadowTimeline))
                {
                    // Logic from "Pain" method in decompilation
                    // Reconstruct MapData if necessary to avoid null refs, though this looks dangerous in production code
                    if (self.mapData == null || self.mapData.roomSizes == null)
                    {
                        self.mapData = new Map.MapData(null, self.manager.rainWorld);
                        self.mapData.roomSizes = new IntVector2[self.activeWorld != null ? self.activeWorld.NumberOfRooms : 0];
                    }

                    return new Vector2(10f, 10f); // Default offset
                }
                return orig(self);
            }

            // Hook for HUD.MapData.ShelterMarkerPosOfRoom
            private Vector2 MapData_ShelterMarkerPosOfRoom(On.HUD.Map.MapData.orig_ShelterMarkerPosOfRoom orig, Map.MapData self, int room)
            {
                if (OnlineManager.lobby != null && !string.IsNullOrEmpty(OnlineManager.lobby.meadowTimeline))
                {
                    IntVector2 size = self.SizeOfRoom(room);
                    return size.ToVector2() * 10f;
                }
                return orig(self, room);
            }

            // Hook for Menu.FastTravelScreen.Singal
            private void FastTravelScreen_Singal(On.Menu.FastTravelScreen.orig_Singal orig, FastTravelScreen self, MenuObject sender, string message)
            {
                // Only intervene if we are in a Rain Meadow lobby with a timeline
                if (OnlineManager.lobby != null && !string.IsNullOrEmpty(OnlineManager.lobby.meadowTimeline))
                {
                    if (message != "CHOOSE")
                    {
                        self.DestroyChoiceMenu();
                    }

                    switch (message)
                    {
                        case "RIPPLE WARP":
                            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
                            self.manager.pendingProcess = self.WarpFastTravelGame;
                            self.PlaySound(SoundID.MENU_Continue_Game);
                            break;

                        case "HOLD TO START":
                            if (self.initiateCharacterFastTravel)
                            {
                                self.manager.menuSetup.startGameCondition = ProcessManager.MenuSetup.StoryGameInitCondition.StartWithFastTravel;
                            }
                            else
                            {
                                self.manager.menuSetup.startGameCondition = ProcessManager.MenuSetup.StoryGameInitCondition.FastTravel;
                            }

                            // Pick random shelter
                            int randomShelterIndex = Random.Range(0, self.activeWorld.shelters.Length);
                            self.selectedShelter = randomShelterIndex;
                            
                            string shelterName = self.activeWorld.GetAbstractRoom(self.activeWorld.shelters[randomShelterIndex]).name;
                            self.manager.menuSetup.regionSelectRoom = shelterName;
                            RainWorld.ShelterAfterPassage = shelterName;
                            
                            self.manager.rainWorld.progression.miscProgressionData.menuRegion = self.activeWorld.name;
                            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
                            self.PlaySound(SoundID.MENU_Continue_Game);
                            break;

                        case "PREVIOUS":
                            self.StepRegion(-1);
                            break;

                        case "NEXT":
                            self.StepRegion(1);
                            break;

                        case "BACK":
                            if (self.IsFastTravelScreen)
                            {
                                self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SlugcatSelect);
                            }
                            else
                            {
                                self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                            }
                            self.PlaySound(SoundID.MENU_Switch_Page_Out);
                            break;

                        case "CHOOSE":
                            self.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                            if (self.choiceButtons.Count == 0)
                            {
                                self.SpawnChoiceMenu();
                            }
                            else
                            {
                                self.DestroyChoiceMenu();
                            }
                            break;
                    }

                    if (message.StartsWith("SLUG"))
                    {
                        string slugcatName = message.Substring(4);
                        self.manager.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat = new SlugcatStats.Name(slugcatName, false);
                        self.PlaySound(SoundID.MENU_Regions_Switch_Region);
                        self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.RegionsOverviewScreen);
                    }
                    else if (message.Contains("CHOICE"))
                    {
                        string numberString = message.Substring("CHOICE".Length);
                        int regionIndex = int.Parse(numberString, NumberStyles.Any, CultureInfo.InvariantCulture);

                        if (regionIndex == self.currentRegion)
                        {
                            self.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                            return;
                        }
                        self.PlaySound(SoundID.MENU_Regions_Switch_Region);
                        self.InitiateRegionSwitch(regionIndex);
                    }
                }
                else
                {
                    // Logic for vanilla behavior
                    orig(self, sender, message);
                }
            }

            // Hook for Menu.FastTravelScreen.ctor
            private void FastTravelScreen_ctor(On.Menu.FastTravelScreen.orig_ctor orig, FastTravelScreen self, ProcessManager manager, ProcessManager.ProcessID ID)
            {
                orig(self, manager, ID);

                if (OnlineManager.lobby == null || string.IsNullOrEmpty(OnlineManager.lobby.meadowTimeline))
                {
                    return;
                }

                self.choiceButtonsPerRow = 5;
                
                // Handle Timeline logic
                string timelineName = !string.IsNullOrEmpty(OnlineManager.lobby.meadowTimeline) ? OnlineManager.lobby.meadowTimeline : "White";
                SlugcatStats.Name timelineSlugcat = new SlugcatStats.Name(timelineName, false);

                self.allRegions = Region.LoadAllRegions(new SlugcatStats.Timeline(timelineSlugcat.value), null);
                self.accessibleRegions.Clear();

                for (int i = 0; i < self.allRegions.Length; i++)
                {
                    // Filter out specific regions
                    if (self.allRegions[i].name != "WRSA" && self.allRegions[i].name != "HR")
                    {
                        self.accessibleRegions.Add(i);
                    }
                    RainMeadow.RainMeadow.Debug($"Passage Mostwhere: {i}");
                }

                // Sort regions: Length 4 first, then by Full Name
                self.accessibleRegions = self.accessibleRegions
                    .OrderBy(index => self.allRegions[index].name.Length == 4)
                    .ThenBy(index => Region.GetRegionFullName(self.allRegions[index].name, timelineSlugcat))
                    .ToList();
            }
        }
    }