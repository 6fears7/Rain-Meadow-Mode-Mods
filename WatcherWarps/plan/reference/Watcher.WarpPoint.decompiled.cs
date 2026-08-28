using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MoreSlugcats;
using RWCustom;
using UnityEngine;

namespace Watcher;

public class WarpPoint : CosmeticSprite, ISpecialWarp, INotifyWhenRoomUnloaded
{
	public interface IOpenWarps
	{
		bool Entering();

		int FadeDuration();

		void UpdateShaders(float timeStacker);
	}

	public class SoundMode : ExtEnum<SoundMode>
	{
		public static SoundMode Idle = new SoundMode("Idle", register: true);

		public static SoundMode SuckIn = new SoundMode("SuckIn", register: true);

		public SoundMode(string value, bool register = false)
			: base(value, register)
		{
		}
	}

	public class State : ExtEnum<State>
	{
		public static State ReadyForWarp = new State("ReadyForWarp", register: true);

		public static State EnterWarp = new State("EnterWarp", register: true);

		public static State ExitWarp = new State("ExitWarp", register: true);

		public static State SpawnItems = new State("SpawnItems", register: true);

		public static State CoolDown = new State("CoolDown", register: true);

		public static State Sealed = new State("Sealed", register: true);

		public static State Closed = new State("Closed", register: true);

		public State(string value, bool register = false)
			: base(value, register)
		{
		}
	}

	public class WarpPointTimer
	{
		public float duration;

		public float progress;

		public float progressOld;

		public float loadingAcceleration;

		public float loadingTimer;

		public Vector4 parameters;

		public bool accelerate;

		public bool rainReset;

		public WarpPoint origin_warpPoint;

		public bool finished;

		public bool pastHalfPoint;

		public bool forceNoExitVisuals;

		public bool darkWarp;

		public WarpPointTimer(float duration, WarpPoint origin_warpPoint)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			base..ctor();
			parameters = origin_warpPoint.parameters;
			this.duration = duration;
			this.origin_warpPoint = origin_warpPoint;
			darkWarp = origin_warpPoint.Data.darkWarp;
			if (origin_warpPoint.room != null)
			{
				origin_warpPoint.room.game.cameras[0].rippleData?.StoreTrailParameters();
			}
		}

		public void Progress()
		{
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Unknown result type (might be due to invalid IL or missing references)
			if (progress < duration / 2f)
			{
				RippleCameraData rippleCameraData = ((origin_warpPoint.room != null) ? origin_warpPoint.room.game.cameras[0].rippleData : null);
				if (rippleCameraData != null)
				{
					float num = Mathf.Clamp01(progress / duration * 3f);
					rippleCameraData.trailPaletteAmount = Mathf.Lerp(rippleCameraData.trailPaletteAmountStored, rippleCameraData.gameplayRippleActive ? 1f : 0f, num);
					rippleCameraData.TrailAmount = Mathf.Lerp(rippleCameraData.trailAmountStored, 0f, num);
				}
				Shader.SetGlobalVector("_warpPointOldPosition", Vector4.op_Implicit(origin_warpPoint.screenPosition));
				accelerate = true;
				progressOld = progress;
				progress++;
			}
			else if (progress > duration / 2f)
			{
				if (!rainReset && origin_warpPoint.room != null)
				{
					origin_warpPoint.room.world.game.globalRain.ResetRain();
					rainReset = true;
				}
				progressOld = progress;
				progress++;
			}
			else
			{
				progressOld = progress;
			}
		}

		public void MovePastHalfPoint()
		{
			pastHalfPoint = true;
			progressOld = progress;
			progress++;
		}

		public void MoveToSecondHalf()
		{
			pastHalfPoint = true;
			progress = duration / 2f + 1f;
			progressOld = progress;
		}

		public void UpdateShaderParameter(float timeStacker)
		{
			float num = Mathf.Lerp(progressOld, progress, timeStacker);
			if (num > duration)
			{
				accelerate = false;
				Shader.SetGlobalFloat("_warpPointAcceleration", 0f);
				Shader.SetGlobalFloat("_playerWarpPointTime", 0f);
				Shader.SetGlobalFloat("_warpPointTime", 0f);
				if (origin_warpPoint.room != null && origin_warpPoint.room.game.world.activeRooms.Contains(origin_warpPoint.room))
				{
					if (origin_warpPoint.Data.destRoom != origin_warpPoint.room.abstractRoom.name)
					{
						origin_warpPoint.ChangeState(State.CoolDown);
					}
				}
				else
				{
					origin_warpPoint.Destroy();
				}
				origin_warpPoint = null;
				finished = true;
			}
			else
			{
				float num2 = 1f - Mathf.Abs(num / duration - 0.5f) * 2f;
				if (origin_warpPoint.room != null)
				{
					origin_warpPoint.room.game.cameras[0].virtualMicrophone.globalSoundMuffle = Mathf.Clamp01((num2 - 0.5f) * 2f);
					origin_warpPoint.room.game.cameras[0].virtualMicrophone.warpTransition = Mathf.Clamp01(num2) * 4f;
				}
				Shader.SetGlobalFloat("_playerWarpPointTime", num / duration);
				Shader.SetGlobalFloat("_warpPointTime", num / duration);
				if (accelerate)
				{
					Shader.SetGlobalFloat("_warpPointAcceleration", loadingTimer += (loadingAcceleration += 0.03f * Time.deltaTime) * Time.deltaTime);
				}
				else
				{
					Shader.SetGlobalFloat("_warpPointAcceleration", 0f);
				}
			}
		}
	}

	public class WarpPointData : PlacedObject.Data
	{
		public class WarpPointSpawnCondition : ExtEnum<WarpPointSpawnCondition>
		{
			public static WarpPointSpawnCondition WatcherOnly = new WarpPointSpawnCondition("WatcherOnly", register: true);

			public static WarpPointSpawnCondition AnySlugcat = new WarpPointSpawnCondition("AnySlugcat", register: true);

			public WarpPointSpawnCondition(string value, bool register = false)
				: base(value, register)
			{
			}
		}

		public struct EffectSettings
		{
			public int vignette;

			public int darkness;

			public int spiralGap;

			public int swirlIntensity;

			public int lensingIntensity;

			public int noiseIntensity;

			public int spiralTwist;

			public int agitationSpeed;

			public int spaghettification;

			public bool outerRimCosmetic;

			public bool badWarpCosmetic;

			public bool spawnBigRift;

			public int activeDuration;

			public int triggerDuration;

			public static EffectSettings DefaultCosmetics()
			{
				return new EffectSettings
				{
					vignette = 0,
					darkness = 6,
					spiralGap = 8,
					swirlIntensity = 4,
					lensingIntensity = 8,
					noiseIntensity = 4,
					spiralTwist = 4,
					agitationSpeed = 4,
					spaghettification = 7,
					outerRimCosmetic = false,
					badWarpCosmetic = false,
					spawnBigRift = false,
					activeDuration = 150,
					triggerDuration = 150
				};
			}

			public static EffectSettings OuterRimCosmetics()
			{
				return new EffectSettings
				{
					vignette = 5,
					darkness = 3,
					spiralGap = 8,
					swirlIntensity = 2,
					lensingIntensity = 8,
					noiseIntensity = 5,
					spiralTwist = 1,
					agitationSpeed = 4,
					spaghettification = 7,
					outerRimCosmetic = true,
					badWarpCosmetic = false,
					spawnBigRift = true,
					activeDuration = 150,
					triggerDuration = 150
				};
			}

			public static EffectSettings BadWarpCosmetics()
			{
				return new EffectSettings
				{
					vignette = 10,
					darkness = 10,
					spiralGap = 15,
					swirlIntensity = 2,
					lensingIntensity = 8,
					noiseIntensity = 4,
					spiralTwist = 15,
					agitationSpeed = 4,
					spaghettification = 10,
					outerRimCosmetic = false,
					badWarpCosmetic = true,
					spawnBigRift = true,
					activeDuration = 150,
					triggerDuration = 150
				};
			}

			public static EffectSettings DynamicCosmetics()
			{
				return new EffectSettings
				{
					vignette = 7,
					darkness = 7,
					spiralGap = 8,
					swirlIntensity = 3,
					lensingIntensity = 8,
					noiseIntensity = 5,
					spiralTwist = 4,
					agitationSpeed = 4,
					spaghettification = 7,
					outerRimCosmetic = false,
					badWarpCosmetic = false,
					spawnBigRift = false,
					activeDuration = 150,
					triggerDuration = 150
				};
			}
		}

		public EffectSettings effectSettings = EffectSettings.DefaultCosmetics();

		public Vector2 panelPos;

		public SlugcatStats.Timeline sourceTimeline;

		public SlugcatStats.Timeline destTimeline;

		public string destRegion;

		public string destRoom;

		public Vector2? destPos;

		public string uuidPair;

		public int cycleExpiry;

		public int cycleSpawnedOn;

		public WarpPointSpawnCondition accessibility = WarpPointSpawnCondition.WatcherOnly;

		public bool deathPersistentWarpPoint;

		public bool limitedUse;

		public int uses;

		public bool noRing;

		public bool darkWarp;

		public int destCam = -1;

		public int thisCam;

		public bool oneWay;

		public bool oneWayEntrance;

		public bool oneWayEntranceIdentified;

		public bool rippleWarp;

		public bool rippleEggWarpPoint;

		public int weaverTriggeredCycle;

		public bool wasNonDynamicWarpBeforeWeaverTriggered;

		public bool oneWayExit
		{
			get
			{
				if (oneWay)
				{
					return !oneWayEntrance;
				}
				return false;
			}
		}

		public bool nonDynamicWarpPoint => cycleExpiry <= 0;

		public bool dynamicWarpPoint => cycleExpiry > 0;

		public string RegionString
		{
			get
			{
				if (destRegion == null && destRoom == null)
				{
					return null;
				}
				if (destRegion != null)
				{
					return destRegion;
				}
				return destRoom.Split(new char[1] { '_' })[0];
			}
			set
			{
				destRegion = value;
			}
		}

		public WarpPointData(PlacedObject owner)
			: base(owner)
		{
			uuidPair = Guid.NewGuid().ToString();
			cycleExpiry = 0;
			cycleSpawnedOn = -1;
			weaverTriggeredCycle = -1;
		}

		public bool UpToDateWithIndexMaps(string roomName)
		{
			if ((!nonDynamicWarpPoint && !wasNonDynamicWarpBeforeWeaverTriggered) || oneWayExit)
			{
				return true;
			}
			string text = destRoom?.ToLowerInvariant() ?? "null";
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, List<string>> regionWarpRoom in Custom.rainWorld.regionWarpRooms)
			{
				for (int i = 0; i < regionWarpRoom.Value.Count; i++)
				{
					string[] array = regionWarpRoom.Value[i].ToLowerInvariant().Split(new char[1] { ':' });
					if (array.Length > 3 && array[1] == "1" == rippleWarp)
					{
						if (array[3] == roomName)
						{
							list.Add(array[0]);
						}
						if (array[0] == roomName && array[3] == text)
						{
							return true;
						}
					}
				}
			}
			foreach (KeyValuePair<string, List<string>> regionSpinningTopRoom in Custom.rainWorld.regionSpinningTopRooms)
			{
				for (int j = 0; j < regionSpinningTopRoom.Value.Count; j++)
				{
					string[] array2 = regionSpinningTopRoom.Value[j].ToLowerInvariant().Split(new char[1] { ':' });
					if (array2.Length > 2)
					{
						if (array2[2] == roomName)
						{
							list.Add(array2[0]);
						}
						if (array2[0] == roomName && array2[2] == text)
						{
							return true;
						}
					}
				}
			}
			for (int k = 0; k < list.Count; k++)
			{
				if (list[k] == text)
				{
					return true;
				}
			}
			return false;
		}

		public void GenerateDestinationLocation(Room realizedDestRoom, World sourceWorld, AbstractRoom sourceRoom)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			bool flag = false;
			foreach (PlacedObject placedObject in realizedDestRoom.roomSettings.placedObjects)
			{
				if (placedObject.type == PlacedObject.Type.DynamicWarpTarget)
				{
					destPos = placedObject.pos;
					flag = true;
				}
				else if ((nonDynamicWarpPoint || wasNonDynamicWarpBeforeWeaverTriggered) && placedObject.type == PlacedObject.Type.WarpPoint && (placedObject.data as WarpPointData).destRoom.ToLowerInvariant() == sourceRoom.name.ToLowerInvariant())
				{
					destPos = placedObject.pos;
					RefreshRegionState(sourceWorld, sourceRoom);
					return;
				}
			}
			if (flag)
			{
				RefreshRegionState(sourceWorld, sourceRoom);
				return;
			}
			State state = Random.state;
			Random.InitState(uuidPair.GetHashCode());
			IntVector2 intVector = Room.DetermineSafeSpawnTile(realizedDestRoom, forWarpPoint: true);
			Random.state = state;
			destPos = new Vector2((float)intVector.x * 20f, (float)intVector.y * 20f);
			RefreshRegionState(sourceWorld, sourceRoom);
		}

		public void RefreshRegionState(World sourceWorld, AbstractRoom sourceRoom)
		{
			if (owner != null)
			{
				string key = IdentifyingString(sourceWorld.game, this, sourceRoom);
				if (sourceWorld.game.GetStorySession.saveState.deathPersistentSaveData.spawnedWarpPoints.Count == 0 && sourceWorld.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints.Count == 0)
				{
					Custom.rainWorld.progression.miscProgressionData.beaten_Watcher_Tutorial = true;
				}
				if (sourceWorld.game.GetStorySession.saveState.deathPersistentSaveData.newlyDiscoveredWarpPoints.ContainsKey(key))
				{
					sourceWorld.game.GetStorySession.saveState.deathPersistentSaveData.newlyDiscoveredWarpPoints[key] = owner.ToString();
				}
				if (sourceWorld.game.GetStorySession.saveState.deathPersistentSaveData.spawnedWarpPoints.ContainsKey(key))
				{
					sourceWorld.game.GetStorySession.saveState.deathPersistentSaveData.spawnedWarpPoints[key] = owner.ToString();
				}
				if (sourceWorld.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints.ContainsKey(key))
				{
					sourceWorld.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints[key] = owner.ToString();
				}
			}
		}

		public bool ExpiredOnCycle(int currentCycleNumber)
		{
			if (dynamicWarpPoint)
			{
				return currentCycleNumber - cycleSpawnedOn > cycleExpiry;
			}
			return false;
		}

		public bool ExpiredOnCycleAndTriggeredWeaver(int currentCycleNumber, SaveState saveState)
		{
			if (ExpiredByWeaverOrWeaverAbility(currentCycleNumber, saveState))
			{
				return !wasNonDynamicWarpBeforeWeaverTriggered;
			}
			return false;
		}

		public bool ExpiredByWeaverOrWeaverAbility(int currentCycleNumber, SaveState saveState)
		{
			if (dynamicWarpPoint && currentCycleNumber - cycleSpawnedOn > cycleExpiry && weaverTriggeredCycle != currentCycleNumber)
			{
				if (!saveState.miscWorldSaveData.hasVoidWeaverAbility)
				{
					return weaverTriggeredCycle > 0;
				}
				return true;
			}
			return false;
		}

		public bool ExpiredByLimitedUses()
		{
			if (limitedUse)
			{
				return uses == 0;
			}
			return false;
		}

		public override void FromString(string s)
		{
			//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			string[] array = Regex.Split(s, "~");
			int num = array.Length;
			panelPos.x = float.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture);
			panelPos.y = float.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture);
			sourceTimeline = ((array[2] == "NULL") ? null : new SlugcatStats.Timeline(array[2]));
			destTimeline = ((array[3] == "NULL") ? null : new SlugcatStats.Timeline(array[3]));
			destRegion = ((array[4] == "NULL") ? null : array[4]);
			destRoom = ((array[5] == "NULL") ? null : array[5]);
			if (array[6] == "NULL" || array[7] == "NULL")
			{
				destPos = null;
			}
			else
			{
				Vector2 zero = Vector2.zero;
				zero.x = float.Parse(array[6], NumberStyles.Any, CultureInfo.InvariantCulture);
				zero.y = float.Parse(array[7], NumberStyles.Any, CultureInfo.InvariantCulture);
				destPos = zero;
			}
			uuidPair = array[8];
			accessibility = new WarpPointSpawnCondition(array[9]);
			EffectSettings effectSettings = EffectSettings.DefaultCosmetics();
			if (num > 10)
			{
				effectSettings.vignette = int.Parse(array[10], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 11)
			{
				effectSettings.darkness = int.Parse(array[11], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 12)
			{
				effectSettings.spiralGap = int.Parse(array[12], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 13)
			{
				effectSettings.swirlIntensity = int.Parse(array[13], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 14)
			{
				effectSettings.lensingIntensity = int.Parse(array[14], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 15)
			{
				effectSettings.noiseIntensity = int.Parse(array[15], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 16)
			{
				effectSettings.spiralTwist = int.Parse(array[16], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 17)
			{
				effectSettings.agitationSpeed = int.Parse(array[17], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 18)
			{
				effectSettings.spaghettification = int.Parse(array[18], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 19)
			{
				effectSettings.triggerDuration = int.Parse(array[19], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 20)
			{
				effectSettings.activeDuration = int.Parse(array[20], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 21)
			{
				cycleSpawnedOn = int.Parse(array[21], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 22)
			{
				cycleExpiry = int.Parse(array[22], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 23)
			{
				deathPersistentWarpPoint = array[23] == "1";
			}
			if (num > 24)
			{
				oneWay = array[24] == "true";
			}
			if (num > 25)
			{
				rippleWarp = array[25] == "true";
			}
			if (num > 26)
			{
				oneWayEntrance = array[26] == "true";
			}
			if (num > 27)
			{
				oneWayEntranceIdentified = array[27] == "true";
			}
			if (num > 28)
			{
				limitedUse = array[28] == "true";
			}
			if (num > 29)
			{
				uses = int.Parse(array[29], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 30)
			{
				weaverTriggeredCycle = int.Parse(array[30], NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			if (num > 31)
			{
				wasNonDynamicWarpBeforeWeaverTriggered = array[31] == "true";
			}
			this.effectSettings = effectSettings;
			unrecognizedAttributes = SaveUtils.PopulateUnrecognizedStringAttrs(array, 32);
		}

		public override string ToString()
		{
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			string baseString = string.Format(CultureInfo.InvariantCulture, "{0}~{1}~{2}~{3}~{4}~{5}~{6}~{7}~{8}~{9}~{10}~{11}~{12}~{13}~{14}~{15}~{16}~{17}~{18}~{19}~{20}~{21}~{22}~{23}~{24}~{25}~{26}~{27}~{28}~{29}~{30}~{31}", new object[32]
			{
				panelPos.x,
				panelPos.y,
				(sourceTimeline == null) ? "NULL" : sourceTimeline.ToString(),
				(destTimeline == null) ? "NULL" : destTimeline.ToString(),
				(RegionString == null) ? "NULL" : RegionString,
				(destRoom == null) ? "NULL" : destRoom,
				(!destPos.HasValue) ? "NULL" : destPos.Value.x.ToString(),
				(!destPos.HasValue) ? "NULL" : destPos.Value.y.ToString(),
				uuidPair,
				accessibility,
				effectSettings.vignette,
				effectSettings.darkness,
				effectSettings.spiralGap,
				effectSettings.swirlIntensity,
				effectSettings.lensingIntensity,
				effectSettings.noiseIntensity,
				effectSettings.spiralTwist,
				effectSettings.agitationSpeed,
				effectSettings.spaghettification,
				effectSettings.triggerDuration,
				effectSettings.activeDuration,
				cycleSpawnedOn,
				cycleExpiry,
				deathPersistentWarpPoint ? "1" : "0",
				oneWay ? "true" : "false",
				rippleWarp ? "true" : "false",
				oneWayEntrance ? "true" : "false",
				oneWayEntranceIdentified ? "true" : "false",
				limitedUse ? "true" : "false",
				uses,
				weaverTriggeredCycle,
				wasNonDynamicWarpBeforeWeaverTriggered ? "true" : "false"
			});
			baseString = SaveState.SetCustomData(this, baseString);
			return SaveUtils.AppendUnrecognizedStringAttrs(baseString, "~", unrecognizedAttributes);
		}
	}

	public PlacedObject placedObject;

	public WarpPointData overrideData;

	public WarpTear warpTear;

	public bool activated;

	public int activationTime;

	public float triggerTime;

	public float lastTriggerTime;

	public string sourceRegion;

	public bool strongPull;

	public IOpenWarps nonPlayerRequestOpen;

	public float radius;

	public float creatureSuckRadius;

	public int strongPullRadius;

	public int normalPullRadius;

	public bool refreshGraphics;

	public bool canPreCast;

	public StaticSoundLoop rippleHintSoundLoop;

	public StaticSoundLoop soundLoop;

	public bool isARestoralPoint;

	public SoundMode currentSoundLoopMode;

	public State currentState;

	public float activationRadius;

	public float activateAnimationTime;

	public float triggerActivationTime;

	public bool guaranteeTrigger;

	public bool warpTearOpenTriggered;

	public int timeWarpTearClosed;

	public float successfullWarpCooldownTimer;

	public int successfullWarpCooldownFrames;

	public bool closesEarly;

	public bool dontSave;

	public Vector4 parameters;

	public Vector2 screenPosition;

	public int spawnTime;

	public bool startedPendingSpawn;

	public Player playerTriggeredWarpPoint;

	public int weaverAnimationTime;

	public int totalWeaverAnimationTime;

	public int weaverThreadAnimTime;

	public bool weaverAnimationStarted;

	public List<Vector2> weaverAnimationsStarts;

	public List<Vector2> weaverAnimationsEnds;

	public List<Vector2> weaverThreadAbsPos;

	public List<WeaverThread> weaverThreads;

	public float badWarpStartTime;

	public float badWarpLerpTime;

	public float badWarpRadiusMin;

	public float badWarpRadiusMax;

	public bool badWarpAnimStarted;

	public bool startingBadWarpAnim;

	public bool warpLocked;

	public bool spawnedBadWarpRipple;

	public bool fadeInOnSpawn;

	public int playOpenSoundAfterDelay;

	public bool dynamicWeaverFadeOut;

	public bool forceNoVisuals;

	public bool lastFrameWarpSequenceInProgress;

	public bool wasDeferredSpawn;

	public bool canWarpToVoidWeaverEnding;

	public static List<CreatureTemplate.Type> blackListedCreatureTypes;

	public static List<AbstractPhysicalObject.AbstractObjectType> blackListedObjectTypes;

	public bool badWarpPrimed;

	public float shockWaveTimer;

	public bool secondShockwave;

	public float shockWaveTimerLimit;

	public CosmeticRipple ripple;

	public bool deletable;

	public bool deleteNextFrame;

	public Counter pulseSoundTimer;

	public Counter pulseHitSoundTimer;

	public bool canPlayOpenPing;

	public WarpPointData Data => placedObject.data as WarpPointData;

	public float PullRadius => strongPull ? strongPullRadius : normalPullRadius;

	public RoomCamera camera => room.game.cameras[0];

	public float visualOpenness
	{
		get
		{
			if (warpTear == null)
			{
				return 0f;
			}
			return warpTear.openAnimation * warpTear.fadeAnim * (1f - warpTear.weaverBlockAnimation);
		}
	}

	public bool anyPlayersInRange { get; set; }

	public bool transportable
	{
		get
		{
			if ((!Data.oneWay || Data.oneWay == Data.oneWayEntrance) && room.game.ActiveRippleLayer == 1 == Data.rippleWarp && (!Data.limitedUse || Data.uses > 0) && (!(successfullWarpCooldownTimer > 0f) || room.game.GetStorySession.pendingWarpPointTransferObjects.Count != 0))
			{
				return !warpLocked;
			}
			return false;
		}
	}

	public bool warpSequenceInProgress
	{
		get
		{
			if (room == null)
			{
				return false;
			}
			return room.world.game.cameras[0].warpPointTimer != null;
		}
	}

	public bool thisWarpSequenceInProgress
	{
		get
		{
			if (warpSequenceInProgress)
			{
				if (room.world.game.cameras[0].warpPointTimer.origin_warpPoint != this)
				{
					return room.world.game.GetStorySession.pendingWarpPointTransferId == MyIdentifyingString();
				}
				return true;
			}
			return false;
		}
	}

	public bool otherWarpSequenceInProgress
	{
		get
		{
			if (warpSequenceInProgress)
			{
				return !thisWarpSequenceInProgress;
			}
			return false;
		}
	}

	public static List<CreatureTemplate.Type> BlackListedCreatureTypes
	{
		get
		{
			if (blackListedCreatureTypes == null)
			{
				BuildBlacklist();
			}
			return blackListedCreatureTypes;
		}
	}

	public static List<AbstractPhysicalObject.AbstractObjectType> BlackListedObjectTypes
	{
		get
		{
			if (blackListedObjectTypes == null)
			{
				BuildBlacklist();
			}
			return blackListedObjectTypes;
		}
	}

	public bool WarpPointAnimationPlaying
	{
		get
		{
			if (!activated)
			{
				if (nonPlayerRequestOpen != null)
				{
					return nonPlayerRequestOpen.Entering();
				}
				return false;
			}
			return true;
		}
	}

	public bool CanPlayWarpSealingAnimation
	{
		get
		{
			if (!warpLocked && (thisWarpSequenceInProgress || currentState == State.SpawnItems) && room.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility && !Data.ExpiredByLimitedUses() && !Data.oneWayExit && RoomCanBeSealed(room.abstractRoom.name))
			{
				return !Data.rippleWarp;
			}
			return false;
		}
	}

	public static bool WarpInProgress => Shader.GetGlobalFloat("_playerWarpPointTime") >= 0f;

	public static bool WarpIsBeingActivated => Shader.GetGlobalFloat("_warpPointTime") != -1f;

	public static void ResetBlacklist()
	{
		blackListedCreatureTypes = null;
		blackListedObjectTypes = null;
	}

	public static void BuildBlacklist()
	{
		blackListedCreatureTypes = new List<CreatureTemplate.Type>
		{
			CreatureTemplate.Type.Overseer,
			CreatureTemplate.Type.Deer,
			CreatureTemplate.Type.BigEel,
			CreatureTemplate.Type.PoleMimic,
			CreatureTemplate.Type.TentaclePlant,
			CreatureTemplate.Type.GarbageWorm,
			CreatureTemplate.Type.TempleGuard
		};
		blackListedObjectTypes = new List<AbstractPhysicalObject.AbstractObjectType>
		{
			AbstractPhysicalObject.AbstractObjectType.BlinkingFlower,
			AbstractPhysicalObject.AbstractObjectType.Oracle,
			AbstractPhysicalObject.AbstractObjectType.AttachedBee,
			AbstractPhysicalObject.AbstractObjectType.CollisionField,
			AbstractPhysicalObject.AbstractObjectType.SeedCob,
			AbstractPhysicalObject.AbstractObjectType.VoidSpawn,
			AbstractPhysicalObject.AbstractObjectType.DartMaggot,
			AbstractPhysicalObject.AbstractObjectType.LobeTree,
			AbstractPhysicalObject.AbstractObjectType.SLOracleSwarmer
		};
		if (ModManager.DLCShared)
		{
			blackListedCreatureTypes.Add(DLCSharedEnums.CreatureTemplateType.BigJelly);
			blackListedCreatureTypes.Add(DLCSharedEnums.CreatureTemplateType.StowawayBug);
		}
		if (ModManager.MSC)
		{
			blackListedCreatureTypes.Add(MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing);
			blackListedObjectTypes.Add(MoreSlugcatsEnums.AbstractObjectType.Bullet);
			blackListedObjectTypes.Add(MoreSlugcatsEnums.AbstractObjectType.Germinator);
			blackListedObjectTypes.Add(MoreSlugcatsEnums.AbstractObjectType.HRGuard);
		}
		if (ModManager.Watcher)
		{
			blackListedCreatureTypes.Add(WatcherEnums.CreatureTemplateType.Rattler);
			blackListedCreatureTypes.Add(WatcherEnums.CreatureTemplateType.Loach);
			blackListedCreatureTypes.Add(WatcherEnums.CreatureTemplateType.RotLoach);
			blackListedCreatureTypes.Add(WatcherEnums.CreatureTemplateType.SandGrub);
			blackListedCreatureTypes.Add(WatcherEnums.CreatureTemplateType.BigSandGrub);
			blackListedCreatureTypes.Add(WatcherEnums.CreatureTemplateType.SkyWhale);
			blackListedObjectTypes.Add(WatcherEnums.AbstractObjectType.RippleSpawn);
			blackListedObjectTypes.Add(WatcherEnums.AbstractObjectType.RippleJelly);
			blackListedObjectTypes.Add(WatcherEnums.AbstractObjectType.KnotSpawn);
			blackListedObjectTypes.Add(WatcherEnums.AbstractObjectType.Prince);
			blackListedObjectTypes.Add(WatcherEnums.AbstractObjectType.PrinceBulb);
		}
	}

	public WarpPoint(Room room, PlacedObject placedObject)
	{
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		radius = 150f;
		creatureSuckRadius = 1000f;
		strongPullRadius = 550;
		normalPullRadius = 150;
		canPreCast = true;
		currentSoundLoopMode = SoundMode.Idle;
		currentState = State.ReadyForWarp;
		activationRadius = 0.23f;
		activateAnimationTime = 50f;
		triggerActivationTime = 50f;
		successfullWarpCooldownFrames = 1200;
		weaverAnimationsStarts = new List<Vector2>();
		weaverAnimationsEnds = new List<Vector2>();
		weaverThreadAbsPos = new List<Vector2>();
		weaverThreads = new List<WeaverThread>();
		badWarpRadiusMin = 20f;
		badWarpRadiusMax = 75f;
		shockWaveTimerLimit = 2f;
		deletable = true;
		pulseSoundTimer = new Counter(30);
		pulseHitSoundTimer = new Counter(30);
		canPlayOpenPing = true;
		base..ctor();
		if (room != null)
		{
			SetRoom(room);
		}
		this.placedObject = placedObject;
		pos = placedObject.pos;
		lastPos = placedObject.pos;
		if (Data.sourceTimeline == null && room != null)
		{
			Data.sourceTimeline = room.game.TimelinePoint;
		}
		if (Data.oneWay && !Data.oneWayEntranceIdentified)
		{
			Data.oneWayEntrance = true;
			Data.oneWayEntranceIdentified = true;
		}
		if (Data.rippleWarp)
		{
			successfullWarpCooldownFrames = 10;
		}
		parameters.x = (float)((Data.effectSettings.vignette << 4) | Data.effectSettings.darkness) / 255f;
		parameters.y = (float)((Data.effectSettings.spiralGap << 4) | Data.effectSettings.swirlIntensity) / 255f;
		parameters.z = (float)((Data.effectSettings.lensingIntensity << 4) | Data.effectSettings.noiseIntensity) / 255f;
		parameters.w = (float)((Data.effectSettings.spiralTwist << 4) | Data.effectSettings.agitationSpeed) / 255f;
		activateAnimationTime += Data.effectSettings.activeDuration;
		triggerActivationTime += Data.effectSettings.triggerDuration;
		if (Data.destCam == -1)
		{
			Data.destCam = GetDestCam(Data);
		}
		if (warpSequenceInProgress)
		{
			ChangeState(State.CoolDown);
			closesEarly = true;
		}
		if (base.room != null && CheckClosedOrSealed(firstFrame: true, out var nextState))
		{
			ChangeState(nextState);
		}
	}

	public static string ChooseDynamicWarpTarget(World world, string oldRoom, string targetRegion, bool badWarp, bool spreadingRot, bool playerCreated)
	{
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		List<string> list;
		if (targetRegion == null || !(targetRegion.ToLowerInvariant() == "wora"))
		{
			list = ((!badWarp) ? GetAvailableDynamicWarpTargets(world, oldRoom, targetRegion, spreadingRot) : GetAvailableBadWarpTargets(world, oldRoom));
		}
		else
		{
			list = GetAvailableOuterRimWarpTargets(world, oldRoom, spreadingRot);
			spreadingRot = false;
		}
		if (list.Count == 0)
		{
			Custom.LogWarning("NO VALID WARP POINTS FOUND! IndexCache might be broken." + ((targetRegion == null) ? "" : (" Target region was [" + targetRegion + "]")));
			return null;
		}
		list.Sort();
		State state = Random.state;
		int num = (badWarp ? world.game.GetStorySession.saveState.miscWorldSaveData.numberOfBadWarpsGenerated : ((!playerCreated) ? world.game.GetStorySession.saveState.miscWorldSaveData.numberOfUndefinedWarpsTransversed : ((targetRegion == null) ? world.game.GetStorySession.saveState.miscWorldSaveData.numberOfWarpPointsGenerated : world.game.GetStorySession.saveState.miscWorldSaveData.numberOfRippleEggWarpsGenerated)));
		Random.InitState(world.game.rainWorld.progression.miscProgressionData.watcherCampaignSeed + num);
		int index = Random.Range(0, list.Count);
		Random.state = state;
		return list[index];
	}

	public static WarpPointData CreateOverrideData(World world, string oldRoom, string chosenRoom, Vector2? chosenDestPosition, bool limitedUse, bool playerCreated)
	{
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		bool flag = false;
		Vector2? destPos = chosenDestPosition;
		string regionString = null;
		if (chosenRoom != null && !destPos.HasValue)
		{
			flag = Custom.rainWorld.levelDeadEndTargets.Contains(chosenRoom);
			regionString = chosenRoom.Split(new char[1] { '_' })[0];
			foreach (PlacedObject placedObject in new RoomSettings(chosenRoom, null, template: false, firstTemplate: false, world.game.TimelinePoint, world.game).placedObjects)
			{
				if (placedObject.type == PlacedObject.Type.DynamicWarpTarget)
				{
					destPos = placedObject.pos;
					break;
				}
			}
		}
		if (Region.IsAncientUrbanRegion(world.name) || Region.IsSentientRotRegion(world.name))
		{
			limitedUse = true;
		}
		bool flag2 = false;
		if (oldRoom != null && !world.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility && (oldRoom.ToLowerInvariant() == "wora_throne05" || oldRoom.ToLowerInvariant() == "wora_throne07" || oldRoom.ToLowerInvariant() == "wora_throne09" || oldRoom.ToLowerInvariant() == "wora_throne10"))
		{
			limitedUse = false;
			flag2 = true;
		}
		WarpPointData warpPointData = new WarpPointData(null);
		warpPointData.destPos = destPos;
		warpPointData.RegionString = regionString;
		warpPointData.destRoom = chosenRoom;
		warpPointData.destTimeline = world.game.TimelinePoint;
		warpPointData.panelPos = Vector2.zero;
		warpPointData.cycleExpiry = (flag2 ? 9999 : Watcher.CYCLE_EXPIRY_PLAYER_CREATED_WARP_POINTS);
		warpPointData.limitedUse = limitedUse;
		warpPointData.uses = (limitedUse ? ((!flag) ? 1 : 2) : 0);
		warpPointData.oneWay = warpPointData.uses == 1;
		warpPointData.oneWayEntrance = warpPointData.oneWay;
		if (!playerCreated)
		{
			warpPointData.oneWayEntranceIdentified = true;
		}
		warpPointData.noRing = true;
		if (warpPointData.RegionString != null && warpPointData.RegionString.ToLowerInvariant() == "wora")
		{
			warpPointData.effectSettings = WarpPointData.EffectSettings.OuterRimCosmetics();
		}
		else if (warpPointData.RegionString != null && Region.IsVanillaSentientRotRegion(warpPointData.RegionString))
		{
			warpPointData.effectSettings = WarpPointData.EffectSettings.BadWarpCosmetics();
		}
		else if (playerCreated)
		{
			warpPointData.effectSettings = WarpPointData.EffectSettings.DynamicCosmetics();
		}
		if (world.game.IsStorySession)
		{
			warpPointData.cycleSpawnedOn = world.game.GetStorySession.saveState.cycleNumber;
		}
		warpPointData.destCam = GetDestCam(warpPointData);
		return warpPointData;
	}

	public static List<string> GetAvailableDynamicWarpTargets(World world, string oldRoom, string targetRegion, bool spreadingRot)
	{
		if (world.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility)
		{
			spreadingRot = false;
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		float rippleLevel = world.game.GetStorySession.saveState.deathPersistentSaveData.rippleLevel;
		string text = null;
		List<string> list3 = new List<string>();
		for (int i = 0; i < world.game.GetStorySession.saveState.regionStates.Length; i++)
		{
			if (world.game.GetStorySession.saveState.regionStates[i] != null)
			{
				list3.Add((world.game.session as StoryGameSession).saveState.regionStates[i].regionName.ToLowerInvariant());
			}
			else if (world.game.GetStorySession.saveState.regionLoadStrings[i] != null)
			{
				string[] array = Regex.Split(Regex.Split(world.game.GetStorySession.saveState.regionLoadStrings[i], "<rgA>")[0], "<rgB>");
				list3.Add(array[1].ToLowerInvariant());
			}
		}
		List<string> list4 = new List<string>();
		if (world.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility)
		{
			List<string> list5 = world.game.GetStorySession.saveState.RoomsWithWarpsRemainingToBeSealed(includeSpinningTopWarps: true);
			for (int j = 0; j < list5.Count; j++)
			{
				if (list5[j].Contains("_"))
				{
					string text2 = list5[j].Split(new char[1] { '_' })[0].ToLowerInvariant();
					if (!list4.Contains(text2) && text2 != world.name.ToLowerInvariant() && world.game.rainWorld.regionDynamicWarpTargets.ContainsKey(text2) && world.game.rainWorld.regionDynamicWarpTargets[text2].Count > 0)
					{
						list4.Add(text2);
					}
				}
			}
		}
		bool flag = world.game.GetStorySession.saveState.miscWorldSaveData.highestPrinceConversationSeen >= PrinceBehavior.PrinceConversation.PrinceConversationToId(WatcherEnums.ConversationID.Prince_7);
		foreach (KeyValuePair<float, List<string>> item in Custom.rainWorld.levelDynamicWarpTargets.OrderBy((KeyValuePair<float, List<string>> x) => x.Key))
		{
			foreach (string item2 in item.Value.OrderBy((string x) => x))
			{
				if (oldRoom != null && item2.ToLowerInvariant() == oldRoom.ToLowerInvariant())
				{
					continue;
				}
				if (targetRegion != null)
				{
					if (!item2.ToLowerInvariant().StartsWith(targetRegion.ToLowerInvariant()))
					{
						continue;
					}
				}
				else if (item2.ToLowerInvariant().StartsWith(world.name.ToLowerInvariant()) || item.Key == 999f)
				{
					continue;
				}
				if (Custom.rainWorld.levelBadWarpTargets.Contains(item2))
				{
					continue;
				}
				if (rippleLevel < item.Key && item.Key > 1f && !world.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility)
				{
					if (text == null)
					{
						text = item2;
					}
					continue;
				}
				bool flag2 = false;
				foreach (KeyValuePair<string, string> discoveredWarpPoint in world.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints)
				{
					string text3 = RoomFromIdentifyingString(discoveredWarpPoint.Key);
					if (item2.ToLowerInvariant() == text3.ToLowerInvariant())
					{
						flag2 = true;
						text = item2;
						break;
					}
				}
				if (flag2)
				{
					continue;
				}
				foreach (KeyValuePair<string, string> spawnedWarpPoint in world.game.GetStorySession.saveState.deathPersistentSaveData.spawnedWarpPoints)
				{
					string text4 = RoomFromIdentifyingString(spawnedWarpPoint.Key);
					if (item2.ToLowerInvariant() == text4.ToLowerInvariant())
					{
						flag2 = true;
						text = item2;
						break;
					}
				}
				if (flag2)
				{
					continue;
				}
				int num = 1;
				if (rippleLevel - item.Key < 0.5f)
				{
					num = 4;
				}
				else if (rippleLevel - item.Key < 1f)
				{
					num = 3;
				}
				else if (rippleLevel - item.Key < 1.5f)
				{
					num = 2;
				}
				if (targetRegion == null)
				{
					string text5 = "";
					if (item2.Contains("_"))
					{
						text5 = item2.Split(new char[1] { '_' })[0].ToLowerInvariant();
					}
					if (text5 != "")
					{
						if (spreadingRot)
						{
							if (!world.game.GetStorySession.saveState.miscWorldSaveData.regionsInfectedBySentientRot.Contains(text5) && !Region.HasSentientRotResistance(text5))
							{
								num *= 8;
								list2.Add(item2);
							}
						}
						else if (!list3.Contains(text5))
						{
							num *= 8;
						}
					}
					if (world.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility)
					{
						num = 1;
						if (list4.Count > 0 && !list4.Contains(text5))
						{
							continue;
						}
					}
				}
				for (int num2 = 0; num2 < num; num2++)
				{
					list.Add(item2);
				}
			}
		}
		if ((spreadingRot & flag) && list2.Count > 0 && !Custom.rainWorld.progression.miscProgressionData.beaten_Watcher_SentientRot)
		{
			list = list2;
		}
		if (list.Count == 0 && text != null)
		{
			list.Add(text);
		}
		return list;
	}

	public static List<string> GetAvailableBadWarpTargets(World world, string oldRoom)
	{
		List<string> list = new List<string>();
		foreach (string levelBadWarpTarget in Custom.rainWorld.levelBadWarpTargets)
		{
			if (!levelBadWarpTarget.ToLowerInvariant().StartsWith("wora") && (oldRoom == null || !(levelBadWarpTarget.ToLowerInvariant() == oldRoom.ToLowerInvariant())))
			{
				list.Add(levelBadWarpTarget);
			}
		}
		return list;
	}

	public static List<string> GetAvailableOuterRimWarpTargets(World world, string oldRoom, bool forceUseMax)
	{
		List<string> list = new List<string>();
		float num = 5f;
		if (world.game.GetStorySession.saveState.miscWorldSaveData.numberOfPrinceEncounters == 0)
		{
			num = 2f;
		}
		else if (world.game.GetStorySession.saveState.miscWorldSaveData.numberOfPrinceEncounters == 1)
		{
			num = 3f;
		}
		else if (world.game.GetStorySession.saveState.miscWorldSaveData.numberOfPrinceEncounters == 2)
		{
			num = 4f;
		}
		if (forceUseMax)
		{
			num = 5f;
		}
		float num2 = 10f;
		float num3 = 0f;
		foreach (KeyValuePair<float, List<string>> levelDynamicWarpTarget in Custom.rainWorld.levelDynamicWarpTargets)
		{
			float num4 = Math.Abs(num - levelDynamicWarpTarget.Key);
			float num5 = num - levelDynamicWarpTarget.Key;
			for (int i = 0; i < levelDynamicWarpTarget.Value.Count; i++)
			{
				if (levelDynamicWarpTarget.Value[i].ToLowerInvariant().StartsWith("wora") && (oldRoom == null || !(levelDynamicWarpTarget.Value[i].ToLowerInvariant() == oldRoom.ToLowerInvariant())) && Custom.rainWorld.levelBadWarpTargets.Contains(levelDynamicWarpTarget.Value[i]) && num4 <= num2)
				{
					if (num4 < num2)
					{
						list.Clear();
					}
					if (num4 == num2 && Mathf.Sign(num5) >= 0f && num3 < 0f)
					{
						list.Clear();
					}
					if (num4 != num2 || !(Mathf.Sign(num5) < 0f) || !(num3 >= 0f))
					{
						list.Add(levelDynamicWarpTarget.Value[i]);
						num2 = num4;
						num3 = Mathf.Sign(num5);
					}
				}
			}
		}
		return list;
	}

	[Obsolete("Use World and roomName parameter function instead")]
	public static List<string> GetAvailableDynamicWarpTargets(AbstractRoom room, string targetRegion, bool spreadingRot)
	{
		return GetAvailableDynamicWarpTargets(room.world, room.name, targetRegion, spreadingRot);
	}

	[Obsolete("Use World and roomName parameter function instead")]
	public static List<string> GetAvailableBadWarpTargets(AbstractRoom room)
	{
		return GetAvailableBadWarpTargets(room.world, room.name);
	}

	[Obsolete("Use World and roomName parameter function instead")]
	public static List<string> GetAvailableOuterRimWarpTargets(AbstractRoom room, bool forceUseMax)
	{
		return GetAvailableOuterRimWarpTargets(room.world, room.name, forceUseMax);
	}

	public void SetRoom(Room newRoom)
	{
		room = newRoom;
		if (room.world.region != null)
		{
			sourceRegion = room.world.region.name;
		}
		else
		{
			sourceRegion = room.world.name;
		}
	}

	public void PerformWarp()
	{
		activated = false;
		if (room.roomSettings.GetEffectAmount(WatcherEnums.RoomEffectType.SentientRotInfection) > 0f || Region.IsSentientRotRegion(room.world.name))
		{
			room.game.GetStorySession.pendingSentientRotInfectionFromWarp = true;
		}
		if (room.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility)
		{
			room.game.GetStorySession.pendingSentientRotInfectionFromWarp = false;
		}
		if (Data.limitedUse && Data.uses > 0)
		{
			Data.uses--;
		}
		bool flag = ModManager.MMF && MMF.cfgVanillaExploits.Value;
		if (room.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility && ((CanPlayWarpSealingAnimation && !flag) || (Data.oneWayEntrance && Data.nonDynamicWarpPoint)))
		{
			ExpireWarpByWeaver();
		}
		if (room.game.GetStorySession.saveState.miscWorldSaveData.cycleFirstStartedWarpJourney == 0)
		{
			room.game.GetStorySession.saveState.miscWorldSaveData.cycleFirstStartedWarpJourney = room.game.GetStorySession.saveState.cycleNumber;
		}
		if (room.game.StoryCharacter == WatcherEnums.SlugcatStatsName.Watcher)
		{
			if (room.game.GetStorySession.saveState.deathPersistentSaveData.maximumRippleLevel < 1f)
			{
				room.game.GetStorySession.saveState.deathPersistentSaveData.maximumRippleLevel = 1f;
			}
			if (room.game.GetStorySession.saveState.deathPersistentSaveData.minimumRippleLevel < 1f)
			{
				room.game.GetStorySession.saveState.deathPersistentSaveData.minimumRippleLevel = 1f;
			}
			if (room.game.GetStorySession.saveState.deathPersistentSaveData.rippleLevel < 1f)
			{
				room.game.GetStorySession.saveState.deathPersistentSaveData.rippleLevel = 1f;
			}
		}
		room.game.GetStorySession.saveState.currentTimelinePosition = Data.destTimeline;
		room.game.overWorld.readyForWarp = true;
	}

	public void LockWarp(bool withAnimation)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		if (!warpLocked)
		{
			warpLocked = true;
			refreshGraphics = true;
			ExpireWarpByWeaver();
			if (!withAnimation && warpTear != null)
			{
				warpTear.weaverBlockAnimation = 1f;
				warpTear.openAnimation = 1f;
			}
			weaverThreadAnimTime = 300;
			weaverAnimationsStarts.Clear();
			weaverAnimationsEnds.Clear();
			weaverThreadAbsPos.Clear();
			int num = Random.Range(12, 16);
			float num2 = badWarpRadiusMax + 60f;
			State state = Random.state;
			Random.InitState(room.abstractRoom.index);
			for (int i = 0; i < num; i++)
			{
				float num3 = 180f / (float)num * (float)i + (float)Random.Range(-20, 20);
				Vector2 val = Custom.DegToVec(num3) * (num2 + (float)Random.Range(-20, 20));
				Vector2 val2 = Custom.DegToVec(num3 + 180f) * (num2 + (float)Random.Range(-20, 20));
				bool flag = i % 2 == 0;
				weaverAnimationsStarts.Add(flag ? val : val2);
				weaverAnimationsEnds.Add(flag ? val2 : val);
				weaverThreadAbsPos.Add(placedObject.pos + Custom.RNV() * 40f);
			}
			warpTear?.GetThreadAnimation(weaverAnimationsStarts, weaverAnimationsEnds, weaverThreadAbsPos);
			weaverAnimationTime = (withAnimation ? (weaverAnimationsStarts.Count + weaverThreadAnimTime) : (-1));
			totalWeaverAnimationTime = weaverAnimationTime;
			weaverAnimationStarted = withAnimation;
			Random.state = state;
			room.game.warpDeferPlayerSpawnRoomName = "";
			room.game.warpDeferID = "";
		}
	}

	public void UpdateWeaverAnimation()
	{
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_0465: Unknown result type (might be due to invalid IL or missing references)
		//IL_0472: Unknown result type (might be due to invalid IL or missing references)
		//IL_047c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Unknown result type (might be due to invalid IL or missing references)
		//IL_0493: Unknown result type (might be due to invalid IL or missing references)
		//IL_049a: Unknown result type (might be due to invalid IL or missing references)
		//IL_049f: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_04dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0366: Unknown result type (might be due to invalid IL or missing references)
		//IL_0373: Unknown result type (might be due to invalid IL or missing references)
		//IL_037d: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0394: Unknown result type (might be due to invalid IL or missing references)
		//IL_039b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		SaveState saveState = room.game.GetStorySession.saveState;
		bool flag = weaverAnimationTime == -1 && saveState.cycleNumber == Data.weaverTriggeredCycle && saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Contains(room.abstractRoom.name.ToLowerInvariant()) && !saveState.miscWorldSaveData.roomsSealedByVoidWeaver.Contains(room.abstractRoom.name.ToLowerInvariant());
		if (!(weaverAnimationStarted | flag))
		{
			return;
		}
		float num = totalWeaverAnimationTime - weaverAnimationTime;
		float num2 = weaverAnimationsStarts.Count;
		float num3 = 1f;
		float weaverBlockAnimation = 1f;
		warpTear.moveBranchToDefaultPos = 1f;
		if (totalWeaverAnimationTime > 0)
		{
			warpTear.openAnimation = 1f;
			num3 = Mathf.InverseLerp(0f, num2, num);
			weaverBlockAnimation = Mathf.InverseLerp(num2, (float)totalWeaverAnimationTime, num);
			warpTear.moveBranchToDefaultPos = Mathf.InverseLerp(0f, (float)weaverThreadAnimTime * 0.25f + num2, num);
		}
		if (num == 60f)
		{
			room.PlaySound(WatcherEnums.WatcherSoundID.Void_Weaver_Ability_Close_Portal, pos);
		}
		warpTear.weaverBlockAnimation = weaverBlockAnimation;
		for (int i = 0; (float)i < num2; i++)
		{
			bool flag2 = totalWeaverAnimationTime < 0;
			if ((flag2 || num3 > 1f / num2 * (float)i) && i >= weaverThreads.Count)
			{
				Vector2 spawnPosA = weaverThreadAbsPos[i] + weaverAnimationsStarts[i] * 1.6f;
				Vector2 spawnPosB = weaverThreadAbsPos[i] + weaverAnimationsEnds[i] * 1.6f;
				WeaverThread weaverThread = new WeaverThread(room, Mathf.Max(400f, Vector2.Distance(weaverAnimationsStarts[i], weaverAnimationsEnds[i])), spawnPosA, spawnPosB, stuckAtA: true, stuckAtB: true, warpPointThread: true)
				{
					bothSides = true,
					conRad = 2f
				};
				if (flag2)
				{
					weaverThread.stuckPosA = weaverThreadAbsPos[i] + weaverAnimationsStarts[i] * 0.6f;
					weaverThread.stuckPosB = weaverThreadAbsPos[i] + weaverAnimationsEnds[i] * 0.6f;
				}
				weaverThread.graphic.startAnimation = (flag2 ? 1 : 0);
				room.AddObject(weaverThread);
				if (flag)
				{
					weaverThread.instantSettle = true;
				}
				if (totalWeaverAnimationTime > 0)
				{
					room.PlaySound(WatcherEnums.WatcherSoundID.Void_Weaver_Ability_Spawn_Thread, spawnPosA);
				}
				weaverThreads.Add(weaverThread);
			}
			if (totalWeaverAnimationTime > 0 && i < weaverThreads.Count)
			{
				WeaverThread weaverThread2 = weaverThreads[i];
				float num4 = Mathf.InverseLerp((float)i, (float)(i + weaverThreadAnimTime), num);
				if (num4 < 0.25f)
				{
					float num5 = Mathf.InverseLerp(0f, 0.25f, num4);
					weaverThread2.stuckPosA = weaverThreadAbsPos[i] + Vector2.Lerp(weaverAnimationsStarts[i] * 1.6f, weaverAnimationsStarts[i] * 1.2f, num5);
					weaverThread2.stuckPosB = weaverThreadAbsPos[i] + Vector2.Lerp(weaverAnimationsEnds[i] * 0.6f, weaverAnimationsEnds[i] * 1.2f, num5);
					weaverThread2.graphic.startAnimation = Mathf.Clamp01(num5);
					weaverThread2.graphic.startAnimation = Mathf.Clamp01(num5);
					weaverThread2.conRad = 8f;
				}
				else
				{
					float num6 = Custom.LerpQuadEaseInOut(0f, 1f, Mathf.InverseLerp(0.25f, 1f, num4));
					weaverThread2.stuckPosA = weaverThreadAbsPos[i] + Vector2.Lerp(weaverAnimationsStarts[i] * 1.2f, weaverAnimationsStarts[i] * 0.6f, num6);
					weaverThread2.stuckPosB = weaverThreadAbsPos[i] + Vector2.Lerp(weaverAnimationsEnds[i] * 1.2f, weaverAnimationsEnds[i] * 0.6f, num6);
					weaverThread2.conRad = 8f - Mathf.Clamp01(num6 * 2f) * 6f;
				}
			}
		}
		if (weaverAnimationTime > 0)
		{
			weaverAnimationTime--;
		}
		else
		{
			weaverAnimationTime = 0;
		}
		if (weaverAnimationTime == 0)
		{
			weaverAnimationStarted = false;
		}
	}

	public void ExpireWarpByWeaver()
	{
		//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0471: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_037d: Unknown result type (might be due to invalid IL or missing references)
		//IL_039e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
		if (((Data.nonDynamicWarpPoint || Data.wasNonDynamicWarpBeforeWeaverTriggered) && (Region.IsWatcherVanillaRegion(room.world.name) || Region.IsVanillaSentientRotRegion(room.world.name))) || Data.rippleWarp || (Data.limitedUse && Data.uses <= 1) || !RoomCanBeSealed(room.abstractRoom.name) || (Data.destRoom != null && !RoomCanBeSealed(Data.destRoom)) || (Data.nonDynamicWarpPoint && !room.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility))
		{
			return;
		}
		bool flag = Data.weaverTriggeredCycle > 0;
		if (!flag && !Data.ExpiredOnCycle(room.game.GetStorySession.saveState.cycleNumber))
		{
			Data.wasNonDynamicWarpBeforeWeaverTriggered = Data.nonDynamicWarpPoint;
			Data.weaverTriggeredCycle = room.game.GetStorySession.saveState.cycleNumber;
			Data.cycleSpawnedOn = room.game.GetStorySession.saveState.cycleNumber - 2;
			Data.cycleExpiry = 1;
		}
		if (!Data.wasNonDynamicWarpBeforeWeaverTriggered && !Data.nonDynamicWarpPoint)
		{
			return;
		}
		if (!room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Contains(room.abstractRoom.name.ToLowerInvariant()))
		{
			room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Add(room.abstractRoom.name.ToLowerInvariant());
		}
		if (Data.destRoom != null && !room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Contains(Data.destRoom.ToLowerInvariant()))
		{
			room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Add(Data.destRoom.ToLowerInvariant());
		}
		if (room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByVoidWeaver.Contains(room.abstractRoom.name.ToLowerInvariant()))
		{
			int num = 15;
			float maxLength = 500f;
			float minLength = 50f;
			float num2 = 200f;
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < 2; j++)
				{
					Vector2 center = Vector2.Lerp(pos + Custom.DegToVec((j == 0) ? 45f : 135f) * 1200f, pos + Custom.DegToVec((j == 0) ? 225f : 315f) * 1200f, (float)i / (float)num);
					if (room.FindFreePoint(center, num2, 5, out var point) && room.FindThreadEnds(point, minLength, maxLength, 20, out var start, out var end))
					{
						float length = Vector2.Distance(start, end) * Random.Range(0.5f, 1f);
						WeaverThread weaverThread = new WeaverThread(room, length, start, end, stuckAtA: true, stuckAtB: true);
						weaverThread.bothSides = true;
						room.AddObject(weaverThread);
					}
				}
			}
		}
		else
		{
			if (flag || (room.world.game.cameras[0].warpPointTimer?.origin_warpPoint == this && (!ModManager.MMF || !MMF.cfgVanillaExploits.Value)))
			{
				return;
			}
			List<string> list = room.game.GetStorySession.saveState.RoomsWithWarpsRemainingToBeSealed(Custom.rainWorld.progression.miscProgressionData.beaten_Watcher_SpinningTop);
			if (list.Count <= 2)
			{
				return;
			}
			list.Sort();
			State state = Random.state;
			Random.InitState(Custom.rainWorld.progression.miscProgressionData.watcherCampaignSeed + list.Count);
			string text = list[Random.Range(0, list.Count)].ToLowerInvariant();
			Random.state = state;
			string text2 = null;
			foreach (KeyValuePair<string, string> spawnedWarpPoint in room.game.GetStorySession.saveState.deathPersistentSaveData.spawnedWarpPoints)
			{
				if (RoomFromIdentifyingString(spawnedWarpPoint.Key).ToLowerInvariant() == text)
				{
					PlacedObject obj = new PlacedObject(PlacedObject.Type.WarpPoint, null);
					string[] s = Regex.Split(spawnedWarpPoint.Value.Trim(), "><");
					(obj.data as WarpPointData).owner.FromString(s);
					text2 = (obj.data as WarpPointData).destRoom;
					break;
				}
			}
			if (text2 == null)
			{
				foreach (KeyValuePair<string, string> discoveredWarpPoint in room.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints)
				{
					if (RoomFromIdentifyingString(discoveredWarpPoint.Key).ToLowerInvariant() == text)
					{
						PlacedObject obj2 = new PlacedObject(PlacedObject.Type.WarpPoint, null);
						string[] s2 = Regex.Split(discoveredWarpPoint.Value.Trim(), "><");
						(obj2.data as WarpPointData).owner.FromString(s2);
						text2 = (obj2.data as WarpPointData).destRoom;
						break;
					}
				}
			}
			if (text2 == null)
			{
				foreach (KeyValuePair<string, List<string>> regionWarpRoom in Custom.rainWorld.regionWarpRooms)
				{
					for (int k = 0; k < regionWarpRoom.Value.Count; k++)
					{
						string[] array = regionWarpRoom.Value[k].Split(new char[1] { ':' });
						if (array[0].ToLowerInvariant() == text && array.Length > 3)
						{
							text2 = array[3];
							break;
						}
					}
					if (text2 != null)
					{
						break;
					}
				}
			}
			if (text2 == null && Custom.rainWorld.progression.miscProgressionData.beaten_Watcher_SpinningTop)
			{
				foreach (KeyValuePair<string, List<string>> regionSpinningTopRoom in Custom.rainWorld.regionSpinningTopRooms)
				{
					for (int l = 0; l < regionSpinningTopRoom.Value.Count; l++)
					{
						string[] array2 = regionSpinningTopRoom.Value[l].Split(new char[1] { ':' });
						if (array2[0].ToLowerInvariant() == text && array2.Length > 2)
						{
							text2 = array2[2];
							break;
						}
					}
					if (text2 != null)
					{
						break;
					}
				}
			}
			if (text2 != null)
			{
				if (!room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByVoidWeaver.Contains(text))
				{
					room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByVoidWeaver.Add(text);
				}
				if (!room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByVoidWeaver.Contains(text2.ToLowerInvariant()))
				{
					room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByVoidWeaver.Add(text2.ToLowerInvariant());
				}
			}
			string[] array3 = new string[3] { "ward_r10", "wssr_cramped", "wssr_lab6" };
			for (int m = 0; m < array3.Length; m++)
			{
				if (!room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByVoidWeaver.Contains(array3[m]) && !room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Contains(array3[m]))
				{
					room.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByVoidWeaver.Add(array3[m]);
				}
			}
		}
	}

	public bool CheckCanWarpToVoidWeaverEnding()
	{
		SaveState saveState = room.game.GetStorySession.saveState;
		if (saveState.miscWorldSaveData.hasVoidWeaverAbility && !saveState.progression.miscProgressionData.beaten_Watcher_VoidWeaver && saveState.RoomsWithWarpsRemainingToBeSealed(includeSpinningTopWarps: true).Count == 0 && !Data.rippleWarp && Data.destRoom != null)
		{
			return !isARestoralPoint;
		}
		return false;
	}

	public void ActivateWeaver()
	{
		bool flag = false;
		bool flag2 = false;
		foreach (KeyValuePair<string, string> discoveredWarpPoint in room.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints)
		{
			PlacedObject placedObject = new PlacedObject(PlacedObject.Type.WarpPoint, null);
			string[] s = Regex.Split(discoveredWarpPoint.Value.Trim(), "><");
			(placedObject.data as WarpPointData).owner.FromString(s);
			if ((placedObject.data as WarpPointData).uuidPair == Data.uuidPair && (placedObject.data as WarpPointData).destRoom != Data.destRoom)
			{
				flag = true;
				if ((placedObject.data as WarpPointData).weaverTriggeredCycle > 0)
				{
					flag2 = true;
				}
				break;
			}
		}
		if (!flag | flag2)
		{
			if (!warpSequenceInProgress)
			{
				deleteNextFrame = true;
			}
		}
		else
		{
			if (!room.world.InitiateWeaverPresence(room.abstractRoom))
			{
				return;
			}
			if (Data.weaverTriggeredCycle <= 0)
			{
				Data.wasNonDynamicWarpBeforeWeaverTriggered = Data.nonDynamicWarpPoint;
			}
			Data.weaverTriggeredCycle = room.game.GetStorySession.saveState.cycleNumber;
			string text = null;
			string text2 = null;
			foreach (KeyValuePair<string, string> discoveredWarpPoint2 in room.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints)
			{
				PlacedObject placedObject2 = new PlacedObject(PlacedObject.Type.WarpPoint, null);
				string[] s2 = Regex.Split(discoveredWarpPoint2.Value.Trim(), "><");
				(placedObject2.data as WarpPointData).owner.FromString(s2);
				if ((placedObject2.data as WarpPointData).uuidPair == Data.uuidPair && (placedObject2.data as WarpPointData).destRoom != Data.destRoom)
				{
					text = discoveredWarpPoint2.Key;
					if ((placedObject2.data as WarpPointData).weaverTriggeredCycle <= 0)
					{
						(placedObject2.data as WarpPointData).wasNonDynamicWarpBeforeWeaverTriggered = (placedObject2.data as WarpPointData).nonDynamicWarpPoint;
					}
					(placedObject2.data as WarpPointData).weaverTriggeredCycle = room.game.GetStorySession.saveState.cycleNumber;
					text2 = placedObject2.data.owner.ToString();
					break;
				}
			}
			if (text != null && text2 != null)
			{
				room.game.GetStorySession.saveState.miscWorldSaveData.discoveredWarpPoints[text] = text2;
			}
			room.game.RequestUnloadAllRoomsExcept(room);
			if (room.game.GetStorySession.saveState.miscWorldSaveData.numberOfVoidWeaverEncounters == 0 && room.game.manager.musicPlayer != null && room.game.world.rainCycle.MusicAllowed)
			{
				MusicEvent musicEvent = new MusicEvent();
				musicEvent.cyclesRest = 5;
				musicEvent.stopAtDeath = true;
				musicEvent.stopAtGate = true;
				musicEvent.songName = "RW_162 - Weaver Energy";
				room.game.manager.musicPlayer.GameRequestsSong(musicEvent);
			}
		}
	}

	public void WarpPrecast()
	{
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		canPreCast = false;
		if (Data.rippleEggWarpPoint)
		{
			room.game.overWorld.InitiateSpecialWarp_WarpPoint(this, Data, useNormalWarpLoader: false);
			room.game.cameras[0].WarpMoveCameraPrecast(Data.destRoom, Data.destCam);
			if (room.game.manager.musicPlayer != null)
			{
				room.game.manager.musicPlayer.GateEvent();
			}
		}
		else if ((room.game.overWorld.warpWorldLoader == null || room.game.overWorld.warpWorldLoader.Finished) && room.game.overWorld.activeWorld == room.world)
		{
			if (room.game.manager.musicPlayer != null)
			{
				room.game.manager.musicPlayer.GateEvent();
			}
			canWarpToVoidWeaverEnding = CheckCanWarpToVoidWeaverEnding();
			if (canWarpToVoidWeaverEnding)
			{
				overrideData = new WarpPointData(null);
				overrideData.destPos = new Vector2(-5200f, 320f);
				overrideData.destRoom = "WRSA_WEAVER";
				overrideData.destTimeline = room.game.TimelinePoint;
				overrideData.panelPos = Vector2.zero;
				overrideData.cycleExpiry = 1;
				overrideData.cycleSpawnedOn = room.game.GetStorySession.saveState.cycleNumber;
				overrideData.destCam = 0;
			}
			else if (room.game.GetStorySession.warpTraversalsLeftUntilFullWarpFatigue == 0 && !Data.rippleWarp && Data.RegionString != null && Data.destRoom != null && !isARestoralPoint && !Region.IsSentientRotRegion(room.world.name))
			{
				string chosenRoom = ChooseDynamicWarpTarget(room.world, room.abstractRoom.name, null, badWarp: true, spreadingRot: false, playerCreated: false);
				overrideData = CreateOverrideData(room.world, room.abstractRoom.name, chosenRoom, null, !ModManager.MMF || !MMF.cfgVanillaExploits.Value, playerCreated: false);
				room.game.GetStorySession.saveState.miscWorldSaveData.numberOfBadWarpsGenerated++;
				Custom.rainWorld.processManager.CueAchievement(RainWorld.AchievementID.BadWarp, 10f);
			}
			if (isARestoralPoint)
			{
				isARestoralPoint = false;
			}
			room.game.overWorld.InitiateSpecialWarp_WarpPoint(this, (overrideData != null) ? overrideData : Data, useNormalWarpLoader: false);
			room.game.cameras[0].WarpMoveCameraPrecast((overrideData != null) ? overrideData.destRoom : Data.destRoom, (overrideData != null) ? overrideData.destCam : Data.destCam);
		}
	}

	public static void ResetShaderParams()
	{
		Shader.SetGlobalFloat("_warpPointAcceleration", 0f);
		Shader.SetGlobalFloat("_playerWarpPointTime", -1f);
		Shader.SetGlobalFloat("_warpPointTime", -1f);
	}

	public static int GetDestCam(WarpPointData data)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		if (!data.destPos.HasValue)
		{
			return 0;
		}
		Vector2 value = data.destPos.Value;
		string text = WorldLoader.FindRoomFile(data.destRoom, includeRootDirectory: false, ".txt");
		if (text == null)
		{
			return 0;
		}
		string[] roomText = File.ReadAllLines(text);
		RoomPreprocessor.VersionFix(ref roomText);
		int num = Convert.ToInt32(roomText[1].Split(new char[1] { '|' })[0].Split(new char[1] { '*' })[1], CultureInfo.InvariantCulture);
		string[] array = roomText[3].Split(new char[1] { '|' });
		Vector2 val = default(Vector2);
		for (int i = 0; i < array.Length; i++)
		{
			((Vector2)(ref val))..ctor(Convert.ToSingle(array[i].Split(new char[1] { ',' })[0], CultureInfo.InvariantCulture), 0f - (800f - (float)num * 20f + Convert.ToSingle(array[i].Split(new char[1] { ',' })[1])));
			if (value.x > val.x && value.x < val.x + 1366f && value.y > val.y && value.y < val.y + 768f)
			{
				return i;
			}
		}
		return 0;
	}

	public void RoomUnloaded()
	{
		if (!thisWarpSequenceInProgress)
		{
			Destroy();
		}
	}

	public override void Destroy()
	{
		base.Destroy();
		if (warpTear != null)
		{
			warpTear.FadeOut(40f);
			warpTear.deleteWhenClosed = true;
		}
		if (room?.game.overWorld.warpWorldLoader != null && room.game.overWorld.specialWarpCallback == this && !thisWarpSequenceInProgress)
		{
			room.game.overWorld.warpWorldLoader.ReturnWorld()?.regionState?.AdaptRegionStateToWorld(-1, -1);
			room.game.overWorld.warpWorldLoader = null;
		}
	}

	public override void Update(bool eu)
	{
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
		//IL_0424: Unknown result type (might be due to invalid IL or missing references)
		//IL_0462: Unknown result type (might be due to invalid IL or missing references)
		//IL_0467: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_07e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_07e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_087e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0883: Unknown result type (might be due to invalid IL or missing references)
		//IL_089e: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bd4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0be0: Unknown result type (might be due to invalid IL or missing references)
		//IL_1065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e30: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e3c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b22: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b2e: Unknown result type (might be due to invalid IL or missing references)
		//IL_12ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_12c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b55: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b61: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c41: Unknown result type (might be due to invalid IL or missing references)
		//IL_148f: Unknown result type (might be due to invalid IL or missing references)
		if (!wasDeferredSpawn)
		{
			wasDeferredSpawn = room.game.warpDeferPlayerSpawnRoomName == room.abstractRoom.name && room.game.warpDeferID == MyIdentifyingString();
		}
		else if (currentState != State.CoolDown && closesEarly && !room.BeingViewed)
		{
			closesEarly = false;
		}
		bool firstFrame = warpTear == null;
		UpdateWarpTear();
		SoundMode soundMode = SoundMode.Idle;
		bool flag = false;
		if (room.game.ActiveRippleLayer == 1 != Data.rippleWarp && (!Data.limitedUse || Data.uses > 0) && !Data.oneWayExit)
		{
			flag = true;
			shockWaveTimer += 1f / 60f;
			if (shockWaveTimer >= shockWaveTimerLimit && !secondShockwave)
			{
				secondShockwave = true;
				room.AddObject(new RippleRing(placedObject.pos, Random.Range(80, 260), 1f, Random.Range(0.7f, 1f)));
				room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Ripple_Hint, placedObject.pos, 1f, Random.Range(0.8f, 1.2f));
			}
			else if (shockWaveTimer >= shockWaveTimerLimit + 0.04f && secondShockwave)
			{
				shockWaveTimerLimit = Random.Range(2f, 6f);
				shockWaveTimer = 0f;
				secondShockwave = false;
				room.AddObject(new RippleRing(placedObject.pos, Random.Range(80, 260), 1f, Random.Range(0.7f, 1f)));
			}
		}
		if (activated)
		{
			soundMode = SoundMode.SuckIn;
		}
		if (soundLoop == null || soundMode != currentSoundLoopMode)
		{
			SoundID soundID = WatcherEnums.WatcherSoundID.Warp_Point_Normal_Idle_LOOP;
			if (soundMode == SoundMode.Idle)
			{
				soundID = (Data.rippleWarp ? WatcherEnums.WatcherSoundID.Warp_Point_Ripple_Idle_LOOP : (Data.oneWayExit ? WatcherEnums.WatcherSoundID.Warp_Point_One_Way_Exit_Idle_LOOP : ((!Data.limitedUse) ? WatcherEnums.WatcherSoundID.Warp_Point_Normal_Idle_LOOP : WatcherEnums.WatcherSoundID.Warp_Point_Bad_Idle_LOOP)));
			}
			else if (soundMode == SoundMode.SuckIn)
			{
				soundID = (Data.rippleWarp ? WatcherEnums.WatcherSoundID.Warp_Point_Ripple_Suck_In_Objects_LOOP : ((!Data.limitedUse) ? WatcherEnums.WatcherSoundID.Warp_Point_Normal_Suck_In_Objects_LOOP : WatcherEnums.WatcherSoundID.Warp_Point_Bad_Suck_In_Objects_LOOP));
			}
			currentSoundLoopMode = soundMode;
			soundLoop = new StaticSoundLoop(soundID, placedObject.pos, room, 1f, 1f)
			{
				fadeOutOnDestroyFrames = 320
			};
		}
		if (soundLoop != null)
		{
			soundLoop.Update();
			soundLoop.pos = placedObject.pos;
			if (warpLocked)
			{
				soundLoop.volume = Mathf.Lerp(soundLoop.volume, 0f, 0.08f);
			}
			else if (soundMode == SoundMode.SuckIn)
			{
				soundLoop.volume = Mathf.Lerp(soundLoop.volume, 1f, 0.08f);
			}
			else if (flag)
			{
				soundLoop.volume = Mathf.Lerp(soundLoop.volume, 0f, 0.08f);
			}
			else if (soundMode == SoundMode.Idle)
			{
				soundLoop.volume = Mathf.Lerp(soundLoop.volume, warpTear?.openAnimation ?? 0f, 0.08f);
			}
		}
		if ((rippleHintSoundLoop == null) & flag)
		{
			rippleHintSoundLoop = new StaticSoundLoop(WatcherEnums.WatcherSoundID.Warp_Point_Ripple_Hint_LOOP, placedObject.pos, room, 1f, 1f);
		}
		if (rippleHintSoundLoop != null)
		{
			rippleHintSoundLoop.Update();
			rippleHintSoundLoop.pos = placedObject.pos;
			rippleHintSoundLoop.volume = Mathf.Lerp(rippleHintSoundLoop.volume, flag ? 1f : 0f, 0.08f);
		}
		if (warpLocked)
		{
			playOpenSoundAfterDelay = 0;
		}
		if (playOpenSoundAfterDelay > 0)
		{
			playOpenSoundAfterDelay--;
			if (playOpenSoundAfterDelay == 0 && warpTearOpenTriggered)
			{
				room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Open, pos);
			}
		}
		if (startingBadWarpAnim)
		{
			deletable = false;
			badWarpStartTime += 1f / 60f;
			if ((double)badWarpStartTime >= 1.1)
			{
				startingBadWarpAnim = false;
				badWarpAnimStarted = true;
				badWarpStartTime = 0f;
			}
		}
		if (badWarpAnimStarted)
		{
			badWarpLerpTime += 0.0050505055f;
			ripple.scale = Mathf.Lerp(badWarpRadiusMin, badWarpRadiusMax, EaseInOutElastic(badWarpLerpTime));
			if (badWarpLerpTime >= 1f)
			{
				bool flag2 = true;
				for (int i = 0; i < room.game.Players.Count; i++)
				{
					if (room.game.Players[i].realizedCreature != null)
					{
						if (!wasDeferredSpawn)
						{
							AddPlayerBackIn(i, placeInRoom: true);
						}
						if (flag2)
						{
							(room.game.Players[i].realizedCreature as Player).PrimeOneWayExit(room);
						}
						flag2 = false;
					}
				}
				badWarpAnimStarted = false;
				room.game.warpDeferPlayerSpawnRoomName = "";
				room.game.warpDeferID = "";
			}
		}
		else if (badWarpLerpTime != 0f)
		{
			badWarpLerpTime -= 1f / 24f;
			ripple.scale = Mathf.Lerp(0f, badWarpRadiusMax, easeInExpo(badWarpLerpTime));
			if (badWarpLerpTime < 0f)
			{
				ripple.slatedForDeletetion = true;
				ripple.RemoveObject();
				room.RemoveObject(ripple);
				ripple = null;
				deletable = true;
				badWarpLerpTime = 0f;
			}
		}
		if (room.game.warpDeferPlayerSpawnRoomName == room.abstractRoom.name && room.game.warpDeferID == MyIdentifyingString())
		{
			for (int j = 0; j < room.game.Players.Count; j++)
			{
				if (room.game.Players[j].realizedCreature != null)
				{
					AddPlayerBackIn(j, placeInRoom: false);
				}
			}
			if (currentState == State.ReadyForWarp)
			{
				ChangeState(State.ExitWarp);
			}
			if (!warpTearOpenTriggered && !startingBadWarpAnim && !badWarpAnimStarted)
			{
				room.game.warpDeferPlayerSpawnRoomName = "";
				room.game.warpDeferID = "";
			}
		}
		base.Update(eu);
		pos = placedObject.pos;
		lastTriggerTime = triggerTime;
		if (!room.game.IsStorySession)
		{
			if (!base.slatedForDeletetion)
			{
				base.slatedForDeletetion = true;
				room.RemoveObject(this);
			}
			return;
		}
		if (deleteNextFrame)
		{
			SlateForDeletion();
			return;
		}
		if (transportable && Random.value < 0.0016666667f && room.game.cameras[0].room == room && Data.weaverTriggeredCycle <= 0)
		{
			room.MaterializeRippleSpawn(pos + Custom.RNV() * Random.Range(0f, radius * 0.8f), Room.RippleSpawnSource.WarpPoint);
		}
		ProvideAir();
		if (MyIdentifyingString() == room.game.GetStorySession.pendingWarpPointTransferId && (room.game.GetStorySession.pendingWarpPointSedateTime > 0 || badWarpAnimStarted || currentState == State.ExitWarp))
		{
			if (room.game.GetStorySession.pendingWarpPointSedateTime > 0)
			{
				room.game.GetStorySession.pendingWarpPointSedateTime--;
			}
			foreach (AbstractCreature creature in room.abstractRoom.creatures)
			{
				Creature realizedCreature = creature.realizedCreature;
				if (realizedCreature != null && (!ModManager.MSC || realizedCreature.abstractCreature.creatureTemplate.type != MoreSlugcatsEnums.CreatureTemplateType.SlugNPC) && realizedCreature.abstractCreature.creatureTemplate.type != CreatureTemplate.Type.Slugcat && realizedCreature.abstractCreature.creatureTemplate.type != CreatureTemplate.Type.Deer)
				{
					realizedCreature.stun = Math.Max(realizedCreature.stun, 20);
				}
			}
		}
		bool flag3 = nonPlayerRequestOpen != null && nonPlayerRequestOpen.Entering();
		if ((currentState == State.ReadyForWarp) & flag3)
		{
			if (!warpTearOpenTriggered)
			{
				playOpenSoundAfterDelay = 80;
			}
			warpTearOpenTriggered = true;
		}
		if (currentState == State.ReadyForWarp && !warpLocked && transportable)
		{
			Player player = null;
			for (int k = 0; k < room.game.Players.Count; k++)
			{
				if (room.game.Players[k].realizedCreature != null && room.game.Players[k].Room.name == room.abstractRoom.name && !room.game.Players[k].realizedCreature.dead)
				{
					Player player2 = room.game.Players[k].realizedCreature as Player;
					float num = Vector2.Distance(pos, player2.mainBodyChunk.pos);
					if (player2.warpPointCooldown <= 0 && player2.warpExhausionTime <= 0 && (player == null || num < Vector2.Distance(pos, player.mainBodyChunk.pos)))
					{
						player = player2;
					}
					if (player2.room != null && !player2.inShortcut && num < PullRadius && player2.warpPointCooldown <= 0 && player2.warpExhausionTime <= 0)
					{
						player2.standingInWarpPointProtectionTime = 10;
					}
				}
			}
			if (player != null)
			{
				float num2 = Vector2.Distance(pos, player.mainBodyChunk.pos);
				if (closesEarly && num2 > PullRadius * 6f)
				{
					closesEarly = false;
				}
				if (!flag3 && !(num2 < PullRadius * (closesEarly ? 2f : 4.5f)))
				{
					if (warpTearOpenTriggered)
					{
						room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Close, pos);
					}
					warpTearOpenTriggered = false;
				}
				else if (num2 < PullRadius * (closesEarly ? 1.5f : 3f))
				{
					if (!warpTearOpenTriggered)
					{
						playOpenSoundAfterDelay = 80;
					}
					warpTearOpenTriggered = true;
				}
				if (player.room != null && !player.inShortcut && num2 < PullRadius && (visualOpenness > 0.5f || guaranteeTrigger) && player.warpPointCooldown <= 0 && player.warpExhausionTime <= 0)
				{
					if (canPreCast && Region.RegionReadyToWarp && !Data.rippleEggWarpPoint)
					{
						WarpPrecast();
					}
					float num3 = 1f + Mathf.Pow(Mathf.InverseLerp(PullRadius, 10f, num2), 0.5f) * 3f;
					num3 = (guaranteeTrigger ? 1f : ((player.touchedNoInputCounter > 0) ? ((player.touchedNoInputCounter >= 20) ? (num3 + Custom.LerpMap(player.touchedNoInputCounter, 20f, 120f, 0f, 3f)) : (num3 * Custom.LerpMap(player.touchedNoInputCounter, 1f, 20f, 0.5f, 1f))) : ((!(triggerTime < triggerActivationTime / 2f)) ? Custom.LerpMap(triggerTime, triggerActivationTime / 2f, triggerActivationTime, -0.5f, -4f) : (num3 * 0.5f))));
					triggerTime += num3;
				}
				if (triggerTime >= triggerActivationTime)
				{
					playerTriggeredWarpPoint = player;
					canWarpToVoidWeaverEnding = CheckCanWarpToVoidWeaverEnding();
					ChangeState(State.EnterWarp);
				}
			}
			if (player == null || Vector2.Distance(pos, player.mainBodyChunk.pos) >= PullRadius)
			{
				if (player == null && !warpSequenceInProgress)
				{
					warpTearOpenTriggered = false;
				}
				if (triggerTime > 0f)
				{
					triggerTime -= 2f;
				}
				else
				{
					triggerTime = 0f;
				}
				if (triggerTime == 0f && player != null)
				{
					canPreCast = true;
					strongPull = false;
				}
			}
		}
		else if (currentState == State.EnterWarp)
		{
			SuckInCreatures();
			if ((float)activationTime < activateAnimationTime)
			{
				activationTime++;
			}
			else
			{
				if ((room.game.overWorld.warpWorldLoader != null && room.game.overWorld.warpWorldLoader.Finished) || room.game.overWorld.activeWorld.name.ToLowerInvariant() == Data.RegionString?.ToLowerInvariant())
				{
					ChangeState(State.ExitWarp);
				}
				if (room.game.overWorld.warpWorldLoader == null && Data.rippleEggWarpPoint && Data.destRoom == null)
				{
					room.game.pauseProcessOnNextComplete = true;
					room.game.overWorld.specialWarpCallback = this;
					room.game.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.WarpFastTravelScreen);
				}
			}
		}
		else if (currentState == State.ExitWarp)
		{
			for (int l = 0; l < room.game.Players.Count; l++)
			{
				if (room.game.Players[l].realizedCreature is Player player3 && player3.room == room)
				{
					player3.levitationActive = true;
					player3.TickLevitation(levitateUp: false);
					player3.levitationActive = false;
					player3.OneWayPlacement(placedObject.pos, l);
					player3.eyesClosedTime = 5;
					player3.repelLocusts = Mathf.Max(player3.repelLocusts, 240);
					player3.warpPointCooldown = 80;
				}
			}
			if (camera.warpPointTimer == null || !camera.warpPointTimer.pastHalfPoint || (Data.oneWay && room.game.warpDeferPlayerSpawnRoomName == ""))
			{
				ChangeState(State.SpawnItems);
			}
		}
		else if (currentState == State.SpawnItems)
		{
			room.game.warpDeferPlayerSpawnRoomName = "";
			room.game.warpDeferID = "";
			State nextState;
			if (room.game.GetStorySession.pendingWarpPointTransferObjects.Count != 0 && MyIdentifyingString() == room.game.GetStorySession.pendingWarpPointTransferId)
			{
				spawnTime++;
				if (startedPendingSpawn)
				{
					AbstractPhysicalObject nextObject = room.game.GetStorySession.pendingWarpPointTransferObjects[0];
					if (SpawnPendingObject(nextObject, immediateSpawn: false))
					{
						room.game.GetStorySession.pendingWarpPointTransferObjects.RemoveAt(0);
						spawnTime = 0;
					}
				}
				else if (spawnTime >= 20)
				{
					startedPendingSpawn = true;
					spawnTime = 0;
				}
			}
			else if (CheckClosedOrSealed(firstFrame, out nextState))
			{
				ChangeState(nextState);
			}
			else
			{
				ChangeState(State.CoolDown);
			}
		}
		else if (currentState == State.CoolDown)
		{
			warpTearOpenTriggered = false;
			float? num4 = null;
			foreach (AbstractCreature player5 in room.game.Players)
			{
				if (player5.Room == room.abstractRoom && player5.realizedCreature is Player player4)
				{
					float num5 = Vector2.Distance(pos, player4.firstChunk.pos);
					if (!num4.HasValue || num4 < num5)
					{
						num4 = num5;
					}
				}
			}
			float num6 = (num4.HasValue ? Custom.LerpMap(num4.Value, activationRadius * 2f, 1000f, 1f, 5f) : 5f);
			successfullWarpCooldownTimer -= num6;
			if (successfullWarpCooldownTimer <= 0f)
			{
				ChangeState(State.ReadyForWarp);
			}
		}
		else if (currentState == State.Sealed)
		{
			UpdateWeaverAnimation();
			if (Data.ExpiredOnCycle(room.game.GetStorySession.saveState.cycleNumber) && !Data.oneWay && !Data.rippleWarp && !Data.nonDynamicWarpPoint && !Data.wasNonDynamicWarpBeforeWeaverTriggered && Data.weaverTriggeredCycle <= 0 && !room.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility && room.game.GetStorySession.voidWeaverEncountersThisCycle.Count == 0 && room.game.cameras[0].room == room && room.game.cameras[0].PositionCurrentlyVisible(pos, 150f, widescreen: true))
			{
				ActivateWeaver();
			}
		}
		else if (currentState == State.Closed && visualOpenness == 0f && !warpSequenceInProgress)
		{
			if (badWarpAnimStarted || badWarpLerpTime != 0f)
			{
				return;
			}
			deleteNextFrame = true;
		}
		if (room.game.manager.upcomingProcess == null)
		{
			Data.RefreshRegionState(room.world, room.abstractRoom);
		}
	}

	public bool CheckClosedOrSealed(bool firstFrame, out State nextState)
	{
		nextState = null;
		if (base.slatedForDeletetion)
		{
			return false;
		}
		if (Data.oneWayExit)
		{
			nextState = State.Closed;
			return true;
		}
		if (CanPlayWarpSealingAnimation)
		{
			nextState = State.Sealed;
			return true;
		}
		bool flag = (Data.nonDynamicWarpPoint || Data.wasNonDynamicWarpBeforeWeaverTriggered) && !room.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility;
		bool flag2 = (room.game.GetStorySession.saveState.miscWorldSaveData.RoomSealedByWeaverOrWeaverAbility(room.abstractRoom.name.ToLowerInvariant()) && !flag) || room.VoidWeaverActive;
		bool flag3 = Data.ExpiredOnCycle(room.game.GetStorySession.saveState.cycleNumber) && !flag;
		bool flag4 = Data.ExpiredByLimitedUses() && deletable;
		if ((Data.ExpiredByWeaverOrWeaverAbility(room.game.GetStorySession.saveState.cycleNumber, room.game.GetStorySession.saveState) && !flag) | flag4)
		{
			nextState = State.Closed;
			return true;
		}
		if (flag3 | flag2)
		{
			if (!Data.rippleWarp)
			{
				nextState = State.Sealed;
				return true;
			}
			nextState = State.Closed;
			return true;
		}
		return false;
	}

	public void ChangeState(State nextState)
	{
		//IL_03ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_0464: Unknown result type (might be due to invalid IL or missing references)
		if (currentState == State.ReadyForWarp)
		{
			triggerTime = 0f;
		}
		else if (currentState == State.EnterWarp)
		{
			activated = false;
			activationTime = 0;
			room.game.cameras[0].ExitCutsceneMode();
		}
		else if (currentState == State.ExitWarp)
		{
			for (int i = 0; i < room.game.Players.Count; i++)
			{
				if (room.game.Players[i].realizedCreature != null)
				{
					(room.game.Players[i].realizedCreature as Player).StopLevitation();
				}
			}
		}
		else if (currentState == State.SpawnItems)
		{
			if (startedPendingSpawn)
			{
				room.game.GetStorySession.importantWarpPointTransferedEntities.Clear();
			}
			startedPendingSpawn = false;
		}
		else if (currentState == State.CoolDown)
		{
			successfullWarpCooldownTimer = 0f;
		}
		else if (!(currentState == State.Closed) && currentState == State.Sealed)
		{
			if (warpTear != null)
			{
				warpTear.weaverBlockAnimation = 0f;
				warpTear.openAnimation = 0f;
			}
			warpLocked = false;
			foreach (WeaverThread weaverThread in weaverThreads)
			{
				weaverThread.Destroy();
			}
			weaverThreads.Clear();
		}
		if (nextState == State.EnterWarp)
		{
			for (int j = 0; j < room.game.Players.Count; j++)
			{
				if (room.game.Players[j].realizedCreature != null && room.game.Players[j].Room.name == room.abstractRoom.name)
				{
					(room.game.Players[j].realizedCreature as Player).warpPointCooldown = 80;
				}
			}
			activated = true;
			room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Perform_Warp);
			room.game.cameras[0].EnterCutsceneMode(playerTriggeredWarpPoint.abstractCreature, RoomCamera.CameraCutsceneType.Standard);
			room.game.GetStorySession.pendingWarpObjectsOriginalGravities.Clear();
			room.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo.Clear();
			activationTime = 0;
			if (room.world.game.cameras[0].warpPointTimer == null)
			{
				room.world.game.cameras[0].warpPointTimer = new WarpPointTimer(activateAnimationTime * 2f, this);
			}
		}
		else if (nextState == State.ExitWarp)
		{
			if (currentState == State.EnterWarp)
			{
				PerformWarp();
			}
		}
		else if (nextState == State.CoolDown)
		{
			successfullWarpCooldownTimer = successfullWarpCooldownFrames;
			closesEarly = true;
			if (visualOpenness > 0.5f && !warpLocked)
			{
				room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Close, pos);
			}
		}
		else if (nextState == State.Sealed)
		{
			if (currentState == State.SpawnItems)
			{
				Custom.rainWorld.processManager.CueAchievement(RainWorld.AchievementID.WeaverAbility, 5f);
			}
			if (warpTear == null)
			{
				UpdateWarpTear();
			}
			LockWarp(currentState == State.SpawnItems);
		}
		else if (nextState == State.Closed && currentState != State.CoolDown && visualOpenness > 0.5f && !warpLocked)
		{
			room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Close, pos);
		}
		currentState = nextState;
	}

	public void SuckInCreatures()
	{
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0484: Unknown result type (might be due to invalid IL or missing references)
		//IL_0495: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0350: Unknown result type (might be due to invalid IL or missing references)
		//IL_035f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0374: Unknown result type (might be due to invalid IL or missing references)
		//IL_0379: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Unknown result type (might be due to invalid IL or missing references)
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_055a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0566: Unknown result type (might be due to invalid IL or missing references)
		//IL_0589: Unknown result type (might be due to invalid IL or missing references)
		//IL_058e: Unknown result type (might be due to invalid IL or missing references)
		float num = Mathf.Min(1f, (float)activationTime / (activateAnimationTime / 2f));
		float activationProgress = Mathf.Min(1f, (float)activationTime / activateAnimationTime);
		Vector2 val = default(Vector2);
		((Vector2)(ref val))..ctor(pos.x, pos.y + 30f);
		for (int i = 0; i < room.physicalObjects.Length; i++)
		{
			for (int j = 0; j < room.physicalObjects[i].Count; j++)
			{
				PhysicalObject physicalObject = room.physicalObjects[i][j];
				if (BlackListedObjectTypes.Contains(physicalObject.abstractPhysicalObject.type) || (physicalObject is Spear && (physicalObject as Spear).stuckInWall.HasValue))
				{
					continue;
				}
				float num2 = Vector2.Distance(pos, physicalObject.firstChunk.pos);
				if ((physicalObject is Creature && num2 > creatureSuckRadius) || num2 > (float)strongPullRadius * 2.5f || (!(physicalObject is Player) && canWarpToVoidWeaverEnding))
				{
					continue;
				}
				bool flag = AbstractPhysicalObject.IsObjectImportant(physicalObject.abstractPhysicalObject, room.world);
				if (!physicalObject.abstractPhysicalObject.IsSameRippleLayer(Data.rippleWarp ? 1 : 0) && !flag)
				{
					continue;
				}
				if (physicalObject is Creature)
				{
					Creature creature = physicalObject as Creature;
					if (creature is Player)
					{
						for (int k = 0; k < creature.grasps.Length; k++)
						{
							if (creature.grasps[k] != null && creature.grasps[k].grabbed.abstractPhysicalObject != null && !room.game.GetStorySession.importantWarpPointTransferedEntities.Contains(creature.grasps[k].grabbed.abstractPhysicalObject.ID))
							{
								room.game.GetStorySession.importantWarpPointTransferedEntities.Add(creature.grasps[k].grabbed.abstractPhysicalObject.ID);
							}
						}
					}
					creature.LoseAllGrasps();
					if (creature.abstractCreature.creatureTemplate.type != CreatureTemplate.Type.Slugcat && (!ModManager.MSC || creature.abstractCreature.creatureTemplate.type != MoreSlugcatsEnums.CreatureTemplateType.SlugNPC))
					{
						creature.stun = Math.Max(creature.stun, 20);
					}
					if (creature is Player player)
					{
						player.warpPointCooldown = 80;
						if (Vector2.Distance(pos, player.mainBodyChunk.pos) < PullRadius || player == playerTriggeredWarpPoint)
						{
							if (!room.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo.Contains(player))
							{
								room.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo.Add(player);
								room.game.GetStorySession.pendingWarpObjectsOriginalGravities.Add(player.GetLocalGravity());
								player.SetLocalGravity(0f);
							}
							player.customPlayerGravity = 0f;
							BodyChunk mainBodyChunk = player.mainBodyChunk;
							mainBodyChunk.vel *= Mathf.Lerp(1f, 0.95f, num);
							BodyChunk obj = player.bodyChunks[1];
							obj.vel *= Mathf.Lerp(1f, 0.95f, num);
							BodyChunk mainBodyChunk2 = player.mainBodyChunk;
							mainBodyChunk2.vel += Custom.DirVec(player.mainBodyChunk.pos, val) * (Custom.LerpMap(Vector2.Distance(player.mainBodyChunk.pos, val), 20f, PullRadius * 0.75f, 2f * num, 5f * num) * Mathf.InverseLerp(0f, 20f, Vector2.Distance(player.mainBodyChunk.pos, val)));
							player.standingInWarpPointProtectionTime = 10;
						}
						if ((float)activationTime == activateAnimationTime - 2f && player.performingActivationTimer > 0)
						{
							player.performingActivationTimer = 0;
							player.cancelCamoCooldown = 120;
							player.camoInputsNeedReset = true;
						}
					}
					if (BlackListedCreatureTypes.Contains(creature.abstractCreature.creatureTemplate.type))
					{
						continue;
					}
				}
				float num3 = (flag ? ((float)strongPullRadius) : PullRadius);
				if ((physicalObject is Player && !(Vector2.Distance(pos, (physicalObject as Player).mainBodyChunk.pos) >= num3)) || !(num2 < num3 * 2.5f))
				{
					continue;
				}
				if (!room.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo.Contains(physicalObject))
				{
					room.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo.Add(physicalObject);
					room.game.GetStorySession.pendingWarpObjectsOriginalGravities.Add(physicalObject.GetLocalGravity());
					physicalObject.SetLocalGravity(0f);
					if (physicalObject is IHaveAStalk)
					{
						(physicalObject as IHaveAStalk).DetatchStalk();
					}
				}
				if (physicalObject is Player)
				{
					(physicalObject as Player).customPlayerGravity = 0f;
				}
				physicalObject.firstChunk.vel = OrbitVector(physicalObject.firstChunk.vel, physicalObject.firstChunk.pos, num3, Mathf.Min(0.75f, physicalObject.firstChunk.mass), activationProgress, 0.95f);
			}
		}
	}

	public Vector2 OrbitVector(Vector2 vel, Vector2 objPos, float usePullRadiusThreshold, float intensity, float activationProgress, float drag)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		float num = Mathf.Min(1f, activationProgress * 2f);
		Vector2 val = Custom.DegToVec(((int)Custom.VecToDeg(Custom.DirVec(pos, objPos)) + 60) % 360);
		Vector2 val2 = pos + val * (usePullRadiusThreshold * Mathf.Lerp(0.6f, 0.2f, activationProgress));
		float toB = 4f * num;
		float fromB = 24f * num;
		if (Vector2.Distance(pos, objPos) > usePullRadiusThreshold * 1.5f)
		{
			val2 = pos;
			toB = 16f * num;
			fromB = 3f * num;
		}
		vel *= Mathf.Lerp(1f, drag, num);
		vel += Custom.DirVec(objPos, val2) * (Custom.LerpMap(Vector2.Distance(objPos, val2), 30f, usePullRadiusThreshold * 1.5f, fromB, toB) * intensity * Mathf.InverseLerp(0f, 30f, Vector2.Distance(objPos, val2)));
		return vel;
	}

	public void ProvideAir()
	{
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		if (!room.BeingViewed || !room.water || room.game.ActiveRippleLayer == 1 != Data.rippleWarp || warpTear == null || visualOpenness <= 0f)
		{
			return;
		}
		for (int i = 0; i < room.game.Players.Count; i++)
		{
			if (room.game.Players[i].realizedCreature is Player player && room.game.Players[i].Room.name == room.abstractRoom.name && player.room != null && !player.inShortcut && !(Vector2.Distance(pos, player.mainBodyChunk.pos) > PullRadius) && (double)player.airInLungs < 0.9)
			{
				player.airInLungs += visualOpenness * 0.005f;
			}
		}
	}

	public float ReduceWind(PhysicalObject pObj)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		if (Data.rippleWarp && (room.game.ActiveRippleLayer != 1 || !pObj.abstractPhysicalObject.IsSameRippleLayer(1)))
		{
			return 1f;
		}
		float pullRadius = PullRadius;
		pullRadius = ((!(successfullWarpCooldownTimer > 0f)) ? (pullRadius * visualOpenness) : (pullRadius * (1f + Mathf.Pow(Mathf.InverseLerp((float)(successfullWarpCooldownFrames / 2), (float)successfullWarpCooldownFrames, successfullWarpCooldownTimer), 0.25f))));
		return Mathf.InverseLerp(pullRadius, pullRadius * 2f, Vector2.Distance(pos, pObj.firstChunk.pos));
	}

	public void PlayPulseSound(float strength)
	{
	}

	public void PlayPulseHitSound(float strength)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Distortion_Wave_Hit_Player, camera.virtualMicrophone.listenerPoint, Mathf.Clamp(2f * strength, 0f, 0.5f), 1.2f);
	}

	public void WarpTearWaveHitPlayer()
	{
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		if (room.game.ActiveRippleLayer == 1 == Data.rippleWarp && !warpSequenceInProgress)
		{
			int num = (int)(Custom.Mod(Custom.rainWorld.processManager.shadersTime / 5f * 0.8f, 1.7f) * 100f) - 40;
			pulseSoundTimer.Tick();
			pulseHitSoundTimer.Tick();
			if (num == 0 && pulseSoundTimer.isFinished && warpTear.fadeAnim > 0f)
			{
				PlayPulseSound(warpTear.fadeAnim);
				pulseSoundTimer.Reset();
			}
			float num2 = 1f - Mathf.Clamp01(Vector2.Distance(placedObject.pos, camera.virtualMicrophone.listenerPoint) * 0.001f);
			if (num == (int)((1f - num2) * 40f) && num2 > 0f && warpTear.fadeAnim > 0f && pulseHitSoundTimer.isFinished)
			{
				PlayPulseHitSound(num2 * warpTear.fadeAnim);
				pulseHitSoundTimer.Reset();
			}
		}
	}

	public void UpdateWarpTear()
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		float num = 240f;
		if (warpTear == null)
		{
			if (currentState == State.Closed)
			{
				return;
			}
			string shaderOverride = null;
			if (Data.effectSettings.outerRimCosmetic)
			{
				shaderOverride = "WarpTearOuter";
			}
			else if (Data.effectSettings.badWarpCosmetic)
			{
				shaderOverride = "WarpTearBad";
			}
			warpTear = new WarpTear(room, placedObject.pos, 0.2f, Data.rippleWarp, room.abstractRoom.index, shaderOverride, Data.effectSettings.spawnBigRift);
			if (fadeInOnSpawn)
			{
				warpTear.fadeAnim = 0f;
				warpTear.openAnimation = 1f;
				if (Data.dynamicWarpPoint && room.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility)
				{
					warpTear.partialWeaverBlock = true;
				}
			}
			if (dynamicWeaverFadeOut)
			{
				warpTear.partialWeaverBlock = true;
				warpTear.openAnimation = 1f;
				warpTear.fadeAnim = 1f;
			}
			pulseSoundTimer.SetToMin();
			pulseHitSoundTimer.SetToMin();
			room.AddObject(warpTear);
		}
		if (warpLocked)
		{
			if (dynamicWeaverFadeOut && !thisWarpSequenceInProgress)
			{
				warpTear.FadeOut(80f);
			}
			return;
		}
		WarpTearWaveHitPlayer();
		if (thisWarpSequenceInProgress)
		{
			warpTearOpenTriggered = true;
			num = 80f;
		}
		bool flag = nonPlayerRequestOpen != null && nonPlayerRequestOpen.Entering();
		if (flag)
		{
			num = nonPlayerRequestOpen.FadeDuration();
			warpTearOpenTriggered = true;
		}
		if (otherWarpSequenceInProgress && warpTear.weaverBlockAnimation < 1f)
		{
			warpTear.moveToBack = true;
		}
		else if (warpTear.moveToBack)
		{
			warpTear.moveToBack = false;
		}
		if (fadeInOnSpawn)
		{
			warpTear.FadeIn(num);
			if (Data.noRing && canPlayOpenPing && warpTear.fadeAnim > 0.5f)
			{
				room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Distortion_Wave_Hit_Player, camera.virtualMicrophone.listenerPoint, 0.7f, 1f);
				canPlayOpenPing = false;
			}
		}
		if (Data.oneWayExit && !warpSequenceInProgress)
		{
			warpTear.FadeOut(num);
		}
		if (!transportable && Data.rippleWarp && !flag)
		{
			warpTearOpenTriggered = false;
		}
		if (warpTearOpenTriggered)
		{
			if (warpTear.openAnimation < 1f)
			{
				warpTear.openAnimation += 1f / num;
			}
			timeWarpTearClosed = 0;
		}
		else
		{
			if (warpTear.openAnimation > 0f)
			{
				warpTear.openAnimation -= 1f / num;
			}
			timeWarpTearClosed++;
		}
	}

	public bool SpawnPendingObject(AbstractPhysicalObject nextObject, bool immediateSpawn)
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Unknown result type (might be due to invalid IL or missing references)
		//IL_0371: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		//IL_0318: Unknown result type (might be due to invalid IL or missing references)
		bool flag = true;
		if (nextObject is AbstractCreature)
		{
			CreatureTemplate.Type type = (nextObject as AbstractCreature).creatureTemplate.TopAncestor().type;
			if (type != CreatureTemplate.Type.Fly && type != CreatureTemplate.Type.Leech && type != CreatureTemplate.Type.Spider)
			{
				flag = false;
			}
		}
		bool num = immediateSpawn || (!flag && spawnTime % 40 == 0) || (flag && spawnTime % 10 == 0);
		bool flag2 = room.game.GetStorySession.importantWarpPointTransferedEntities.Contains(nextObject.ID);
		if (num)
		{
			bool flag3 = false;
			Vector2 val = (flag2 ? pos : (pos + Custom.RNV() * (radius / 2f)));
			for (int i = 0; i < 100; i++)
			{
				if (!room.GetTile(room.GetTilePosition(val)).Solid)
				{
					flag3 = true;
					break;
				}
				val = pos + Custom.RNV() * (radius / 2f);
			}
			if (!flag3)
			{
				val = pos;
			}
			nextObject.pos = room.GetWorldCoordinate(val);
			if (room.world != nextObject.world)
			{
				nextObject.world = room.world;
				if (nextObject is AbstractCreature)
				{
					AbstractCreature abstractCreature = nextObject as AbstractCreature;
					if (abstractCreature.creatureTemplate.AI)
					{
						abstractCreature.abstractAI.NewWorld(room.world);
					}
				}
				room.abstractRoom.AddEntity(nextObject);
			}
			nextObject.RealizeInRoom();
			if (nextObject is AbstractCreature && nextObject.realizedObject != null)
			{
				AbstractCreature abstractCreature2 = nextObject as AbstractCreature;
				if (abstractCreature2.creatureTemplate.AI)
				{
					abstractCreature2.InitiateAI();
					abstractCreature2.abstractAI.RealAI.NewRoom(room);
				}
				abstractCreature2.realizedCreature.repelLocusts = Mathf.Max(abstractCreature2.realizedCreature.repelLocusts, 240);
			}
			if (nextObject.realizedObject != null && !immediateSpawn)
			{
				PhysicalObject realizedObject = nextObject.realizedObject;
				Vector2 val2 = ((flag2 || realizedObject is Player) ? Vector2.zero : (Custom.RNV() * Random.Range(12f, 32f)));
				val2.y *= 1.4f;
				if (!(realizedObject is Scavenger))
				{
					if (realizedObject is Creature)
					{
						(realizedObject as Creature).mainBodyChunk.vel = val2 * Mathf.Min(0.75f, (realizedObject as Creature).mainBodyChunk.mass);
					}
					else
					{
						for (int j = 0; j < realizedObject.bodyChunks.Length; j++)
						{
							realizedObject.bodyChunks[j].vel = Vector2.zero;
							realizedObject.bodyChunks[j].vel = val2 * Mathf.Min(0.75f, Random.Range(realizedObject.bodyChunks[j].mass + 0.1f, realizedObject.bodyChunks[j].mass + 0.25f));
						}
					}
				}
				room.AddObject(new ShockWave(val, realizedObject.firstChunk.rad * 10f, Random.Range(0.3f, 0.7f), 20));
				room.PlaySound(WatcherEnums.WatcherSoundID.Warp_Point_Spit_Out_Object, val);
				if (nextObject is AbstractCreature)
				{
					AbstractCreature abstractCreature3 = nextObject as AbstractCreature;
					bool flag4 = false;
					float num2 = -1f;
					foreach (AbstractCreature player in room.world.game.Players)
					{
						if (abstractCreature3.state != null && abstractCreature3.state.socialMemory != null)
						{
							num2 = Mathf.Max(num2, abstractCreature3.state.socialMemory.GetLike(player.ID));
						}
					}
					if (abstractCreature3.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Slugcat)
					{
						flag4 = true;
					}
					else if (ModManager.MSC && abstractCreature3.creatureTemplate.TopAncestor().type == MoreSlugcatsEnums.CreatureTemplateType.SlugNPC)
					{
						flag4 = true;
					}
					else if (abstractCreature3.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.LizardTemplate && abstractCreature3.state.alive && num2 > 0.5f)
					{
						flag4 = true;
					}
					if (!flag4)
					{
						(nextObject.realizedObject as Creature).Stun(200);
					}
				}
			}
			return true;
		}
		return false;
	}

	public void SlateForDeletion()
	{
		if (!base.slatedForDeletetion && warpTear != null)
		{
			warpTear.FadeOut(240f);
		}
		base.slatedForDeletetion = true;
		room.RemoveObject(this);
	}

	public void SpawnBadWarpRipple(bool animate)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		if (!spawnedBadWarpRipple)
		{
			PlacedObject placedObject = new PlacedObject(PlacedObject.Type.CosmeticRipple, null);
			WarpPointData warpPointData = ((overrideData != null) ? overrideData : Data);
			(placedObject.data as CosmeticRippleData).panelPos = this.placedObject.pos;
			(placedObject.data as CosmeticRippleData).handlePos = (placedObject.data as CosmeticRippleData).panelPos;
			(placedObject.data as CosmeticRippleData).scale = 20f;
			(placedObject.data as CosmeticRippleData).intensity = (warpPointData.rippleWarp ? 0.16f : 0.38f);
			(placedObject.data as CosmeticRippleData).invert = false;
			(placedObject.data as CosmeticRippleData).animateOnSpawn = animate;
			(placedObject.data as CosmeticRippleData).square = false;
			(placedObject.data as CosmeticRippleData).leavesTrail = true;
			(placedObject.data as CosmeticRippleData).fallOff = 1f;
			(placedObject.data as CosmeticRippleData).depthMix = 0.5f;
			(placedObject.data as CosmeticRippleData).squish = 0f;
			(placedObject.data as CosmeticRippleData).pos = this.placedObject.pos;
			(placedObject.data as CosmeticRippleData).cycleExpiry = 5;
			(placedObject.data as CosmeticRippleData).isGameplay = false;
			(placedObject.data as CosmeticRippleData).isTransition = false;
			placedObject.pos = (placedObject.data as CosmeticRippleData).pos;
			if (room.game.IsStorySession)
			{
				(placedObject.data as CosmeticRippleData).cycleSpawnedOn = room.game.GetStorySession.saveState.cycleNumber;
			}
			room.game.cameras[0].UpdateRippleData(room, room.game.cameras[0].currentCameraPosition);
			ripple = room.TrySpawnRippleRegion(placedObject, saveInRegionState: false, skipIfInRegionState: false, changeRippleState: false);
			spawnedBadWarpRipple = true;
		}
	}

	public FShader GetWarpShader()
	{
		string text = (Data.noRing ? "WarpNoRing" : "Warp");
		if (!Data.darkWarp)
		{
			WarpPointTimer warpPointTimer = camera.warpPointTimer;
			if (warpPointTimer == null || !warpPointTimer.darkWarp || otherWarpSequenceInProgress)
			{
				goto IL_0052;
			}
		}
		text += "ThroughDark";
		goto IL_0052;
		IL_0052:
		return Custom.rainWorld.Shaders[text];
	}

	public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
	{
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		sLeaser.sprites = new FSprite[3];
		sLeaser.sprites[0] = new FSprite("Futile_White");
		bool flag = (!Data.oneWay || Data.oneWay == Data.oneWayEntrance) && (!Data.limitedUse || Data.uses > 0);
		if (warpLocked)
		{
			flag = false;
		}
		lastPos = (pos = placedObject.pos);
		sLeaser.sprites[0].shader = Custom.rainWorld.Shaders[Data.rippleWarp ? "WarpCircleRipple" : "WarpCircleBasic"];
		sLeaser.sprites[0].scale = (flag ? (radius / 4f) : 0f);
		sLeaser.sprites[0].alpha = (flag ? (2f / radius) : 0f);
		sLeaser.sprites[0].isVisible = false;
		sLeaser.sprites[1] = new FSprite("Futile_White");
		sLeaser.sprites[1].shader = GetWarpShader();
		WarpPointTimer warpPointTimer = rCam.warpPointTimer;
		if (warpPointTimer != null && !otherWarpSequenceInProgress)
		{
			sLeaser.sprites[1].color = new Color(warpPointTimer.parameters.x, warpPointTimer.parameters.y, warpPointTimer.parameters.z, warpPointTimer.parameters.w);
			forceNoVisuals = warpPointTimer.forceNoExitVisuals;
		}
		else
		{
			sLeaser.sprites[1].color = new Color(parameters.x, parameters.y, parameters.z, parameters.w);
		}
		sLeaser.sprites[1].scale = rCam.sSize.x * 0.125f + 8f;
		sLeaser.sprites[2] = new FSprite("Futile_White");
		sLeaser.sprites[2].shader = Custom.rainWorld.Shaders["WarpPlayerDistortion"];
		sLeaser.sprites[2].scale = rCam.sSize.x * 0.125f + 8f;
		sLeaser.sprites[2].alpha = (flag ? ((float)Data.effectSettings.spaghettification / 15f) : 0f);
		if (!warpSequenceInProgress && rCam.room.abstractRoom.name == "WRSA_WEAVER")
		{
			sLeaser.sprites[2].isVisible = false;
		}
		if (forceNoVisuals)
		{
			sLeaser.SetAllSpritesVisibility(isVisible: false);
		}
		AddToContainer(sLeaser, rCam, null);
	}

	public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cc: Unknown result type (might be due to invalid IL or missing references)
		if (base.slatedForDeletetion || room != rCam.room)
		{
			sLeaser.CleanSpritesAndRemove();
			return;
		}
		if (refreshGraphics)
		{
			sLeaser.RemoveAllSpritesFromContainer();
			InitiateSprites(sLeaser, rCam);
			refreshGraphics = false;
		}
		bool flag = otherWarpSequenceInProgress;
		if (!thisWarpSequenceInProgress && lastFrameWarpSequenceInProgress)
		{
			sLeaser.sprites[1].shader = GetWarpShader();
		}
		WarpPointTimer warpPointTimer = rCam.warpPointTimer;
		if (warpPointTimer != null && !flag)
		{
			sLeaser.sprites[1].color = new Color(warpPointTimer.parameters.x, warpPointTimer.parameters.y, warpPointTimer.parameters.z, warpPointTimer.parameters.w);
		}
		else
		{
			sLeaser.sprites[1].color = new Color(parameters.x, parameters.y, parameters.z, parameters.w);
		}
		bool flag2 = room.ViewedByAnyCamera(pos, radius * 0.5f) && !flag;
		FContainer fContainer = rCam.ReturnFContainer("WarpPoint");
		sLeaser.sprites[1].MoveInFrontOfOtherNode(fContainer.GetChildAt(fContainer.GetChildCount() - 1));
		sLeaser.sprites[0].MoveBehindOtherNode(sLeaser.sprites[1]);
		FContainer fContainer2 = rCam.ReturnFContainer("Items");
		sLeaser.sprites[2].MoveInFrontOfOtherNode(fContainer2.GetChildAt(fContainer2.GetChildCount() - 1));
		if (nonPlayerRequestOpen != null && nonPlayerRequestOpen.Entering())
		{
			nonPlayerRequestOpen.UpdateShaders(timeStacker);
		}
		else if (rCam.warpPointTimer == null && !activated && !flag)
		{
			Shader.SetGlobalFloat("_warpPointAcceleration", 0f);
			Shader.SetGlobalFloat("_playerWarpPointTime", Mathf.Lerp(lastTriggerTime, triggerTime, timeStacker) / triggerActivationTime - 1f);
			Shader.SetGlobalFloat("_warpPointTime", Mathf.Lerp(lastTriggerTime, triggerTime, timeStacker) / triggerActivationTime - 1f);
		}
		if (flag2)
		{
			screenPosition = (pos - rCam.pos) / rCam.sSize;
			Shader.SetGlobalVector("_warpPointPosition", Vector4.op_Implicit(screenPosition));
		}
		sLeaser.sprites[1].isVisible = flag2;
		sLeaser.sprites[0].x = pos.x - camPos.x;
		sLeaser.sprites[0].y = pos.y - camPos.y;
		sLeaser.sprites[1].x = pos.x - camPos.x;
		sLeaser.sprites[1].y = pos.y - camPos.y;
		sLeaser.sprites[2].x = pos.x - camPos.x;
		sLeaser.sprites[2].y = pos.y - camPos.y;
		if (!WarpPointAnimationPlaying)
		{
			sLeaser.sprites[0].alpha = 2f / PullRadius * 1f;
			sLeaser.sprites[0].alpha = Mathf.Lerp(lastTriggerTime, triggerTime, timeStacker) / triggerActivationTime;
		}
		else
		{
			sLeaser.sprites[0].alpha = 2f / PullRadius * Mathf.Clamp(1f - (float)activationTime / activateAnimationTime * 1.5f, 0f, 1f);
		}
		lastFrameWarpSequenceInProgress = warpSequenceInProgress;
		if (forceNoVisuals)
		{
			sLeaser.SetAllSpritesVisibility(isVisible: false);
		}
		base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
	}

	public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
	{
	}

	public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
	{
		rCam.ReturnFContainer("Items").AddChild(sLeaser.sprites[2]);
		FContainer fContainer = rCam.ReturnFContainer("WarpPoint");
		fContainer.AddChild(sLeaser.sprites[0]);
		fContainer.AddChild(sLeaser.sprites[1]);
	}

	public static string IdentifyingString(RainWorldGame game, WarpPointData data, AbstractRoom sourceRoom)
	{
		SlugcatStats.Timeline timeline = data.sourceTimeline;
		if (timeline == null)
		{
			timeline = game.TimelinePoint;
		}
		return sourceRoom.name + ":" + timeline?.ToString() + ":" + data.uuidPair;
	}

	public static string IdentifyingString(RainWorldGame game, WarpPointData data, string sourceRoom)
	{
		SlugcatStats.Timeline timeline = data.sourceTimeline;
		if (timeline == null)
		{
			timeline = game.TimelinePoint;
		}
		return sourceRoom + ":" + timeline?.ToString() + ":" + data.uuidPair;
	}

	public static string RoomFromIdentifyingString(string identifyingString)
	{
		return identifyingString.Split(new char[1] { ':' })[0];
	}

	public static string RoomFromValueString(string valueString)
	{
		return valueString.Split(new char[1] { '~' })[5];
	}

	public string MyIdentifyingString()
	{
		if (room == null)
		{
			return null;
		}
		return IdentifyingString(room.game, Data, room.abstractRoom);
	}

	public float EaseInOutElastic(float t)
	{
		float num = (float)Math.PI * 4f / 9f;
		if (t != 0f)
		{
			if (t != 1f)
			{
				if (!((double)t < 0.5))
				{
					return Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * num) / 2f + 1f;
				}
				return (0f - Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * num)) / 2f;
			}
			return 1f;
		}
		return 0f;
	}

	public float easeInOutQuint(float t)
	{
		if (!((double)t < 0.5))
		{
			return 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;
		}
		return 16f * t * t * t * t * t;
	}

	public float easeInExpo(float t)
	{
		if (t != 0f)
		{
			return Mathf.Pow(2f, 10f * t - 10f);
		}
		return 0f;
	}

	public void AddPlayerBackIn(int i, bool placeInRoom)
	{
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		WorldCoordinate worldCoordinate = new WorldCoordinate(room.abstractRoom.index, 0, 0, -1);
		worldCoordinate.Tile = new IntVector2((int)(placedObject.pos.x / 20f), (int)(placedObject.pos.y / 20f));
		room.game.Players[i].pos = worldCoordinate;
		if (placeInRoom && (!Data.rippleEggWarpPoint || room != room.game.Players[i].Room?.realizedRoom || (room.game.Players[i].realizedCreature is Player player && room != player.room)))
		{
			room.abstractRoom.AddEntity(room.game.Players[i]);
			room.game.Players[i].realizedCreature.PlaceInRoom(room);
		}
		(room.game.Players[i].realizedCreature as Player).OneWayPlacement(placedObject.pos, i);
		(room.game.Players[i].realizedCreature as Player).warpPointCooldown = 80;
		room.game.Players[i].realizedCreature.repelLocusts = Mathf.Max(room.game.Players[i].realizedCreature.repelLocusts, 240);
	}

	public void RemovePlayers(int i)
	{
		if (room != null && room.game.Players[i].realizedCreature != null)
		{
			room.game.Players[i].realizedCreature.room.RemoveObject(room.game.Players[i].realizedCreature);
		}
	}

	public void RemoveFromSaveData()
	{
		SaveState saveState = (room.game.IsStorySession ? room.game.GetStorySession.saveState : null);
		string key = MyIdentifyingString();
		string text = Data.destRoom;
		foreach (string key3 in RainWorld.roomNameToIndex.Keys)
		{
			if (key3.ToLowerInvariant() == text.ToLowerInvariant())
			{
				text = key3;
				break;
			}
		}
		string key2 = IdentifyingString(room.game, Data, text);
		if (saveState.deathPersistentSaveData.spawnedWarpPoints.ContainsKey(key))
		{
			saveState.deathPersistentSaveData.spawnedWarpPoints.Remove(key);
		}
		if (saveState.deathPersistentSaveData.newlyDiscoveredWarpPoints.ContainsKey(key))
		{
			saveState.deathPersistentSaveData.newlyDiscoveredWarpPoints.Remove(key);
		}
		if (saveState.miscWorldSaveData.discoveredWarpPoints.ContainsKey(key))
		{
			saveState.miscWorldSaveData.discoveredWarpPoints.Remove(key);
		}
		if (saveState.deathPersistentSaveData.spawnedWarpPoints.ContainsKey(key2))
		{
			saveState.deathPersistentSaveData.spawnedWarpPoints.Remove(key2);
		}
		if (saveState.deathPersistentSaveData.newlyDiscoveredWarpPoints.ContainsKey(key2))
		{
			saveState.deathPersistentSaveData.newlyDiscoveredWarpPoints.Remove(key2);
		}
		if (saveState.miscWorldSaveData.discoveredWarpPoints.ContainsKey(key2))
		{
			saveState.miscWorldSaveData.discoveredWarpPoints.Remove(key2);
		}
	}

	public static void ScrubNonDeathPersistentData(SaveState saveState)
	{
		saveState.miscWorldSaveData.roomsSealedByVoidWeaver = new List<string>(saveState.miscWorldSaveData.oldRoomsSealedByVoidWeaver);
		saveState.miscWorldSaveData.roomsSealedByWeaverAbility = new List<string>(saveState.miscWorldSaveData.oldRoomsSealedByWeaverAbility);
		Dictionary<string, string> newlyDiscoveredWarpPoints = saveState.deathPersistentSaveData.newlyDiscoveredWarpPoints;
		Dictionary<string, string> spawnedWarpPoints = saveState.deathPersistentSaveData.spawnedWarpPoints;
		Dictionary<string, string> discoveredWarpPoints = saveState.miscWorldSaveData.discoveredWarpPoints;
		for (int i = 0; i < 3; i++)
		{
			Dictionary<string, string> dictionary = i switch
			{
				0 => newlyDiscoveredWarpPoints, 
				1 => spawnedWarpPoints, 
				_ => discoveredWarpPoints, 
			};
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
			try
			{
				foreach (KeyValuePair<string, string> item in dictionary)
				{
					string text = RoomFromIdentifyingString(item.Key);
					if (!saveState.miscWorldSaveData.RoomSealedByWeaverOrWeaverAbility(text?.ToLowerInvariant()))
					{
						PlacedObject placedObject = new PlacedObject(PlacedObject.Type.WarpPoint, null);
						string[] s = Regex.Split(item.Value.Trim(), "><");
						placedObject.FromString(s);
						WarpPointData warpPointData = placedObject.data as WarpPointData;
						if ((warpPointData.nonDynamicWarpPoint || warpPointData.wasNonDynamicWarpBeforeWeaverTriggered) && warpPointData.weaverTriggeredCycle == saveState.cycleNumber)
						{
							warpPointData.wasNonDynamicWarpBeforeWeaverTriggered = false;
							warpPointData.weaverTriggeredCycle = -1;
							warpPointData.cycleExpiry = 0;
							dictionary2[item.Key] = placedObject.ToString();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Custom.LogWarning("exception when scrubbing warp save data!: " + ex);
			}
			foreach (KeyValuePair<string, string> item2 in dictionary2)
			{
				dictionary[item2.Key] = item2.Value;
			}
		}
	}

	public static bool RoomCanBeSealed(string roomName)
	{
		roomName = roomName.ToLowerInvariant();
		if (roomName != "wora_egg04")
		{
			return roomName != "wora_dial";
		}
		return false;
	}

	public Room getSourceRoom()
	{
		return room;
	}

	[Obsolete("Created as a method of transferring warp points, but this code is irrelevant for now")]
	public void WarpTransfer(Room newRoom, WarpPointData useData)
	{
		if (newRoom.warpPoints.Count < 1)
		{
			return;
		}
		foreach (WarpPoint warpPoint in newRoom.warpPoints)
		{
			if (!(IdentifyingString(newRoom.game, warpPoint.Data, newRoom.abstractRoom) != IdentifyingString(newRoom.game, Data, newRoom.abstractRoom)))
			{
				continue;
			}
			for (int i = 0; i < newRoom.game.Players.Count; i++)
			{
				AbstractCreature abstractCreature = newRoom.game.Players[i];
				if (abstractCreature.realizedCreature == null)
				{
					continue;
				}
				Player player = abstractCreature.realizedCreature as Player;
				(room.game.session as StoryGameSession).saveState.deathPersistentSaveData.reinforcedKarma = true;
				for (int j = 0; j < room.game.cameras.Length; j++)
				{
					if (room.game.cameras[j].hud != null)
					{
						room.game.cameras[j].hud.karmaMeter.reinforceAnimation = 0;
					}
				}
				player.warpPointToRestore = new WarpPointData(null);
				player.warpPointToRestore.destRoom = warpPoint.Data.destRoom;
				player.warpPointToRestore.destPos = warpPoint.Data.destPos;
			}
			warpPoint.SlateForDeletion();
		}
	}

	[Obsolete("Use room parameter function instead.")]
	public void NewWorldLoaded()
	{
	}

	public void NewWorldLoaded(Room newRoom)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_069a: Unknown result type (might be due to invalid IL or missing references)
		WarpPoint warpPoint = null;
		WarpPointData warpPointData = ((overrideData != null) ? overrideData : Data);
		if (newRoom == room)
		{
			warpPoint = this;
		}
		else
		{
			PlacedObject placedObject = new PlacedObject(PlacedObject.Type.WarpPoint, null);
			(placedObject.data as WarpPointData).destPos = pos;
			(placedObject.data as WarpPointData).RegionString = sourceRegion;
			(placedObject.data as WarpPointData).destRoom = ((room != null) ? room.abstractRoom.name : warpPointData.destRoom);
			(placedObject.data as WarpPointData).destTimeline = warpPointData.sourceTimeline;
			(placedObject.data as WarpPointData).panelPos = Vector2.zero;
			(placedObject.data as WarpPointData).uuidPair = warpPointData.uuidPair;
			(placedObject.data as WarpPointData).effectSettings = warpPointData.effectSettings;
			(placedObject.data as WarpPointData).cycleSpawnedOn = warpPointData.cycleSpawnedOn;
			(placedObject.data as WarpPointData).cycleExpiry = warpPointData.cycleExpiry;
			(placedObject.data as WarpPointData).deathPersistentWarpPoint = warpPointData.deathPersistentWarpPoint;
			(placedObject.data as WarpPointData).oneWay = warpPointData.oneWay;
			(placedObject.data as WarpPointData).oneWayEntrance = warpPointData.oneWay && !warpPointData.oneWayEntrance;
			(placedObject.data as WarpPointData).oneWayEntranceIdentified = warpPointData.oneWayEntranceIdentified;
			(placedObject.data as WarpPointData).rippleWarp = warpPointData.rippleWarp;
			(placedObject.data as WarpPointData).limitedUse = warpPointData.limitedUse;
			(placedObject.data as WarpPointData).uses = warpPointData.uses;
			(placedObject.data as WarpPointData).accessibility = warpPointData.accessibility;
			(placedObject.data as WarpPointData).weaverTriggeredCycle = warpPointData.weaverTriggeredCycle;
			(placedObject.data as WarpPointData).wasNonDynamicWarpBeforeWeaverTriggered = warpPointData.wasNonDynamicWarpBeforeWeaverTriggered;
			(placedObject.data as WarpPointData).effectSettings.badWarpCosmetic = warpPointData.effectSettings.badWarpCosmetic;
			(placedObject.data as WarpPointData).effectSettings.outerRimCosmetic = warpPointData.effectSettings.outerRimCosmetic;
			if (!warpPointData.destPos.HasValue)
			{
				foreach (PlacedObject placedObject2 in newRoom.roomSettings.placedObjects)
				{
					if (placedObject2.type == PlacedObject.Type.DynamicWarpTarget)
					{
						warpPointData.destPos = placedObject2.pos;
						placedObject.pos = placedObject2.pos;
						break;
					}
				}
			}
			placedObject.pos = warpPointData.destPos.Value;
			bool flag = false;
			if (newRoom.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Contains(newRoom.abstractRoom.name.ToLowerInvariant()))
			{
				newRoom.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Remove(newRoom.abstractRoom.name.ToLowerInvariant());
				flag = true;
			}
			warpPoint = newRoom.CheckForSpawnedWarp(placedObject.data as WarpPointData) ?? newRoom.TrySpawnWarpPoint(placedObject, !dontSave) ?? newRoom.ForceSpawnWarpPoint(placedObject, !dontSave);
			if (flag && newRoom.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility)
			{
				newRoom.game.GetStorySession.saveState.miscWorldSaveData.roomsSealedByWeaverAbility.Add(newRoom.abstractRoom.name.ToLowerInvariant());
			}
			warpPoint.Data.uses = warpPointData.uses;
			if (fadeInOnSpawn && newRoom.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility && !canWarpToVoidWeaverEnding)
			{
				warpPoint.dynamicWeaverFadeOut = true;
			}
			else if (!warpPoint.Data.oneWay && !warpPoint.Data.rippleWarp && newRoom.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility)
			{
				if (warpPoint.Data.limitedUse && warpPoint.Data.uses <= 1)
				{
					warpPoint.warpLocked = true;
					warpPoint.Data.oneWay = true;
					warpPoint.dynamicWeaverFadeOut = true;
				}
				warpPoint.refreshGraphics = true;
				newRoom.game.GetStorySession.saveState.miscWorldSaveData.numberOfWarpPointsSealed++;
			}
			warpPoint.ChangeState(State.ExitWarp);
		}
		warpPoint.closesEarly = true;
		List<AbstractPhysicalObject> list = new List<AbstractPhysicalObject>();
		if (warpPoint.Data.oneWay)
		{
			warpPoint.startingBadWarpAnim = true;
			warpPoint.SpawnBadWarpRipple(animate: true);
		}
		int warpPunish = RegionState.RippleSpawnEggState.WarpPunish;
		if (warpPunish > 0)
		{
			MiscWorldSaveData miscWorldSaveData = newRoom.game.GetStorySession.saveState.miscWorldSaveData;
			miscWorldSaveData.rippleEggsCollected -= warpPunish;
			for (int i = 0; i < warpPunish; i++)
			{
				if (miscWorldSaveData.rippleEggsToRespawn.Count <= 0)
				{
					break;
				}
				miscWorldSaveData.rippleEggsToRespawn.RemoveAt(0);
			}
		}
		newRoom.game.GetStorySession.warpsTraversedThisCycle++;
		for (int j = 0; j < newRoom.game.Players.Count; j++)
		{
			AbstractCreature abstractCreature = newRoom.game.Players[j];
			if (BlackListedCreatureTypes.Contains(abstractCreature.creatureTemplate.type))
			{
				continue;
			}
			if (!list.Contains(abstractCreature))
			{
				list.Add(abstractCreature);
			}
			if (abstractCreature.realizedCreature == null)
			{
				continue;
			}
			Player player = abstractCreature.realizedCreature as Player;
			if (newRoom.game.GetStorySession.finalWarpSequenceStarted)
			{
				player.pendingForcedWarpPos = null;
				player.pendingForcedWarpRoom = null;
				player.rippleDeathIntensity = 0f;
				player.rippleDeathTime = 0;
				if (player.forceMonolithWarp)
				{
					MonolithScene.SpawnMonolithScene(newRoom);
					WarpPointTimer warpPointTimer = camera.warpPointTimer;
					if (warpPointTimer != null)
					{
						warpPointTimer.forceNoExitVisuals = true;
					}
				}
				player.forceMonolithWarp = false;
			}
			for (int k = 0; k < player.grasps.Length; k++)
			{
				if (player.grasps[k] != null && !list.Contains(player.grasps[k].grabbed.abstractPhysicalObject))
				{
					list.Add(player.grasps[k].grabbed.abstractPhysicalObject);
				}
			}
			if (warpPoint.Data.oneWay)
			{
				player.OneWayPlacement(warpPoint.placedObject.pos, j);
				player.warpPointCooldown = 80;
				RemovePlayers(j);
			}
			player.ApplyWarpFatigue(newRoom.game);
			player.watcherInstability = 0f;
			player.watcherInstabilityGain = 0f;
			player.watcherTargetInstability = 0f;
			player.watcherForceOriginalLook = false;
		}
		List<PhysicalObject> list2 = new List<PhysicalObject>();
		for (int l = 0; l < newRoom.roomSettings.placedObjects.Count; l++)
		{
			if (!newRoom.roomSettings.placedObjects[l].deactivatedByWarpFilter)
			{
				continue;
			}
			for (int m = 0; m < newRoom.physicalObjects.Length; m++)
			{
				for (int n = 0; n < newRoom.physicalObjects[m].Count; n++)
				{
					if (newRoom.physicalObjects[m][n].abstractPhysicalObject is AbstractConsumable && (newRoom.physicalObjects[m][n].abstractPhysicalObject as AbstractConsumable).placedObjectIndex == l)
					{
						list2.Add(newRoom.physicalObjects[m][n]);
					}
				}
			}
		}
		for (int num = 0; num < list2.Count; num++)
		{
			newRoom.RemoveObject(list2[num]);
			list2[num].Destroy();
		}
		List<AbstractWorldEntity> list3 = new List<AbstractWorldEntity>();
		for (int num2 = 0; num2 < newRoom.roomSettings.placedObjects.Count; num2++)
		{
			if (!newRoom.roomSettings.placedObjects[num2].deactivatedByWarpFilter)
			{
				continue;
			}
			for (int num3 = 0; num3 < newRoom.abstractRoom.entities.Count; num3++)
			{
				if (newRoom.abstractRoom.entities[num3] is AbstractConsumable && (newRoom.abstractRoom.entities[num3] as AbstractConsumable).placedObjectIndex == num2)
				{
					list3.Add(newRoom.abstractRoom.entities[num3]);
				}
			}
		}
		for (int num4 = 0; num4 < list3.Count; num4++)
		{
			newRoom.abstractRoom.RemoveEntity(list3[num4]);
		}
		for (int num5 = 0; num5 < newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo.Count; num5++)
		{
			newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].SetLocalGravity(newRoom.game.GetStorySession.pendingWarpObjectsOriginalGravities[num5]);
			if (newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5] is Player)
			{
				(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5] as Player).customPlayerGravity = 0.9f;
			}
			if (newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].grabbedBy.Count > 0 && !list.Contains(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject))
			{
				list.Add(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject);
			}
			else
			{
				if (list.Contains(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject))
				{
					continue;
				}
				newRoom.game.GetStorySession.pendingWarpPointTransferObjects.Add(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject);
				if (AbstractPhysicalObject.IsObjectImportant(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject, newRoom.world) && !room.game.GetStorySession.importantWarpPointTransferedEntities.Contains(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject.ID))
				{
					room.game.GetStorySession.importantWarpPointTransferedEntities.Add(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject.ID);
				}
				if (!string.IsNullOrEmpty(newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject.placedObjectOrigin))
				{
					string[] array = newRoom.game.GetStorySession.pendingWarpObjectsToRestoreGravityTo[num5].abstractPhysicalObject.placedObjectOrigin.Split(new char[1] { ':' });
					if (newRoom.game.GetStorySession.warpedPlacedObjects.ContainsKey(array[0]))
					{
						newRoom.game.GetStorySession.warpedPlacedObjects[array[0]].Add(int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture));
						continue;
					}
					newRoom.game.GetStorySession.warpedPlacedObjects.Add(array[0], new List<int> { int.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture) });
				}
			}
		}
		for (int num6 = 0; num6 < newRoom.game.GetStorySession.pendingWarpPointTransferObjects.Count; num6++)
		{
			AbstractPhysicalObject abstractPhysicalObject = newRoom.game.GetStorySession.pendingWarpPointTransferObjects[num6];
			if (abstractPhysicalObject.realizedObject != null && abstractPhysicalObject.realizedObject.room != null)
			{
				abstractPhysicalObject.realizedObject.room.RemoveObject(abstractPhysicalObject.realizedObject);
			}
			if (abstractPhysicalObject.world != null && abstractPhysicalObject.Room != null)
			{
				abstractPhysicalObject.Room.RemoveEntity(abstractPhysicalObject);
			}
		}
		newRoom.game.GetStorySession.pendingWarpPointTransferId = IdentifyingString(newRoom.game, warpPoint.Data, newRoom.abstractRoom);
		newRoom.game.GetStorySession.pendingWarpPointSedateTime = 400;
		if (newRoom.game.GetStorySession.pendingSentientRotInfectionFromWarp && newRoom.InfectRoomWithSentientRot(0.25f))
		{
			newRoom.game.GetStorySession.saveState.miscWorldSaveData.numberOfSentientRotSpreads++;
			if (!Region.IsWatcherVanillaRegion(newRoom.world.name) && !newRoom.game.GetStorySession.saveState.miscWorldSaveData.regionsInfectedBySentientRotSpread.Contains(newRoom.world.name.ToLowerInvariant()))
			{
				newRoom.game.GetStorySession.saveState.miscWorldSaveData.regionsInfectedBySentientRotSpread.Add(newRoom.world.name.ToLowerInvariant());
			}
		}
		if (!newRoom.game.rainWorld.progression.miscProgressionData.regionsVisited.ContainsKey(newRoom.world.name))
		{
			newRoom.game.rainWorld.progression.miscProgressionData.regionsVisited.Add(newRoom.world.name, new List<string>());
		}
		if (!newRoom.game.rainWorld.progression.miscProgressionData.regionsVisited[newRoom.world.name].Contains(newRoom.game.StoryCharacter.value))
		{
			newRoom.game.rainWorld.progression.miscProgressionData.regionsVisited[newRoom.world.name].Add(newRoom.game.StoryCharacter.value);
		}
		if (Region.IsAncientUrbanRegion(newRoom.world.name))
		{
			Custom.rainWorld.processManager.CueAchievement(RainWorld.AchievementID.AncientUrban, 5f);
		}
		else
		{
			Custom.rainWorld.processManager.CueAchievement(RainWorld.AchievementID.WarpPoint, 5f);
		}
		Custom.rainWorld.progression.miscProgressionData.SetTokenCollected(WatcherEnums.LevelUnlockID.HP);
		if (newRoom.game.GetStorySession.spinningTopWarpsLeadingToRippleScreen.Contains(MyIdentifyingString()))
		{
			newRoom.game.GetStorySession.saveState.warpPointTargetAfterWarpPointSave = warpPointData;
			newRoom.game.GetStorySession.saveState.transferCreaturesAfterWarpPointSave.Clear();
			newRoom.game.GetStorySession.saveState.transferObjectsAfterWarpPointSave.Clear();
			newRoom.game.GetStorySession.saveState.importantTransferEntitiesAfterWarpPointSave.Clear();
			for (int num7 = 0; num7 < newRoom.game.GetStorySession.pendingWarpPointTransferObjects.Count; num7++)
			{
				if (newRoom.game.GetStorySession.pendingWarpPointTransferObjects[num7] is AbstractCreature)
				{
					newRoom.game.GetStorySession.saveState.transferCreaturesAfterWarpPointSave.Add(SaveState.AbstractCreatureToStringStoryWorld(newRoom.game.GetStorySession.pendingWarpPointTransferObjects[num7] as AbstractCreature));
				}
				else
				{
					newRoom.game.GetStorySession.saveState.transferObjectsAfterWarpPointSave.Add(newRoom.game.GetStorySession.pendingWarpPointTransferObjects[num7]);
				}
			}
			for (int num8 = 0; num8 < newRoom.game.GetStorySession.importantWarpPointTransferedEntities.Count; num8++)
			{
				newRoom.game.GetStorySession.saveState.importantTransferEntitiesAfterWarpPointSave.Add(newRoom.game.GetStorySession.importantWarpPointTransferedEntities[num8]);
			}
			if (newRoom.game.GetStorySession.saveState.miscWorldSaveData.hasSkippedFirstWarpFatigueTransfer == 0)
			{
				newRoom.game.GetStorySession.saveState.preserveWarpFatigueAfterWarpPointSave = 0;
				newRoom.game.GetStorySession.saveState.miscWorldSaveData.hasSkippedFirstWarpFatigueTransfer = 1;
			}
			else
			{
				newRoom.game.GetStorySession.saveState.preserveWarpFatigueAfterWarpPointSave = newRoom.game.GetStorySession.warpsTraversedThisCycle;
			}
			newRoom.game.Win(malnourished: false, fromWarpPoint: true);
		}
		newRoom.game.cameras[0].ExitCutsceneMode();
		if (ModManager.Watcher && newRoom.world.weaverPresence == null && newRoom.game.GetStorySession.saveState.miscWorldSaveData.hasVoidWeaverAbility && !Custom.rainWorld.progression.miscProgressionData.beaten_Watcher_VoidWeaver)
		{
			newRoom.world.InitiateWeaverHintTrailToOpenWarpPoints(newRoom.abstractRoom);
		}
	}
}
