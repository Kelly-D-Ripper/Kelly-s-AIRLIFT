using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mirage;
using NuclearOption.Chat;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KellysAirlift
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.nikkorap.blueprinter", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "kelly.nuclearoption.airlift";
        public const string PluginName = "Kelly's AIRLIFT";
        public const string PluginVersion = "0.14.0";

        private const string ChimeraKey = "Aryx_CargoPlane1";
        private const string LegacyChimeraAlias = "Aryx_MC260_Chimera";
        private const string ChimeraDefinitionAssetName = "Aryx_MC260_Chimera_Definition";
        private const string ChimeraManifestName = "Aryx MC-260 Chimera";
        private const string ChimeraAssemblyName = "Aryx_MC260_Chimera";
        private const string RadarMountDefault = "MC260_RadarContainerx1";
        private const string LegacyMunitionsContainerMount = "MunitionsContainerx1";
        private const string LegacyIncorrectMunitionsPalletMount = "MunitionsPallet2x4";
        private const string MunitionsPalletMountDefault = "MunitionsSmallPallet2x4";
        private const string R9MountDefault = "Aryx_MC260_R9SAMLauncherx1";
        private const string SlmmrMountDefault = "Aryx_IRSAM_Turret_x1";
        private const string TransportJammerMountDefault = "JammingPod1";
        private const string CombatSuppressionMountDefault = "AGM-68";
        private const string Type12MountDefault = "Aryx_MC260_Type12_x1";
        private const string AfvIfvMountDefault = "6x6_1_IFVx1";
        private const string AfvAaMountDefault = "6x6_1_AAx1";
        private const string FrcvDisplayDefault = "FRCV-105 LT";
        private const string RearCargoBay = "Cargo Bay Rear";
        private const string FrontCargoBay = "Cargo Bay Front";
        private const string MissionBay = "Mission Bay";
        private const string WingPylons = "Wing Pylons";
        private const float HardMaximumR9RadarDistance = 17000f;
        private const float TransportJammerTargetRange = 17000f;
        private const float CombatSuppressionAircraftMaxHeight = 35f;

        private static readonly FieldInfo ActiveAiAircraftField = AccessTools.Field(typeof(FactionHQ), "activeAIAircraft");
        private static readonly FieldInfo GroundVehicleParachuteField = AccessTools.Field(typeof(GroundVehicle), "parachuteSystem");
        private static readonly FieldInfo ContainerParachuteField = AccessTools.Field(typeof(Container), "parachuteSystem");
        private static readonly FieldInfo WeaponStationIndexField = AccessTools.Field(typeof(WeaponStation), "weaponIndex");
        private static readonly FieldInfo MountedCargoDoorField = AccessTools.Field(typeof(MountedCargo), "cargoDoor");
        private static readonly FieldInfo WeaponHardpointField = AccessTools.Field(typeof(Weapon), "hardpoint");
        private static readonly MethodInfo LandingSwitchModeMethod =
            AccessTools.Method(typeof(AIPilotLandingState), "SwitchMode");
        private static readonly Type LandingModeType =
            AccessTools.Inner(typeof(AIPilotLandingState), "LandingMode");
        private static readonly List<CargoCatalogueEntry> Catalogue = new List<CargoCatalogueEntry>
        {
            Entry(RadarMountDefault, "Radar Container", 1, true,
                "Chimera rear/front cargo bay; provides SARH guidance to nearby R9 launchers."),
            Entry(MunitionsPalletMountDefault, "Small Munitions Pallet x4", 4, true,
                "Base-game x4 small-pallet cargo mount; valid in both Chimera cargo bays."),
            Entry(R9MountDefault, "R9 SAM Launcher", 1, true,
                "Chimera Mission Bay only; one launcher per aircraft."),
            Entry(SlmmrMountDefault, "SLMMR-A3 SAM Launcher", 1, true,
                "Chimera rear/front cargo bay; two independent x1 mounts are used."),
            Entry(Type12MountDefault, "Type-12 MBT", 1, false,
                "Combat-drop Mission Bay load. Ground-level runway release only."),
            Entry(AfvIfvMountDefault, "AFV6 IFV", 1, false,
                "Combat-drop cargo-bay load. Ground-level runway release only."),
            Entry(AfvAaMountDefault, "AFV6 AA", 1, false,
                "Combat-drop cargo-bay load. Ground-level runway release only."),
            Entry("Aryx_FRCV_Artillery_x1", "FRCV SPG", 1, true,
                "Catalogue evidence retained; not part of the battery manifest."),
            Entry("Aryx_MC260_UGVDozer_2x", "M12 Jackknife x2", 2, true,
                "Catalogue evidence retained; not part of the battery manifest.")
        };

        private static Plugin _instance;
        private Harmony _harmony;
        private ConfigEntry<ulong> _ownerSteamId;
        private ConfigEntry<bool> _allowListenServer;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _announceMilestonesInGame;
        private ConfigEntry<string> _aircraftKey;
        private ConfigEntry<string> _radarMountKey;
        private ConfigEntry<string> _munitionsMountKey;
        private ConfigEntry<string> _r9MountKey;
        private ConfigEntry<string> _slmmrMountKey;
        private ConfigEntry<string> _transportJammerMountKey;
        private ConfigEntry<string> _combatType12MountKey;
        private ConfigEntry<string> _combatAfvIfvMountKey;
        private ConfigEntry<string> _combatAfvAaMountKey;
        private ConfigEntry<string> _combatFrcvMountKey;
        private ConfigEntry<string> _combatFrcvDisplayName;
        private ConfigEntry<string> _combatSuppressionMount;
        private ConfigEntry<float> _combatSuppressionRange;
        private ConfigEntry<float> _combatSuppressionRetry;
        private ConfigEntry<float> _combatDropAltitude;
        private ConfigEntry<float> _combatDescentDistance;
        private ConfigEntry<float> _combatTrailSpacing;
        private ConfigEntry<float> _combatMinimumRunwayLength;
        private ConfigEntry<float> _combatMinimumRunwayWidth;
        private ConfigEntry<float> _combatMaximumSlope;
        private ConfigEntry<float> _combatRunwayDispersalDistance;
        private ConfigEntry<bool> _publicCombatPurchasesEnabled;
        private ConfigEntry<float> _publicCombatPurchaseCost;
        private ConfigEntry<float> _publicCombatPurchaseCooldown;
        private ConfigEntry<string> _zoneName;
        private ConfigEntry<string> _zoneMapKey;
        private ConfigEntry<float> _zoneX;
        private ConfigEntry<float> _zoneZ;
        private ConfigEntry<bool> _requireRecordedZones;
        private ConfigEntry<bool> _useOwnerAircraftPosition;
        private ConfigEntry<float> _ownerAheadDistance;
        private ConfigEntry<float> _zoneSearchRadius;
        private ConfigEntry<float> _zoneSearchStep;
        private ConfigEntry<float> _dynamicAirstripMinimumRadius;
        private ConfigEntry<float> _dynamicAirstripMaximumRadius;
        private ConfigEntry<int> _dynamicAirstripAttempts;
        private ConfigEntry<float> _dynamicBuildingClearance;
        private ConfigEntry<string> _expectedFaction;
        private ConfigEntry<float> _approachHeading;
        private ConfigEntry<float> _r9RadarOffset;
        private ConfigEntry<float> _munitionsOffset;
        private ConfigEntry<float> _slmmrOffset;
        private ConfigEntry<float> _cruiseAltitude;
        private ConfigEntry<float> _dropAltitude;
        private ConfigEntry<float> _descentDistance;
        private ConfigEntry<float> _releaseLeadDistance;
        private ConfigEntry<float> _spawnDistance;
        private ConfigEntry<float> _formationSpacing;
        private ConfigEntry<float> _egressDistance;
        private ConfigEntry<float> _egressTurnDegrees;
        private ConfigEntry<float> _spawnSpeed;
        private ConfigEntry<float> _formationSpawnInterval;
        private ConfigEntry<bool> _asyncFormationSpawning;
        private ConfigEntry<float> _asyncSpawnIntegrationBudget;
        private ConfigEntry<float> _cruiseThrottle;
        private ConfigEntry<float> _maximumSlope;
        private ConfigEntry<float> _seaMargin;
        private ConfigEntry<float> _clearanceRadius;
        private ConfigEntry<float> _clearanceHeight;
        private ConfigEntry<float> _hostileSeparation;
        private ConfigEntry<float> _releaseRadius;
        private ConfigEntry<float> _precisionParachuteLeadTime;
        private ConfigEntry<float> _precisionLateTolerance;
        private ConfigEntry<float> _precisionAlignment;
        private ConfigEntry<float> _altitudeTolerance;
        private ConfigEntry<float> _minimumReleaseSpeed;
        private ConfigEntry<float> _maximumReleaseSpeed;
        private ConfigEntry<float> _maximumRoll;
        private ConfigEntry<float> _maximumVerticalSpeed;
        private ConfigEntry<float> _releaseInterval;
        private ConfigEntry<float> _cargoDoorHoldTime;
        private ConfigEntry<float> _cargoDoorRefreshInterval;
        private ConfigEntry<int> _flareBurstCount;
        private ConfigEntry<float> _flareBurstInterval;
        private ConfigEntry<float> _operationTimeout;
        private ConfigEntry<float> _cargoSpawnTimeout;
        private ConfigEntry<float> _cargoTouchdownTimeout;
        private ConfigEntry<string> _postDrop;
        private ConfigEntry<float> _postDropDelay;
        private ConfigEntry<float> _returnArrivalRadius;
        private ConfigEntry<string> _preferredRecoveryAirbase;
        private ConfigEntry<float> _recoveryHoldRadius;
        private ConfigEntry<float> _recoveryHoldAltitude;
        private ConfigEntry<bool> _airportCaptureAirliftsEnabled;
        private ConfigEntry<bool> _allowListenServerCaptureTesting;
        private ConfigEntry<bool> _allowDynamicCaptureZones;
        private ConfigEntry<float> _airportCaptureCooldown;
        private ConfigEntry<float> _automaticOffMapMargin;
        private ConfigEntry<bool> _cleanupCargoWhenEmpty;

        private BatteryOperation _operation;
        private readonly List<DeployedCargoRecord> _trackedCargo = new List<DeployedCargoRecord>();
        private int _nextOperationId = 1;
        private float _nextTick;
        private float _nextPlayerScan;
        private float _nextPersistentUpdate;
        private float _nextAirbaseOwnershipScan;
        private bool _automaticStartContext;
        private bool _captureMapEligibilityKnown;
        private bool _captureMapEligible;
        private readonly Dictionary<Airbase, FactionHQ> _knownAirbaseOwners =
            new Dictionary<Airbase, FactionHQ>();
        private readonly Dictionary<string, float> _captureCooldownUntil =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Queue<CaptureAirliftRequest> _pendingCaptureAirlifts =
            new Queue<CaptureAirliftRequest>();
        private readonly Dictionary<FactionHQ, float> _combatPurchaseCooldownUntil =
            new Dictionary<FactionHQ, float>();
        private readonly Dictionary<ulong, float> _nextPublicPurchaseCommand =
            new Dictionary<ulong, float>();
        private float _nextAdminCommand;
        private string _missionIdentity;
        private bool _serverHadPlayers;
        private bool _cachedHasPlayers;
        private bool _chimeraContentActivated;
        private string _chimeraActivationReport;
        private string _zoneFilePath;

        private sealed class CoordinatedAircraftSpawn
        {
            public Aircraft Aircraft;
            public Exception Error;
            public long TotalMilliseconds;
            public long NetworkMilliseconds;
            public float WorstObservedFrameMilliseconds;
        }

        private static CargoCatalogueEntry Entry(string key, string displayName, int rounds, bool certified, string note)
        {
            return new CargoCatalogueEntry {
                Key = key,
                DisplayName = displayName,
                PublishedRounds = rounds,
                CertifiedForParachuteDrop = certified,
                ResearchNote = note
            };
        }

        private void Awake()
        {
            _instance = this;
            BindConfiguration();
            _zoneFilePath = Path.Combine(Paths.ConfigPath, "kelly.nuclearoption.airlift.zones.tsv");
            try
            {
                ValidateRequiredApi();
                _harmony = new Harmony(PluginGuid);
                MethodInfo chatMethod = AccessTools.Method(typeof(ChatManager),
                    "UserCode_CmdSendChatMessage_-456754112",
                    new[] { typeof(string), typeof(bool), typeof(INetworkPlayer) });
                if (chatMethod == null) throw new MissingMethodException("0.34 server chat RPC implementation was not found.");
                _harmony.Patch(chatMethod, prefix: new HarmonyMethod(typeof(Plugin), nameof(ServerChatCommandPrefix)));

                MethodInfo spawnUnit = AccessTools.Method(typeof(Spawner), nameof(Spawner.SpawnUnit),
                    new[] { typeof(UnitDefinition), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Player) });
                if (spawnUnit == null) throw new MissingMethodException("0.34 Spawner.SpawnUnit signature was not found.");
                _harmony.Patch(spawnUnit, postfix: new HarmonyMethod(typeof(Plugin), nameof(SpawnUnitPostfix)));

                Logger.LogInfo(PluginName + " " + PluginVersion
                    + " loaded. Four-Chimera battery operations are standalone and fail-closed.");
                Logger.LogInfo("Chimera compatibility uses stable addon identity plus live "
                    + "aircraft, cargo-mount, station, jammer, and bay-door capability validation.");
            }
            catch (Exception exception)
            {
                Logger.LogError("AIRLIFT disabled because required 0.34 APIs failed validation: " + exception);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_operation != null) AbortOperation("plugin unloaded", true);
            CleanupPersistentlyTrackedUnits();
            if (_harmony != null) _harmony.UnpatchSelf();
            if (_instance == this) _instance = null;
        }

        private void BindConfiguration()
        {
            _ownerSteamId = Config.Bind("Authorization", "OwnerSteamId", 0UL,
                "Only this Steam ID may issue /airlift commands. Zero disables manual commands; set explicitly on every server.");
            _allowListenServer = Config.Bind("Authorization", "AllowListenServerTesting", false,
                "Allow authoritative operation on a listen server. Dedicated servers are always allowed.");
            _diagnostics = Config.Bind("Diagnostics", "VerboseLogging", true,
                "Log manifest validation, transport transitions, cargo tracking, and radar-link measurements.");
            _announceMilestonesInGame = Config.Bind("Messaging",
                "AnnounceMilestonesInGame", true,
                "Broadcast only AIRLIFT launch, individual aircraft-loss, and successful-drop milestones. Routine phases remain log-only; manual command replies remain private.");

            _aircraftKey = Config.Bind("Battery", "AircraftDefinition", ChimeraKey,
                "Exact Encyclopedia aircraft key. The battery manifest requires this Chimera definition.");
            string normalizedAircraftKey =
                AirliftPolicy.NormalizeChimeraDefinitionKey(_aircraftKey.Value);
            if (!string.Equals(normalizedAircraftKey, _aircraftKey.Value, StringComparison.Ordinal))
            {
                Logger.LogWarning("Migrating obsolete AIRLIFT aircraft alias '" + LegacyChimeraAlias
                    + "' to the Chimera bundle's native definition key '" + ChimeraKey + "'.");
                _aircraftKey.Value = normalizedAircraftKey;
            }
            _radarMountKey = Config.Bind("Battery", "RadarMount", RadarMountDefault,
                "Exact WeaponMount key for the radar container.");
            _munitionsMountKey = Config.Bind("Battery", "MunitionsMount", MunitionsPalletMountDefault,
                "Exact WeaponMount key for each x4 small-munitions-pallet package.");
            string configuredMunitionsMount = (_munitionsMountKey.Value ?? string.Empty).Trim();
            if (string.Equals(configuredMunitionsMount,
                    LegacyMunitionsContainerMount, StringComparison.Ordinal)
                || string.Equals(configuredMunitionsMount,
                    LegacyIncorrectMunitionsPalletMount, StringComparison.Ordinal))
            {
                Logger.LogWarning("Migrating obsolete munitions mount '" + configuredMunitionsMount
                    + "' to the Nuclear Option 0.34 x4 small-pallet key '"
                    + MunitionsPalletMountDefault + "'.");
                _munitionsMountKey.Value = MunitionsPalletMountDefault;
            }
            _r9MountKey = Config.Bind("Battery", "R9Mount", R9MountDefault,
                "Exact WeaponMount key used by each of three Mission-Bay R9 transports.");
            _slmmrMountKey = Config.Bind("Battery", "SlmmrMount", SlmmrMountDefault,
                "Exact WeaponMount key used by the two retained SLMMR transports.");
            _transportJammerMountKey = Config.Bind("Battery", "TransportJammerMount",
                TransportJammerMountDefault,
                "Exact Radar Jamming Pod WeaponMount key used on every Chimera Wing Pylons hardpoint.");
            _r9RadarOffset = Config.Bind("Battery", "R9RadarTargetOffset", 10f,
                "Horizontal target offset for each R9 from the radar. Maximum supported coverage is 17 km.");
            _munitionsOffset = Config.Bind("Battery", "MunitionsTargetOffset", 12f,
                "Distance fore/aft of the radar used for the two small-pallet release groups.");
            _slmmrOffset = Config.Bind("Battery", "SlmmrTargetOffset", 38f,
                "Lateral target offset for each SLMMR launcher.");

            _combatType12MountKey = Config.Bind("CombatDrop", "Type12Mount",
                Type12MountDefault,
                "Exact Chimera Mission Bay WeaponMount key for each Type-12 MBT.");
            _combatAfvIfvMountKey = Config.Bind("CombatDrop", "AfvIfvMount",
                AfvIfvMountDefault,
                "Exact base-game WeaponMount key for AFV6 IFV cargo. 'IFC' in the design brief is interpreted as IFV.");
            _combatAfvAaMountKey = Config.Bind("CombatDrop", "AfvAaMount",
                AfvAaMountDefault,
                "Exact base-game WeaponMount key for AFV6 AA cargo.");
            _combatFrcvMountKey = Config.Bind("CombatDrop", "FrcvMount", "",
                "Optional exact FRCV-105 LT WeaponMount key. Empty strictly resolves one live Chimera cargo option by display name.");
            _combatFrcvDisplayName = Config.Bind("CombatDrop", "FrcvDisplayName",
                FrcvDisplayDefault,
                "Exact displayed identity used only when FrcvMount is empty; ambiguous or missing matches fail closed.");
            _combatSuppressionMount = Config.Bind("CombatDrop", "SuppressionMount",
                CombatSuppressionMountDefault,
                "Exact json key or unique displayed identity for the three-round AGM-68 Wing Pylons mount used by combat transports.");
            _combatSuppressionRange = Config.Bind("CombatDrop", "SuppressionRange", 8000f,
                "Maximum range at which combat transports may attack a tracked hostile ground unit in the selected runway corridor; clamped to 2000-15000 m.");
            _combatSuppressionRetry = Config.Bind("CombatDrop", "SuppressionRetrySeconds", 8f,
                "Minimum time before another AGM-68 may be assigned to the same surviving runway obstruction; clamped to 3-30 seconds.");
            _combatDropAltitude = Config.Bind("CombatDrop", "DropRadarAltitude", 5f,
                "Ground-level combat release height. Hard-clamped to 2-5 metres AGL.");
            _combatDescentDistance = Config.Bind("CombatDrop", "DescentStartDistance", 6000f,
                "Distance before the runway release line at which the formation begins its low-level descent.");
            _combatTrailSpacing = Config.Bind("CombatDrop", "TrailSpacing", 140f,
                "Longitudinal centreline spacing between the five Chimeras, clamped to 100-200 metres.");
            _combatMinimumRunwayLength = Config.Bind("CombatDrop", "MinimumRunwayLength", 1200f,
                "Preferred live runway length for a five-Chimera RAPID drop. If unavailable, AIRLIFT may use a fully validated compact runway down to 600m.");
            _combatMinimumRunwayWidth = Config.Bind("CombatDrop", "MinimumRunwayWidth", 25f,
                "Preferred live runway width. If unavailable, AIRLIFT may use a fully validated compact runway down to 8m.");
            _combatMaximumSlope = Config.Bind("CombatDrop", "MaximumSlopeDegrees", 2f,
                "Hard live-terrain slope limit at every combat cargo target.");
            _combatRunwayDispersalDistance = Config.Bind("CombatDrop",
                "RunwayDispersalDistance", 180f,
                "Distance combat vehicles drive perpendicular to the runway immediately after native landing activation; clamped to 80-400m.");
            _publicCombatPurchasesEnabled = Config.Bind("PublicPurchase",
                "EnableCombatDropPurchases", true,
                "Allow faction players to purchase a server-authoritative RAPID drop from the optional public Donate-menu client addon.");
            _publicCombatPurchaseCost = Config.Bind("PublicPurchase",
                "CombatDropCost", 400f,
                "Player allocation charged for an accepted RAPID drop, in Nuclear Option's native million-dollar units; 400 means $400m. Clamped to $1m-$1b. Keep the public UI config synchronized for an accurate displayed price.");
            _publicCombatPurchaseCooldown = Config.Bind("PublicPurchase",
                "CombatDropCooldownSeconds", 300f,
                "Per-faction cooldown after an accepted RAPID drop. A five-minute minimum is enforced.");

            _zoneName = Config.Bind("DropZone", "Name", "SOUTH_BOSCALI_FIELD", "Named battery drop zone.");
            _zoneMapKey = Config.Bind("DropZone", "MapKey", "Terrain1", "Exact Mission.MapKey.Path value.");
            _zoneX = Config.Bind("DropZone", "GlobalX", -11800f,
                "Radar target global X; offline profile only, always runtime revalidated.");
            _zoneZ = Config.Bind("DropZone", "GlobalZ", -4900f,
                "Radar target global Z; offline profile only, always runtime revalidated.");
            _requireRecordedZones = Config.Bind("DropZone", "RequireRecordedZones", false,
                "Refuse /airlift start unless a saved /addzone location validates on the running map.");
            _useOwnerAircraftPosition = Config.Bind("DropZone", "UseOwnerAircraftPosition", false,
                "Use a point ahead of the authorized owner's aircraft, then search nearby for a fully valid cluster.");
            _ownerAheadDistance = Config.Bind("DropZone", "OwnerAheadDistance", 1500f,
                "Distance ahead of the owner's aircraft at which automatic zone search begins.");
            _zoneSearchRadius = Config.Bind("DropZone", "SearchRadius", 1800f,
                "Maximum automatic search radius around the owner-directed point.");
            _zoneSearchStep = Config.Bind("DropZone", "SearchStep", 300f,
                "Spacing between automatic zone-search rings.");
            _dynamicAirstripMinimumRadius = Config.Bind("DropZone",
                "DynamicAirstripMinimumRadius", 900f,
                "Minimum random landing-point distance from the named airbase centre.");
            _dynamicAirstripMaximumRadius = Config.Bind("DropZone",
                "DynamicAirstripMaximumRadius", 3200f,
                "Maximum random landing-point distance from the named airbase centre.");
            _dynamicAirstripAttempts = Config.Bind("DropZone",
                "DynamicAirstripAttempts", 64,
                "Maximum random candidates tested by '<airbase> random'. Clamped to 8-128.");
            _dynamicBuildingClearance = Config.Bind("DropZone",
                "DynamicBuildingClearance", 120f,
                "Required static-obstruction clearance around a dynamic landing centre.");
            _expectedFaction = Config.Bind("DropZone", "ExpectedFaction", "",
                "Optional exact faction name/tag. Empty means the authorized owner's current faction.");
            _approachHeading = Config.Bind("DropZone", "ApproachHeadingDegrees", 90f,
                "Flight heading through the zone (0 north/+Z, 90 east/+X).");
            _cruiseAltitude = Config.Bind("DropZone", "CruiseRadarAltitude", 1200f,
                "Ingress radar altitude before the synchronized descent.");
            _dropAltitude = Config.Bind("DropZone", "DropRadarAltitude", 450f,
                "Target radar altitude during cargo release. AIRLIFT enforces a 450 m minimum.");
            _descentDistance = Config.Bind("Flight", "DescentStartDistance", 5000f,
                "Distance before the planned release line at which all Chimeras descend together.");
            _releaseLeadDistance = Config.Bind("Flight", "ReleaseLeadDistance", 1400f,
                "Minimum/fallback upstream route offset. Live release uses the precision impact predictor.");
            _maximumSlope = Config.Bind("DropZone", "MaximumSlopeDegrees", 7f,
                "Hard terrain-slope ceiling at every cargo target. Dynamic selection still prefers the flattest valid cluster.");
            _seaMargin = Config.Bind("DropZone", "SeaLevelMargin", 3f, "Required terrain height above local sea level.");
            _clearanceRadius = Config.Bind("DropZone", "ClearanceRadius", 12f,
                "Static and live-unit obstruction radius around each individual cargo target.");
            _clearanceHeight = Config.Bind("DropZone", "ClearanceHeight", 25f,
                "Static obstruction clearance height at every cargo target.");
            _hostileSeparation = Config.Bind("DropZone", "HostileSeparation", 900f,
                "Minimum horizontal distance from hostile units.");

            _spawnDistance = Config.Bind("Flight", "SpawnDistance", 6000f,
                "Ingress distance behind the zone for the lead transport.");
            _formationSpacing = Config.Bind("Flight", "TrailSpacing", 120f,
                "Legacy key: lateral spacing between synchronized transport lanes, clamped to 80-250 m.");
            _egressDistance = Config.Bind("Flight", "EgressDistance", 4500f,
                "Fly-through distance beyond each transport's drop point.");
            _egressTurnDegrees = Config.Bind("Flight", "EgressTurnDegrees", 30f,
                "Shared post-drop bank. Positive turns every Chimera right; negative turns every Chimera left; clamped to 60 degrees.");
            _spawnSpeed = Config.Bind("Flight", "SpawnSpeed", 145f, "Initial true velocity in m/s.");
            _formationSpawnInterval = Config.Bind("Flight", "FormationSpawnInterval", 0.75f,
                "Unscaled seconds between heavyweight aircraft instantiations, clamped to 0.25-2.0.");
            _asyncFormationSpawning = Config.Bind("Flight", "AsyncFormationSpawning", true,
                "Use Unity's incremental async prefab integration before native server network spawning.");
            _asyncSpawnIntegrationBudget = Config.Bind("Flight", "AsyncSpawnIntegrationBudgetMs", 2f,
                "Maximum async prefab integration work per frame while assembling the formation; clamped to 0.5-8 ms.");
            _cruiseThrottle = Config.Bind("Flight", "MinimumCruiseThrottle", 0.72f, "Minimum transport throttle.");
            _releaseRadius = Config.Bind("Flight", "ReleaseRadius", 750f,
                "ALPHA safe-point acquisition radius, clamped to 600-1,200 m. Followers use its exact release gate.");
            _precisionParachuteLeadTime = Config.Bind("Flight",
                "PrecisionParachuteLeadTime", 0f,
                "Extra predicted flight time allowed for native parachute drag. Clamped to 0-15 seconds.");
            _precisionLateTolerance = Config.Bind("Flight",
                "PrecisionLateTolerance", 1.5f,
                "Maximum predicted late-release tolerance before AIRLIFT forces the synchronized drop. Clamped to 0.25-5 seconds.");
            _precisionAlignment = Config.Bind("Flight",
                "PrecisionAlignmentDegrees", 25f,
                "Maximum angle between horizontal aircraft velocity and the landing point for a precision release. Clamped to 5-45 degrees.");
            _altitudeTolerance = Config.Bind("Flight", "AltitudeTolerance", 200f,
                "Allowed radar-altitude error. AIRLIFT enforces a 200 m minimum.");
            _minimumReleaseSpeed = Config.Bind("Flight", "MinimumReleaseSpeed", 75f, "Minimum release speed.");
            _maximumReleaseSpeed = Config.Bind("Flight", "MaximumReleaseSpeed", 190f, "Maximum release speed.");
            _maximumRoll = Config.Bind("Flight", "MaximumAbsoluteRoll", 18f,
                "Maximum absolute bank angle during release.");
            _maximumVerticalSpeed = Config.Bind("Flight", "MaximumAbsoluteVerticalSpeed", 30f,
                "Maximum absolute vertical speed during release.");
            _releaseInterval = Config.Bind("Flight", "SequentialReleaseInterval", 0.35f,
                "Seconds between confirmed native cargo releases on the same aircraft.");
            _cargoDoorHoldTime = Config.Bind("Flight", "CargoDoorHoldTime", 30f,
                "Seconds each native Chimera cargo-door command remains active; clamped to 15-120 seconds.");
            _cargoDoorRefreshInterval = Config.Bind("Flight", "CargoDoorRefreshInterval", 5f,
                "Seconds between low-cost cargo-door refreshes during ingress; clamped to 2-15 seconds.");
            _flareBurstCount = Config.Bind("Flight", "DropFlareBurstCount", 10,
                "Number of explicit native flare pulses fired by every surviving Chimera during the drop; clamped to 1-20.");
            _flareBurstInterval = Config.Bind("Flight", "DropFlareBurstInterval", 0.5f,
                "Seconds between native flare pulses; clamped to 0.2-2.0 seconds.");
            _operationTimeout = Config.Bind("Flight", "OperationTimeout", 480f, "Whole battery operation timeout.");
            _cargoSpawnTimeout = Config.Bind("Flight", "CargoSpawnConfirmationTimeout", 20f,
                "Wait for each native rail deployment SpawnUnit result.");
            _cargoTouchdownTimeout = Config.Bind("Flight", "CargoTouchdownTimeout", 180f,
                "Wait after deployment for radar/R9 touchdown and the 17 km coverage check.");

            _postDrop = Config.Bind("PostDrop", "Behavior", "Return",
                "Return flies every surviving transport back beyond its original entry edge and despawns only the aircraft. Land remains available. Despawn is a compatibility alias for Return.");
            _postDropDelay = Config.Bind("PostDrop", "Delay", 8f, "Delay after each transport's final confirmed cargo spawn.");
            _returnArrivalRadius = Config.Bind("PostDrop", "ReturnArrivalRadius", 600f,
                "Arrival radius around each original off-map entry point before AIRLIFT despawns only that transport; clamped to 200-1200 m.");
            _preferredRecoveryAirbase = Config.Bind("PostDrop", "PreferredRecoveryAirbase", "Maris",
                "Friendly suitable airbase preferred for the first Chimera pair; the second pair uses another suitable field when possible.");
            _recoveryHoldRadius = Config.Bind("PostDrop", "RecoveryHoldRadius", 3500f,
                "Radius of the separated Chimera holding pattern around the recovery airbase.");
            _recoveryHoldAltitude = Config.Bind("PostDrop", "RecoveryHoldRadarAltitude", 1000f,
                "Base radar altitude for queued transports awaiting landing clearance.");

            _airportCaptureAirliftsEnabled = Config.Bind("Automatic",
                "EnableAirportCaptureAirlifts", false,
                "Dedicated-server director: queue an airlift when either faction takes control of an airport; recorded zones are preferred and validated dynamic fallback is configurable.");
            _allowListenServerCaptureTesting = Config.Bind("Automatic",
                "AllowListenServerCaptureTesting", false,
                "Allow airport-capture triggers on a graphical local host. Keep false for server-only production behavior.");
            _allowDynamicCaptureZones = Config.Bind("Automatic",
                "AllowValidatedDynamicZoneFallback", true,
                "When a captured airport has no recorded zone, search live terrain around that airport and dispatch only if the full landing cluster validates.");
            _airportCaptureCooldown = Config.Bind("Automatic",
                "AirportCaptureCooldownSeconds", 600f,
                "Minimum time before the same airport/faction combination may trigger another automatic airlift; clamped to 60-3600 seconds.");
            _automaticOffMapMargin = Config.Bind("Automatic",
                "OffMapSpawnMargin", 3000f,
                "Automatic capture flights extend their configured inbound line until the formation is this far outside a map edge; clamped to 1000-10000 m.");
            _cleanupCargoWhenEmpty = Config.Bind("Lifecycle", "CleanupDeployedCargoWhenServerEmpty", true,
                "Destroy every tracked deployed battery unit when the server empties.");

        }

        private void ValidateRequiredApi()
        {
            if (ActiveAiAircraftField == null) throw new MissingFieldException("FactionHQ.activeAIAircraft");
            if (GroundVehicleParachuteField == null) throw new MissingFieldException("GroundVehicle.parachuteSystem");
            if (WeaponStationIndexField == null) throw new MissingFieldException("WeaponStation.weaponIndex");
            if (MountedCargoDoorField == null) throw new MissingFieldException("MountedCargo.cargoDoor");
            if (WeaponHardpointField == null) throw new MissingFieldException("Weapon.hardpoint");
            PostDropBehavior configuredPostDrop;
            if (AirliftPolicy.TryParsePostDrop(_postDrop.Value, out configuredPostDrop)
                && configuredPostDrop == PostDropBehavior.Land
                && (LandingSwitchModeMethod == null || LandingModeType == null))
                throw new MissingMethodException("AIPilotLandingState.SwitchMode(LandingMode)");
            if (AccessTools.Method(typeof(WeaponStation), nameof(WeaponStation.LaunchMount),
                    new[] { typeof(Unit), typeof(Unit), typeof(GlobalPosition) }) == null)
                throw new MissingMethodException("WeaponStation.LaunchMount(Unit, Unit, GlobalPosition)");
            if (AccessTools.Method(typeof(MountedCargo), nameof(MountedCargo.Fire)) == null)
                throw new MissingMethodException("MountedCargo.Fire");
            if (AccessTools.Method(typeof(JammingPod), nameof(JammingPod.SetTarget),
                    new[] { typeof(Unit) }) == null)
                throw new MissingMethodException("JammingPod.SetTarget(Unit)");
            if (AccessTools.Method(typeof(JammingPod), nameof(JammingPod.Fire),
                    new[] { typeof(Unit), typeof(Unit), typeof(Vector3),
                        typeof(WeaponStation), typeof(GlobalPosition) }) == null)
                throw new MissingMethodException("JammingPod.Fire(Unit, Unit, Vector3, WeaponStation, GlobalPosition)");
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < _nextTick) return;
            _nextTick = now + ActiveUpdateInterval();
            MissionManager manager = SafeMissionManager();
            bool authoritative = manager != null && AirliftPolicy.CanRun(manager.IsServer, MissionManager.IsRunning,
                Application.isBatchMode, _allowListenServer.Value);

            if (!authoritative)
            {
                if (_operation != null) AbortOperation("authoritative mission stopped or changed", true);
                CleanupPersistentlyTrackedUnits();
                _missionIdentity = null;
                _serverHadPlayers = false;
                _cachedHasPlayers = false;
                ResetCaptureDirector();
                return;
            }

            string missionIdentity = CurrentMissionIdentity();
            if (_missionIdentity != null && !string.Equals(_missionIdentity, missionIdentity, StringComparison.Ordinal))
            {
                if (_operation != null) AbortOperation("mission changed", true);
                CleanupPersistentlyTrackedUnits();
                ResetCaptureDirector();
            }
            _missionIdentity = missionIdentity;

            if (now >= _nextPlayerScan)
            {
                _nextPlayerScan = now + 1f;
                _cachedHasPlayers = false;
                foreach (Player player in UnitRegistry.playerLookup.Values)
                {
                    if (player == null || player.SteamID == 0UL) continue;
                    _cachedHasPlayers = true;
                    break;
                }
            }
            bool hasPlayers = _cachedHasPlayers;
            if (hasPlayers) _serverHadPlayers = true;
            else if (_serverHadPlayers)
            {
                if (_operation != null) AbortOperation("server emptied", _cleanupCargoWhenEmpty.Value);
                CleanupPersistentlyTrackedUnits(_cleanupCargoWhenEmpty.Value);
                _serverHadPlayers = false;
                _cachedHasPlayers = false;
                _nextAdminCommand = 0f;
                ResetCaptureDirector();
                return;
            }

            if (now >= _nextPersistentUpdate)
            {
                bool inspectionPending = UpdatePersistentlyTrackedUnits();
                _nextPersistentUpdate = now + (inspectionPending ? 0.5f : 30f);
            }
            if (_operation != null) UpdateOperation(now);
            if (hasPlayers) UpdateAirportCaptureDirector(now);
        }

        private void ResetCaptureDirector()
        {
            _knownAirbaseOwners.Clear();
            _captureCooldownUntil.Clear();
            _pendingCaptureAirlifts.Clear();
            _combatPurchaseCooldownUntil.Clear();
            _nextPublicPurchaseCommand.Clear();
            _nextAirbaseOwnershipScan = 0f;
            _automaticStartContext = false;
            _captureMapEligibilityKnown = false;
            _captureMapEligible = false;
        }

        private void UpdateAirportCaptureDirector(float now)
        {
            bool automaticActive = _airportCaptureAirliftsEnabled.Value
                && (Application.isBatchMode || _allowListenServerCaptureTesting.Value);
            if (!automaticActive) return;
            if (!CaptureDirectorSupportsCurrentMap()) return;
            if (now >= _nextAirbaseOwnershipScan)
            {
                _nextAirbaseOwnershipScan = now + 1f;
                ScanAirportOwnershipChanges(now);
            }
            if (_operation != null || _pendingCaptureAirlifts.Count == 0) return;

            CaptureAirliftRequest request = _pendingCaptureAirlifts.Dequeue();
            if (request == null || request.Airbase == null || request.Airbase.disabled
                || request.Hq == null || request.Airbase.CurrentHQ != request.Hq)
                return;
            Player factionContext = UnitRegistry.playerLookup.Values.FirstOrDefault(player =>
                player != null && player.SteamID != 0UL && player.HQ == request.Hq);
            if (factionContext == null)
            {
                if (now - request.QueuedAt < 120f)
                    _pendingCaptureAirlifts.Enqueue(request);
                else
                    Logger.LogWarning("AIRLIFT automatic request for "
                        + request.AirbaseName + " expired after two minutes without a live "
                        + PreferredFactionKey(request.Hq.faction) + " player context; cooldown not consumed.");
                return;
            }

            _automaticStartContext = true;
            try
            {
                StartOperation(factionContext, null, request.ZoneSelector);
            }
            finally
            {
                _automaticStartContext = false;
            }
            if (_operation != null)
            {
                _captureCooldownUntil[request.CooldownKey] = now
                    + Mathf.Clamp(_airportCaptureCooldown.Value, 60f, 3600f);
                string factionName = request.Hq.faction == null
                    ? null
                    : request.Hq.faction.factionName;
                if (string.IsNullOrWhiteSpace(factionName))
                    factionName = PreferredFactionKey(request.Hq.faction);
                Announce(AirliftPolicy.CaptureAirliftAnnouncement(factionName));
            }
            else
            {
                Logger.LogWarning("AIRLIFT automatic dispatch for " + request.AirbaseName
                    + " did not start; cooldown not consumed so a later recapture can retry.");
            }
        }

        private bool CaptureDirectorSupportsCurrentMap()
        {
            if (_captureMapEligibilityKnown) return _captureMapEligible;
            _captureMapEligibilityKnown = true;
            string mapIdentity = CurrentMapIdentity();
            bool hasRecordedZones = AirliftPolicy.AutomaticMapHasRecordedZones(
                mapIdentity, LoadRecordedZones().Select(zone => zone.MapIdentity));
            _captureMapEligible = hasRecordedZones
                || (_allowDynamicCaptureZones.Value
                    && !string.IsNullOrWhiteSpace(mapIdentity));
            if (_captureMapEligible)
            {
                Logger.LogInfo("AIRLIFT automatic capture director enabled for map '"
                    + AirliftPolicy.CanonicalMapKey(mapIdentity) + "' (recorded zones="
                    + hasRecordedZones + "; validated dynamic fallback="
                    + _allowDynamicCaptureZones.Value + ").");
            }
            else
            {
                Logger.LogInfo("AIRLIFT automatic capture director dormant: running map '"
                    + (string.IsNullOrWhiteSpace(mapIdentity)
                        ? "<unknown>" : AirliftPolicy.CanonicalMapKey(mapIdentity))
                    + "' has no recorded AIRLIFT zones.");
            }
            return _captureMapEligible;
        }

        private void ScanAirportOwnershipChanges(float now)
        {
            Airbase[] liveAirbases = UnityEngine.Object.FindObjectsOfType<Airbase>();
            var liveSet = new HashSet<Airbase>(liveAirbases.Where(airbase =>
                airbase != null && !airbase.disabled));
            foreach (Airbase stale in _knownAirbaseOwners.Keys.Where(airbase =>
                    airbase == null || !liveSet.Contains(airbase)).ToArray())
                _knownAirbaseOwners.Remove(stale);

            foreach (Airbase airbase in liveSet)
            {
                FactionHQ current = airbase.CurrentHQ;
                FactionHQ previous;
                if (!_knownAirbaseOwners.TryGetValue(airbase, out previous))
                {
                    _knownAirbaseOwners.Add(airbase, current);
                    continue;
                }
                bool ownerChanged = previous != current;
                if (!ownerChanged) continue;
                _knownAirbaseOwners[airbase] = current;
                if (!AirliftPolicy.AirportCaptureDetected(true, ownerChanged,
                        current != null && current.faction != null))
                    continue;

                string airbaseName = AirbaseName(airbase);
                if (!_airportCaptureAirliftsEnabled.Value
                    || (!Application.isBatchMode && !_allowListenServerCaptureTesting.Value))
                    continue;
                string cooldownKey = airbaseName + "|" + PreferredFactionKey(current.faction);
                float cooldownUntil;
                if (_captureCooldownUntil.TryGetValue(cooldownKey, out cooldownUntil)
                    && !AirliftPolicy.AirportCaptureCooldownElapsed(now, cooldownUntil))
                    continue;
                string zoneSelector;
                string zoneError;
                if (!TryChooseCapturedAirportZone(airbaseName, current,
                        out zoneSelector, out zoneError))
                {
                    Logger.LogWarning("AIRLIFT capture trigger ignored " + airbaseName
                        + ": " + zoneError + ".");
                    continue;
                }
                bool alreadyQueued = _pendingCaptureAirlifts.Any(item => item != null
                    && item.Airbase == airbase && item.Hq == current);
                if (alreadyQueued) continue;
                while (_pendingCaptureAirlifts.Count >= 8)
                {
                    CaptureAirliftRequest expired = _pendingCaptureAirlifts.Dequeue();
                    Logger.LogWarning("AIRLIFT automatic queue limit reached; discarded oldest request for "
                        + (expired == null ? "an unavailable airport" : expired.AirbaseName) + ".");
                }
                _pendingCaptureAirlifts.Enqueue(new CaptureAirliftRequest {
                    Airbase = airbase,
                    Hq = current,
                    AirbaseName = airbaseName,
                    ZoneSelector = zoneSelector,
                    CooldownKey = cooldownKey,
                    QueuedAt = now
                });
                Logger.LogInfo("AIRLIFT queued automatic "
                    + PreferredFactionKey(current.faction) + " capture lift for "
                    + airbaseName + " using zone '" + zoneSelector + "'.");
            }
        }

        private bool TryChooseCapturedAirportZone(string airbaseName, FactionHQ hq,
            out string selector, out string error)
        {
            selector = null;
            error = null;
            string mapIdentity = CurrentMapIdentity();
            List<RecordedDropZone> candidates = LoadRecordedZones().Where(zone => zone != null
                    && AirliftPolicy.ZoneMapMatches(zone.MapIdentity, mapIdentity)
                    && RecordedZoneMatchesFaction(zone, hq == null ? null : hq.faction)
                    && AirliftPolicy.ZoneSelectorMatches(zone.Name,
                        zone.NearestAirbaseName, airbaseName))
                .OrderBy(zone => UnityEngine.Random.value).ToList();
            if (candidates.Count == 0)
            {
                if (_allowDynamicCaptureZones.Value)
                {
                    selector = airbaseName + " random";
                    Logger.LogInfo("AIRLIFT has no recorded "
                        + PreferredFactionKey(hq == null ? null : hq.faction)
                        + " zone for '" + airbaseName
                        + "'; queued the validated dynamic airport fallback.");
                    return true;
                }
                error = "no recorded " + PreferredFactionKey(hq == null ? null : hq.faction)
                    + " drop zone matches this airport on map '"
                    + AirliftPolicy.CanonicalMapKey(mapIdentity) + "'";
                return false;
            }
            selector = candidates[0].Name;
            return true;
        }

        private float ActiveUpdateInterval()
        {
            BatteryOperation battery = _operation;
            if (battery == null) return 0.5f;
            if ((battery.DropProfileStarted && !battery.SynchronizedReleaseStarted)
                || (battery.SynchronizedReleaseStarted
                    && battery.SynchronizedReleaseWave < battery.ReleaseWaveCount))
                return 0.05f;
            for (int index = 0; index < battery.Transports.Count; index++)
            {
                TransportOperation transport = battery.Transports[index];
                if (transport != null && !transport.IsTerminal
                    && transport.JammerTarget != null)
                    return 0.05f;
            }
            return 0.1f;
        }

        private static bool ServerChatCommandPrefix(string message, INetworkPlayer sender)
        {
            Plugin plugin = _instance;
            return plugin == null || plugin.HandleServerCommand(message, sender);
        }

        private bool HandleServerCommand(string message, INetworkPlayer sender)
        {
            if (string.IsNullOrWhiteSpace(message)) return true;
            string trimmed = message.Trim();
            bool isAddZone = trimmed.Equals("/addzone", StringComparison.OrdinalIgnoreCase);
            bool isZones = trimmed.Equals("/zones", StringComparison.OrdinalIgnoreCase);
            bool isCommand = isAddZone || isZones
                || trimmed.Equals("/airlift", StringComparison.OrdinalIgnoreCase)
                || (trimmed.Length > 8 && trimmed.StartsWith("/airlift", StringComparison.OrdinalIgnoreCase)
                    && char.IsWhiteSpace(trimmed[8]));
            if (!isCommand) return true;

            Player player;
            if (sender == null || !PlayerHelper.TryGetPlayer(sender, out player) || player == null)
            {
                SendPrivate(sender, "AIRLIFT: Access denied.");
                return false;
            }
            string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool publicPurchase = parts.Length >= 3
                && parts[1].Equals("purchase", StringComparison.OrdinalIgnoreCase)
                && AirliftPolicy.IsRapidCommandToken(parts[2]);
            if (publicPurchase)
            {
                float nextAllowed;
                if (_nextPublicPurchaseCommand.TryGetValue(player.SteamID, out nextAllowed)
                    && Time.unscaledTime < nextAllowed)
                {
                    SendPrivate(sender, "AIRLIFT: Please wait before requesting another purchase.");
                    return false;
                }
                _nextPublicPurchaseCommand[player.SteamID] = Time.unscaledTime + 2f;
                HandlePublicCombatPurchase(player, sender, parts.Length > 3
                    ? string.Join(" ", parts.Skip(3).ToArray()) : null);
                return false;
            }
            if (!AirliftPolicy.IsAuthorized(player.SteamID, _ownerSteamId.Value))
            {
                SendPrivate(sender, "AIRLIFT: Access denied.");
                return false;
            }
            if (Time.unscaledTime < _nextAdminCommand)
            {
                SendPrivate(sender, "AIRLIFT: Please wait before sending another command.");
                return false;
            }
            _nextAdminCommand = Time.unscaledTime + 1.5f;

            if (isAddZone)
            {
                AddZoneCommand(player, sender);
                return false;
            }
            if (isZones)
            {
                SendPrivate(sender, ZonesText(player));
                return false;
            }

            string verb = parts.Length < 2 ? "help" : parts[1].ToLowerInvariant();
            switch (verb)
            {
                case "start":
                    StartOperation(player, sender, parts.Length > 2
                        ? string.Join(" ", parts.Skip(2).ToArray()) : null);
                    break;
                case "wave":
                    StartOwnerWave(player, sender, parts);
                    break;
                case "rapid":
                case "combat":
                    StartOwnerCombatDrop(player, sender, parts);
                    break;
                case "abort":
                    if (_operation == null) SendPrivate(sender, "AIRLIFT: No active operation.");
                    else
                    {
                        AbortOperation("owner abort", true);
                        SendPrivate(sender, "AIRLIFT: Battery operation aborted and tracked units cleaned up.");
                    }
                    break;
                case "status":
                    SendPrivate(sender, StatusText());
                    break;
                case "validate":
                    ValidateCommand(player, sender, parts.Length > 2
                        ? string.Join(" ", parts.Skip(2).ToArray()) : null);
                    break;
                case "catalog":
                    SendPrivate(sender, CatalogueText());
                    break;
                case "auto":
                    HandleAutomaticCommand(sender, parts);
                    break;
                default:
                    SendPrivate(sender, "AIRLIFT commands: /addzone | /zones | "
                        + "/airlift start <airbase> <number|random> | "
                        + "/airlift wave <BDF|PALA> <airbase> <number|random> | "
                        + "/airlift rapid <BDF|PALA> <airbase> <runway-number|random> | "
                        + "/airlift purchase rapid <enemy-airport> | "
                        + "validate <airbase> <number|random> | status | catalog | abort | "
                        + "auto set <on|off> <cooldown-seconds> | auto status");
                    break;
            }
            return false;
        }

        private void HandleAutomaticCommand(INetworkPlayer recipient, string[] parts)
        {
            if (parts.Length == 3 && parts[2].Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                SendPrivate(recipient, AutomaticStatusText());
                return;
            }
            if (parts.Length != 5 || !parts[2].Equals("set", StringComparison.OrdinalIgnoreCase))
            {
                SendPrivate(recipient,
                    "AIRLIFT: Usage: /airlift auto set <on|off> <cooldown-seconds> | /airlift auto status.");
                return;
            }
            bool enabledValue;
            if (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)) enabledValue = true;
            else if (parts[3].Equals("off", StringComparison.OrdinalIgnoreCase)) enabledValue = false;
            else
            {
                SendPrivate(recipient, "AIRLIFT: Automatic state must be on or off.");
                return;
            }
            float cooldown;
            if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out cooldown)
                || cooldown < 60f || cooldown > 3600f)
            {
                SendPrivate(recipient, "AIRLIFT: Capture cooldown must be 60-3600 seconds.");
                return;
            }
            _airportCaptureAirliftsEnabled.Value = enabledValue;
            _airportCaptureCooldown.Value = cooldown;
            Config.Save();
            ResetCaptureDirector();
            SendPrivate(recipient, "AIRLIFT: Automatic capture settings saved. " + AutomaticStatusText());
        }

        private string AutomaticStatusText()
        {
            return "AIRLIFT: automatic airport-capture deployments="
                + (_airportCaptureAirliftsEnabled.Value ? "on" : "off")
                + "; cooldown="
                + Mathf.Clamp(_airportCaptureCooldown.Value, 60f, 3600f)
                    .ToString("0", CultureInfo.InvariantCulture)
                + " seconds; queued=" + _pendingCaptureAirlifts.Count.ToString(CultureInfo.InvariantCulture)
                + ".";
        }

        private void HandlePublicCombatPurchase(Player player,
            INetworkPlayer recipient, string requestedAirport)
        {
            if (!_publicCombatPurchasesEnabled.Value)
            {
                SendPrivate(recipient, "AIRLIFT RAPID: Public purchases are disabled.");
                return;
            }
            MissionManager manager = SafeMissionManager();
            if (manager == null || !AirliftPolicy.CanRun(manager.IsServer,
                    MissionManager.IsRunning, Application.isBatchMode,
                    _allowListenServer.Value))
            {
                SendPrivate(recipient,
                    "AIRLIFT: No server-authoritative mission is available for this purchase.");
                return;
            }
            if (player == null || player.HQ == null || player.HQ.faction == null)
            {
                SendPrivate(recipient,
                    "AIRLIFT RAPID: Join a faction before purchasing a drop.");
                return;
            }
            if (_operation != null)
            {
                SendPrivate(recipient, "AIRLIFT: Operation " + _operation.Id
                    + " is active; your allocation was not charged.");
                return;
            }

            FactionHQ hq = player.HQ;
            float now = Time.unscaledTime;
            float cooldownUntil;
            if (_combatPurchaseCooldownUntil.TryGetValue(hq, out cooldownUntil))
            {
                float remaining = AirliftPolicy.CombatPurchaseCooldownRemaining(
                    now, cooldownUntil);
                if (remaining > 0f)
                {
                    SendPrivate(recipient, "AIRLIFT: "
                        + PreferredFactionKey(hq.faction)
                        + " RAPID cooldown has "
                        + Mathf.CeilToInt(remaining).ToString(CultureInfo.InvariantCulture)
                        + " seconds remaining; your allocation was not charged.");
                    return;
                }
            }

            float cost = AirliftPolicy.EffectiveCombatPurchaseCost(
                _publicCombatPurchaseCost.Value);
            if (player.Allocation < cost)
            {
                SendPrivate(recipient, "AIRLIFT RAPID: The drop costs "
                    + UnitConverter.ValueReading(cost) + "; available allocation is "
                    + UnitConverter.ValueReading(player.Allocation) + ".");
                return;
            }

            Airbase targetAirbase;
            string airportError;
            if (!TryResolvePublicPurchaseAirbase(hq, requestedAirport,
                    out targetAirbase, out airportError))
            {
                SendPrivate(recipient, "AIRLIFT: " + airportError
                    + " Your allocation was not charged.");
                return;
            }

            int expectedId = _nextOperationId;
            StartCombatOperation(player, recipient, hq, targetAirbase, "random");
            BatteryOperation started = _operation;
            if (started == null || started.Id != expectedId
                || started.Kind != AirliftOperationKind.CombatRunwayDrop)
                return;

            try
            {
                player.AddAllocation(-cost);
            }
            catch (Exception exception)
            {
                Logger.LogError("AIRLIFT-" + started.Id
                    + " could not debit the accepted combat purchase: " + exception);
                AbortOperation("combat purchase debit failed", true);
                SendPrivate(recipient,
                    "AIRLIFT: The purchase could not be charged and was cancelled.");
                return;
            }
            started.PurchasingPlayer = player;
            started.PurchasingSteamId = player.SteamID;
            started.PurchaseCost = cost;
            started.PurchaseCharged = true;
            started.PurchaseRefundEligible = true;
            _combatPurchaseCooldownUntil[hq] = now
                + AirliftPolicy.EffectiveCombatPurchaseCooldown(
                    _publicCombatPurchaseCooldown.Value);
            string factionName = string.IsNullOrWhiteSpace(hq.faction.factionName)
                ? PreferredFactionKey(hq.faction) : hq.faction.factionName;
            Announce("A " + factionName + " player purchased a RAPID runway assault at "
                + AirbaseName(targetAirbase) + "; five Chimeras will arrive soon.");
            SendPrivate(recipient, "AIRLIFT RAPID: Purchase accepted for "
                + AirbaseName(targetAirbase) + ". Charged "
                + UnitConverter.ValueReading(cost)
                + "; the faction cooldown is five minutes.");
        }

        private static bool TryResolvePublicPurchaseAirbase(FactionHQ hq,
            string requestedAirport, out Airbase airbase, out string error)
        {
            airbase = null;
            error = null;
            if (hq == null)
            {
                error = "No purchasing faction is available.";
                return false;
            }
            List<Airbase> candidates = UnityEngine.Object.FindObjectsOfType<Airbase>()
                .Where(candidate => candidate != null && !candidate.disabled
                    && AirliftPolicy.CombatPurchaseTargetIsEnemy(
                        candidate.CurrentHQ != null,
                        candidate.CurrentHQ != null
                            && candidate.CurrentHQ.faction != null,
                        candidate.CurrentHQ != null
                            && candidate.CurrentHQ.faction == hq.faction)
                    && candidate.runways != null
                    && candidate.runways.Any(runway => runway != null
                        && runway.Start != null && runway.End != null))
                .OrderBy(AirbaseName, StringComparer.OrdinalIgnoreCase).ToList();
            if (candidates.Count == 0)
            {
                error = "No enemy-held airport with usable runway data is available.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(requestedAirport))
            {
                error = "Choose a destination airport in the Donate menu, or use "
                    + "/airlift purchase rapid <enemy-airport>.";
                return false;
            }
            string requested = requestedAirport.Trim();
            List<Airbase> matches = candidates.Where(candidate =>
                string.Equals(AirbaseName(candidate), requested,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
            {
                string compact = CompactAirbaseText(requested);
                matches = candidates.Where(candidate =>
                    CompactAirbaseText(AirbaseName(candidate)) == compact).ToList();
            }
            if (matches.Count != 1)
            {
                error = matches.Count > 1
                    ? "The selected airport name is ambiguous."
                    : "The selected airport is no longer enemy-held or has no usable runway.";
                return false;
            }
            airbase = matches[0];
            return true;
        }

        private void StartOwnerWave(Player owner, INetworkPlayer recipient,
            string[] commandParts)
        {
            if (commandParts == null || commandParts.Length < 5)
            {
                SendPrivate(recipient,
                    "AIRLIFT: Usage: /airlift wave <BDF|PALA> <airbase> <number|random>.");
                return;
            }
            string factionKey;
            if (!AirliftPolicy.TryNormalizeOwnerFaction(commandParts[2], out factionKey))
            {
                SendPrivate(recipient, "AIRLIFT: Team must be BDF or PALA.");
                return;
            }
            FactionHQ requestedHq;
            string factionError;
            if (!TryFindFactionHq(factionKey, out requestedHq, out factionError))
            {
                SendPrivate(recipient, "AIRLIFT: " + factionError + ".");
                return;
            }
            string airportSelector;
            string dropSelector;
            if (!AirliftPolicy.TryParseOwnerWaveSelection(
                    string.Join(" ", commandParts.Skip(3).ToArray()),
                    out airportSelector, out dropSelector))
            {
                SendPrivate(recipient,
                    "AIRLIFT: Select an airport followed by a positive zone number or random.");
                return;
            }
            Airbase targetAirbase = FindRuntimeAirbase(new[] { airportSelector });
            if (targetAirbase == null || targetAirbase.disabled)
            {
                SendPrivate(recipient, "AIRLIFT: Airport '" + airportSelector
                    + "' is unavailable in the running mission.");
                return;
            }
            if (targetAirbase.CurrentHQ != requestedHq)
            {
                SendPrivate(recipient, "AIRLIFT: " + factionKey + " does not control '"
                    + AirbaseName(targetAirbase) + "'.");
                return;
            }
            string requestedZone = airportSelector + " " + dropSelector;
            StartOperation(owner, recipient, requestedZone, requestedHq);
        }

        private void StartOwnerCombatDrop(Player owner, INetworkPlayer recipient,
            string[] commandParts)
        {
            if (commandParts == null || commandParts.Length < 5)
            {
                SendPrivate(recipient,
                    "AIRLIFT: Usage: /airlift rapid <BDF|PALA> <airbase> <runway-number|random>. "
                    + "The legacy 'combat' verb remains accepted.");
                return;
            }
            string factionKey;
            if (!AirliftPolicy.TryNormalizeOwnerFaction(commandParts[2], out factionKey))
            {
                SendPrivate(recipient, "AIRLIFT: Team must be BDF or PALA.");
                return;
            }
            FactionHQ requestedHq;
            string factionError;
            if (!TryFindFactionHq(factionKey, out requestedHq, out factionError))
            {
                SendPrivate(recipient, "AIRLIFT: " + factionError + ".");
                return;
            }
            string airportSelector;
            string runwaySelector;
            if (!AirliftPolicy.TryParseOwnerWaveSelection(
                    string.Join(" ", commandParts.Skip(3).ToArray()),
                    out airportSelector, out runwaySelector))
            {
                SendPrivate(recipient,
                    "AIRLIFT: Select an airport followed by a positive runway number or random.");
                return;
            }
            Airbase targetAirbase = FindRuntimeAirbase(new[] { airportSelector });
            if (targetAirbase == null || targetAirbase.disabled)
            {
                SendPrivate(recipient, "AIRLIFT: Airport '" + airportSelector
                    + "' is unavailable in the running mission.");
                return;
            }
            if (targetAirbase.CurrentHQ != requestedHq)
            {
                SendPrivate(recipient, "AIRLIFT: " + factionKey + " does not control '"
                    + AirbaseName(targetAirbase) + "'.");
                return;
            }
            StartCombatOperation(owner, recipient, requestedHq, targetAirbase,
                runwaySelector);
        }

        private static bool TryFindFactionHq(string factionKey, out FactionHQ hq,
            out string error)
        {
            hq = null;
            error = null;
            FactionHQ[] matches = UnityEngine.Object.FindObjectsOfType<FactionHQ>()
                .Where(candidate => candidate != null && candidate.faction != null
                    && (AirliftPolicy.CanonicalFactionKey(candidate.faction.factionTag)
                            == factionKey
                        || AirliftPolicy.CanonicalFactionKey(candidate.faction.factionName)
                            == factionKey))
                .ToArray();
            if (matches.Length == 1)
            {
                hq = matches[0];
                return true;
            }
            error = matches.Length == 0
                ? "no live " + factionKey + " faction HQ exists in this mission"
                : "multiple live " + factionKey + " faction HQs were found; refusing an ambiguous wave";
            return false;
        }

        private void AddZoneCommand(Player owner, INetworkPlayer recipient)
        {
            if (owner == null || owner.HQ == null || owner.HQ.faction == null
                || owner.Aircraft == null || owner.Aircraft.disabled)
            {
                SendPrivate(recipient,
                    "AIRLIFT: Fly directly over the intended landing-zone centre, then use /addzone.");
                return;
            }

            string mapIdentity = CurrentMapIdentity();
            if (string.IsNullOrWhiteSpace(mapIdentity))
            {
                SendPrivate(recipient, "AIRLIFT: The running map could not be identified.");
                return;
            }

            Airbase nearestAirbase = owner.HQ.GetAirbases()
                .Where(airbase => airbase != null && !airbase.disabled
                    && airbase.CurrentHQ == owner.HQ)
                .OrderBy(airbase => HorizontalDistance(owner.Aircraft.transform.position,
                    AirbasePosition(airbase)))
                .FirstOrDefault();
            if (nearestAirbase == null)
            {
                SendPrivate(recipient, "AIRLIFT: No friendly airbase exists for automatic zone naming.");
                return;
            }

            Vector3 forward = owner.Aircraft.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.01f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 candidate = owner.Aircraft.transform.position;
            candidate.y = 0f;

            Dictionary<CargoRole, Vector3> targets;
            string validationReport;
            if (!TryValidateTargetCluster(owner.HQ, candidate, forward, _r9RadarOffset.Value,
                    mapIdentity, owner.Aircraft, out targets, out validationReport))
            {
                SendPrivate(recipient, "AIRLIFT: Zone not saved: " + validationReport + ".");
                return;
            }

            Vector3 point = targets[CargoRole.Radar];
            GlobalPosition globalPoint = GlobalPositionExtensions.ToGlobalPosition(point);
            List<RecordedDropZone> zones = LoadRecordedZones();
            string factionKey = PreferredFactionKey(owner.HQ.faction);
            RecordedDropZone duplicate = zones.FirstOrDefault(zone => zone != null
                && AirliftPolicy.ZoneMapMatches(zone.MapIdentity, mapIdentity)
                && RecordedZoneMatchesFaction(zone, owner.HQ.faction)
                && HorizontalDistance(zone.GlobalPoint, globalPoint) < 100f);
            if (duplicate != null)
            {
                SendPrivate(recipient, "AIRLIFT: This location is already saved as "
                    + duplicate.Name + ".");
                return;
            }

            string airbaseName = AirbaseName(nearestAirbase);
            int sequence = zones.Count(zone => zone != null
                && AirliftPolicy.ZoneMapMatches(zone.MapIdentity, mapIdentity)
                && RecordedZoneMatchesFaction(zone, owner.HQ.faction)
                && string.Equals(zone.NearestAirbaseName, airbaseName,
                    StringComparison.OrdinalIgnoreCase)) + 1;
            float heading = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z)
                * Mathf.Rad2Deg, 360f);
            var recorded = new RecordedDropZone {
                Name = airbaseName + "-" + factionKey + "-LZ-"
                    + sequence.ToString("00", CultureInfo.InvariantCulture),
                MapIdentity = AirliftPolicy.CanonicalMapKey(mapIdentity),
                NearestAirbaseName = airbaseName,
                FactionKey = factionKey,
                GlobalPoint = globalPoint,
                ApproachHeading = heading
            };
            zones.Add(recorded);
            try
            {
                SaveRecordedZones(zones);
            }
            catch (Exception exception)
            {
                Logger.LogError("AIRLIFT could not save recorded zone: " + exception);
                SendPrivate(recipient, "AIRLIFT: Zone validation passed but the zone file could not be written.");
                return;
            }

            SendPrivate(recipient, "AIRLIFT: Saved " + recorded.Name + " for " + factionKey
                + " on " + recorded.MapIdentity
                + " at global " + FormatGlobalPosition(recorded.GlobalPoint) + ", approach "
                + recorded.ApproachHeading.ToString("0", CultureInfo.InvariantCulture)
                + " degree inbound approach heading. AIRLIFT now has "
                + zones.Count(zone => zone != null
                    && AirliftPolicy.ZoneMapMatches(zone.MapIdentity, mapIdentity)
                    && RecordedZoneMatchesFaction(zone, owner.HQ.faction))
                + " " + factionKey + " zone(s) on this map.");
        }

        private string ZonesText(Player owner)
        {
            string mapIdentity = CurrentMapIdentity();
            Faction faction = owner == null || owner.HQ == null ? null : owner.HQ.faction;
            string factionKey = PreferredFactionKey(faction);
            List<RecordedDropZone> zones = LoadRecordedZones().Where(zone => zone != null
                && AirliftPolicy.ZoneMapMatches(zone.MapIdentity, mapIdentity)
                && RecordedZoneMatchesFaction(zone, faction)).ToList();
            if (zones.Count == 0)
                return "AIRLIFT: No recorded " + factionKey + " zones for "
                    + AirliftPolicy.CanonicalMapKey(mapIdentity)
                    + ". Fly over a suitable site and use /addzone.";
            return "AIRLIFT " + factionKey + " zones for "
                + AirliftPolicy.CanonicalMapKey(mapIdentity) + ": "
                + string.Join(" | ", zones.Select(zone => zone.Name + " "
                    + FormatGlobalPosition(zone.GlobalPoint)).ToArray())
                + ". Select one with /airlift start <airbase> <number>, "
                + "for example /airlift start dustbowl 1. Dynamic tests: "
                + "/airlift start k92 random or /airlift start dustbowl random.";
        }

        private static string PreferredFactionKey(Faction faction)
        {
            if (faction == null) return "<unknown-faction>";
            string tagKey = AirliftPolicy.CanonicalFactionKey(faction.factionTag);
            if (tagKey == "BDF" || tagKey == "PALA") return tagKey;
            string nameKey = AirliftPolicy.CanonicalFactionKey(faction.factionName);
            if (nameKey == "BDF" || nameKey == "PALA") return nameKey;
            if (tagKey.Length > 0) return tagKey;
            return nameKey.Length == 0 ? "<unknown-faction>" : nameKey;
        }

        private static bool RecordedZoneMatchesFaction(RecordedDropZone zone, Faction faction)
        {
            return zone != null && faction != null
                && AirliftPolicy.ZoneFactionMatches(zone.FactionKey,
                    faction.factionName, faction.factionTag);
        }

        private List<RecordedDropZone> LoadRecordedZones()
        {
            var zones = new List<RecordedDropZone>();
            if (string.IsNullOrWhiteSpace(_zoneFilePath) || !File.Exists(_zoneFilePath)) return zones;
            try
            {
                foreach (string rawLine in File.ReadAllLines(_zoneFilePath))
                {
                    string line = rawLine == null ? string.Empty : rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    string[] fields = line.Split('\t');
                    float x;
                    float y;
                    float z;
                    float heading;
                    if (fields.Length != 10
                        || !AirliftPolicy.IsGlobalCoordinateSpace(fields[4])
                        || !AirliftPolicy.IsApproachHeadingConvention(fields[5])
                        || !float.TryParse(fields[6], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out x)
                        || !float.TryParse(fields[7], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out y)
                        || !float.TryParse(fields[8], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out z)
                        || !float.TryParse(fields[9], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out heading))
                    {
                        Logger.LogWarning("AIRLIFT ignored non-v5, non-GLOBAL, non-APPROACH_HEADING, or malformed recorded-zone line: "
                            + rawLine);
                        continue;
                    }
                    zones.Add(new RecordedDropZone {
                        Name = fields[0],
                        MapIdentity = fields[1],
                        NearestAirbaseName = fields[2],
                        FactionKey = AirliftPolicy.CanonicalFactionKey(fields[3]),
                        GlobalPoint = new GlobalPosition(x, y, z),
                        ApproachHeading = Mathf.Repeat(heading, 360f)
                    });
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning("AIRLIFT could not read recorded zones: " + exception.Message);
            }
            return zones;
        }

        private void SaveRecordedZones(IEnumerable<RecordedDropZone> zones)
        {
            string[] lines = new[] {
                "# Kelly's AIRLIFT recorded landing zones v5",
                "# Name<TAB>MapIdentity<TAB>NearestFriendlyAirbase<TAB>Faction<TAB>CoordinateSpace<TAB>DirectionConvention<TAB>X<TAB>Y<TAB>Z<TAB>ApproachHeading"
            }.Concat(zones.Where(zone => zone != null).Select(zone =>
                SafeZoneField(zone.Name) + "\t"
                + SafeZoneField(AirliftPolicy.CanonicalMapKey(zone.MapIdentity)) + "\t"
                + SafeZoneField(zone.NearestAirbaseName) + "\t"
                + SafeZoneField(AirliftPolicy.CanonicalFactionKey(zone.FactionKey)) + "\t"
                + "GLOBAL\t"
                + "APPROACH_HEADING\t"
                + zone.GlobalPoint.x.ToString("R", CultureInfo.InvariantCulture) + "\t"
                + zone.GlobalPoint.y.ToString("R", CultureInfo.InvariantCulture) + "\t"
                + zone.GlobalPoint.z.ToString("R", CultureInfo.InvariantCulture) + "\t"
                + zone.ApproachHeading.ToString("R", CultureInfo.InvariantCulture))).ToArray();
            File.WriteAllLines(_zoneFilePath, lines, new System.Text.UTF8Encoding(false));
        }

        private static string SafeZoneField(string value)
        {
            return (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static Vector3 AirbasePosition(Airbase airbase)
        {
            if (airbase == null) return Vector3.zero;
            return airbase.center != null ? airbase.center.position : airbase.transform.position;
        }

        private void StartOperation(Player owner, INetworkPlayer recipient,
            string requestedZone, FactionHQ requestedHq = null)
        {
            MissionManager manager = SafeMissionManager();
            if (manager == null || !AirliftPolicy.CanRun(manager.IsServer, MissionManager.IsRunning,
                    Application.isBatchMode, _allowListenServer.Value))
            {
                SendPrivate(recipient, "AIRLIFT: No server-authoritative mission is running.");
                return;
            }
            if (_operation != null)
            {
                SendPrivate(recipient, "AIRLIFT: Battery operation " + _operation.Id + " is still active.");
                return;
            }
            FactionHQ operationHq = requestedHq ?? (owner == null ? null : owner.HQ);
            if (operationHq == null || operationHq.faction == null)
            {
                SendPrivate(recipient, "AIRLIFT: The requested faction HQ is unavailable.");
                return;
            }

            PostDropBehavior behavior;
            if (!AirliftPolicy.TryParsePostDrop(_postDrop.Value, out behavior))
            {
                SendPrivate(recipient, "AIRLIFT: PostDrop.Behavior must be Return, Land, or Despawn.");
                return;
            }

            Dictionary<CargoRole, Vector3> targets = null;
            Vector3 forward;
            RecordedDropZone selectedZone = null;
            string zoneReport;
            if (!TryResolveDropTargets(owner, operationHq, requestedZone,
                    out targets, out forward, out selectedZone, out zoneReport))
            {
                Logger.LogWarning("Battery drop-zone validation failed: " + zoneReport);
                SendPrivate(recipient, "AIRLIFT: Drop zone rejected: " + zoneReport + ".");
                return;
            }

            AircraftDefinition definition;
            List<TransportOperation> transports;
            string packageReport;
            if (!TryResolveBatteryManifest(operationHq, targets,
                    out definition, out transports, out packageReport))
            {
                Logger.LogError("Battery manifest validation failed: " + packageReport);
                SendPrivate(recipient, "AIRLIFT: Battery manifest rejected: " + packageReport + ".");
                return;
            }

            Spawner spawner = UnityEngine.Object.FindObjectOfType<Spawner>();
            if (spawner == null)
            {
                SendPrivate(recipient, "AIRLIFT: Mission Spawner is not ready.");
                return;
            }

            int id = _nextOperationId++;
            var battery = new BatteryOperation {
                Id = id,
                Kind = AirliftOperationKind.AntiAirBattery,
                ZoneName = selectedZone == null ? _zoneName.Value : selectedZone.Name,
                MapKey = selectedZone == null ? _zoneMapKey.Value : selectedZone.MapIdentity,
                Hq = operationHq,
                RadarTarget = targets[CargoRole.Radar],
                ReservedDropPosition = GlobalPositionExtensions.ToGlobalPosition(targets[CargoRole.Radar]),
                DatumOrigin = Datum.originPosition,
                StartedAt = Time.unscaledTime,
                CruiseRadarAltitude = AirliftPolicy.EffectiveCruiseRadarAltitude(
                    _cruiseAltitude.Value, _dropAltitude.Value),
                DropRadarAltitude = AirliftPolicy.EffectiveDropRadarAltitude(_dropAltitude.Value),
                LeaderReleasePointPrepared = true,
                SharedReleasePointEstablished = true
            };
            float startingSpeed = Mathf.Max(30f, _spawnSpeed.Value);
            float nominalFallTime = Kinematics.FallTime(battery.DropRadarAltitude, 0f);
            float plannedPrecisionLead = startingSpeed * (nominalFallTime
                + Mathf.Clamp(_precisionParachuteLeadTime.Value, 0f, 15f));
            float plannedReleaseLead = Mathf.Clamp(Mathf.Max(
                AirliftPolicy.EffectiveReleaseLeadDistance(_releaseLeadDistance.Value),
                plannedPrecisionLead), 400f, 4000f);
            Vector3 releaseCenter = targets[CargoRole.Radar]
                - (forward * plannedReleaseLead);
            battery.SharedReleasePoint = releaseCenter;
            operationHq.RegisterDropZone(battery.ReservedDropPosition);
            battery.DropZoneRegistered = true;
            _operation = battery;

            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
            float egressTurn = AirliftPolicy.EffectiveEgressTurnDegrees(
                _egressTurnDegrees.Value);
            Vector3 egressDirection = Quaternion.AngleAxis(egressTurn,
                Vector3.up) * forward;
            egressDirection.y = 0f;
            egressDirection.Normalize();
            float lateralSpacing =
                AirliftPolicy.EffectiveFormationLateralSpacing(_formationSpacing.Value);
            float stagingInterval =
                AirliftPolicy.EffectiveFormationSpawnInterval(_formationSpawnInterval.Value);
            bool useOffMapSpawn = AirliftPolicy.UseOffMapSpawn(
                _automaticStartContext, Application.isBatchMode);
            float baseSpawnDistance = useOffMapSpawn
                ? ComputeAutomaticOffMapSpawnDistance(releaseCenter, forward)
                : Mathf.Max(1000f, _spawnDistance.Value);
            var transportLoadouts = new List<Loadout>();
            for (int index = 0; index < transports.Count; index++)
            {
                TransportOperation transport = transports[index];
                float lateral = (index - ((transports.Count - 1) * 0.5f)) * lateralSpacing;
                Vector3 formationOffset = right * lateral;
                Vector3 dropPoint = releaseCenter + formationOffset;
                float remainingStagedSpawns = transports.Count - 1 - index;
                float spawnDistance = baseSpawnDistance
                    + (startingSpeed * stagingInterval * remainingStagedSpawns);
                Vector3 spawn = dropPoint - (forward * spawnDistance);
                Vector3 egress = dropPoint + (egressDirection
                    * Mathf.Max(1000f, _egressDistance.Value));
                spawn.y = dropPoint.y + battery.CruiseRadarAltitude;
                egress.y = dropPoint.y + battery.DropRadarAltitude;
                transportLoadouts.Add(BuildLoadout(definition, transport.Cargo, transport.JammerMount));
                transport.Hq = operationHq;
                transport.DropPoint = dropPoint;
                transport.SpawnPoint = spawn;
                transport.EgressPoint = egress;
                transport.ReturnPoint = spawn;
                transport.FlightDestination = dropPoint
                    + (forward * Mathf.Max(1500f, plannedReleaseLead + 500f));
                transport.FormationOffset = formationOffset;
                transport.IngressDirection = forward;
                transport.EgressDirection = egressDirection;
                transport.TargetRadarAltitude = battery.CruiseRadarAltitude;
                transport.CruiseThrottle = Mathf.Clamp01(_cruiseThrottle.Value);
                transport.StartedAt = Time.unscaledTime;
                transport.PhaseStartedAt = Time.unscaledTime;
                transport.PostDropBehavior = behavior;
                transport.Phase = TransportPhase.WaitingForSystems;
                battery.Transports.Add(transport);
            }

            float plannedInboundHeading = Mathf.Repeat(
                Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
            GlobalPosition plannedTargetGlobal =
                GlobalPositionExtensions.ToGlobalPosition(targets[CargoRole.Radar]);
            GlobalPosition plannedReleaseGlobal =
                GlobalPositionExtensions.ToGlobalPosition(releaseCenter);
            GlobalPosition plannedSpawnGlobal = GlobalPositionExtensions.ToGlobalPosition(
                battery.Transports[0].SpawnPoint - battery.Transports[0].FormationOffset);
            GlobalPosition plannedEgressGlobal = GlobalPositionExtensions.ToGlobalPosition(
                battery.Transports[0].EgressPoint - battery.Transports[0].FormationOffset);
            Logger.LogInfo("AIRLIFT-" + id + " direct route: formation spawn "
                + FormatGlobalPosition(plannedSpawnGlobal) + " -> release line "
                + FormatGlobalPosition(plannedReleaseGlobal) + " -> target "
                + FormatGlobalPosition(plannedTargetGlobal) + " -> egress "
                + FormatGlobalPosition(plannedEgressGlobal) + "; inbound course "
                + plannedInboundHeading.ToString("0.0", CultureInfo.InvariantCulture)
                + " degrees; nominal precision lead "
                + plannedReleaseLead.ToString("0", CultureInfo.InvariantCulture)
                + " m (fall "
                + nominalFallTime.ToString("0.00", CultureInfo.InvariantCulture)
                + " s plus parachute allowance); "
                + (useOffMapSpawn
                    ? (_automaticStartContext ? "automatic off-map" : "dedicated-server off-map")
                    : "local manual test")
                + " spawn distance "
                + baseSpawnDistance.ToString("0", CultureInfo.InvariantCulture)
                + " m; coordinated egress bank "
                + Mathf.Abs(egressTurn).ToString("0", CultureInfo.InvariantCulture)
                + " degrees " + (egressTurn < 0f ? "left" : "right") + "." );

            battery.FormationSpawnInProgress = true;
            Announce("AIRLIFT-" + id
                + ": assembling the heavy transport formation in staged server spawns.");
            StartCoroutine(SpawnFormationCoroutine(battery, spawner, definition, transportLoadouts,
                rotation, forward, recipient, zoneReport, packageReport));
        }

        private float ComputeAutomaticOffMapSpawnDistance(Vector3 releaseCenter,
            Vector3 inboundForward)
        {
            float configured = Mathf.Max(1000f, _spawnDistance.Value);
            LevelInfo level = UnityEngine.Object.FindObjectOfType<LevelInfo>();
            MapSettings settings = level == null ? null : level.LoadedMapSettings;
            if (settings == null) return Mathf.Max(configured, 15000f);
            float halfX = Mathf.Max(2000f, settings.MapSize.x * 0.5f);
            float halfZ = Mathf.Max(2000f, settings.MapSize.y * 0.5f);
            return AirliftPolicy.RequiredOffMapSpawnDistance(
                releaseCenter.x, releaseCenter.z, inboundForward.x, inboundForward.z,
                halfX, halfZ, _automaticOffMapMargin.Value, configured);
        }

        private void StartCombatOperation(Player owner, INetworkPlayer recipient,
            FactionHQ operationHq, Airbase targetAirbase, string runwaySelector)
        {
            MissionManager manager = SafeMissionManager();
            if (manager == null || !AirliftPolicy.CanRun(manager.IsServer,
                    MissionManager.IsRunning, Application.isBatchMode,
                    _allowListenServer.Value))
            {
                SendPrivate(recipient, "AIRLIFT: No server-authoritative mission is running.");
                return;
            }
            if (_operation != null)
            {
                SendPrivate(recipient, "AIRLIFT: Operation " + _operation.Id + " is still active.");
                return;
            }
            PostDropBehavior behavior;
            if (!AirliftPolicy.TryParsePostDrop(_postDrop.Value, out behavior))
            {
                SendPrivate(recipient, "AIRLIFT: PostDrop.Behavior must be Return, Land, or Despawn.");
                return;
            }

            Dictionary<CargoRole, Vector3> targets;
            Vector3 forward;
            Vector3 targetCenter;
            string zoneName;
            string mapIdentity;
            string zoneReport;
            float runwayLength;
            float runwayWidth;
            if (!TryResolveCombatRunwayTargets(operationHq, targetAirbase,
                    runwaySelector, out targets, out forward, out targetCenter,
                    out runwayLength, out runwayWidth, out zoneName,
                    out mapIdentity, out zoneReport))
            {
                Logger.LogWarning("RAPID runway validation failed: " + zoneReport);
                SendPrivate(recipient, "AIRLIFT RAPID: Runway rejected: " + zoneReport + ".");
                return;
            }

            AircraftDefinition definition;
            List<TransportOperation> transports;
            string packageReport;
            if (!TryResolveCombatManifest(operationHq, targets, out definition,
                    out transports, out packageReport))
            {
                Logger.LogError("Combat manifest validation failed: " + packageReport);
                SendPrivate(recipient, "AIRLIFT: Combat manifest rejected: "
                    + packageReport + ".");
                return;
            }
            Spawner spawner = UnityEngine.Object.FindObjectOfType<Spawner>();
            if (spawner == null)
            {
                SendPrivate(recipient, "AIRLIFT: Mission Spawner is not ready.");
                return;
            }

            float dropAltitude = AirliftPolicy.EffectiveCombatDropAltitude(
                _combatDropAltitude.Value);
            float startingSpeed = Mathf.Max(30f, _spawnSpeed.Value);
            float nominalFallTime = Kinematics.FallTime(dropAltitude, 0f);
            float plannedReleaseLead = Mathf.Clamp(startingSpeed * nominalFallTime,
                80f, 350f);
            Vector3 releaseCenter = targetCenter - (forward * plannedReleaseLead);
            int id = _nextOperationId++;
            var operation = new BatteryOperation {
                Id = id,
                Kind = AirliftOperationKind.CombatRunwayDrop,
                ZoneName = zoneName,
                MapKey = mapIdentity,
                Hq = operationHq,
                RadarTarget = targetCenter,
                ReservedDropPosition = GlobalPositionExtensions.ToGlobalPosition(targetCenter),
                DatumOrigin = Datum.originPosition,
                StartedAt = Time.unscaledTime,
                CruiseRadarAltitude = AirliftPolicy.EffectiveCruiseRadarAltitude(
                    _cruiseAltitude.Value, 450f),
                DropRadarAltitude = dropAltitude,
                SuppressionRunwayCenter = targetCenter,
                SuppressionRunwayForward = forward,
                SuppressionRunwayHalfLength = runwayLength * 0.5f,
                SuppressionRunwayHalfWidth = runwayWidth * 0.5f,
                LeaderReleasePointPrepared = true,
                SharedReleasePointEstablished = true,
                SharedReleasePoint = releaseCenter,
                RadarLinkCheckComplete = true,
                RadarLinkValidated = true,
                ReleaseWaveCount = transports.Max(item => item.Cargo.Count)
            };
            operationHq.RegisterDropZone(operation.ReservedDropPosition);
            operation.DropZoneRegistered = true;
            _operation = operation;

            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            float egressTurn = AirliftPolicy.EffectiveEgressTurnDegrees(
                _egressTurnDegrees.Value);
            Vector3 egressDirection = Quaternion.AngleAxis(egressTurn,
                Vector3.up) * forward;
            egressDirection.y = 0f;
            egressDirection.Normalize();
            float stagingInterval = AirliftPolicy.EffectiveFormationSpawnInterval(
                _formationSpawnInterval.Value);
            // Combat drops are public, hostile-runway operations. They always
            // enter from beyond the map edge, including owner-triggered and
            // listen-server purchases; the old close spawn is AA-test-only.
            float baseSpawnDistance = ComputeAutomaticOffMapSpawnDistance(
                releaseCenter, forward);
            var loadouts = new List<Loadout>();
            for (int index = 0; index < transports.Count; index++)
            {
                TransportOperation transport = transports[index];
                Vector3 laneTarget = index < 2
                    ? targets[index == 0 ? CargoRole.Type12Alpha : CargoRole.Type12Bravo]
                    : index == 2 ? (targets[CargoRole.AfvIfvCharlie]
                        + targets[CargoRole.AfvAaCharlie]) * 0.5f
                    : index == 3 ? (targets[CargoRole.AfvIfvDelta]
                        + targets[CargoRole.AfvAaDelta]) * 0.5f
                    : (targets[CargoRole.FrcvRear] + targets[CargoRole.FrcvFront]) * 0.5f;
                Vector3 formationOffset = laneTarget - targetCenter;
                formationOffset.y = 0f;
                Vector3 dropPoint = releaseCenter + formationOffset;
                float remainingStagedSpawns = transports.Count - 1 - index;
                float spawnDistance = baseSpawnDistance
                    + (startingSpeed * stagingInterval * remainingStagedSpawns);
                Vector3 spawn = dropPoint - (forward * spawnDistance);
                Vector3 egress = dropPoint + (egressDirection
                    * Mathf.Max(1000f, _egressDistance.Value));
                spawn.y = dropPoint.y + operation.CruiseRadarAltitude;
                egress.y = dropPoint.y + dropAltitude;
                loadouts.Add(BuildLoadout(definition, transport.Cargo,
                    transport.SuppressionMount));
                transport.Hq = operationHq;
                transport.DropPoint = dropPoint;
                transport.SpawnPoint = spawn;
                transport.EgressPoint = egress;
                transport.ReturnPoint = spawn;
                transport.FlightDestination = targetCenter
                    + (forward * Mathf.Max(1800f, plannedReleaseLead + 700f));
                transport.FormationOffset = formationOffset;
                transport.IngressDirection = forward;
                transport.EgressDirection = egressDirection;
                transport.TargetRadarAltitude = operation.CruiseRadarAltitude;
                transport.ReleaseAltitudeTolerance = 2f;
                transport.MaximumReleaseRadarAltitude = 5f;
                transport.CruiseThrottle = Mathf.Clamp01(_cruiseThrottle.Value);
                transport.StartedAt = Time.unscaledTime;
                transport.PhaseStartedAt = Time.unscaledTime;
                transport.PostDropBehavior = behavior;
                transport.Phase = TransportPhase.WaitingForSystems;
                operation.Transports.Add(transport);
            }

            GlobalPosition plannedCombatSpawn =
                GlobalPositionExtensions.ToGlobalPosition(
                    operation.Transports[0].SpawnPoint
                    - operation.Transports[0].FormationOffset);
            GlobalPosition plannedCombatTarget =
                GlobalPositionExtensions.ToGlobalPosition(targetCenter);
            Logger.LogInfo("AIRLIFT-" + id + " combat route: off-map formation spawn "
                + FormatGlobalPosition(plannedCombatSpawn) + " -> enemy runway "
                + FormatGlobalPosition(plannedCombatTarget) + "; boundary-derived "
                + "spawn distance " + baseSpawnDistance.ToString("0",
                    CultureInfo.InvariantCulture) + " m plus staged separation.");

            operation.FormationSpawnInProgress = true;
            Announce("AIRLIFT-" + id
                + ": RAPID runway assault package inbound in five MC-260 Chimeras.");
            StartCoroutine(SpawnFormationCoroutine(operation, spawner, definition,
                loadouts, rotation, forward, recipient, zoneReport, packageReport));
        }

        private IEnumerator SpawnFormationCoroutine(BatteryOperation battery, Spawner spawner,
            AircraftDefinition definition, List<Loadout> transportLoadouts,
            Quaternion rotation, Vector3 forward, INetworkPlayer recipient,
            string zoneReport, string packageReport)
        {
            yield return null;
            float interval = AirliftPolicy.EffectiveFormationSpawnInterval(_formationSpawnInterval.Value);
            for (int index = 0; index < battery.Transports.Count; index++)
            {
                if (_operation != battery) yield break;
                TransportOperation transport = battery.Transports[index];
                var spawn = new CoordinatedAircraftSpawn();
                yield return SpawnAircraftCoordinated(spawner, definition.unitPrefab,
                    transportLoadouts[index], GlobalPositionExtensions.ToGlobalPosition(
                        transport.SpawnPoint), rotation,
                    forward * Mathf.Max(30f, _spawnSpeed.Value), battery.Hq,
                    "AIRLIFT-" + battery.Id.ToString(CultureInfo.InvariantCulture)
                        + "-" + transport.Callsign, 0.85f, 0.35f, spawn);
                Aircraft aircraft = spawn.Aircraft;
                LogCoordinatedSpawn(BatteryLabel(battery, transport), spawn);
                if (_operation != battery)
                {
                    DestroyNetworkUnit(aircraft);
                    yield break;
                }
                if (aircraft == null)
                {
                    if (spawn.Error != null)
                        Logger.LogError(BatteryLabel(battery, transport)
                            + " coordinated spawn failed: " + spawn.Error);
                    AbortOperation("Spawner failed to create " + transport.Callsign, true);
                    SendPrivate(recipient, "AIRLIFT: Spawn failed; the partial battery was cleaned up.");
                    yield break;
                }

                transport.Aircraft = aircraft;
                transport.StartedAt = Time.unscaledTime;
                transport.PhaseStartedAt = Time.unscaledTime;
                aircraft.SetGear(false);
                ControlInputs inputs = aircraft.GetInputs();
                if (inputs != null)
                {
                    inputs.throttle = 1f;
                    inputs.brake = 0f;
                }
                ExcludeFromNativeAiLimit(transport, battery.Id);
                yield return new WaitForSecondsRealtime(interval);
            }

            if (_operation != battery) yield break;
            battery.FormationSpawnInProgress = false;
            battery.PurchaseRefundEligible = false;
            Announce("AIRLIFT-" + battery.Id + ": launched - "
                + battery.Transports.Count.ToString(CultureInfo.InvariantCulture)
                + " MC-260 Chimeras inbound to " + battery.ZoneName
                + (battery.Kind == AirliftOperationKind.CombatRunwayDrop
                    ? " with a ground-level combat force."
                    : " with an airdropped AA battery."),
                AirliftAnnouncementKind.Launch);
            DebugLog("AIRLIFT-" + battery.Id + " staged formation spawn complete. "
                + zoneReport + " " + packageReport);
        }

        private IEnumerator SpawnAircraftCoordinated(Spawner spawner, GameObject prefab,
            Loadout loadout, GlobalPosition globalPosition, Quaternion rotation,
            Vector3 startingVelocity, FactionHQ hq, string uniqueName, float skill,
            float bravery, CoordinatedAircraftSpawn result)
        {
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            if (!_asyncFormationSpawning.Value)
            {
                try
                {
                    result.Aircraft = spawner.SpawnAircraft(null, prefab, loadout, 1f,
                        new LiveryKey(0), globalPosition, rotation, startingVelocity, null,
                        hq, uniqueName, skill, bravery);
                }
                catch (Exception exception)
                {
                    result.Error = exception;
                }
                totalTimer.Stop();
                result.TotalMilliseconds = totalTimer.ElapsedMilliseconds;
                yield break;
            }

            AsyncInstantiateOperation<GameObject> operation;
            float previousBudget = AsyncInstantiateOperation.GetIntegrationTimeMS();
            try
            {
                AsyncInstantiateOperation.SetIntegrationTimeMS(
                    Mathf.Clamp(_asyncSpawnIntegrationBudget.Value, 0.5f, 8f));
                operation = UnityEngine.Object.InstantiateAsync(prefab,
                    GlobalPositionExtensions.ToLocalPosition(globalPosition), rotation);
            }
            catch (Exception exception)
            {
                AsyncInstantiateOperation.SetIntegrationTimeMS(previousBudget);
                result.Error = exception;
                totalTimer.Stop();
                result.TotalMilliseconds = totalTimer.ElapsedMilliseconds;
                yield break;
            }

            float previousFrameAt = Time.realtimeSinceStartup;
            try
            {
                while (!operation.isDone)
                {
                    yield return null;
                    float frameAt = Time.realtimeSinceStartup;
                    result.WorstObservedFrameMilliseconds = Mathf.Max(
                        result.WorstObservedFrameMilliseconds,
                        (frameAt - previousFrameAt) * 1000f);
                    previousFrameAt = frameAt;
                }
            }
            finally
            {
                AsyncInstantiateOperation.SetIntegrationTimeMS(previousBudget);
            }

            GameObject spawnedObject = null;
            try
            {
                GameObject[] objects = operation.Result;
                if (objects == null || objects.Length != 1 || objects[0] == null)
                    throw new InvalidOperationException(
                        "async prefab integration returned no aircraft object");
                spawnedObject = objects[0];
                Aircraft aircraft = spawnedObject.GetComponent<Aircraft>();
                if (aircraft == null)
                    throw new InvalidOperationException(
                        "async aircraft clone has no Aircraft component");

                aircraft.NetworkHQ = hq;
                aircraft.NetworkUniqueName = uniqueName;
                aircraft.NetworkspawningHangar = null;
                aircraft.NetworkstartPosition = globalPosition;
                aircraft.NetworkstartRotation = rotation;
                aircraft.NetworkstartingVelocity = startingVelocity;
                aircraft.Networkloadout = loadout;
                aircraft.NetworkfuelLevel = 1f;
                aircraft.skill = skill;
                if (hq != null) aircraft.skill *= hq.airSkillMultiplier;
                aircraft.bravery = bravery;
                aircraft.SetLiveryKey(new LiveryKey(0), false);
                aircraft.NetworkplayerRef = PlayerRef.Invalid;
                aircraft.NetworkunitName = aircraft.definition.unitName;

                var networkTimer = System.Diagnostics.Stopwatch.StartNew();
                ServerObjectManagerExtensions.Spawn(spawner.ServerObjectManager,
                    spawnedObject, (INetworkPlayer)null);
                networkTimer.Stop();
                result.NetworkMilliseconds = networkTimer.ElapsedMilliseconds;
                result.Aircraft = aircraft;
            }
            catch (Exception exception)
            {
                result.Error = exception;
                if (spawnedObject != null)
                {
                    try { UnityEngine.Object.Destroy(spawnedObject); } catch { }
                }
            }
            totalTimer.Stop();
            result.TotalMilliseconds = totalTimer.ElapsedMilliseconds;
        }

        private void LogCoordinatedSpawn(string label, CoordinatedAircraftSpawn spawn)
        {
            Logger.LogInfo(label + (_asyncFormationSpawning.Value
                ? " async-integrated" : " native synchronous")
                + " aircraft spawn completed in "
                + spawn.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)
                + " ms total; network integration "
                + spawn.NetworkMilliseconds.ToString(CultureInfo.InvariantCulture)
                + " ms; worst observed assembly frame "
                + spawn.WorstObservedFrameMilliseconds.ToString("0.0",
                    CultureInfo.InvariantCulture) + " ms.");
        }

        private void UpdateOperation(float now)
        {
            BatteryOperation battery = _operation;
            if (battery == null) return;
            RebaseOperationForDatumShift(battery);
            if (now - battery.StartedAt > Mathf.Max(60f, _operationTimeout.Value))
            {
                bool recoveryCommitted = battery.Transports.Any(transport => transport != null
                    && (transport.ReleaseCursor >= transport.Cargo.Count
                        || transport.Phase == TransportPhase.Egress
                        || transport.Phase == TransportPhase.RecoveryHolding
                        || transport.Phase == TransportPhase.Returning
                        || transport.Phase == TransportPhase.Landing
                        || transport.Phase == TransportPhase.Complete));
                if (!battery.HasTransportLoss && !recoveryCommitted)
                {
                    AbortOperation("whole operation timeout", true);
                    return;
                }
                if (!battery.TimeoutSuppressionAnnounced)
                {
                    battery.TimeoutSuppressionAnnounced = true;
                    Logger.LogWarning("AIRLIFT-" + battery.Id
                        + " cleanup timeout suppressed after cargo release/recovery commitment; surviving Chimeras and deployed cargo are retained.");
                }
            }
            if (battery.FormationSpawnInProgress)
            {
                for (int index = 0; index < battery.Transports.Count; index++)
                {
                    TransportOperation spawned = battery.Transports[index];
                    if (spawned == null || spawned.Aircraft == null) continue;
                    if (_operation != battery) return;
                    UpdateTransport(battery, spawned, now);
                }
                return;
            }

            UpdateDropProfile(battery, now);
            UpdateFlareBurst(battery, now);
            RefreshThreatSnapshot(battery, now);
            for (int index = 0; index < battery.Transports.Count; index++)
            {
                TransportOperation transport = battery.Transports[index];
                if (_operation != battery) return;
                UpdateTransport(battery, transport, now);
            }
            if (_operation != battery) return;
            UpdateCoordinatedEgressStart(battery, now);
            if (_operation != battery) return;
            UpdateCoordinatedEgressBreak(battery, now);
            if (_operation != battery) return;
            UpdateRecoveryLandingQueue(battery, now);
            if (_operation != battery) return;
            UpdateSynchronizedRelease(battery, now);
            if (_operation != battery) return;
            string linkError = null;
            bool linkReady = !battery.RadarLinkCheckComplete
                && TryValidateActualRadarLink(battery, out linkError);
            if (linkReady && !battery.RadarLinkCheckComplete)
            {
                battery.RadarLinkCheckComplete = true;
                battery.RadarLinkValidated = true;
                Announce("AIRLIFT-" + battery.Id
                    + ": radar and all three R9 sites landed within the 17 km coverage radius.");
            }
            else if (linkError != null && !battery.RadarLinkCheckComplete)
            {
                battery.RadarLinkCheckComplete = true;
                battery.RadarLinkValidated = false;
                Logger.LogWarning("AIRLIFT-" + battery.Id + " deployed battery is degraded: "
                    + linkError + ". Units are being retained.");
                Announce("AIRLIFT-" + battery.Id + ": warning—" + linkError
                    + "; deployed units will remain in place.");
            }

            bool allTerminal = battery.Transports.Count > 0;
            for (int index = 0; index < battery.Transports.Count && allTerminal; index++)
                allTerminal = battery.Transports[index] != null
                    && battery.Transports[index].IsTerminal;
            if (!allTerminal) return;
            if (!battery.RadarLinkCheckComplete)
            {
                float lastDeployment = battery.StartedAt;
                for (int index = 0; index < battery.DeployedCargo.Count; index++)
                {
                    DeployedCargoRecord record = battery.DeployedCargo[index];
                    if (record != null && record.SpawnedAt > lastDeployment)
                        lastDeployment = record.SpawnedAt;
                }
                if (now - lastDeployment > Mathf.Max(30f, _cargoTouchdownTimeout.Value))
                {
                    battery.RadarLinkCheckComplete = true;
                    Logger.LogWarning("AIRLIFT-" + battery.Id
                        + " radar/R9 touchdown validation timed out; deployed units are being retained.");
                    Announce("AIRLIFT-" + battery.Id
                        + ": touchdown validation timed out; deployed units will remain in place.");
                }
                return;
            }
            FinishOperation(battery.RadarLinkValidated
                ? "battery deployed within radar coverage"
                : "battery deployed with unconfirmed or degraded radar coverage");
        }

        private void RebaseOperationForDatumShift(BatteryOperation battery)
        {
            if (battery == null) return;
            Vector3 currentOrigin = Datum.originPosition;
            Vector3 localShift = new Vector3(
                AirliftPolicy.DatumRebaseDelta(battery.DatumOrigin.x, currentOrigin.x),
                AirliftPolicy.DatumRebaseDelta(battery.DatumOrigin.y, currentOrigin.y),
                AirliftPolicy.DatumRebaseDelta(battery.DatumOrigin.z, currentOrigin.z));
            if (localShift.sqrMagnitude < 0.01f) return;

            battery.DatumOrigin = currentOrigin;
            battery.RadarTarget += localShift;
            battery.SharedReleasePoint += localShift;
            for (int index = 0; index < battery.Transports.Count; index++)
            {
                TransportOperation transport = battery.Transports[index];
                if (transport == null) continue;
                transport.DropPoint += localShift;
                transport.SpawnPoint += localShift;
                transport.EgressPoint += localShift;
                transport.ReturnPoint += localShift;
                transport.FlightDestination += localShift;
                for (int cargoIndex = 0; cargoIndex < transport.Cargo.Count; cargoIndex++)
                {
                    CargoAssignment assignment = transport.Cargo[cargoIndex];
                    if (assignment != null) assignment.TargetPoint += localShift;
                }
            }
            for (int index = 0; index < battery.DeployedCargo.Count; index++)
            {
                DeployedCargoRecord cargo = battery.DeployedCargo[index];
                if (cargo != null) cargo.IntendedTarget += localShift;
            }
            DebugLog("AIRLIFT-" + battery.Id + " rebased route plan by "
                + FormatVector(localShift) + " after Datum origin changed to "
                + FormatVector(currentOrigin) + ".");
        }

        private void UpdateDropProfile(BatteryOperation battery, float now)
        {
            if (battery == null || battery.DropProfileStarted) return;
            TransportOperation leader = null;
            for (int index = 0; index < battery.Transports.Count; index++)
            {
                TransportOperation candidate = battery.Transports[index];
                if (candidate == null || candidate.IsTerminal || candidate.Aircraft == null
                    || candidate.Aircraft.disabled)
                    continue;
                if (leader == null) leader = candidate;
                if (string.Equals(candidate.Callsign, "ALPHA", StringComparison.Ordinal))
                {
                    leader = candidate;
                    break;
                }
            }
            float descentDistance = battery.Kind == AirliftOperationKind.CombatRunwayDrop
                ? _combatDescentDistance.Value : _descentDistance.Value;
            if (leader == null || !AirliftPolicy.ShouldStartDropProfile(
                    HorizontalDistance(leader.Aircraft.transform.position, leader.DropPoint),
                    descentDistance))
                return;

            battery.DropProfileStarted = true;
            for (int index = 0; index < battery.Transports.Count; index++)
            {
                TransportOperation transport = battery.Transports[index];
                if (transport != null && !transport.IsTerminal)
                {
                    transport.TargetRadarAltitude = battery.DropRadarAltitude;
                    if (battery.Kind == AirliftOperationKind.CombatRunwayDrop)
                    {
                        transport.GearDownForDrop = true;
                        if (transport.Aircraft != null)
                            transport.Aircraft.SetGear(true);
                    }
                }
            }
            Announce("AIRLIFT-" + battery.Id
                + ": synchronized descent to "
                + battery.DropRadarAltitude.ToString("0", CultureInfo.InvariantCulture)
                + " m radar altitude for the drop run.");
        }

        private void TriggerDropFlareBurst(BatteryOperation battery, float now)
        {
            if (battery == null || battery.FlaresTriggered) return;
            battery.FlaresTriggered = true;
            battery.FlareBurstsRemaining = AirliftPolicy.EffectiveFlareBurstCount(
                _flareBurstCount.Value);
            battery.NextFlareBurstAt = now;
            Announce("AIRLIFT-" + battery.Id + ": synchronized flare sequence—"
                + battery.FlareBurstsRemaining.ToString(CultureInfo.InvariantCulture)
                + " bursts during the drop.");
            UpdateFlareBurst(battery, now);
        }

        private void UpdateFlareBurst(BatteryOperation battery, float now)
        {
            if (battery == null || !battery.FlaresTriggered
                || battery.FlareBurstsRemaining <= 0
                || now < battery.NextFlareBurstAt)
                return;
            int triggered = 0;
            foreach (TransportOperation transport in battery.Transports)
            {
                Aircraft aircraft = transport == null ? null : transport.Aircraft;
                if (aircraft == null || aircraft.disabled || aircraft.countermeasureManager == null
                    || aircraft.countermeasureManager.GetFlareAmmoProportion() <= 0f)
                    continue;
                try
                {
                    // Nuclear Option's native helper explicitly selects countermeasure
                    // station zero (flares), pulses it, and releases the trigger after
                    // 0.1 seconds. Repeating it avoids relying on an arbitrary activeIndex.
                    aircraft.countermeasureManager.PopFlares();
                    transport.CountermeasureHoldUntil = now + 0.2f;
                    triggered++;
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(BatteryLabel(battery, transport)
                        + " native flare pulse failed: "
                        + exception.GetType().Name + ".");
                }
            }
            int pulse = AirliftPolicy.EffectiveFlareBurstCount(_flareBurstCount.Value)
                - battery.FlareBurstsRemaining + 1;
            battery.FlareBurstsRemaining--;
            battery.NextFlareBurstAt = now
                + AirliftPolicy.EffectiveFlareBurstInterval(_flareBurstInterval.Value);
            DebugLog("AIRLIFT-" + battery.Id + " native flare pulse "
                + pulse.ToString(CultureInfo.InvariantCulture) + "/"
                + AirliftPolicy.EffectiveFlareBurstCount(_flareBurstCount.Value)
                    .ToString(CultureInfo.InvariantCulture) + " fired on "
                + triggered.ToString(CultureInfo.InvariantCulture) + " surviving Chimera(s).");
        }

        private void UpdateTransport(BatteryOperation battery, TransportOperation transport, float now)
        {
            if (transport == null || transport.IsTerminal) return;
            if (transport.Aircraft == null || transport.Aircraft.disabled)
            {
                HandleTransportLoss(battery, transport,
                    transport.Callsign + " was destroyed or became unavailable", now);
                return;
            }

            ExcludeFromNativeAiLimit(transport, battery.Id);
            RefreshCargoDoors(transport, now, false);
            UpdateTransportJamming(battery, transport, now);
            UpdateCombatRunwaySuppression(battery, transport, now);

            switch (transport.Phase)
            {
                case TransportPhase.WaitingForSystems:
                    bool pilotReady = TryAttachTransportState(transport);
                    bool cargoReady = pilotReady && TryResolveSpawnedCargoStations(transport);
                    bool doorsReady = cargoReady && TryResolveTransportCargoDoors(transport);
                    bool wingSystemsReady = doorsReady
                        && (battery.Kind == AirliftOperationKind.CombatRunwayDrop
                            ? TryResolveTransportSuppressionStation(transport)
                            : TryResolveTransportJammers(transport));
                    if (pilotReady && cargoReady && doorsReady && wingSystemsReady)
                    {
                        transport.SetPhase(TransportPhase.Ingress, now);
                        RefreshCargoDoors(transport, now, true);
                        DebugLog(BatteryLabel(battery, transport)
                            + " systems validated; cargo doors pre-opened and ingress started.");
                    }
                    else if (now - transport.PhaseStartedAt > 20f)
                    {
                        Logger.LogWarning(BatteryLabel(battery, transport)
                            + " initialization diagnostic: " + DescribeTransportSystems(transport));
                        string failure = transport.Callsign + (pilotReady
                            ? " native cargo/door/wing station sequence failed validation"
                            : " pilot failed to initialize");
                        if (!IsolateRuntimeFailureAfterCasualty(battery, transport, failure, now))
                            AbortOperation(failure, true);
                        return;
                    }
                    break;

                case TransportPhase.Ingress:
                    bool isLeader = IsFormationReleaseLeader(battery, transport);
                    if (isLeader && !battery.LeaderReleasePointPrepared
                        && LeaderAcquisitionWindowSatisfied(transport))
                    {
                        string preparationReport;
                        if (TryPrepareLeaderReleasePoint(battery, transport, out preparationReport))
                        {
                            Announce(BatteryLabel(battery, transport)
                                + " selected a safe live release point at "
                                + FormatVector(battery.RadarTarget) + ".");
                            DebugLog(BatteryLabel(battery, transport) + " " + preparationReport);
                        }
                        else
                        {
                            DebugLog(BatteryLabel(battery, transport)
                                + " rejected a candidate live release point: " + preparationReport);
                        }
                    }
                    else if (isLeader && !battery.LeaderReleasePointPrepared && PassedDropZone(transport,
                        AirliftPolicy.EffectiveLeaderReleaseRadius(_releaseRadius.Value) + 250f))
                    {
                        battery.LeaderReleasePointPrepared = true;
                        battery.SharedReleasePoint = battery.RadarTarget;
                        battery.SharedReleasePointEstablished = true;
                        foreach (TransportOperation formationTransport in battery.Transports)
                            RouteTransportThroughPoint(formationTransport, battery.RadarTarget);
                        Announce("AIRLIFT-" + battery.Id
                            + ": live acquisition corridor missed; using the prevalidated synchronized release line.");
                    }
                    break;

                case TransportPhase.Releasing:
                    if (battery.SynchronizedReleaseWave >= battery.ReleaseWaveCount)
                        ReleaseSequentially(battery, transport, now);
                    break;

                case TransportPhase.Egress:
                    if (!transport.EgressReady && now >= transport.DespawnAt)
                    {
                        float arrivalRadius = Mathf.Clamp(
                            _returnArrivalRadius.Value, 500f, 1000f);
                        bool reachedTurnPoint = HorizontalDistance(
                            transport.Aircraft.transform.position,
                            transport.EgressPoint) <= arrivalRadius;
                        Vector3 egressLeg = transport.EgressPoint - transport.DropPoint;
                        egressLeg.y = 0f;
                        Vector3 beyondTurn = transport.Aircraft.transform.position
                            - transport.EgressPoint;
                        beyondTurn.y = 0f;
                        bool passedTurnPoint = egressLeg.sqrMagnitude > 1f
                            && Vector3.Dot(beyondTurn, egressLeg.normalized) > 0f;
                        bool failsafeClear = now - transport.PhaseStartedAt > 60f;
                        if (reachedTurnPoint || passedTurnPoint || failsafeClear)
                        {
                            transport.EgressReady = true;
                            DebugLog(BatteryLabel(battery, transport)
                                + " clear of the coordinated egress turn"
                                + (failsafeClear ? " by time fail-safe" : string.Empty)
                                + ".");
                        }
                    }
                    break;

                case TransportPhase.RecoveryHolding:
                    UpdateRecoveryHoldingDestination(transport, now);
                    break;

                case TransportPhase.Returning:
                    float returnDistance = HorizontalDistance(
                        transport.Aircraft.transform.position, transport.ReturnPoint);
                    if (AirliftPolicy.OffMapReturnReached(returnDistance,
                            _returnArrivalRadius.Value))
                        CompleteOffMapExtraction(battery, transport);
                    break;

                case TransportPhase.Landing:
                    if (transport.Aircraft.IsLanded())
                        FinishTransport(battery, transport,
                            "landed; released to native pilot/ejection cleanup");
                    else if (transport.Pilot == null || transport.Pilot.dead
                        || transport.Pilot.ejected)
                        FinishTransport(battery, transport,
                            "native recovery pilot became unavailable; aircraft retained");
                    else if (transport.Pilot.currentState != transport.Pilot.AILandingState
                        && now - transport.PhaseStartedAt > 2f)
                    {
                        if (HasActiveNativeMissileWarning(transport.Aircraft))
                        {
                            // Native landing intentionally yields to AICombat while a missile
                            // warning is active. Leave that defense intact, then retry promptly.
                            transport.NextLandingRetryAt = now + 2f;
                            break;
                        }
                        if (now < transport.NextLandingRetryAt) break;

                        Airbase nativeChoice;
                        string landingError;
                        if (transport.RecoveryAirbase != null
                            && !transport.RecoveryAirbase.disabled
                            && transport.RecoveryAirbase.CurrentHQ == battery.Hq
                            && TryFindNearestRecoveryAirbase(transport.Aircraft, battery.Hq,
                                out nativeChoice, out landingError)
                            && nativeChoice == transport.RecoveryAirbase)
                        {
                            transport.Pilot.SwitchState(transport.Pilot.AILandingState);
                            TryAccelerateNativeLanding(transport.Pilot,
                                BatteryLabel(battery, transport));
                            transport.PhaseStartedAt = now;
                            transport.NextLandingRetryAt = now + 5f;
                            DebugLog(BatteryLabel(battery, transport)
                                + " resumed its native landing approach after defensive interruption.");
                            break;
                        }

                        transport.Pilot.SwitchState(transport.TransportState);
                        transport.NextRecoveryUpdateAt = now;
                        transport.NextLandingRetryAt = now + 2f;
                        transport.SetPhase(TransportPhase.RecoveryHolding, now);
                        UpdateRecoveryHoldingDestination(transport, now);
                        Announce(BatteryLabel(battery, transport)
                            + ": defensive interruption complete; diverting straight back to "
                            + transport.RecoveryAirbaseName + ".");
                    }
                    break;

                case TransportPhase.Despawning:
                    if (now >= transport.DespawnAt)
                        FinishTransport(battery, transport,
                            "legacy despawn request completed without forced destruction");
                    break;
            }
        }

        private static bool IsFormationReleaseLeader(BatteryOperation battery,
            TransportOperation candidate)
        {
            if (battery == null || candidate == null || candidate.IsTerminal
                || candidate.Aircraft == null || candidate.Aircraft.disabled)
                return false;
            for (int index = 0; index < battery.Transports.Count; index++)
            {
                TransportOperation transport = battery.Transports[index];
                if (transport == null || transport.IsTerminal || transport.Aircraft == null
                    || transport.Aircraft.disabled)
                    continue;
                return transport == candidate;
            }
            return false;
        }

        private static bool HasActiveNativeMissileWarning(Aircraft aircraft)
        {
            MissileWarning warning = aircraft == null ? null : aircraft.GetMissileWarningSystem();
            return warning != null && warning.knownMissiles != null
                && warning.knownMissiles.Any(missile => missile != null && !missile.disabled);
        }

        private void UpdateSynchronizedRelease(BatteryOperation battery, float now)
        {
            if (battery == null || battery.FormationSpawnInProgress) return;
            if (!battery.SynchronizedReleaseStarted)
            {
                if (!battery.LeaderReleasePointPrepared) return;

                int survivors = 0;
                int ready = 0;
                int releaseStateReady = 0;
                bool forceImmediate = false;
                for (int index = 0; index < battery.Transports.Count; index++)
                {
                    TransportOperation transport = battery.Transports[index];
                    if (transport == null || transport.IsTerminal
                        || transport.Aircraft == null || transport.Aircraft.disabled)
                        continue;
                    survivors++;
                    if (AircraftReleaseStateSatisfied(transport)) releaseStateReady++;
                    if (transport.Phase != TransportPhase.Ingress) return;
                    bool predictionReady;
                    bool predictionMissed;
                    string predictionReport;
                    if (TryEvaluatePrecisionRelease(battery, transport,
                            false,
                            out predictionReady, out predictionMissed,
                            out predictionReport))
                    {
                        if (predictionReady) ready++;
                        if (predictionMissed) forceImmediate = true;
                    }
                    else if (SharedReleaseGateSatisfied(transport))
                    {
                        ready++;
                    }
                    if (PassedPrecisionTarget(battery, transport, 150f))
                        forceImmediate = true;
                }
                if (battery.Kind == AirliftOperationKind.CombatRunwayDrop
                    && releaseStateReady != survivors)
                    forceImmediate = false;
                if (survivors == 0
                    || (!AirliftPolicy.SynchronizedReleaseReady(ready, survivors)
                        && !forceImmediate))
                    return;

                battery.SynchronizedReleaseStarted = true;
                battery.SynchronizedReleaseWave = 0;
                battery.NextSynchronizedReleaseAt = now;
                var predictionReports = new List<string>(survivors);
                for (int index = 0; index < battery.Transports.Count; index++)
                {
                    TransportOperation transport = battery.Transports[index];
                    if (transport == null || transport.IsTerminal
                        || transport.Aircraft == null || transport.Aircraft.disabled)
                        continue;
                    bool ignoredReady;
                    bool ignoredMissed;
                    string predictionReport;
                    TryEvaluatePrecisionRelease(battery, transport, true,
                        out ignoredReady, out ignoredMissed, out predictionReport);
                    predictionReports.Add(transport.Callsign + " " + predictionReport);
                    transport.ImmediateRelease = forceImmediate;
                    transport.SetPhase(TransportPhase.Releasing, now);
                    transport.NextReleaseAt = now;
                }
                Logger.LogInfo("AIRLIFT-" + battery.Id
                    + " synchronized precision solution: "
                    + string.Join(" | ", predictionReports.ToArray()));
                TriggerDropFlareBurst(battery, now);
                Announce("AIRLIFT-" + battery.Id + ": "
                    + survivors.ToString(CultureInfo.InvariantCulture)
                    + (survivors == 1 ? " surviving Chimera " : " surviving Chimeras ")
                    + (forceImmediate
                        ? "passed the precision window; forcing a synchronized immediate drop."
                        : "reached the predicted impact solution; synchronized cargo drop commencing."));
            }

            ReleaseSynchronizedWave(battery, now);
        }

        private void ReleaseSynchronizedWave(BatteryOperation battery, float now)
        {
            int wave = battery.SynchronizedReleaseWave;
            if (wave >= battery.ReleaseWaveCount || now < battery.NextSynchronizedReleaseAt) return;
            List<TransportOperation> releasing = battery.Transports.Where(transport =>
                transport != null && !transport.IsTerminal && transport.Aircraft != null
                && !transport.Aircraft.disabled
                && transport.Phase == TransportPhase.Releasing).ToList();
            var waveTransports = releasing.Where(transport =>
                transport.Cargo.Count > wave).ToList();
            if (waveTransports.Count == 0) return;
            var assignments = waveTransports.Select(transport =>
                transport.Cargo[wave]).ToArray();
            if (assignments.Any(assignment => assignment == null
                || assignment.Station == null))
                return;

            bool releasedAny = false;
            for (int index = 0; index < waveTransports.Count; index++)
            {
                TransportOperation transport = waveTransports[index];
                CargoAssignment assignment = assignments[index];
                if (assignment.ReleaseCommands >= assignment.ExpectedCargoCount
                    || !assignment.Station.Ready())
                    continue;
                assignment.ReleaseCommands++;
                try
                {
                    assignment.Station.LaunchMount(transport.Aircraft, null,
                        assignment.NetworkTarget);
                }
                catch (Exception exception)
                {
                    assignment.ReleaseCommands--;
                    string failure = transport.Callsign + " synchronized native "
                        + assignment.Role + " release threw " + exception.GetType().Name;
                    if (!IsolateRuntimeFailureAfterCasualty(battery, transport, failure, now))
                    {
                        AbortOperation(failure, true);
                        return;
                    }
                    continue;
                }
                releasedAny = true;
                if (assignment.ReleaseCommands >= assignment.ExpectedCargoCount)
                    transport.ReleaseCursor = wave + 1;
                transport.NextReleaseAt = now + Mathf.Max(0.1f, _releaseInterval.Value);
                GlobalPosition releaseGlobal = GlobalPositionExtensions.ToGlobalPosition(
                    transport.Aircraft.transform.position);
                DebugLog(BatteryLabel(battery, transport) + " sent synchronized wave "
                    + (wave + 1).ToString(CultureInfo.InvariantCulture) + " native "
                    + assignment.Role + " release at global "
                    + FormatGlobalPosition(releaseGlobal) + " toward "
                    + FormatGlobalPosition(assignment.NetworkTarget) + "; horizontal lead "
                    + HorizontalDistance(releaseGlobal, assignment.NetworkTarget)
                        .ToString("0", CultureInfo.InvariantCulture) + " m.");
            }

            if (releasedAny)
                battery.NextSynchronizedReleaseAt =
                    now + Mathf.Max(0.1f, _releaseInterval.Value);
            if (!AirliftPolicy.SynchronizedCargoWaveComplete(
                    assignments.Select(assignment => assignment.ReleaseCommands).ToArray(),
                    assignments.Select(assignment => assignment.ExpectedCargoCount).ToArray()))
                return;

            battery.SynchronizedReleaseWave++;
            Announce("AIRLIFT-" + battery.Id + ": synchronized cargo wave "
                + (wave + 1).ToString(CultureInfo.InvariantCulture)
                + "/" + battery.ReleaseWaveCount.ToString(CultureInfo.InvariantCulture)
                + " complete; all assigned units released.");
        }

        private static void RefreshThreatSnapshot(BatteryOperation battery, float now)
        {
            if (battery == null || now < battery.NextThreatSnapshotAt) return;
            bool required = false;
            if (!required)
            {
                for (int index = 0; index < battery.Transports.Count; index++)
                {
                    TransportOperation transport = battery.Transports[index];
                    if (transport == null || transport.IsTerminal
                        || transport.JammingPods.Count == 0)
                        continue;
                    required = true;
                    break;
                }
            }
            if (!required)
            {
                battery.ThreatAircraft.Clear();
                battery.NextThreatSnapshotAt = now + 0.5f;
                return;
            }
            battery.ThreatAircraft.Clear();
            List<Aircraft> registered = UnitRegistry.allAircraft;
            if (registered != null)
            {
                for (int index = 0; index < registered.Count; index++)
                {
                    Aircraft aircraft = registered[index];
                    if (aircraft != null && !aircraft.disabled)
                        battery.ThreatAircraft.Add(aircraft);
                }
            }
            battery.NextThreatSnapshotAt = now + 0.25f;
        }

        private static bool MissileTargetsTransport(BatteryOperation battery,
            Missile missile)
        {
            if (battery == null || missile == null) return false;
            for (int index = 0; index < battery.Transports.Count; index++)
            {
                TransportOperation transport = battery.Transports[index];
                if (transport != null && transport.Aircraft != null
                    && missile.targetID.Equals(transport.Aircraft.persistentID))
                    return true;
            }
            return false;
        }

        private static Missile FindNearestNativeMissileWarning(BatteryOperation battery,
            Vector3 origin, float maximumRange, bool requireTracked)
        {
            if (battery == null) return null;
            Missile nearest = null;
            float nearestDistance = Mathf.Max(0f, maximumRange);
            nearestDistance *= nearestDistance;
            for (int transportIndex = 0;
                transportIndex < battery.Transports.Count; transportIndex++)
            {
                TransportOperation transport = battery.Transports[transportIndex];
                Aircraft aircraft = transport == null ? null : transport.Aircraft;
                MissileWarning warning = aircraft == null
                    ? null : aircraft.GetMissileWarningSystem();
                if (warning == null || warning.knownMissiles == null) continue;
                List<Missile> missiles = warning.knownMissiles;
                for (int missileIndex = 0; missileIndex < missiles.Count; missileIndex++)
                {
                    Missile missile = missiles[missileIndex];
                    if (missile == null || missile.disabled
                        || !MissileTargetsTransport(battery, missile)
                        || (requireTracked && !IsTrackedForJamming(battery.Hq, missile)))
                        continue;
                    float distance = HorizontalSquareDistance(
                        missile.transform.position, origin);
                    if (distance >= nearestDistance) continue;
                    nearest = missile;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private void HandleTransportLoss(BatteryOperation battery,
            TransportOperation transport, string reason, float now)
        {
            if (battery == null || transport == null || transport.IsTerminal) return;
            transport.JammerTarget = null;
            transport.JammerTargetReason = null;
            transport.JammingPods.Clear();
            transport.JammerStations.Clear();
            transport.SetPhase(TransportPhase.Aborted, now);
            battery.HasTransportLoss = true;
            int survivors = battery.Transports.Count(item => item != null
                && !item.IsTerminal && item.Aircraft != null && !item.Aircraft.disabled);
            Logger.LogWarning(BatteryLabel(battery, transport) + " lost: " + reason
                + "; " + survivors.ToString(CultureInfo.InvariantCulture)
                + " surviving Chimera(s) continue independently.");
            Announce(BatteryLabel(battery, transport) + ": shot down; "
                + survivors.ToString(CultureInfo.InvariantCulture)
                + (survivors == 1 ? " Chimera is" : " Chimeras are")
                + " continuing the assigned drop and recovery plan.",
                AirliftAnnouncementKind.AircraftShotDown);
        }

        private bool IsolateRuntimeFailureAfterCasualty(BatteryOperation battery,
            TransportOperation transport, string reason, float now)
        {
            if (battery == null || transport == null || !battery.HasTransportLoss)
                return false;

            Logger.LogWarning(BatteryLabel(battery, transport) + " isolated runtime fault after "
                + "a formation casualty: " + reason + "; surviving aircraft will not be deleted.");
            if (transport.Aircraft == null || transport.Aircraft.disabled)
            {
                HandleTransportLoss(battery, transport, reason, now);
                return true;
            }

            bool packageReleased = AirliftPolicy.RecoveryAllowed(
                transport.ReleaseCursor, transport.Cargo.Count,
                transport.DeployedCargo.Count, transport.ExpectedCargoCount);
            if (!packageReleased)
            {
                transport.NextReleaseAt = now + 1f;
                if (transport.Phase == TransportPhase.WaitingForSystems)
                    transport.PhaseStartedAt = now;
                DebugLog(BatteryLabel(battery, transport)
                    + " remains committed to cargo release after isolated fault.");
                return true;
            }

            transport.PackageDeploymentConfirmed = true;
            DebugLog(BatteryLabel(battery, transport)
                + " package confirmed after isolated fault; waiting at the synchronized egress barrier.");
            return true;
        }

        private bool TryAttachTransportState(TransportOperation transport)
        {
            if (transport.Pilot != null && transport.TransportState != null) return true;
            if (transport.Aircraft.pilots == null) return false;
            Pilot pilot = transport.Aircraft.pilots.FirstOrDefault(candidate =>
                candidate != null && !candidate.playerControlled)
                ?? transport.Aircraft.pilots.FirstOrDefault(candidate => candidate != null);
            if (pilot == null) return false;
            var state = new AirliftTransportState(transport);
            state.Initialize(pilot);
            pilot.SwitchState(state);
            transport.Pilot = pilot;
            transport.TransportState = state;
            return true;
        }

        private bool TryResolveSpawnedCargoStations(TransportOperation transport)
        {
            if (transport.Cargo.All(assignment => assignment.Station != null)) return true;
            List<WeaponStation> stations = transport.Aircraft.weaponStations;
            if (stations == null) return false;
            if (transport.Cargo.Any(assignment => assignment.ExpectedCargoCount <= 0
                || assignment.ExpectedUnitKeys.Count == 0))
                return false;

            var liveStations = new List<WeaponStation>();
            var liveKeys = new List<string>();
            var liveCargo = new List<MountedCargo>();
            foreach (WeaponStation station in stations.Where(candidate =>
                candidate != null && candidate.Cargo && candidate.Weapons != null))
            {
                int weaponCount = station.Weapons.Count;
                if (weaponCount == 0) continue;
                int startIndex;
                try
                {
                    startIndex = (int)WeaponStationIndexField.GetValue(station);
                }
                catch
                {
                    return false;
                }
                startIndex = ((startIndex % weaponCount) + weaponCount) % weaponCount;

                for (int offset = 0; offset < weaponCount; offset++)
                {
                    MountedCargo cargo = station.Weapons[(startIndex + offset) % weaponCount] as MountedCargo;
                    if (cargo == null || cargo.cargo == null) continue;
                    string key = cargo.cargo.jsonKey ?? string.Empty;
                    int rounds = Math.Max(0, cargo.GetAmmoTotal());
                    for (int round = 0; round < rounds; round++)
                    {
                        liveStations.Add(station);
                        liveKeys.Add(key);
                        liveCargo.Add(cargo);
                    }
                }
            }

            if (liveKeys.Count != transport.ExpectedCargoCount) return false;
            int sequenceIndex = 0;
            for (int assignmentIndex = 0; assignmentIndex < transport.Cargo.Count; assignmentIndex++)
            {
                CargoAssignment assignment = transport.Cargo[assignmentIndex];
                WeaponStation assignmentStation = null;
                MountedCargo assignmentCargo = null;
                for (int round = 0; round < assignment.ExpectedCargoCount; round++)
                {
                    if (sequenceIndex >= liveKeys.Count
                        || !assignment.ExpectedUnitKeys.Contains(liveKeys[sequenceIndex]))
                        return false;
                    if (assignmentStation == null)
                    {
                        assignmentStation = liveStations[sequenceIndex];
                        assignmentCargo = liveCargo[sequenceIndex];
                    }
                    else if (assignmentStation != liveStations[sequenceIndex])
                        return false;
                    sequenceIndex++;
                }
                assignment.Station = assignmentStation;
                assignment.MountedCargo = assignmentCargo;
            }
            return sequenceIndex == liveKeys.Count;
        }

        private static bool TryResolveTransportCargoDoors(TransportOperation transport)
        {
            if (transport.CargoDoors.Count > 0) return true;
            for (int index = 0; index < transport.Cargo.Count; index++)
            {
                MountedCargo cargo = transport.Cargo[index].MountedCargo;
                if (cargo == null) return false;
                BayDoor directDoor = MountedCargoDoorField.GetValue(cargo) as BayDoor;
                if (directDoor != null)
                    AddUniqueReference(transport.CargoDoors, directDoor);
                Hardpoint hardpoint = WeaponHardpointField.GetValue(cargo) as Hardpoint;
                if (hardpoint == null || hardpoint.bayDoors == null) continue;
                for (int doorIndex = 0; doorIndex < hardpoint.bayDoors.Length; doorIndex++)
                {
                    BayDoor hardpointDoor = hardpoint.bayDoors[doorIndex];
                    if (hardpointDoor != null)
                        AddUniqueReference(transport.CargoDoors, hardpointDoor);
                }
            }
            return transport.CargoDoors.Count > 0;
        }

        private void RefreshCargoDoors(TransportOperation transport, float now,
            bool force)
        {
            if (transport == null || transport.CargoDoors.Count == 0
                || (!force && transport.Phase != TransportPhase.Ingress
                    && transport.Phase != TransportPhase.Releasing)
                || (!force && now < transport.NextCargoDoorRefreshAt))
                return;
            float holdTime = Mathf.Clamp(_cargoDoorHoldTime.Value, 15f, 120f);
            for (int index = 0; index < transport.CargoDoors.Count; index++)
            {
                BayDoor door = transport.CargoDoors[index];
                if (door != null) door.OpenDoor(holdTime);
            }
            transport.NextCargoDoorRefreshAt = now
                + Mathf.Clamp(_cargoDoorRefreshInterval.Value, 2f, 15f);
        }

        private static bool TryResolveTransportJammers(TransportOperation transport)
        {
            if (transport.JammingPods.Count > 0)
                return transport.JammerStations.Count == transport.JammingPods.Count;
            if (transport.Aircraft.weaponStations == null || transport.JammerMount == null
                || transport.JammerMount.prefab == null)
                return false;

            List<WeaponStation> exactStations = transport.Aircraft.weaponStations
                .Where(station => station != null && station.WeaponInfo != null
                    && ReferenceEquals(station.WeaponInfo, transport.JammerMount.info)
                    && station.WeaponInfo.jammer && station.Weapons != null).ToList();
            if (exactStations.Count != 1) return false;
            WeaponStation exactStation = exactStations[0];
            if (exactStation.Weapons.Any(weapon => weapon != null && !(weapon is JammingPod)))
                return false;
            List<JammingPod> live = exactStation.Weapons.OfType<JammingPod>()
                .Where(pod => pod != null).Distinct().ToList();
            if (!AirliftPolicy.HasExactTransportJammerPodCount(live.Count)) return false;
            for (int index = 0; index < live.Count; index++)
            {
                transport.JammingPods.Add(live[index]);
                transport.JammerStations.Add(exactStation);
            }
            return true;
        }

        private static bool TryResolveTransportSuppressionStation(
            TransportOperation transport)
        {
            if (transport.SuppressionStation != null)
                return transport.SuppressionStation.Ammo >= 0;
            if (transport.Aircraft == null
                || transport.Aircraft.weaponStations == null
                || transport.SuppressionMount == null
                || transport.SuppressionMount.info == null)
                return false;
            List<WeaponStation> exact = transport.Aircraft.weaponStations
                .Where(station => station != null && station.WeaponInfo != null
                    && ReferenceEquals(station.WeaponInfo,
                        transport.SuppressionMount.info)
                    && station.Weapons != null).ToList();
            if (exact.Count != 1) return false;
            WeaponStation station = exact[0];
            if (station.Cargo || station.WeaponInfo.nuclear
                || !station.WeaponInfo.missile
                || station.Weapons.Count != 3
                || station.Weapons.Any(weapon => weapon == null
                    || !(weapon is MountedMissile)))
                return false;
            transport.SuppressionStation = station;
            return true;
        }

        private void UpdateCombatRunwaySuppression(BatteryOperation battery,
            TransportOperation transport, float now)
        {
            if (battery == null || transport == null
                || battery.Kind != AirliftOperationKind.CombatRunwayDrop)
                return;
            if (transport.Phase != TransportPhase.Ingress)
            {
                ClearSuppressionTarget(transport);
                return;
            }
            WeaponStation station = transport.SuppressionStation;
            Aircraft aircraft = transport.Aircraft;
            if (station == null || aircraft == null || aircraft.disabled
                || station.Ammo <= 0 || now < transport.NextSuppressionLaunchAt)
                return;

            RefreshCombatSuppressionTargets(battery, now);
            float retry;
            Unit target = battery.SuppressionTargets
                .Where(candidate => candidate != null && !candidate.disabled
                    && (!battery.SuppressionRetryAt.TryGetValue(candidate,
                            out retry) || now >= retry))
                .OrderBy(candidate => HorizontalSquareDistance(
                    candidate.transform.position, aircraft.transform.position))
                .FirstOrDefault(candidate => CombatSuppressionLaunchReady(
                    battery, transport, candidate));
            if (target == null)
            {
                ClearSuppressionTarget(transport);
                return;
            }

            try
            {
                GlobalPosition known;
                if (battery.Hq == null
                    || !battery.Hq.TryGetKnownPosition(target, out known))
                    return;
                transport.SuppressionTarget = target;
                if (transport.Pilot != null)
                    transport.Pilot.SetPrimaryTarget(target);
                if (aircraft.weaponManager != null)
                {
                    aircraft.weaponManager.ClearTargetList();
                    aircraft.weaponManager.AddTargetList(target);
                    aircraft.weaponManager.SetActiveStation(station.Number);
                    aircraft.weaponManager.currentWeaponStation = station;
                }
                station.LaunchMount(aircraft, target, known);
                transport.SuppressionLaunches++;
                transport.NextSuppressionLaunchAt = now + 1.5f;
                battery.SuppressionRetryAt[target] = now
                    + Mathf.Clamp(_combatSuppressionRetry.Value, 3f, 30f);
                DebugLog(BatteryLabel(battery, transport)
                    + " launched AGM-68 runway suppression round "
                    + transport.SuppressionLaunches.ToString(
                        CultureInfo.InvariantCulture) + " at "
                    + target.persistentID + ".");
            }
            catch (Exception exception)
            {
                transport.NextSuppressionLaunchAt = now + 2f;
                Logger.LogWarning(BatteryLabel(battery, transport)
                    + " AGM-68 suppression launch failed: "
                    + exception.GetType().Name + ".");
            }
        }

        private void RefreshCombatSuppressionTargets(BatteryOperation battery,
            float now)
        {
            if (battery == null || now < battery.NextSuppressionScanAt) return;
            battery.NextSuppressionScanAt = now + 0.5f;
            Vector3 forward = battery.SuppressionRunwayForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.5f)
            {
                battery.SuppressionTargets.Clear();
                return;
            }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Unit[] targets = UnityEngine.Object.FindObjectsOfType<Unit>()
                .Where(unit => IsHostileRunwaySuppressionTarget(unit,
                    battery.Hq))
                .Where(unit => IsInsideRunwaySuppressionEnvelope(unit, battery,
                    forward, right))
                .OrderBy(unit => HorizontalSquareDistance(unit.transform.position,
                    battery.SuppressionRunwayCenter)).ToArray();
            battery.SuppressionTargets.Clear();
            battery.SuppressionTargets.AddRange(targets);
            var live = new HashSet<Unit>(targets);
            foreach (Unit stale in battery.SuppressionRetryAt.Keys
                .Where(unit => unit == null || unit.disabled
                    || !live.Contains(unit)).ToArray())
                battery.SuppressionRetryAt.Remove(stale);
        }

        private bool CombatSuppressionLaunchReady(BatteryOperation battery,
            TransportOperation transport, Unit target)
        {
            Aircraft aircraft = transport == null ? null : transport.Aircraft;
            WeaponStation station = transport == null
                ? null : transport.SuppressionStation;
            if (!IsHostileRunwaySuppressionTarget(target,
                    battery == null ? null : battery.Hq)
                || aircraft == null || aircraft.rb == null || station == null
                || station.WeaponInfo == null || station.Ammo <= 0
                || station.Reloading || station.SalvoInProgress
                || !station.Ready() || station.SafetyIsOn(aircraft))
                return false;
            GlobalPosition known;
            if (battery.Hq == null
                || !battery.Hq.TryGetKnownPosition(target, out known))
                return false;
            Vector3 delta = known
                - GlobalPositionExtensions.ToGlobalPosition(
                    aircraft.transform.position);
            float distance = delta.magnitude;
            TargetRequirements requirements = station.WeaponInfo.targetRequirements;
            float configuredRange = Mathf.Clamp(_combatSuppressionRange.Value,
                2000f, 15000f);
            float weaponRange = requirements.maxRange > 0f
                ? requirements.maxRange : configuredRange;
            if (distance < Mathf.Max(0f, requirements.minRange)
                || distance > Mathf.Min(configuredRange, weaponRange)
                || aircraft.rb.velocity.magnitude
                    < Mathf.Max(0f, requirements.minOwnerSpeed)
                || Vector3.Angle(aircraft.transform.forward, delta)
                    > Mathf.Max(1f, requirements.minAlignment))
                return false;
            if (requirements.lineOfSight
                && !target.LineOfSight(aircraft.transform.position, 1000f))
                return false;
            TrackingInfo tracking;
            if (!battery.Hq.trackingDatabase.TryGetValue(target.persistentID,
                    out tracking))
                return false;
            Vector3 runwayForward = battery.SuppressionRunwayForward;
            runwayForward.y = 0f;
            if (runwayForward.sqrMagnitude < 0.5f) return false;
            runwayForward.Normalize();
            Vector3 runwayRight = Vector3.Cross(Vector3.up,
                runwayForward).normalized;
            if (!IsInsideRunwaySuppressionEnvelope(target, battery,
                    runwayForward, runwayRight))
                return false;
            // Native combat analysis treats even a rolling aircraft as an air
            // target. AGM-68 suppression treats aircraft still on the runway
            // as surface obstructions while retaining native station, seeker,
            // range, LOS, tracking and launch checks.
            if (target is Aircraft) return true;
            try
            {
                return CombatAI.AnalyzeTarget(station, aircraft, tracking, 0f,
                    distance, maxRangeMultiplier: 100f).opportunity > 0f;
            }
            catch { return false; }
        }

        private static bool IsHostileRunwaySuppressionTarget(Unit unit,
            FactionHQ hq)
        {
            return unit != null && !unit.disabled && !(unit is Missile)
                && unit.rb != null && hq != null
                && hq.faction != null && unit.NetworkHQ != null
                && unit.NetworkHQ.faction != null
                && unit.NetworkHQ.faction != hq.faction;
        }

        private static bool IsInsideRunwaySuppressionEnvelope(Unit unit,
            BatteryOperation battery, Vector3 forward, Vector3 right)
        {
            if (unit == null || battery == null) return false;
            Vector3 delta = unit.transform.position
                - battery.SuppressionRunwayCenter;
            float height = delta.y;
            delta.y = 0f;
            if (!AirliftPolicy.RunwaySuppressionCorridorContains(
                    Vector3.Dot(delta, forward), Vector3.Dot(delta, right),
                    battery.SuppressionRunwayHalfLength,
                    battery.SuppressionRunwayHalfWidth))
                return false;
            return !(unit is Aircraft)
                || AirliftPolicy.RunwayAircraftSuppressionEligible(height,
                    CombatSuppressionAircraftMaxHeight);
        }

        private static void ClearSuppressionTarget(TransportOperation transport)
        {
            if (transport == null || transport.SuppressionTarget == null) return;
            transport.SuppressionTarget = null;
            if (transport.Pilot != null)
                transport.Pilot.SetPrimaryTarget(null);
            if (transport.Aircraft != null
                && transport.Aircraft.weaponManager != null)
                transport.Aircraft.weaponManager.ClearTargetList();
        }

        private void UpdateTransportJamming(BatteryOperation battery,
            TransportOperation transport, float now)
        {
            if (transport.JammingPods.Count == 0) return;
            if (now >= transport.NextJammerScanAt)
            {
                transport.NextJammerScanAt = now + 0.25f;
                Unit previous = transport.JammerTarget;
                Missile incoming = FindNearestNativeMissileWarning(battery,
                    transport.Aircraft.transform.position,
                    TransportJammerTargetRange, true);
                if (incoming != null)
                {
                    transport.JammerTarget = incoming;
                    transport.JammerTargetReason = "incoming radar missile";
                }
                else
                {
                    Aircraft hostile = null;
                    float nearestDistance = TransportJammerTargetRange
                        * TransportJammerTargetRange;
                    for (int index = 0; index < battery.ThreatAircraft.Count; index++)
                    {
                        Aircraft candidate = battery.ThreatAircraft[index];
                        if (candidate == null || candidate.disabled
                            || candidate == transport.Aircraft
                            || candidate.NetworkHQ == null || battery.Hq == null
                            || candidate.NetworkHQ == battery.Hq
                            || candidate.NetworkHQ.faction == battery.Hq.faction
                            || !candidate.HasRadarEmission()
                            || !IsTrackedForJamming(battery.Hq, candidate))
                            continue;
                        float distance = HorizontalSquareDistance(
                            candidate.transform.position,
                            transport.Aircraft.transform.position);
                        if (distance >= nearestDistance) continue;
                        hostile = candidate;
                        nearestDistance = distance;
                    }
                    transport.JammerTarget = hostile;
                    transport.JammerTargetReason = hostile == null ? null : "hostile radar aircraft";
                }
                if (previous != transport.JammerTarget)
                {
                    DebugLog(BatteryLabel(battery, transport) + " jammer target "
                        + (transport.JammerTarget == null ? "cleared" : "set to "
                            + transport.JammerTargetReason + " "
                            + transport.JammerTarget.persistentID) + ".");
                }
            }

            Unit target = transport.JammerTarget;
            if (target == null || target.disabled
                || HorizontalSquareDistance(target.transform.position,
                    transport.Aircraft.transform.position)
                    > TransportJammerTargetRange * TransportJammerTargetRange)
            {
                foreach (JammingPod pod in transport.JammingPods)
                    if (pod != null) pod.SetTarget(null);
                return;
            }

            Vector3 inheritedVelocity = transport.Aircraft.rb == null
                ? Vector3.zero : transport.Aircraft.rb.velocity;
            GlobalPosition aimpoint =
                GlobalPositionExtensions.ToGlobalPosition(target.transform.position);
            for (int index = 0; index < transport.JammingPods.Count; index++)
            {
                JammingPod pod = transport.JammingPods[index];
                if (pod == null) continue;
                WeaponStation station = index < transport.JammerStations.Count
                    ? transport.JammerStations[index] : null;
                if (station == null) continue;
                try
                {
                    pod.SetTarget(target);
                    pod.Fire(transport.Aircraft, target, inheritedVelocity, station, aimpoint);
                }
                catch (Exception exception)
                {
                    DebugLog(BatteryLabel(battery, transport) + " native JammingPod activation threw "
                        + exception.GetType().Name + ".");
                }
            }
        }

        private static bool IsTrackedForJamming(FactionHQ hq, Unit target)
        {
            if (hq == null || target == null) return false;
            try { return hq.IsTargetBeingTracked(target); }
            catch { return false; }
        }

        private static string DescribeTransportSystems(TransportOperation transport)
        {
            int pilotCount = transport.Aircraft.pilots == null
                ? -1
                : transport.Aircraft.pilots.Count(candidate => candidate != null);
            if (transport.Aircraft.weaponStations == null)
                return "pilots=" + pilotCount + "; weaponStations=<null>";

            var descriptions = new List<string>();
            for (int stationIndex = 0; stationIndex < transport.Aircraft.weaponStations.Count; stationIndex++)
            {
                WeaponStation station = transport.Aircraft.weaponStations[stationIndex];
                if (station == null) continue;
                string weaponInfo = station.WeaponInfo == null
                    ? "<null>"
                    : (station.WeaponInfo.weaponName ?? "<unnamed>")
                        + "/cargo=" + station.WeaponInfo.cargo
                        + "/jammer=" + station.WeaponInfo.jammer;
                var cargo = station.Weapons == null
                    ? new string[0]
                    : station.Weapons.OfType<MountedCargo>().Where(item => item != null).Select(item =>
                        (item.cargo == null ? "<null>" : item.cargo.jsonKey)
                        + "x" + Math.Max(0, item.GetAmmoTotal())).ToArray();
                int currentIndex = -1;
                try { currentIndex = (int)WeaponStationIndexField.GetValue(station); }
                catch { }
                descriptions.Add("#" + stationIndex + "[Cargo=" + station.Cargo + ",current="
                    + currentIndex + ",info=" + weaponInfo + ",mounted="
                    + (cargo.Length == 0 ? "<none>" : string.Join(",", cargo))
                    + ",jammingPods=" + (station.Weapons == null ? 0
                        : station.Weapons.OfType<JammingPod>().Count())
                    + ",mountedMissiles=" + (station.Weapons == null ? 0
                        : station.Weapons.OfType<MountedMissile>().Count()) + "]");
            }
            string expected = string.Join(",", transport.Cargo.Select(assignment =>
                assignment.Role + "=" + string.Join("+", assignment.ExpectedUnitKeys.ToArray())).ToArray());
            return "pilots=" + pilotCount + "; expected=" + expected + "; stations="
                + string.Join(" ", descriptions.ToArray());
        }

        private void ReleaseSequentially(BatteryOperation battery, TransportOperation transport, float now)
        {
            if (AirliftPolicy.BatteryReleaseComplete(transport.ReleaseCursor, transport.Cargo.Count))
            {
                if (transport.DeployedCargo.Count >= transport.ExpectedCargoCount)
                {
                    if (!transport.PackageDeploymentConfirmed)
                    {
                        transport.PackageDeploymentConfirmed = true;
                        DebugLog(BatteryLabel(battery, transport)
                            + " package confirmed; holding the straight drop lane at the synchronized egress barrier.");
                    }
                }
                else if (now - transport.NextReleaseAt > Mathf.Max(5f, _cargoSpawnTimeout.Value))
                {
                    string failure = transport.Callsign
                        + " native cargo spawn confirmation timed out";
                    if (!IsolateRuntimeFailureAfterCasualty(battery, transport, failure, now))
                        AbortOperation(failure, true);
                }
                return;
            }

            CargoAssignment assignment = transport.Cargo[transport.ReleaseCursor];
            if (assignment.ReleaseCommands >= assignment.ExpectedCargoCount)
            {
                transport.ReleaseCursor++;
                return;
            }

            if (!transport.ImmediateRelease && !ReleaseWindowSatisfied(transport))
            {
                if (PassedDropZone(transport,
                    AirliftPolicy.EffectiveLeaderReleaseRadius(_releaseRadius.Value) + 250f))
                {
                    transport.ImmediateRelease = true;
                    Announce(BatteryLabel(battery, transport)
                        + ": release envelope lost; completing cargo release immediately.");
                }
                else
                {
                    return;
                }
            }
            if (!AirliftPolicy.ReleaseDue(assignment.ReleaseCommands, assignment.ExpectedCargoCount,
                    now, transport.NextReleaseAt)
                || assignment.Station == null || !assignment.Station.Ready())
                return;

            assignment.ReleaseCommands++;
            try
            {
                assignment.Station.LaunchMount(transport.Aircraft, null, assignment.NetworkTarget);
            }
            catch (Exception exception)
            {
                assignment.ReleaseCommands--;
                string failure = transport.Callsign + " native " + assignment.Role
                    + " release threw " + exception.GetType().Name;
                if (!IsolateRuntimeFailureAfterCasualty(battery, transport, failure, now))
                    AbortOperation(failure, true);
                return;
            }
            transport.NextReleaseAt = now + Mathf.Max(0.1f, _releaseInterval.Value);
            if (assignment.ReleaseCommands >= assignment.ExpectedCargoCount)
                transport.ReleaseCursor++;
            DebugLog(BatteryLabel(battery, transport) + " sent native " + assignment.Role
                + " release " + assignment.ReleaseCommands + "/" + assignment.ExpectedCargoCount + ".");
        }

        private void UpdateCoordinatedEgressStart(BatteryOperation battery, float now)
        {
            if (battery == null || battery.CoordinatedEgressStarted) return;
            List<TransportOperation> survivors = battery.Transports.Where(transport =>
                transport != null && !transport.IsTerminal
                && transport.Aircraft != null && !transport.Aircraft.disabled).ToList();
            if (survivors.Count == 0
                || survivors.Any(transport => !transport.PackageDeploymentConfirmed))
                return;

            battery.CoordinatedEgressStarted = true;
            for (int index = 0; index < survivors.Count; index++)
            {
                TransportOperation transport = survivors[index];
                transport.GearDownForDrop = false;
                transport.Aircraft.SetGear(false);
                if (battery.Kind == AirliftOperationKind.CombatRunwayDrop)
                    transport.TargetRadarAltitude = Mathf.Min(400f,
                        battery.CruiseRadarAltitude);
                transport.FlightDestination = transport.EgressPoint;
                transport.DespawnAt = now + Mathf.Max(0f, _postDropDelay.Value);
                transport.EgressReady = false;
                transport.SetPhase(TransportPhase.Egress, now);
            }
            Announce("AIRLIFT-" + battery.Id + ": drop successful at " + battery.ZoneName
                + " - " + battery.DeployedCargo.Count.ToString(CultureInfo.InvariantCulture)
                + " cargo unit(s) deployed and all surviving Chimeras are clear.",
                AirliftAnnouncementKind.DropSucceeded);
        }

        private void UpdateCoordinatedEgressBreak(BatteryOperation battery, float now)
        {
            if (battery == null || battery.CoordinatedEgressReleased) return;
            List<TransportOperation> survivors = battery.Transports.Where(transport =>
                transport != null && !transport.IsTerminal
                && transport.Aircraft != null && !transport.Aircraft.disabled).ToList();
            if (survivors.Count == 0) return;

            // No aircraft may turn toward its own airport until every surviving
            // transport has completed the package and cleared the common bank.
            if (survivors.Any(transport => transport.Phase != TransportPhase.Egress
                    || !transport.EgressReady))
                return;

            battery.CoordinatedEgressReleased = true;
            Announce("AIRLIFT-" + battery.Id
                + ": formation clear of the synchronized egress bank; recovery pairs splitting now.");
            for (int index = 0; index < survivors.Count; index++)
            {
                if (_operation != battery) return;
                BeginPostDrop(battery, survivors[index], now);
            }
        }

        private void BeginPostDrop(BatteryOperation battery, TransportOperation transport, float now)
        {
            if (!AirliftPolicy.RecoveryAllowed(transport.ReleaseCursor,
                    transport.Cargo.Count, transport.DeployedCargo.Count,
                    transport.ExpectedCargoCount))
            {
                transport.SetPhase(TransportPhase.Releasing, now);
                Logger.LogWarning(BatteryLabel(battery, transport)
                    + " recovery request rejected until every assigned cargo unit is released and confirmed.");
                return;
            }
            switch (transport.PostDropBehavior)
            {
                case PostDropBehavior.Return:
                    transport.FlightDestination = transport.ReturnPoint;
                    transport.TargetRadarAltitude = battery.CruiseRadarAltitude;
                    transport.SetPhase(TransportPhase.Returning, now);
                    DebugLog(BatteryLabel(battery, transport)
                        + " returning through its original off-map entry corridor; deployed cargo retained.");
                    break;
                case PostDropBehavior.Land:
                    if (transport.Pilot == null || transport.Pilot.AILandingState == null)
                    {
                        string failure = transport.Callsign
                            + " native fixed-wing landing state is unavailable";
                        if (battery.HasTransportLoss)
                        {
                            Logger.LogWarning(BatteryLabel(battery, transport) + " " + failure
                                + "; returning to the friendly recovery point without deleting survivors.");
                            transport.FlightDestination = transport.ReturnPoint;
                            transport.SetPhase(TransportPhase.Returning, now);
                        }
                        else
                        {
                            AbortOperation(failure, true);
                        }
                        return;
                    }
                    transport.RecoverySequence = battery.Transports.IndexOf(transport);
                    Airbase recoveryAirbase;
                    string recoveryError;
                    if (!TryFindPairRecoveryAirbase(transport, battery,
                            out recoveryAirbase, out recoveryError))
                    {
                        Logger.LogWarning(BatteryLabel(battery, transport)
                            + " recovery unavailable; releasing without forced destruction: "
                            + recoveryError + ".");
                        FinishTransport(battery, transport,
                            "no suitable friendly recovery airbase");
                        return;
                    }
                    transport.RecoveryAirbase = recoveryAirbase;
                    transport.RecoveryAirbaseName = AirbaseName(recoveryAirbase);
                    transport.RecoveryHoldAngle = transport.RecoverySequence * 90f;
                    transport.NextRecoveryUpdateAt = now;
                    transport.NextLandingRetryAt = now;
                    transport.SetPhase(TransportPhase.RecoveryHolding, now);
                    UpdateRecoveryHoldingDestination(transport, now);
                    Announce(BatteryLabel(battery, transport) + ": package clear; recovery pair "
                        + (AirliftPolicy.RecoveryPairIndex(transport.RecoverySequence) + 1)
                            .ToString(CultureInfo.InvariantCulture)
                        + " diverting to " + transport.RecoveryAirbaseName + ".");
                    break;
                default:
                    Logger.LogInfo(BatteryLabel(battery, transport)
                        + " legacy Despawn behavior mapped to safe off-map extraction.");
                    transport.PostDropBehavior = PostDropBehavior.Return;
                    BeginPostDrop(battery, transport, now);
                    break;
            }
        }

        private void UpdateRecoveryHoldingDestination(TransportOperation transport, float now)
        {
            if (transport == null || transport.RecoveryAirbase == null
                || transport.RecoveryAirbase.disabled || now < transport.NextRecoveryUpdateAt)
                return;
            transport.NextRecoveryUpdateAt = now + 1f;
            Vector3 center = transport.RecoveryAirbase.center != null
                ? transport.RecoveryAirbase.center.position
                : transport.RecoveryAirbase.transform.position;
            // The first aircraft in each pair flies directly to the field. Its mate uses
            // a fixed nearby staging point until the runway is clear; no slow orbit.
            float side = (transport.RecoverySequence % 2 == 0) ? 0f
                : Mathf.Clamp(_recoveryHoldRadius.Value * 0.5f, 1000f, 2200f);
            transport.FlightDestination = center + new Vector3(side, 0f, 0f);
            transport.TargetRadarAltitude = Mathf.Clamp(_recoveryHoldAltitude.Value, 700f, 1800f)
                + ((transport.RecoverySequence % 2) * 150f);
        }

        private void UpdateRecoveryLandingQueue(BatteryOperation battery, float now)
        {
            if (battery == null) return;
            var occupiedFields = new HashSet<Airbase>(battery.Transports.Where(item => item != null
                    && !item.IsTerminal && item.Phase == TransportPhase.Landing
                    && item.RecoveryAirbase != null)
                .Select(item => item.RecoveryAirbase));
            List<TransportOperation> candidates = battery.Transports.Where(item => item != null
                    && !item.IsTerminal && item.Phase == TransportPhase.RecoveryHolding
                    && item.Aircraft != null && !item.Aircraft.disabled
                    && now >= item.NextLandingRetryAt)
                .OrderBy(item => item.RecoverySequence).ToList();
            foreach (TransportOperation candidate in candidates)
            {
                if (candidate.RecoveryAirbase == null || candidate.RecoveryAirbase.disabled
                    || candidate.RecoveryAirbase.CurrentHQ != battery.Hq)
                {
                    Airbase diversion;
                    string diversionError;
                    if (!TryFindPairRecoveryAirbase(candidate, battery,
                            out diversion, out diversionError))
                        continue;
                    candidate.RecoveryAirbase = diversion;
                    candidate.RecoveryAirbaseName = AirbaseName(diversion);
                    candidate.NextRecoveryUpdateAt = now;
                    UpdateRecoveryHoldingDestination(candidate, now);
                    Announce(BatteryLabel(battery, candidate) + ": diverting recovery pair to "
                        + candidate.RecoveryAirbaseName + ".");
                }
                if (occupiedFields.Contains(candidate.RecoveryAirbase)) continue;

                Vector3 center = candidate.RecoveryAirbase.center != null
                    ? candidate.RecoveryAirbase.center.position
                    : candidate.RecoveryAirbase.transform.position;
                candidate.FlightDestination = center;
                if (HorizontalDistance(candidate.Aircraft.transform.position, center) > 7000f)
                    continue;

                Airbase nativeChoice;
                string error;
                if (!TryFindNearestRecoveryAirbase(candidate.Aircraft, battery.Hq,
                        out nativeChoice, out error) || nativeChoice != candidate.RecoveryAirbase)
                    continue;

                candidate.Pilot.SwitchState(candidate.Pilot.AILandingState);
                TryAccelerateNativeLanding(candidate.Pilot,
                    BatteryLabel(battery, candidate));
                candidate.NextLandingRetryAt = now + 5f;
                candidate.SetPhase(TransportPhase.Landing, now);
                occupiedFields.Add(candidate.RecoveryAirbase);
                Announce(BatteryLabel(battery, candidate) + ": cleared for immediate native landing at "
                    + candidate.RecoveryAirbaseName + ".");
            }
        }

        private void TryAccelerateNativeLanding(Pilot pilot, string label)
        {
            if (pilot == null || pilot.AILandingState == null
                || pilot.currentState != pilot.AILandingState)
                return;
            try
            {
                // EnterState has already selected and registered a native runway. Moving
                // from JoinPattern (0) to FinalTurn (1) removes the very large three-
                // turning-radius orbit while preserving final alignment and glideslope.
                object finalTurn = Enum.ToObject(LandingModeType, 1);
                LandingSwitchModeMethod.Invoke(pilot.AILandingState,
                    new[] { finalTurn });
                DebugLog(label
                    + " accelerated native landing from join-pattern to final-turn.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning(label + " could not accelerate the native final turn: "
                    + exception.GetType().Name + "; normal landing remains active.");
            }
        }

        private bool TryFindPairRecoveryAirbase(TransportOperation transport,
            BatteryOperation battery, out Airbase airbase, out string error)
        {
            airbase = null;
            error = null;
            if (transport == null || transport.Aircraft == null || transport.Aircraft.disabled
                || battery == null || battery.Hq == null
                || transport.Aircraft.NetworkHQ != battery.Hq)
            {
                error = "aircraft or faction is unavailable";
                return false;
            }
            int pair = AirliftPolicy.RecoveryPairIndex(transport.RecoverySequence);
            TransportOperation pairMate = battery.Transports.FirstOrDefault(item => item != null
                && item != transport && item.RecoveryAirbase != null
                && AirliftPolicy.RecoveryPairIndex(item.RecoverySequence) == pair);
            if (pairMate != null && !pairMate.RecoveryAirbase.disabled
                && pairMate.RecoveryAirbase.CurrentHQ == battery.Hq)
            {
                airbase = pairMate.RecoveryAirbase;
                return true;
            }

            RunwayQuery query;
            if (!TryBuildLandingQuery(transport.Aircraft, out query, out error)) return false;
            List<Airbase> suitable = battery.Hq.GetAirbases().Where(item => item != null
                    && !item.disabled && item.CurrentHQ == battery.Hq && item.IsSuitable(query))
                .OrderBy(item => HorizontalDistance(transport.Aircraft.transform.position,
                    item.center != null ? item.center.position : item.transform.position)).ToList();
            if (suitable.Count == 0)
            {
                error = "no suitable friendly landing runway is available";
                return false;
            }
            if (pair == 0)
            {
                string preferred = (_preferredRecoveryAirbase.Value ?? string.Empty).Trim();
                airbase = suitable.FirstOrDefault(item => preferred.Length > 0
                    && AirbaseName(item).IndexOf(preferred,
                        StringComparison.OrdinalIgnoreCase) >= 0) ?? suitable[0];
                return true;
            }

            var earlierPairFields = new HashSet<Airbase>(battery.Transports.Where(item => item != null
                    && item.RecoveryAirbase != null
                    && AirliftPolicy.RecoveryPairIndex(item.RecoverySequence) < pair)
                .Select(item => item.RecoveryAirbase));
            string firstPairPreference = (_preferredRecoveryAirbase.Value ?? string.Empty).Trim();
            Airbase preferredFirstPairField = suitable.FirstOrDefault(item =>
                firstPairPreference.Length > 0 && AirbaseName(item).IndexOf(firstPairPreference,
                    StringComparison.OrdinalIgnoreCase) >= 0);
            if (preferredFirstPairField != null)
                earlierPairFields.Add(preferredFirstPairField);
            airbase = suitable.FirstOrDefault(item => !earlierPairFields.Contains(item)) ?? suitable[0];
            return true;
        }

        private bool TryFindPreferredRecoveryAirbase(Aircraft aircraft, FactionHQ hq,
            out Airbase airbase, out string error)
        {
            airbase = null;
            error = null;
            if (aircraft == null || aircraft.disabled || hq == null || aircraft.NetworkHQ != hq)
            {
                error = "aircraft or faction is unavailable";
                return false;
            }
            RunwayQuery query;
            if (!TryBuildLandingQuery(aircraft, out query, out error)) return false;
            string preferred = (_preferredRecoveryAirbase.Value ?? string.Empty).Trim();
            if (preferred.Length > 0)
            {
                airbase = hq.GetAirbases().Where(item => item != null && !item.disabled
                        && item.CurrentHQ == hq && item.IsSuitable(query)
                        && AirbaseName(item).IndexOf(preferred,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(item => HorizontalDistance(aircraft.transform.position,
                        item.center != null ? item.center.position : item.transform.position))
                    .FirstOrDefault();
            }
            if (airbase != null) return true;
            airbase = hq.GetNearestAirbase(aircraft.transform.position, query);
            if (airbase == null || airbase.disabled || airbase.CurrentHQ != hq)
            {
                airbase = null;
                error = "no suitable friendly landing runway is available";
                return false;
            }
            return true;
        }

        private static bool TryBuildLandingQuery(Aircraft aircraft, out RunwayQuery query,
            out string error)
        {
            query = default(RunwayQuery);
            error = null;
            AircraftParameters parameters = aircraft == null ? null : aircraft.GetAircraftParameters();
            if (parameters == null || aircraft.definition == null)
            {
                error = "aircraft landing parameters are unavailable";
                return false;
            }
            query = new RunwayQuery {
                RunwayType = RunwayQueryType.Landing,
                MinSize = parameters.verticalLanding
                    ? aircraft.definition.length : parameters.takeoffDistance,
                LandingSpeed = parameters.verticalLanding ? 0f : parameters.landingSpeed,
                TailHook = aircraft.weaponManager != null && aircraft.weaponManager.HasTailHook()
            };
            return true;
        }

        private static bool TryFindNearestRecoveryAirbase(Aircraft aircraft, FactionHQ hq,
            out Airbase airbase, out string error)
        {
            airbase = null;
            error = null;
            if (aircraft == null || aircraft.disabled)
            {
                error = "aircraft is unavailable";
                return false;
            }
            if (hq == null || aircraft.NetworkHQ != hq)
            {
                error = "aircraft does not belong to the operation faction";
                return false;
            }

            RunwayQuery query;
            if (!TryBuildLandingQuery(aircraft, out query, out error)) return false;
            airbase = hq.GetNearestAirbase(aircraft.transform.position, query);
            if (airbase == null || airbase.disabled || airbase.CurrentHQ != hq)
            {
                airbase = null;
                error = "no suitable friendly landing runway is available";
                return false;
            }
            return true;
        }

        private static string AirbaseName(Airbase airbase)
        {
            if (airbase == null) return "<unknown airbase>";
            if (airbase.SavedAirbase != null
                && !string.IsNullOrWhiteSpace(airbase.SavedAirbase.DisplayName))
                return airbase.SavedAirbase.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(airbase.NetworknetworkUniqueName))
                return airbase.NetworknetworkUniqueName.Trim();
            return string.IsNullOrWhiteSpace(airbase.name) ? "<unnamed airbase>" : airbase.name.Trim();
        }

        private static bool TryResolveExactUnitDefinition(string key, out UnitDefinition definition,
            out string source, out string error)
        {
            definition = null;
            source = null;
            error = null;
            var matches = new List<UnitDefinition>();
            UnitDefinition indexed = null;
            if (Encyclopedia.Lookup != null && Encyclopedia.Lookup.TryGetValue(key, out indexed)
                && indexed != null && string.Equals(indexed.jsonKey, key, StringComparison.Ordinal))
                AddUniqueReference(matches, indexed);

            foreach (UnitDefinition candidate in Resources.FindObjectsOfTypeAll<UnitDefinition>())
            {
                if (candidate != null && string.Equals(candidate.jsonKey, key, StringComparison.Ordinal))
                    AddUniqueReference(matches, candidate);
            }
            if (matches.Count == 0)
            {
                string nearby = string.Join(", ", Resources.FindObjectsOfTypeAll<AircraftDefinition>()
                    .Where(candidate => candidate != null && !string.IsNullOrEmpty(candidate.jsonKey)
                        && (candidate.jsonKey.IndexOf("MC260", StringComparison.OrdinalIgnoreCase) >= 0
                            || candidate.jsonKey.IndexOf("Chimera", StringComparison.OrdinalIgnoreCase) >= 0))
                    .Select(candidate => candidate.jsonKey).Distinct().Take(6).ToArray());
                error = "exact aircraft definition '" + key
                    + "' is absent from Encyclopedia and loaded resources"
                    + (nearby.Length == 0 ? string.Empty : "; nearby loaded keys: " + nearby);
                return false;
            }
            if (matches.Count != 1)
            {
                error = "exact aircraft definition '" + key + "' resolved " + matches.Count
                    + " distinct loaded objects; refusing ambiguous content";
                return false;
            }
            definition = matches[0];
            source = ReferenceEquals(indexed, definition) ? "Encyclopedia.Lookup"
                : "exact loaded-resource fallback";
            return true;
        }

        private static bool TryResolveExactWeaponMount(string key, out WeaponMount mount,
            out string source, out string error)
        {
            mount = null;
            source = null;
            error = null;
            var matches = new List<WeaponMount>();
            WeaponMount indexed = null;
            if (Encyclopedia.WeaponLookup != null && Encyclopedia.WeaponLookup.TryGetValue(key, out indexed)
                && indexed != null && string.Equals(indexed.jsonKey, key, StringComparison.Ordinal))
                AddUniqueReference(matches, indexed);

            foreach (WeaponMount candidate in Resources.FindObjectsOfTypeAll<WeaponMount>())
            {
                if (candidate != null && string.Equals(candidate.jsonKey, key, StringComparison.Ordinal))
                    AddUniqueReference(matches, candidate);
            }
            if (matches.Count == 0)
            {
                error = "exact weapon mount '" + key
                    + "' is absent from Encyclopedia and loaded resources";
                return false;
            }
            if (matches.Count != 1)
            {
                error = "exact weapon mount '" + key + "' resolved " + matches.Count
                    + " distinct loaded objects; refusing ambiguous content";
                return false;
            }
            mount = matches[0];
            source = ReferenceEquals(indexed, mount) ? "Encyclopedia.WeaponLookup"
                : "exact loaded-resource fallback";
            return true;
        }

        private static void AddUniqueReference<T>(List<T> values, T candidate) where T : class
        {
            if (!values.Any(existing => ReferenceEquals(existing, candidate))) values.Add(candidate);
        }

        private bool TryActivateVerifiedLegacyChimeraContent(out string report)
        {
            try
            {
                return TryActivateVerifiedLegacyChimeraContentCore(out report);
            }
            catch (Exception exception)
            {
                Exception cause = exception is TargetInvocationException
                    && exception.InnerException != null
                    ? exception.InnerException
                    : exception;
                report = "compatible Chimera activation failed safely: "
                    + cause.GetType().Name + ": " + cause.Message;
                Logger.LogError("AIRLIFT " + report);
                return false;
            }
        }

        private bool TryActivateVerifiedLegacyChimeraContentCore(out string report)
        {
            report = _chimeraActivationReport;
            if (_chimeraContentActivated) return true;

            Type blueprinterType = AccessTools.TypeByName("Blueprinter.Plugin");
            Type incrementalType = AccessTools.TypeByName("Blueprinter.Ops.EncyclopediaIncremental");
            if (blueprinterType == null || incrementalType == null)
            {
                report = "Blueprinter 1.8.21 legacy content services are not loaded";
                return false;
            }

            PropertyInfo instanceProperty = AccessTools.Property(blueprinterType, "Instance");
            PropertyInfo completeProperty = AccessTools.Property(blueprinterType, "PatchingComplete");
            object blueprinter = instanceProperty == null
                ? null
                : instanceProperty.GetValue(null, null);
            if (blueprinter == null)
            {
                report = "Blueprinter instance is unavailable";
                return false;
            }
            if (completeProperty == null || !(bool)completeProperty.GetValue(blueprinter, null))
            {
                report = "Blueprinter has not completed Chimera patching yet";
                return false;
            }

            Assembly[] chimeraAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly != null
                    && string.Equals(assembly.GetName().Name, ChimeraAssemblyName,
                        StringComparison.Ordinal))
                .ToArray();
            if (chimeraAssemblies.Length != 1)
            {
                report = "expected exactly one loaded " + ChimeraAssemblyName
                    + " assembly, found " + chimeraAssemblies.Length;
                return false;
            }
            string chimeraAssemblyPath = chimeraAssemblies[0].Location;
            if (string.IsNullOrWhiteSpace(chimeraAssemblyPath)
                || !File.Exists(chimeraAssemblyPath))
            {
                report = "loaded Chimera assembly has no verifiable file path";
                return false;
            }
            FieldInfo registryField = AccessTools.Field(blueprinterType, "bundleRegistry");
            FieldInfo encyclopediaField = AccessTools.Field(blueprinterType, "_encyclopedia");
            object registry = registryField == null ? null : registryField.GetValue(blueprinter);
            Encyclopedia encyclopedia = encyclopediaField == null
                ? null
                : encyclopediaField.GetValue(blueprinter) as Encyclopedia;
            PropertyInfo bundlesProperty = registry == null
                ? null
                : AccessTools.Property(registry.GetType(), "BundlesByName");
            IEnumerable bundles = bundlesProperty == null
                ? null
                : bundlesProperty.GetValue(registry, null) as IEnumerable;
            if (bundles == null || encyclopedia == null)
            {
                report = "Blueprinter bundle registry or Encyclopedia is unavailable";
                return false;
            }

            var matchingBundles = new List<object>();
            var matchingVersions = new List<string>();
            foreach (object pair in bundles)
            {
                if (pair == null) continue;
                PropertyInfo valueProperty = AccessTools.Property(pair.GetType(), "Value");
                object loadedBundle = valueProperty == null ? null : valueProperty.GetValue(pair, null);
                if (loadedBundle == null) continue;
                FieldInfo manifestField = AccessTools.Field(loadedBundle.GetType(), "Manifest");
                object manifest = manifestField == null ? null : manifestField.GetValue(loadedBundle);
                if (manifest == null) continue;
                FieldInfo modNameField = AccessTools.Field(manifest.GetType(), "modName");
                FieldInfo modVersionField = AccessTools.Field(manifest.GetType(), "modVersion");
                string modName = modNameField == null ? null : modNameField.GetValue(manifest) as string;
                string modVersion = modVersionField == null ? null : modVersionField.GetValue(manifest) as string;
                if (string.Equals(modName, ChimeraManifestName, StringComparison.Ordinal))
                {
                    matchingBundles.Add(loadedBundle);
                    matchingVersions.Add(string.IsNullOrWhiteSpace(modVersion)
                        ? "<unspecified>" : modVersion);
                }
            }
            if (matchingBundles.Count != 1)
            {
                report = "expected exactly one loaded '" + ChimeraManifestName
                    + "' bundle of any compatible version, found " + matchingBundles.Count;
                return false;
            }

            object bundleRecord = matchingBundles[0];
            FieldInfo filePathField = AccessTools.Field(bundleRecord.GetType(), "filePath");
            string filePath = filePathField == null ? null : filePathField.GetValue(bundleRecord) as string;

            string[] requiredMountKeys = {
                RadarMountDefault,
                R9MountDefault,
                SlmmrMountDefault
            };
            UnitDefinition publishedAircraft;
            string publishedAircraftSource;
            string publishedAircraftError;
            bool alreadyPublished = TryResolveExactUnitDefinition(ChimeraKey,
                    out publishedAircraft, out publishedAircraftSource,
                    out publishedAircraftError)
                && publishedAircraft is AircraftDefinition;
            var publishedMountSources = new List<string>();
            for (int index = 0; index < requiredMountKeys.Length && alreadyPublished; index++)
            {
                WeaponMount publishedMount;
                string publishedMountSource;
                string publishedMountError;
                alreadyPublished = TryResolveExactWeaponMount(requiredMountKeys[index],
                    out publishedMount, out publishedMountSource, out publishedMountError);
                if (alreadyPublished) publishedMountSources.Add(publishedMountSource);
            }
            if (alreadyPublished)
            {
                _chimeraContentActivated = true;
                _chimeraActivationReport = "using already-published compatible Chimera "
                    + matchingVersions[0] + " definitions without rescanning its AssetBundle";
                report = _chimeraActivationReport;
                Logger.LogInfo("AIRLIFT " + report + ".");
                return true;
            }

            FieldInfo assetBundleField = AccessTools.Field(bundleRecord.GetType(), "AssetBundle");
            AssetBundle assetBundle = assetBundleField == null
                ? null
                : assetBundleField.GetValue(bundleRecord) as AssetBundle;
            if (assetBundle == null)
            {
                report = "compatible Chimera bundle has no live AssetBundle";
                return false;
            }

            UnityEngine.Object[] unitAssets = assetBundle.LoadAllAssets(typeof(UnitDefinition))
                ?? new UnityEngine.Object[0];
            UnityEngine.Object[] mountAssets = assetBundle.LoadAllAssets(typeof(WeaponMount))
                ?? new UnityEngine.Object[0];
            AircraftDefinition[] aircraftMatches = unitAssets.OfType<AircraftDefinition>()
                .Where(item => item != null
                    && string.Equals(item.name, ChimeraDefinitionAssetName, StringComparison.Ordinal)
                    && string.Equals(item.jsonKey, ChimeraKey, StringComparison.Ordinal))
                .ToArray();
            if (aircraftMatches.Length != 1)
            {
                report = "verified bundle resolved " + aircraftMatches.Length
                    + " exact " + ChimeraDefinitionAssetName + "/" + ChimeraKey
                    + " aircraft definitions";
                return false;
            }

            foreach (string requiredMountKey in requiredMountKeys)
            {
                int matches = mountAssets.OfType<WeaponMount>().Count(item => item != null
                    && string.Equals(item.jsonKey, requiredMountKey, StringComparison.Ordinal));
                if (matches != 1)
                {
                    report = "verified bundle resolved " + matches + " exact '"
                        + requiredMountKey + "' WeaponMount assets";
                    return false;
                }
            }

            MethodInfo tryAdd = AccessTools.Method(incrementalType, "TryAdd",
                new[] { typeof(Encyclopedia), typeof(UnityEngine.Object), typeof(ICollection<string>) });
            if (tryAdd == null)
            {
                report = "Blueprinter EncyclopediaIncremental.TryAdd signature is unavailable";
                return false;
            }

            UnityEngine.Object[] eligible = unitAssets.Concat(mountAssets)
                .Where(item => item != null && IsAutomaticEncyclopediaAsset(item))
                .OrderBy(NetworkDefinitionKey, StringComparer.Ordinal)
                .ThenBy(item => item.GetType().FullName, StringComparer.Ordinal)
                .ToArray();
            var added = new List<string>();
            foreach (UnityEngine.Object asset in eligible)
                tryAdd.Invoke(null, new object[] { encyclopedia, asset, added });

            UnitDefinition aircraft;
            string aircraftSource;
            string aircraftError;
            if (!TryResolveExactUnitDefinition(ChimeraKey, out aircraft,
                    out aircraftSource, out aircraftError)
                || !(aircraft is AircraftDefinition))
            {
                report = "legacy registration did not publish the exact Chimera: " + aircraftError;
                return false;
            }
            foreach (string requiredMountKey in requiredMountKeys)
            {
                WeaponMount mount;
                string mountSource;
                string mountError;
                if (!TryResolveExactWeaponMount(requiredMountKey, out mount,
                        out mountSource, out mountError))
                {
                    report = "legacy registration did not publish '" + requiredMountKey
                        + "': " + mountError;
                    return false;
                }
            }

            _chimeraContentActivated = true;
            _chimeraActivationReport = "activated compatible Chimera " + matchingVersions[0]
                + " schema-3 content from "
                + (string.IsNullOrWhiteSpace(filePath) ? "embedded bundle" : filePath)
                + ": "
                + added.Count + " native Encyclopedia/network entries added from "
                + eligible.Length + " eligible definitions";
            report = _chimeraActivationReport;
            Logger.LogInfo("AIRLIFT " + report + ".");
            return true;
        }

        private static bool IsAutomaticEncyclopediaAsset(UnityEngine.Object asset)
        {
            FieldInfo optOut = asset == null
                ? null
                : AccessTools.Field(asset.GetType(), "dontAutomaticallyAddToEncyclopedia");
            return optOut == null || !(bool)optOut.GetValue(asset);
        }

        private static string NetworkDefinitionKey(UnityEngine.Object asset)
        {
            UnitDefinition unit = asset as UnitDefinition;
            if (unit != null) return unit.jsonKey ?? string.Empty;
            WeaponMount mount = asset as WeaponMount;
            if (mount != null) return mount.jsonKey ?? string.Empty;
            return asset == null ? string.Empty : asset.name ?? string.Empty;
        }

        private bool TryResolveBatteryManifest(FactionHQ hq, Dictionary<CargoRole, Vector3> targets,
            out AircraftDefinition definition, out List<TransportOperation> transports, out string report)
        {
            definition = null;
            transports = new List<TransportOperation>();
            report = null;

            string activationReport;
            if (!TryActivateVerifiedLegacyChimeraContent(out activationReport))
            {
                report = activationReport;
                return false;
            }

            string aircraftKey = AirliftPolicy.NormalizeChimeraDefinitionKey(_aircraftKey.Value);
            UnitDefinition rawDefinition;
            string definitionSource;
            string definitionError;
            if (!TryResolveExactUnitDefinition(aircraftKey, out rawDefinition,
                    out definitionSource, out definitionError))
            {
                report = definitionError;
                return false;
            }
            definition = rawDefinition as AircraftDefinition;
            if (definition == null || definition.unitPrefab == null
                || definition.unitPrefab.GetComponent<Aircraft>() == null)
            {
                report = "'" + aircraftKey + "' is not a valid AircraftDefinition prefab";
                return false;
            }
            if (!string.Equals(aircraftKey, ChimeraKey, StringComparison.Ordinal))
            {
                report = "battery manifest only permits exact Chimera definition '" + ChimeraKey + "'";
                return false;
            }
            if (!definition.IsAllowed(MissionManager.AllowEventContent))
            {
                report = "Chimera definition is disabled for this mission";
                return false;
            }

            transports.Add(NewTransport("ALPHA",
                NewAssignment(CargoRole.MunitionsRear, _munitionsMountKey.Value, RearCargoBay, targets),
                NewAssignment(CargoRole.SlmmrAlpha, _slmmrMountKey.Value, FrontCargoBay, targets)));
            transports.Add(NewTransport("BRAVO",
                NewAssignment(CargoRole.Radar, _radarMountKey.Value, RearCargoBay, targets),
                NewAssignment(CargoRole.R9Alpha, _r9MountKey.Value, MissionBay, targets)));
            transports.Add(NewTransport("CHARLIE",
                NewAssignment(CargoRole.R9Bravo, _r9MountKey.Value, MissionBay, targets),
                NewAssignment(CargoRole.MunitionsFront, _munitionsMountKey.Value, FrontCargoBay, targets)));
            transports.Add(NewTransport("DELTA",
                NewAssignment(CargoRole.SlmmrCharlie, _slmmrMountKey.Value, RearCargoBay, targets),
                NewAssignment(CargoRole.R9Charlie, _r9MountKey.Value, MissionBay, targets)));

            Aircraft prefabAircraft = definition.unitPrefab.GetComponent<Aircraft>();
            WeaponManager manager = prefabAircraft.weaponManager;
            if (manager == null || manager.hardpointSets == null)
            {
                report = "Chimera prefab has no hardpoint catalogue";
                return false;
            }

            WeaponMount jammerMount;
            string jammerReport;
            if (!TryResolveTransportJammerMount(manager,
                    (_transportJammerMountKey.Value ?? string.Empty).Trim(),
                    out jammerMount, out jammerReport))
            {
                report = "Chimera defensive ECM: " + jammerReport;
                return false;
            }

            foreach (TransportOperation transport in transports)
            {
                transport.JammerMount = jammerMount;
                var usedHardpoints = new HashSet<int>();
                foreach (CargoAssignment assignment in transport.Cargo)
                {
                    string assignmentReport;
                    int hardpointIndex;
                    if (!TryResolveAssignment(manager, assignment, out hardpointIndex, out assignmentReport))
                    {
                        report = transport.Callsign + "/" + assignment.Role + ": " + assignmentReport;
                        return false;
                    }
                    if (!usedHardpoints.Add(hardpointIndex))
                    {
                        report = transport.Callsign + " assigns more than one package to hardpoint set "
                            + assignment.HardpointSetName;
                        return false;
                    }
                }

                Loadout loadout = BuildLoadout(definition, transport.Cargo,
                    transport.JammerMount);
                if (!loadout.AllowedByHQ(manager, hq))
                {
                    report = transport.Callsign + " loadout is restricted for the owner's faction";
                    return false;
                }
            }

            report = "Resolved four exact Chimera loadouts, eight cargo mounts, and fourteen deployed units: "
                + "ALPHA rear pallet-x4+SLMMR-A3; BRAVO radar+R9; "
                + "CHARLIE R9+front pallet-x4; DELTA SLMMR-A3+R9; "
                + "all four with native Radar Jamming Pods"
                + " (definition source: " + definitionSource + "; " + activationReport + ").";
            return true;
        }

        private static bool TryResolveTransportJammerMount(WeaponManager manager,
            string mountKey, out WeaponMount mount, out string report)
        {
            mount = null;
            report = null;
            WeaponMount candidate;
            string mountSource;
            string mountError;
            if (!TryResolveExactWeaponMount(mountKey, out candidate, out mountSource, out mountError))
            {
                report = mountError;
                return false;
            }
            if (!string.Equals(mountKey, TransportJammerMountDefault, StringComparison.Ordinal))
            {
                report = "transport ECM only permits exact Radar Jamming Pod mount '"
                    + TransportJammerMountDefault + "'";
                return false;
            }
            if (candidate.info == null || candidate.prefab == null
                || !candidate.info.jammer
                || candidate.prefab.GetComponentsInChildren<JammingPod>(true).Length == 0
                || !candidate.IsAllowed(MissionManager.AllowEventContent))
            {
                report = "mount '" + mountKey
                    + "' is disabled or is not a native JammingPod mount";
                return false;
            }

            List<HardpointSet> named = manager.hardpointSets.Where(set =>
                set != null && string.Equals(set.name, WingPylons, StringComparison.Ordinal)).ToList();
            if (named.Count != 1)
            {
                report = "expected exactly one Chimera hardpoint named '" + WingPylons
                    + "', found " + named.Count;
                return false;
            }
            if (named[0].weaponOptions == null || !named[0].weaponOptions.Any(option => option == candidate))
            {
                report = "exact jammer mount '" + mountKey
                    + "' is not offered by Chimera hardpoint '" + WingPylons + "'";
                return false;
            }
            mount = candidate;
            report = "resolved " + mountKey + " from " + mountSource;
            return true;
        }

        private bool TryResolveCombatManifest(FactionHQ hq,
            Dictionary<CargoRole, Vector3> targets, out AircraftDefinition definition,
            out List<TransportOperation> transports, out string report)
        {
            definition = null;
            transports = new List<TransportOperation>();
            report = null;

            string activationReport;
            if (!TryActivateVerifiedLegacyChimeraContent(out activationReport))
            {
                report = activationReport;
                return false;
            }
            string aircraftKey = AirliftPolicy.NormalizeChimeraDefinitionKey(_aircraftKey.Value);
            UnitDefinition rawDefinition;
            string definitionSource;
            string definitionError;
            if (!TryResolveExactUnitDefinition(aircraftKey, out rawDefinition,
                    out definitionSource, out definitionError))
            {
                report = definitionError;
                return false;
            }
            definition = rawDefinition as AircraftDefinition;
            if (definition == null || definition.unitPrefab == null
                || definition.unitPrefab.GetComponent<Aircraft>() == null
                || !string.Equals(aircraftKey, ChimeraKey, StringComparison.Ordinal)
                || !definition.IsAllowed(MissionManager.AllowEventContent))
            {
                report = "the exact enabled Chimera aircraft definition is unavailable";
                return false;
            }

            transports.Add(NewTransport("ALPHA",
                NewAssignment(CargoRole.Type12Alpha, _combatType12MountKey.Value,
                    MissionBay, targets, false, true)));
            transports.Add(NewTransport("BRAVO",
                NewAssignment(CargoRole.Type12Bravo, _combatType12MountKey.Value,
                    MissionBay, targets, false, true)));
            transports.Add(NewTransport("CHARLIE",
                NewAssignment(CargoRole.AfvIfvCharlie, _combatAfvIfvMountKey.Value,
                    RearCargoBay, targets, false, true),
                NewAssignment(CargoRole.AfvAaCharlie, _combatAfvAaMountKey.Value,
                    FrontCargoBay, targets, false, true)));
            transports.Add(NewTransport("DELTA",
                NewAssignment(CargoRole.AfvIfvDelta, _combatAfvIfvMountKey.Value,
                    RearCargoBay, targets, false, true),
                NewAssignment(CargoRole.AfvAaDelta, _combatAfvAaMountKey.Value,
                    FrontCargoBay, targets, false, true)));
            transports.Add(NewTransport("ECHO",
                NewAssignment(CargoRole.FrcvRear, _combatFrcvMountKey.Value,
                    RearCargoBay, targets, false, true, _combatFrcvDisplayName.Value),
                NewAssignment(CargoRole.FrcvFront, _combatFrcvMountKey.Value,
                    FrontCargoBay, targets, false, true, _combatFrcvDisplayName.Value)));

            Aircraft prefabAircraft = definition.unitPrefab.GetComponent<Aircraft>();
            WeaponManager manager = prefabAircraft.weaponManager;
            if (manager == null || manager.hardpointSets == null)
            {
                report = "Chimera prefab has no hardpoint catalogue";
                return false;
            }
            WeaponMount suppressionMount;
            string suppressionReport;
            if (!TryResolveCombatSuppressionMount(manager,
                    (_combatSuppressionMount.Value ?? string.Empty).Trim(),
                    out suppressionMount, out suppressionReport))
            {
                report = "Chimera runway suppression: " + suppressionReport;
                return false;
            }

            foreach (TransportOperation transport in transports)
            {
                transport.SuppressionMount = suppressionMount;
                var usedHardpoints = new HashSet<int>();
                foreach (CargoAssignment assignment in transport.Cargo)
                {
                    int hardpointIndex;
                    string assignmentReport;
                    if (!TryResolveAssignment(manager, assignment, out hardpointIndex,
                            out assignmentReport))
                    {
                        report = transport.Callsign + "/" + assignment.Role + ": "
                            + assignmentReport;
                        return false;
                    }
                    if (!usedHardpoints.Add(hardpointIndex))
                    {
                        report = transport.Callsign
                            + " assigns two packages to the same Chimera hardpoint";
                        return false;
                    }
                }
                Loadout loadout = BuildLoadout(definition, transport.Cargo,
                    transport.SuppressionMount);
                if (!loadout.AllowedByHQ(manager, hq))
                {
                    report = transport.Callsign
                        + " combat loadout is restricted for the selected faction";
                    return false;
                }
            }

            report = "Resolved five exact ground-level Chimera loadouts: two Type-12 MBTs; "
                + "two AFV6 IFV+AFV6 AA pairs; one dual "
                + _combatFrcvDisplayName.Value + "; all with AGM-68 x3 runway-suppression mounts"
                + " (FRCV mount=" + transports[4].Cargo[0].MountKey
                + "; suppression mount=" + suppressionMount.jsonKey
                + "; definition source: " + definitionSource + "; "
                + activationReport + ").";
            return true;
        }

        private static bool TryResolveCombatSuppressionMount(WeaponManager manager,
            string requestedIdentity, out WeaponMount mount, out string report)
        {
            mount = null;
            report = null;
            if (manager == null || manager.hardpointSets == null)
            {
                report = "Chimera prefab has no hardpoint catalogue";
                return false;
            }
            List<HardpointSet> named = manager.hardpointSets.Where(set =>
                set != null && string.Equals(set.name, WingPylons,
                    StringComparison.Ordinal)).ToList();
            if (named.Count != 1 || named[0].weaponOptions == null)
            {
                report = "expected exactly one populated Chimera hardpoint named '"
                    + WingPylons + "', found " + named.Count;
                return false;
            }
            string identity = CanonicalCargoDisplay(string.IsNullOrWhiteSpace(
                requestedIdentity) ? CombatSuppressionMountDefault : requestedIdentity);
            List<WeaponMount> matches = named[0].weaponOptions.Where(candidate =>
            {
                if (candidate == null) return false;
                string[] values = {
                    candidate.jsonKey, candidate.mountName,
                    candidate.info == null ? null : candidate.info.weaponName,
                    candidate.info == null ? null : candidate.info.shortName
                };
                return values.Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(CanonicalCargoDisplay)
                    .Any(value => value == identity
                        || (identity == CanonicalCargoDisplay(
                                CombatSuppressionMountDefault)
                            && value.Contains(identity)));
            }).Distinct().ToList();
            if (matches.Count != 1)
            {
                report = "expected exactly one '" + requestedIdentity
                    + "' option on Chimera " + WingPylons + ", found "
                    + matches.Count;
                return false;
            }
            WeaponMount candidate = matches[0];
            if (candidate.info == null || candidate.prefab == null
                || !candidate.info.missile || candidate.info.nuclear
                || candidate.info.cargo || candidate.Cargo
                || candidate.info.effectiveness.antiSurface <= 0f
                || named[0].hardpoints == null || named[0].hardpoints.Count != 3
                || candidate.prefab.GetComponentsInChildren<MountedMissile>(true).Length != 1
                || !candidate.IsAllowed(MissionManager.AllowEventContent))
            {
                report = "resolved option '" + (candidate.jsonKey ?? "<no key>")
                    + "' is not an enabled, non-nuclear, three-round native anti-surface missile mount";
                return false;
            }
            mount = candidate;
            report = "resolved " + candidate.jsonKey + " ("
                + candidate.mountName + ", " + candidate.info.weaponName + ")";
            return true;
        }

        private bool TryResolveAssignment(WeaponManager manager, CargoAssignment assignment,
            out int hardpointIndex, out string report)
        {
            hardpointIndex = -1;
            report = null;
            string mountKey = (assignment.MountKey ?? string.Empty).Trim();
            CargoCatalogueEntry catalogueEntry = Catalogue.FirstOrDefault(item =>
                string.Equals(item.Key, mountKey, StringComparison.Ordinal));
            if (assignment.RequiresParachute
                && (catalogueEntry == null || !catalogueEntry.CertifiedForParachuteDrop))
            {
                report = "mount '" + mountKey + "' is not certified for this parachute battery manifest";
                return false;
            }

            List<int> namedSets = new List<int>();
            for (int index = 0; index < manager.hardpointSets.Length; index++)
            {
                HardpointSet set = manager.hardpointSets[index];
                if (set != null && string.Equals(set.name, assignment.HardpointSetName, StringComparison.Ordinal))
                    namedSets.Add(index);
            }
            if (namedSets.Count != 1)
            {
                report = "expected exactly one Chimera hardpoint named '" + assignment.HardpointSetName
                    + "', found " + namedSets.Count;
                return false;
            }
            hardpointIndex = namedSets[0];
            HardpointSet hardpoint = manager.hardpointSets[hardpointIndex];

            WeaponMount mount;
            if (mountKey.Length > 0)
            {
                string mountSource;
                string mountError;
                if (!TryResolveExactWeaponMount(mountKey, out mount, out mountSource, out mountError))
                {
                    report = mountError;
                    return false;
                }
            }
            else
            {
                string expected = CanonicalCargoDisplay(assignment.ExpectedDisplayName);
                IEnumerable<WeaponMount> liveOptions = hardpoint.weaponOptions
                    ?? Enumerable.Empty<WeaponMount>();
                WeaponMount[] matches = liveOptions
                    .Where(option => option != null && CargoMountMatchesDisplay(option, expected))
                    .Distinct().ToArray();
                if (matches.Length != 1)
                {
                    report = "expected exactly one live '" + assignment.ExpectedDisplayName
                        + "' cargo mount on '" + assignment.HardpointSetName + "', found "
                        + matches.Length;
                    return false;
                }
                mount = matches[0];
                mountKey = mount.jsonKey ?? string.Empty;
                assignment.MountKey = mountKey;
            }
            if (mount.prefab == null || mount.info == null || !mount.info.cargo || !mount.Cargo
                || !mount.IsAllowed(MissionManager.AllowEventContent))
            {
                report = "mount '" + mountKey
                    + "' is disabled or lacks WeaponInfo.cargo/WeaponMount.Cargo";
                return false;
            }

            MountedCargo[] mountedCargo = mount.prefab.GetComponentsInChildren<MountedCargo>(true);
            if (mountedCargo == null || mountedCargo.Length == 0)
            {
                report = "mount '" + mountKey + "' contains no MountedCargo components";
                return false;
            }
            int expectedCargo = 0;
            foreach (MountedCargo cargo in mountedCargo)
            {
                string cargoError;
                if (!ValidateMountedCargo(cargo, assignment.RequiresParachute,
                        assignment.RequiresGroundVehicle, out cargoError))
                {
                    report = cargoError;
                    return false;
                }
                int rounds = Math.Max(0, cargo.GetAmmoTotal());
                expectedCargo += rounds;
                if (cargo.cargo != null) assignment.ExpectedUnitKeys.Add(cargo.cargo.jsonKey);
            }
            int publishedRounds = catalogueEntry == null ? expectedCargo : catalogueEntry.PublishedRounds;
            if (expectedCargo <= 0) expectedCargo = publishedRounds;
            if (expectedCargo != publishedRounds || assignment.ExpectedUnitKeys.Count == 0)
            {
                report = "cargo assignment expected " + publishedRounds
                    + " cargo units, but '" + mountKey + "' resolved " + expectedCargo;
                return false;
            }

            if (hardpoint.weaponOptions == null || !hardpoint.weaponOptions.Any(option => option == mount))
            {
                report = "exact mount '" + mountKey + "' is not offered by hardpoint '"
                    + assignment.HardpointSetName + "'";
                return false;
            }

            assignment.Mount = mount;
            assignment.ExpectedCargoCount = expectedCargo;
            return true;
        }

        private static string CanonicalCargoDisplay(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant).ToArray());
        }

        private static bool CargoMountMatchesDisplay(WeaponMount mount, string expected)
        {
            if (mount == null || expected.Length == 0) return false;
            if (CanonicalCargoDisplay(mount.mountName) == expected) return true;
            if (mount.info != null && CanonicalCargoDisplay(mount.info.weaponName) == expected)
                return true;
            if (mount.prefab == null) return false;
            return mount.prefab.GetComponentsInChildren<MountedCargo>(true).Any(cargo =>
                cargo != null && cargo.cargo != null
                && (CanonicalCargoDisplay(cargo.cargo.unitName) == expected
                    || CanonicalCargoDisplay(cargo.cargo.jsonKey) == expected));
        }

        private static TransportOperation NewTransport(string callsign, params CargoAssignment[] assignments)
        {
            var transport = new TransportOperation { Callsign = callsign };
            transport.Cargo.AddRange(assignments);
            return transport;
        }

        private static CargoAssignment NewAssignment(CargoRole role, string mountKey, string hardpointSetName,
            Dictionary<CargoRole, Vector3> targets, bool requiresParachute = true,
            bool requiresGroundVehicle = false, string expectedDisplayName = null)
        {
            Vector3 target = targets[role];
            return new CargoAssignment {
                Role = role,
                MountKey = (mountKey ?? string.Empty).Trim(),
                ExpectedDisplayName = expectedDisplayName,
                HardpointSetName = hardpointSetName,
                RequiresParachute = requiresParachute,
                RequiresGroundVehicle = requiresGroundVehicle,
                TargetPoint = target,
                NetworkTarget = GlobalPositionExtensions.ToGlobalPosition(target)
            };
        }

        private static Loadout BuildLoadout(AircraftDefinition definition,
            IEnumerable<CargoAssignment> assignments, WeaponMount wingMount)
        {
            WeaponManager manager = definition.unitPrefab.GetComponent<Aircraft>().weaponManager;
            var byName = assignments.ToDictionary(item => item.HardpointSetName, item => item.Mount,
                StringComparer.Ordinal);
            if (wingMount != null) byName.Add(WingPylons, wingMount);
            var loadout = new Loadout();
            foreach (HardpointSet hardpoint in manager.hardpointSets)
            {
                WeaponMount mount;
                loadout.weapons.Add(hardpoint != null && byName.TryGetValue(hardpoint.name, out mount)
                    ? mount : null);
            }
            return loadout;
        }

        private static bool ValidateMountedCargo(MountedCargo mountedCargo,
            bool requiresParachute, bool requiresGroundVehicle, out string error)
        {
            error = null;
            if (mountedCargo == null || mountedCargo.cargo == null || mountedCargo.cargo.unitPrefab == null)
            {
                error = "MountedCargo has no cargo UnitDefinition/prefab";
                return false;
            }
            GroundVehicle vehicle = mountedCargo.cargo.unitPrefab.GetComponent<GroundVehicle>();
            Container container = mountedCargo.cargo.unitPrefab.GetComponent<Container>();
            if (requiresGroundVehicle && vehicle == null)
            {
                error = "combat cargo unit '" + mountedCargo.cargo.jsonKey
                    + "' is not a native GroundVehicle";
                return false;
            }
            if (!AirliftPolicy.IsSupportedNativeCargoType(vehicle != null, container != null))
            {
                error = "cargo unit '" + mountedCargo.cargo.jsonKey
                    + "' is neither a native GroundVehicle nor Container";
                return false;
            }
            if (requiresParachute && vehicle != null
                && (GroundVehicleParachuteField == null
                    || GroundVehicleParachuteField.GetValue(vehicle) as GameObject == null))
            {
                error = "cargo unit '" + mountedCargo.cargo.jsonKey
                    + "' has no native GroundVehicle parachuteSystem";
                return false;
            }
            if (requiresParachute && container != null
                && (ContainerParachuteField == null
                    || ContainerParachuteField.GetValue(container) as GameObject == null))
            {
                error = "cargo unit '" + mountedCargo.cargo.jsonKey
                    + "' has no native Container parachuteSystem";
                return false;
            }
            if (!mountedCargo.cargo.IsAllowed(MissionManager.AllowEventContent))
            {
                error = "cargo unit '" + mountedCargo.cargo.jsonKey + "' is disabled for this mission";
                return false;
            }
            return true;
        }

        private bool TryResolveDynamicAirstripTargets(FactionHQ hq,
            string airbaseSelector, string mapIdentity,
            out Dictionary<CargoRole, Vector3> targets, out Vector3 forward,
            out RecordedDropZone selectedZone, out string report)
        {
            targets = new Dictionary<CargoRole, Vector3>();
            forward = Vector3.zero;
            selectedZone = null;
            report = null;
            if (hq == null || hq.faction == null)
            {
                report = "dynamic airstrip selection requires a valid faction HQ";
                return false;
            }

            Airbase targetAirbase = FindRuntimeAirbase(new[] { airbaseSelector });
            if (targetAirbase == null || targetAirbase.disabled)
            {
                report = "the live target airbase for '" + airbaseSelector
                    + "' was not found or was disabled";
                return false;
            }
            if (targetAirbase.CurrentHQ != hq)
            {
                report = "target airport '" + AirbaseName(targetAirbase)
                    + "' is no longer controlled by "
                    + PreferredFactionKey(hq.faction);
                return false;
            }

            Vector3 areaCenter = AirbasePosition(targetAirbase);
            Airbase sourceAirbase = UnityEngine.Object.FindObjectsOfType<Airbase>()
                .Where(airbase => airbase != null && airbase != targetAirbase
                    && !airbase.disabled && airbase.CurrentHQ == hq)
                .OrderBy(airbase =>
                    (AirbasePosition(airbase) - areaCenter).sqrMagnitude)
                .FirstOrDefault();
            if (sourceAirbase == null)
            {
                report = "no other friendly operational airport is available as an ingress "
                    + "source for '" + AirbaseName(targetAirbase) + "'";
                return false;
            }

            string areaName = AirbaseName(targetAirbase);
            Vector3 sourceCenter = AirbasePosition(sourceAirbase);
            float minimumRadius = Mathf.Max(250f,
                _dynamicAirstripMinimumRadius.Value);
            float maximumRadius = Mathf.Max(minimumRadius + 100f,
                _dynamicAirstripMaximumRadius.Value);
            int attempts = Mathf.Clamp(_dynamicAirstripAttempts.Value, 8, 128);
            string lastFailure = "no candidate was generated";
            Dictionary<CargoRole, Vector3> flattestTargets = null;
            Vector3 flattestForward = Vector3.zero;
            RecordedDropZone flattestZone = null;
            string flattestValidationReport = null;
            float flattestMaximumSlope = float.MaxValue;
            int flattestAttempt = 0;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                float radius = Mathf.Sqrt(Mathf.Lerp(minimumRadius * minimumRadius,
                    maximumRadius * maximumRadius, UnityEngine.Random.value));
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                Vector3 candidate = areaCenter + new Vector3(Mathf.Cos(angle) * radius,
                    0f, Mathf.Sin(angle) * radius);
                Vector3 terrainCenter;
                if (!TryPrefilterDynamicTerrain(candidate, out terrainCenter,
                        out lastFailure))
                    continue;

                Vector3 candidateForward = terrainCenter - sourceCenter;
                candidateForward.y = 0f;
                if (candidateForward.sqrMagnitude <= 100f)
                {
                    lastFailure = "candidate is too close to the ingress source";
                    continue;
                }
                candidateForward.Normalize();

                Dictionary<CargoRole, Vector3> candidateTargets;
                string validationReport;
                if (!TryValidateTargetCluster(hq, terrainCenter, candidateForward,
                        _r9RadarOffset.Value, mapIdentity, null,
                        out candidateTargets, out validationReport))
                {
                    lastFailure = validationReport;
                    continue;
                }

                float candidateMaximumSlope;
                if (!TryMeasureMaximumClusterSlope(candidateTargets.Values,
                        out candidateMaximumSlope))
                {
                    lastFailure = "validated cluster slope could not be measured";
                    continue;
                }
                if (!AirliftPolicy.ShouldPreferTerrainCandidate(
                        candidateMaximumSlope, flattestMaximumSlope))
                    continue;

                float heading = Mathf.Repeat(Mathf.Atan2(candidateForward.x,
                    candidateForward.z)
                    * Mathf.Rad2Deg, 360f);
                var candidateZone = new RecordedDropZone {
                    Name = areaName + "-" + PreferredFactionKey(hq.faction) + "-RANDOM",
                    MapIdentity = AirliftPolicy.CanonicalMapKey(mapIdentity),
                    NearestAirbaseName = areaName,
                    FactionKey = PreferredFactionKey(hq.faction),
                    GlobalPoint = GlobalPositionExtensions.ToGlobalPosition(
                        candidateTargets[CargoRole.Radar]),
                    ApproachHeading = heading
                };
                flattestTargets = candidateTargets;
                flattestForward = candidateForward;
                flattestZone = candidateZone;
                flattestValidationReport = validationReport;
                flattestMaximumSlope = candidateMaximumSlope;
                flattestAttempt = attempt;
                if (flattestMaximumSlope <= 1f)
                    break;
            }

            if (flattestZone != null)
            {
                targets = flattestTargets;
                forward = flattestForward;
                selectedZone = flattestZone;
                report = "Flattest valid dynamic " + areaName
                    + " terrain selected from "
                    + flattestAttempt.ToString(CultureInfo.InvariantCulture) + "/"
                    + attempts.ToString(CultureInfo.InvariantCulture)
                    + " search attempts at global "
                    + FormatGlobalPosition(selectedZone.GlobalPoint)
                    + "; maximum eight-target slope "
                    + flattestMaximumSlope.ToString("0.0", CultureInfo.InvariantCulture)
                    + " degrees; friendly corridor " + AirbaseName(sourceAirbase)
                    + " -> " + AirbaseName(targetAirbase) + ", inbound "
                    + selectedZone.ApproachHeading.ToString("0.0",
                        CultureInfo.InvariantCulture) + " degrees. "
                    + flattestValidationReport;
                return true;
            }

            report = "no clear random terrain was found "
                + minimumRadius.ToString("0", CultureInfo.InvariantCulture) + "-"
                + maximumRadius.ToString("0", CultureInfo.InvariantCulture) + " m around "
                + AirbaseName(targetAirbase) + " after "
                + attempts.ToString(CultureInfo.InvariantCulture)
                + " attempts; last rejection: " + lastFailure;
            return false;
        }

        private static bool TryMeasureMaximumClusterSlope(IEnumerable<Vector3> points,
            out float maximumSlope)
        {
            maximumSlope = 0f;
            bool measuredAny = false;
            foreach (Vector3 point in points ?? Enumerable.Empty<Vector3>())
            {
                RaycastHit hit;
                if (!PathfindingAgent.RaycastTerrain(point, out hit))
                    return false;
                measuredAny = true;
                maximumSlope = Mathf.Max(maximumSlope,
                    Vector3.Angle(hit.normal, Vector3.up));
            }
            return measuredAny;
        }

        private bool TryResolveCombatRunwayTargets(FactionHQ hq, Airbase airbase,
            string runwaySelector, out Dictionary<CargoRole, Vector3> targets,
            out Vector3 forward, out Vector3 center, out float runwayLength,
            out float runwayWidth, out string zoneName, out string mapIdentity,
            out string report)
        {
            targets = new Dictionary<CargoRole, Vector3>();
            forward = Vector3.zero;
            center = Vector3.zero;
            zoneName = null;
            runwayLength = 0f;
            runwayWidth = 0f;
            mapIdentity = CurrentMapIdentity();
            report = null;
            if (!AirliftPolicy.AutomaticMapHasRecordedZones(mapIdentity,
                    LoadRecordedZones().Select(zone => zone.MapIdentity)))
            {
                report = "RAPID drops are disabled because map '"
                    + AirliftPolicy.CanonicalMapKey(mapIdentity)
                    + "' has no recorded AIRLIFT zones";
                return false;
            }
            if (hq == null || hq.faction == null || airbase == null
                || airbase.disabled
                || !AirliftPolicy.CombatPurchaseTargetIsEnemy(
                    airbase.CurrentHQ != null,
                    airbase.CurrentHQ != null
                        && airbase.CurrentHQ.faction != null,
                    airbase.CurrentHQ != null
                        && airbase.CurrentHQ.faction == hq.faction)
                || airbase.runways == null)
            {
                report = "the selected enemy-held airport/runway data is unavailable";
                return false;
            }

            float minimumLength = Mathf.Clamp(_combatMinimumRunwayLength.Value, 600f, 4000f);
            float minimumWidth = Mathf.Clamp(_combatMinimumRunwayWidth.Value, 8f, 80f);
            var eligible = new List<KeyValuePair<int, Airbase.Runway>>();
            for (int index = 0; index < airbase.runways.Length; index++)
            {
                Airbase.Runway runway = airbase.runways[index];
                if (runway == null || !AirliftPolicy.CombatRunwayEligible(
                        runway.Start != null && runway.End != null,
                        runway.Landing || runway.Takeoff, runway.IsLevel(),
                        runway.Length, runway.GetWidth(), minimumLength, minimumWidth))
                    continue;
                Vector3 axis = runway.End.position - runway.Start.position;
                axis.y = 0f;
                if (axis.sqrMagnitude > 100f)
                    eligible.Add(new KeyValuePair<int, Airbase.Runway>(index, runway));
            }
            bool compactRunwayFallback = false;
            if (eligible.Count == 0)
            {
                compactRunwayFallback = true;
                for (int index = 0; index < airbase.runways.Length; index++)
                {
                    Airbase.Runway runway = airbase.runways[index];
                    if (runway == null || !AirliftPolicy.CombatRunwayEligible(
                            runway.Start != null && runway.End != null,
                            runway.Landing || runway.Takeoff, runway.IsLevel(),
                            runway.Length, runway.GetWidth(), 600f, 8f))
                        continue;
                    Vector3 axis = runway.End.position - runway.Start.position;
                    axis.y = 0f;
                    if (axis.sqrMagnitude > 100f)
                        eligible.Add(new KeyValuePair<int, Airbase.Runway>(index,
                            runway));
                }
                if (eligible.Count == 0)
                {
                    report = "'" + AirbaseName(airbase)
                        + "' has no level operational runway at least 600m x 8m";
                    return false;
                }
            }

            KeyValuePair<int, Airbase.Runway> selected;
            if (string.Equals(runwaySelector, "random", StringComparison.OrdinalIgnoreCase))
                selected = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            else
            {
                int rawNumber;
                if (!int.TryParse(runwaySelector, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out rawNumber) || rawNumber < 1)
                {
                    report = "runway selector must be a positive runway number or random";
                    return false;
                }
                KeyValuePair<int, Airbase.Runway>[] exact = eligible.Where(item =>
                    item.Key == rawNumber - 1).ToArray();
                if (exact.Length != 1)
                {
                    report = "runway " + rawNumber + " at '" + AirbaseName(airbase)
                        + "' is unavailable or fails the RAPID size/level checks";
                    return false;
                }
                selected = exact[0];
            }

            Airbase sourceAirbase = UnityEngine.Object.FindObjectsOfType<Airbase>()
                .Where(candidate => candidate != null && candidate != airbase
                    && !candidate.disabled && candidate.CurrentHQ == hq)
                .OrderBy(candidate => HorizontalDistance(AirbasePosition(candidate),
                    AirbasePosition(airbase))).FirstOrDefault();
            if (sourceAirbase == null)
            {
                report = "no second friendly airport exists to define a friendly-territory ingress";
                return false;
            }

            Airbase.Runway chosen = selected.Value;
            runwayLength = chosen.Length;
            runwayWidth = chosen.GetWidth();
            Vector3 start = chosen.Start.position;
            Vector3 end = chosen.End.position;
            center = (start + end) * 0.5f;
            Vector3 axisForward = end - start;
            axisForward.y = 0f;
            axisForward.Normalize();
            Vector3 friendlyApproach = center - AirbasePosition(sourceAirbase);
            friendlyApproach.y = 0f;
            if (Vector3.Dot(axisForward, friendlyApproach) < 0f) axisForward = -axisForward;
            forward = axisForward;

            float spacing = AirliftPolicy.EffectiveCombatTrailSpacing(
                _combatTrailSpacing.Value, chosen.Length);
            float requiredHalfLength = (2f * spacing) + 130f;
            if (chosen.Length * 0.5f < requiredHalfLength)
            {
                report = "runway " + (selected.Key + 1)
                    + " does not have enough usable centreline for five spaced Chimeras";
                return false;
            }
            Vector3 runwayCenter = center;
            Vector3 runwayForward = forward;
            Vector3[] lane = Enumerable.Range(0, 5).Select(index =>
                runwayCenter + (runwayForward * AirliftPolicy.CombatTrailOffset(
                    index, 5, spacing))).ToArray();
            float packageSeparation = 18f;
            var configured = new Dictionary<CargoRole, Vector3> {
                { CargoRole.Type12Alpha, lane[0] },
                { CargoRole.Type12Bravo, lane[1] },
                { CargoRole.AfvIfvCharlie, lane[2] - (forward * packageSeparation) },
                { CargoRole.AfvAaCharlie, lane[2] + (forward * packageSeparation) },
                { CargoRole.AfvIfvDelta, lane[3] - (forward * packageSeparation) },
                { CargoRole.AfvAaDelta, lane[3] + (forward * packageSeparation) },
                { CargoRole.FrcvRear, lane[4] - (forward * packageSeparation) },
                { CargoRole.FrcvFront, lane[4] + (forward * packageSeparation) }
            };
            foreach (KeyValuePair<CargoRole, Vector3> item in configured)
            {
                Vector3 terrainPoint;
                string pointError;
                if (!TryValidateCombatRunwayPoint(hq, item.Key.ToString(), item.Value,
                        mapIdentity, out terrainPoint, out pointError))
                {
                    report = pointError;
                    return false;
                }
                targets.Add(item.Key, terrainPoint);
            }
            center.y = targets[CargoRole.AfvIfvCharlie].y;
            zoneName = AirbaseName(airbase) + " runway "
                + (selected.Key + 1).ToString(CultureInfo.InvariantCulture);
            report = "Selected live " + zoneName + " (" + chosen.Length.ToString("0",
                CultureInfo.InvariantCulture) + "m x " + chosen.GetWidth().ToString("0",
                CultureInfo.InvariantCulture) + "m"
                + (compactRunwayFallback ? "; compact-runway fallback" : string.Empty)
                + "; trail spacing " + spacing.ToString("0",
                    CultureInfo.InvariantCulture) + "m); friendly ingress "
                + AirbaseName(sourceAirbase) + " -> " + AirbaseName(airbase)
                + "; eight ground-level targets validated along the runway centreline.";
            return true;
        }

        private bool TryValidateCombatRunwayPoint(FactionHQ hq, string label,
            Vector3 configuredPoint, string expectedMapKey, out Vector3 terrainPoint,
            out string report)
        {
            terrainPoint = configuredPoint;
            if (!AirliftPolicy.ZoneMapMatches(CurrentMapIdentity(), expectedMapKey))
            {
                report = label + " rejected: running map changed";
                return false;
            }
            RaycastHit hit;
            if (!PathfindingAgent.RaycastTerrain(configuredPoint, out hit))
            {
                report = label + " rejected: terrain was not found below the runway";
                return false;
            }
            terrainPoint = hit.point;
            Vector3 validatedPoint = terrainPoint;
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (terrainPoint.y <= Datum.LocalSeaY + Mathf.Max(0f, _seaMargin.Value)
                || slope > Mathf.Clamp(_combatMaximumSlope.Value, 0.25f, 3f))
            {
                report = label + " rejected: runway terrain is water or slopes "
                    + slope.ToString("0.0", CultureInfo.InvariantCulture) + " degrees";
                return false;
            }
            float unitClearance = 18f;
            if (UnityEngine.Object.FindObjectsOfType<Unit>().Any(unit => unit != null
                    && !unit.disabled && !IsCurrentOperationUnit(unit)
                    && !(unit is Aircraft)
                    && !IsHostileRunwaySuppressionTarget(unit, hq)
                    && HorizontalDistance(unit.transform.position, validatedPoint) < unitClearance))
            {
                report = label + " rejected: a friendly or non-targetable unit obstructs the runway release point";
                return false;
            }
            // Aircraft using the runway are transient and do not block preflight;
            // hostile runway traffic and ground units may be engaged after spawn.
            if (!hq.IsDropZoneClear(GlobalPositionExtensions.ToGlobalPosition(terrainPoint)))
            {
                report = label + " rejected by the faction's native drop-zone check";
                return false;
            }
            report = label + " runway point passed";
            return true;
        }

        private bool TryPrefilterDynamicTerrain(Vector3 candidate,
            out Vector3 terrainPoint, out string report)
        {
            terrainPoint = candidate;
            RaycastHit hit;
            if (!PathfindingAgent.RaycastTerrain(candidate, out hit))
            {
                report = "terrain was not found below the random candidate";
                return false;
            }
            terrainPoint = hit.point;
            if (terrainPoint.y <= Datum.LocalSeaY + Mathf.Max(0f, _seaMargin.Value))
            {
                report = "random candidate is water or below the configured sea margin";
                return false;
            }
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > Mathf.Max(0f, _maximumSlope.Value))
            {
                report = "random candidate slope "
                    + slope.ToString("0.0", CultureInfo.InvariantCulture)
                    + " degrees exceeds the configured limit";
                return false;
            }

            float radius = Mathf.Max(25f, _dynamicBuildingClearance.Value);
            float height = Mathf.Max(8f, _clearanceHeight.Value);
            Collider[] overlaps = Physics.OverlapBox(
                terrainPoint + (Vector3.up * ((height * 0.5f) + 1f)),
                new Vector3(radius, height * 0.5f, radius), Quaternion.identity,
                PhysicsLayers.StaticsMask | PhysicsLayers.ExclusionZonesMask,
                QueryTriggerInteraction.Collide);
            Collider obstruction = overlaps.FirstOrDefault(collider =>
                collider != null && collider != hit.collider);
            if (obstruction != null)
            {
                report = "random candidate is within "
                    + radius.ToString("0", CultureInfo.InvariantCulture)
                    + " m of static obstruction '" + obstruction.name + "'";
                return false;
            }
            report = "dynamic terrain prefilter passed";
            return true;
        }

        private static Airbase FindRuntimeAirbase(IEnumerable<string> aliases)
        {
            Airbase[] airbases = UnityEngine.Object.FindObjectsOfType<Airbase>()
                .Where(airbase => airbase != null).ToArray();
            string[] compactAliases = (aliases ?? Enumerable.Empty<string>())
                .Select(CompactAirbaseText).Where(alias => alias.Length > 0).ToArray();
            foreach (string alias in compactAliases)
            {
                Airbase exact = airbases.FirstOrDefault(airbase =>
                    CompactAirbaseText(AirbaseName(airbase)) == alias);
                if (exact != null) return exact;
            }
            foreach (string alias in compactAliases)
            {
                Airbase partial = airbases.FirstOrDefault(airbase =>
                    CompactAirbaseText(AirbaseName(airbase)).Contains(alias)
                    || alias.Contains(CompactAirbaseText(AirbaseName(airbase))));
                if (partial != null) return partial;
            }
            string[] canonicalAliases = (aliases ?? Enumerable.Empty<string>())
                .Select(AirliftPolicy.CanonicalAirbaseKey)
                .Where(alias => alias.Length > 0).ToArray();
            foreach (string alias in canonicalAliases)
            {
                Airbase canonical = airbases.FirstOrDefault(airbase =>
                    AirliftPolicy.CanonicalAirbaseKey(AirbaseName(airbase)) == alias);
                if (canonical != null) return canonical;
            }
            return null;
        }

        private static string CompactAirbaseText(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
        }

        private bool TryResolveDropTargets(Player owner, string requestedZone,
            out Dictionary<CargoRole, Vector3> targets,
            out Vector3 forward, out RecordedDropZone selectedZone, out string report)
        {
            return TryResolveDropTargets(owner, owner == null ? null : owner.HQ,
                requestedZone, out targets, out forward, out selectedZone, out report);
        }

        private bool TryResolveDropTargets(Player owner, FactionHQ hq,
            string requestedZone, out Dictionary<CargoRole, Vector3> targets,
            out Vector3 forward, out RecordedDropZone selectedZone, out string report)
        {
            targets = new Dictionary<CargoRole, Vector3>();
            selectedZone = null;
            string runningMap = CurrentMapKey();
            string mapIdentity = CurrentMapIdentity();

            List<RecordedDropZone> eligible = LoadRecordedZones().Where(zone =>
                zone != null && AirliftPolicy.ZoneMapMatches(zone.MapIdentity, mapIdentity)
                && RecordedZoneMatchesFaction(zone, hq == null ? null : hq.faction))
                .ToList();
            string selector = (requestedZone ?? string.Empty).Trim();
            string dynamicAirbaseSelector;
            if (AirliftPolicy.TryParseDynamicZoneSelector(selector,
                    out dynamicAirbaseSelector))
            {
                return TryResolveDynamicAirstripTargets(hq,
                    dynamicAirbaseSelector, mapIdentity, out targets, out forward,
                    out selectedZone, out report);
            }
            List<RecordedDropZone> recorded;
            if (selector.Length > 0)
            {
                recorded = eligible.Where(zone => AirliftPolicy.ZoneSelectorMatches(
                    zone.Name, zone.NearestAirbaseName, selector)).ToList();
                if (recorded.Count == 0)
                {
                    forward = Vector3.zero;
                    report = "zone selector '" + selector + "' did not match any "
                        + PreferredFactionKey(hq == null ? null : hq.faction)
                        + " zone on map '" + AirliftPolicy.CanonicalMapKey(mapIdentity)
                        + "'; use /zones";
                    return false;
                }
                if (recorded.Count > 1)
                {
                    forward = Vector3.zero;
                    report = "zone selector '" + selector + "' is ambiguous: "
                        + string.Join(", ", recorded.Select(zone => zone.Name).ToArray())
                        + "; include the zone number shown by /zones";
                    return false;
                }
            }
            else
            {
                recorded = eligible.OrderBy(zone => UnityEngine.Random.value).ToList();
            }
            string lastRecordedFailure = null;
            foreach (RecordedDropZone zone in recorded)
            {
                float inboundHeading = zone.ApproachHeading;
                float recordedHeadingRadians = inboundHeading * Mathf.Deg2Rad;
                Vector3 recordedForward =
                    new Vector3(Mathf.Sin(recordedHeadingRadians), 0f,
                        Mathf.Cos(recordedHeadingRadians)).normalized;
                Dictionary<CargoRole, Vector3> recordedTargets;
                string recordedReport;
                Vector3 recordedLocalPoint =
                    GlobalPositionExtensions.ToLocalPosition(zone.GlobalPoint);
                Vector3 datumOffset = Datum.originPosition;
                string coordinateReport = "global "
                    + FormatGlobalPosition(zone.GlobalPoint) + " -> local "
                    + FormatVector(recordedLocalPoint) + " using datum "
                    + FormatVector(datumOffset) + "; restored inbound approach "
                    + inboundHeading.ToString("0.0", CultureInfo.InvariantCulture)
                    + " degrees";
                if (!TryValidateTargetCluster(hq, recordedLocalPoint, recordedForward,
                        _r9RadarOffset.Value, zone.MapIdentity, null,
                        out recordedTargets, out recordedReport))
                {
                    lastRecordedFailure = zone.Name + " [" + coordinateReport
                        + "]: " + recordedReport;
                    continue;
                }
                targets = recordedTargets;
                forward = recordedForward;
                selectedZone = zone;
                report = "Selected recorded zone '" + zone.Name + "' near "
                    + zone.NearestAirbaseName + " on map '" + zone.MapIdentity
                    + "'; " + coordinateReport + ". "
                    + recordedReport;
                return true;
            }

            if (selector.Length > 0)
            {
                forward = Vector3.zero;
                report = "selected zone '" + recorded[0].Name
                    + "' failed live validation: " + lastRecordedFailure;
                return false;
            }

            if (_requireRecordedZones.Value)
            {
                forward = Vector3.zero;
                report = recorded.Count == 0
                    ? "no /addzone landing zones are saved for "
                        + PreferredFactionKey(hq == null ? null : hq.faction)
                        + " on map '" + AirliftPolicy.CanonicalMapKey(mapIdentity) + "'"
                    : "all recorded " + PreferredFactionKey(hq == null ? null : hq.faction)
                        + " zones for map '" + AirliftPolicy.CanonicalMapKey(mapIdentity)
                        + "' failed live validation; last rejection: " + lastRecordedFailure;
                return false;
            }

            string configuredMap = (_zoneMapKey.Value ?? string.Empty).Trim();
            if (!AirliftPolicy.MapKeyMatches(runningMap, configuredMap))
            {
                forward = Vector3.zero;
                report = "configured map key '" + configuredMap + "' does not match running key '"
                    + (string.IsNullOrEmpty(runningMap) ? "<empty/legacy>" : runningMap) + "'";
                return false;
            }

            float headingRadians = _approachHeading.Value * Mathf.Deg2Rad;
            forward = new Vector3(Mathf.Sin(headingRadians), 0f, Mathf.Cos(headingRadians)).normalized;
            Vector3 radar = new Vector3(_zoneX.Value, 0f, _zoneZ.Value);
            if (_useOwnerAircraftPosition.Value)
            {
                if (owner == null || owner.Aircraft == null || owner.Aircraft.disabled)
                {
                    report = "automatic drop-zone search requires the authorized owner to be in an aircraft";
                    return false;
                }
                Vector3 ownerForward = owner.Aircraft.transform.forward;
                ownerForward.y = 0f;
                if (ownerForward.sqrMagnitude > 0.01f) forward = ownerForward.normalized;
                radar = owner.Aircraft.transform.position
                    + (forward * Mathf.Max(250f, _ownerAheadDistance.Value));
                radar.y = 0f;
            }

            float r9Offset = _r9RadarOffset.Value;
            string configuredSpacingError = AirliftPolicy.ValidateR9RadarSpacing(r9Offset, HardMaximumR9RadarDistance);
            if (configuredSpacingError != null || r9Offset <= 0f)
            {
                report = configuredSpacingError ?? "R9/radar offset must be greater than zero";
                return false;
            }

            List<Vector3> candidates = _useOwnerAircraftPosition.Value
                ? BuildZoneSearchCandidates(radar, Mathf.Max(0f, _zoneSearchRadius.Value),
                    Mathf.Max(50f, _zoneSearchStep.Value))
                : new List<Vector3> { radar };
            string lastFailure = "no candidate points were generated";
            foreach (Vector3 candidate in candidates)
            {
                Dictionary<CargoRole, Vector3> candidateTargets;
                string candidateReport;
                if (!TryValidateTargetCluster(hq, candidate, forward, r9Offset,
                        configuredMap, null, out candidateTargets, out candidateReport))
                {
                    lastFailure = candidateReport;
                    continue;
                }
                targets = candidateTargets;
                report = (_useOwnerAircraftPosition.Value ? "Auto-selected " : "Configured ")
                    + "zone on running map key '"
                    + (string.IsNullOrEmpty(runningMap) ? "<empty/legacy>" : runningMap)
                    + "'. " + candidateReport;
                return true;
            }

            report = (recorded.Count == 0 ? "no recorded zones exist for map '" + mapIdentity + "'; "
                    : "all " + recorded.Count + " recorded zones were unavailable"
                        + (lastRecordedFailure == null ? "; " : " (last: " + lastRecordedFailure + "); "))
                + "no safe configured eight-target cluster found across " + candidates.Count
                + " candidate points near " + FormatVector(radar) + "; last rejection: " + lastFailure;
            return false;
        }

        private bool TryValidateTargetCluster(FactionHQ hq, Vector3 radar, Vector3 forward,
            float r9Offset, string expectedMapKey, Unit ignoredUnit,
            out Dictionary<CargoRole, Vector3> targets, out string report)
        {
            targets = new Dictionary<CargoRole, Vector3>();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            var configured = new Dictionary<CargoRole, Vector3> {
                { CargoRole.Radar, radar },
                { CargoRole.MunitionsRear, radar - (forward * Mathf.Max(2f, _munitionsOffset.Value)) },
                { CargoRole.MunitionsFront, radar + (forward * Mathf.Max(2f, _munitionsOffset.Value)) },
                { CargoRole.R9Alpha, radar - (right * r9Offset) },
                { CargoRole.R9Bravo, radar + (right * r9Offset) },
                { CargoRole.R9Charlie, radar + (forward * Mathf.Max(15f, r9Offset)) },
                { CargoRole.SlmmrAlpha, radar - (right * Mathf.Max(25f, _slmmrOffset.Value)) },
                { CargoRole.SlmmrCharlie, radar + (forward * Mathf.Max(50f, _slmmrOffset.Value)) }
            };
            foreach (KeyValuePair<CargoRole, Vector3> item in configured)
            {
                Vector3 terrainPoint;
                string pointReport;
                if (!TryValidateZonePoint(hq, item.Key.ToString(), item.Value, expectedMapKey,
                        ignoredUnit,
                        out terrainPoint, out pointReport))
                {
                    report = pointReport;
                    return false;
                }
                targets.Add(item.Key, terrainPoint);
            }

            float alphaDistance = HorizontalDistance(targets[CargoRole.Radar], targets[CargoRole.R9Alpha]);
            float bravoDistance = HorizontalDistance(targets[CargoRole.Radar], targets[CargoRole.R9Bravo]);
            float charlieDistance = HorizontalDistance(targets[CargoRole.Radar], targets[CargoRole.R9Charlie]);
            string alphaError = AirliftPolicy.ValidateR9RadarSpacing(alphaDistance, HardMaximumR9RadarDistance);
            string bravoError = AirliftPolicy.ValidateR9RadarSpacing(bravoDistance, HardMaximumR9RadarDistance);
            string charlieError = AirliftPolicy.ValidateR9RadarSpacing(charlieDistance, HardMaximumR9RadarDistance);
            if (alphaError != null || bravoError != null || charlieError != null)
            {
                report = "validated target geometry failed: R9 distances are "
                    + alphaDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m and "
                    + bravoDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m and "
                    + charlieDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m";
                return false;
            }

            report = "Eight package targets validated (including two x4 pallet groups); intended R9/radar distances "
                + alphaDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m and "
                + bravoDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m and "
                + charlieDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m.";
            return true;
        }

        private static List<Vector3> BuildZoneSearchCandidates(Vector3 center, float radius, float step)
        {
            var candidates = new List<Vector3> { center };
            int rings = Mathf.CeilToInt(radius / step);
            for (int ring = 1; ring <= rings; ring++)
            {
                float distance = Mathf.Min(radius, ring * step);
                const int points = 12;
                for (int index = 0; index < points; index++)
                {
                    float angle = (Mathf.PI * 2f * index) / points;
                    candidates.Add(center + new Vector3(Mathf.Cos(angle) * distance, 0f,
                        Mathf.Sin(angle) * distance));
                }
            }
            return candidates;
        }

        private bool TryValidateZonePoint(FactionHQ hq, string label, Vector3 configuredPoint,
            string expectedMapKey, Unit ignoredUnit,
            out Vector3 terrainPoint, out string report)
        {
            terrainPoint = configuredPoint;
            var facts = new ZoneFacts();
            string runningMap = CurrentMapKey();
            facts.MapMatches = AirliftPolicy.ZoneMapMatches(CurrentMapIdentity(), expectedMapKey)
                || AirliftPolicy.MapKeyMatches(runningMap, expectedMapKey);

            LevelInfo level = UnityEngine.Object.FindObjectOfType<LevelInfo>();
            float halfX = level != null && level.LoadedMapSettings != null
                ? level.LoadedMapSettings.MapSize.x * 0.5f : 0f;
            float halfZ = level != null && level.LoadedMapSettings != null
                ? level.LoadedMapSettings.MapSize.y * 0.5f : 0f;
            facts.InMapBounds = halfX > 0f && halfZ > 0f
                && Mathf.Abs(terrainPoint.x) <= halfX - _clearanceRadius.Value
                && Mathf.Abs(terrainPoint.z) <= halfZ - _clearanceRadius.Value;

            RaycastHit hit;
            facts.TerrainFound = PathfindingAgent.RaycastTerrain(terrainPoint, out hit);
            if (facts.TerrainFound)
            {
                terrainPoint = hit.point;
                facts.AboveWater = terrainPoint.y > Datum.LocalSeaY + Mathf.Max(0f, _seaMargin.Value);
                facts.SlopeDegrees = Vector3.Angle(hit.normal, Vector3.up);
                float radius = Mathf.Max(2f, _clearanceRadius.Value);
                float height = Mathf.Max(4f, _clearanceHeight.Value);
                Vector3 center = terrainPoint + (Vector3.up * ((height * 0.5f) + 1f));
                Collider[] overlaps = Physics.OverlapBox(center,
                    new Vector3(radius, height * 0.5f, radius), Quaternion.identity,
                    PhysicsLayers.StaticsMask | PhysicsLayers.ExclusionZonesMask,
                    QueryTriggerInteraction.Collide);
                facts.Obstructed = overlaps.Any(collider => collider != null && collider != hit.collider);
            }

            string expectedFaction = (_expectedFaction.Value ?? string.Empty).Trim();
            facts.FactionValid = hq != null && hq.faction != null && (expectedFaction.Length == 0
                || string.Equals(expectedFaction, hq.faction.factionName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(expectedFaction, hq.faction.factionTag, StringComparison.OrdinalIgnoreCase));

            Vector3 validatedPoint = terrainPoint;
            float unitClearance = Mathf.Max(2f, _clearanceRadius.Value);
            facts.Obstructed = facts.Obstructed || UnityEngine.Object.FindObjectsOfType<Unit>().Any(unit =>
                unit != null && !unit.disabled
                && unit != ignoredUnit
                && !IsCurrentOperationUnit(unit)
                && HorizontalDistance(unit.transform.position, validatedPoint) < unitClearance);
            float hostileRange = Mathf.Max(0f, _hostileSeparation.Value);
            facts.HostileNearby = hq != null && UnityEngine.Object.FindObjectsOfType<Unit>().Any(unit =>
                unit != null && !unit.disabled && unit.NetworkHQ != null && unit.NetworkHQ != hq
                && unit.NetworkHQ.faction != hq.faction
                && HorizontalDistance(unit.transform.position, validatedPoint) < hostileRange);
            facts.NativeZoneClear = hq != null
                && hq.IsDropZoneClear(GlobalPositionExtensions.ToGlobalPosition(terrainPoint));

            string error = AirliftPolicy.ValidateZone(facts, _maximumSlope.Value);
            report = error == null
                ? label + " target " + FormatVector(terrainPoint) + " slope "
                    + facts.SlopeDegrees.ToString("0.0", CultureInfo.InvariantCulture) + " degrees"
                : label + " target rejected: " + error;
            return error == null;
        }

        private bool IsCurrentOperationUnit(Unit unit)
        {
            BatteryOperation battery = _operation;
            if (unit == null || battery == null) return false;
            if (battery.Transports.Any(transport => transport.Aircraft == unit)) return true;
            return battery.DeployedCargo.Any(record => record != null && record.Unit == unit);
        }

        private bool ReleaseWindowSatisfied(TransportOperation transport)
        {
            Aircraft aircraft = transport.Aircraft;
            Vector3 velocity = aircraft.rb != null ? aircraft.rb.velocity : Vector3.zero;
            float roll = Mathf.Abs(Mathf.DeltaAngle(aircraft.transform.eulerAngles.z, 0f));
            float tolerance = transport.ReleaseAltitudeTolerance > 0f
                ? transport.ReleaseAltitudeTolerance
                : AirliftPolicy.EffectiveAltitudeTolerance(_altitudeTolerance.Value);
            if (transport.MaximumReleaseRadarAltitude > 0f
                && aircraft.radarAlt > transport.MaximumReleaseRadarAltitude)
                return false;
            return AirliftPolicy.WithinReleaseWindow(
                HorizontalDistance(aircraft.transform.position, transport.DropPoint),
                AirliftPolicy.EffectiveLeaderReleaseRadius(_releaseRadius.Value),
                aircraft.radarAlt, transport.TargetRadarAltitude,
                tolerance,
                aircraft.speed, _minimumReleaseSpeed.Value, _maximumReleaseSpeed.Value,
                roll, _maximumRoll.Value, Mathf.Abs(velocity.y), _maximumVerticalSpeed.Value);
        }

        private bool LeaderAcquisitionWindowSatisfied(TransportOperation transport)
        {
            return ReleaseWindowSatisfied(transport);
        }

        private bool SharedReleaseGateSatisfied(TransportOperation transport)
        {
            Vector3 route = transport.IngressDirection;
            route.y = 0f;
            if (route.sqrMagnitude <= 1f) return false;
            route.Normalize();

            Vector3 toDrop = transport.DropPoint - transport.Aircraft.transform.position;
            toDrop.y = 0f;
            float alongTrack = Vector3.Dot(toDrop, route);
            float crossTrack = (toDrop - (route * alongTrack)).magnitude;
            if (!AirliftPolicy.WithinSharedReleaseGate(alongTrack, crossTrack)) return false;

            return AircraftReleaseStateSatisfied(transport);
        }

        private bool TryEvaluatePrecisionRelease(BatteryOperation battery,
            TransportOperation transport, bool formatReport, out bool ready,
            out bool missed, out string report)
        {
            ready = false;
            missed = false;
            report = "prediction unavailable; fixed release gate retained";
            Aircraft aircraft = transport == null ? null : transport.Aircraft;
            if (battery == null || aircraft == null || aircraft.disabled
                || aircraft.rb == null)
                return false;

            Vector3 touchdownPoint = battery.RadarTarget + transport.FormationOffset;
            Vector3 toTarget = touchdownPoint - aircraft.transform.position;
            float initialHeight = aircraft.transform.position.y - touchdownPoint.y;
            toTarget.y = 0f;
            if (initialHeight <= 0f || toTarget.sqrMagnitude <= 1f)
                return false;

            Vector3 horizontalVelocity = aircraft.rb.velocity;
            horizontalVelocity.y = 0f;
            if (horizontalVelocity.sqrMagnitude <= 1f)
                return false;

            float fallTime = Kinematics.FallTime(initialHeight,
                aircraft.rb.velocity.y);
            float horizontalDistance = toTarget.magnitude;
            float alongTrackSpeed = Mathf.Max(
                Vector3.Dot(horizontalVelocity, toTarget.normalized), 1f);
            float timeToTarget = horizontalDistance / alongTrackSpeed;
            float timeDifference = timeToTarget - fallTime;
            float alignment = Vector3.Angle(toTarget, horizontalVelocity);
            if (float.IsNaN(fallTime) || float.IsInfinity(fallTime)
                || fallTime < 0f || float.IsNaN(timeDifference)
                || float.IsInfinity(timeDifference))
                return false;

            float lead = Mathf.Clamp(_precisionParachuteLeadTime.Value, 0f, 15f);
            float late = Mathf.Clamp(_precisionLateTolerance.Value, 0.25f, 5f);
            float maximumAlignment = Mathf.Clamp(_precisionAlignment.Value, 5f, 45f);
            ready = AirliftPolicy.PrecisionReleaseReady(timeDifference, lead,
                late, alignment, maximumAlignment)
                && AircraftReleaseStateSatisfied(transport);
            missed = AirliftPolicy.PrecisionReleaseMissed(timeDifference, late);
            if (formatReport)
            {
                report = "distance="
                    + horizontalDistance.ToString("0", CultureInfo.InvariantCulture)
                    + "m, height=" + initialHeight.ToString("0", CultureInfo.InvariantCulture)
                    + "m, along-speed="
                    + alongTrackSpeed.ToString("0.0", CultureInfo.InvariantCulture)
                    + "m/s, fall=" + fallTime.ToString("0.00", CultureInfo.InvariantCulture)
                    + "s, target=" + timeToTarget.ToString("0.00", CultureInfo.InvariantCulture)
                    + "s, delta=" + timeDifference.ToString("0.00", CultureInfo.InvariantCulture)
                    + "s, alignment=" + alignment.ToString("0.0", CultureInfo.InvariantCulture)
                    + "deg, lead=" + lead.ToString("0.00", CultureInfo.InvariantCulture)
                    + "s, ready=" + ready;
            }
            return true;
        }

        private bool AircraftReleaseStateSatisfied(TransportOperation transport)
        {
            Aircraft aircraft = transport.Aircraft;
            Vector3 velocity = aircraft.rb != null ? aircraft.rb.velocity : Vector3.zero;
            float roll = Mathf.Abs(Mathf.DeltaAngle(aircraft.transform.eulerAngles.z, 0f));
            float tolerance = transport.ReleaseAltitudeTolerance > 0f
                ? transport.ReleaseAltitudeTolerance
                : AirliftPolicy.EffectiveAltitudeTolerance(_altitudeTolerance.Value);
            if (transport.MaximumReleaseRadarAltitude > 0f
                && aircraft.radarAlt > transport.MaximumReleaseRadarAltitude)
                return false;
            return AirliftPolicy.WithinReleaseWindow(
                0f, 1f, aircraft.radarAlt, transport.TargetRadarAltitude,
                tolerance,
                aircraft.speed, _minimumReleaseSpeed.Value, _maximumReleaseSpeed.Value,
                roll, _maximumRoll.Value, Mathf.Abs(velocity.y), _maximumVerticalSpeed.Value);
        }

        private static bool PassedPrecisionTarget(BatteryOperation battery,
            TransportOperation transport, float distanceBeyond)
        {
            if (battery == null || transport == null || transport.Aircraft == null)
                return false;
            Vector3 route = transport.IngressDirection;
            route.y = 0f;
            Vector3 laneTarget = battery.RadarTarget + transport.FormationOffset;
            Vector3 fromTarget = transport.Aircraft.transform.position - laneTarget;
            fromTarget.y = 0f;
            return route.sqrMagnitude > 1f
                && Vector3.Dot(fromTarget, route.normalized)
                    > Mathf.Max(40f, distanceBeyond);
        }

        private bool TryPrepareLeaderReleasePoint(BatteryOperation battery,
            TransportOperation leader, out string report)
        {
            report = null;
            Vector3 route = leader.IngressDirection;
            route.y = 0f;
            if (route.sqrMagnitude <= 1f)
            {
                report = "leader route has no horizontal direction";
                return false;
            }
            route.Normalize();

            Vector3 candidate = leader.Aircraft.transform.position - leader.FormationOffset;
            candidate.y = 0f;
            GlobalPosition previousReservation = battery.ReservedDropPosition;
            bool hadReservation = battery.DropZoneRegistered;
            if (hadReservation) DeregisterDropZone(battery);

            Dictionary<CargoRole, Vector3> targets;
            string validationReport;
            bool valid;
            try
            {
                valid = TryValidateTargetCluster(battery.Hq, candidate, route, _r9RadarOffset.Value,
                    battery.MapKey, null, out targets, out validationReport);
            }
            catch (Exception exception)
            {
                targets = null;
                validationReport = "live target validation threw " + exception.GetType().Name;
                valid = false;
            }

            if (!valid)
            {
                if (hadReservation)
                {
                    battery.Hq.RegisterDropZone(previousReservation);
                    battery.DropZoneRegistered = true;
                }
                report = validationReport;
                return false;
            }

            foreach (TransportOperation transport in battery.Transports)
            {
                foreach (CargoAssignment assignment in transport.Cargo)
                {
                    Vector3 target = targets[assignment.Role];
                    assignment.TargetPoint = target;
                    assignment.NetworkTarget = GlobalPositionExtensions.ToGlobalPosition(target);
                }
                RouteTransportThroughPoint(transport, targets[CargoRole.Radar]);
            }

            battery.RadarTarget = targets[CargoRole.Radar];
            battery.ReservedDropPosition = GlobalPositionExtensions.ToGlobalPosition(battery.RadarTarget);
            battery.Hq.RegisterDropZone(battery.ReservedDropPosition);
            battery.DropZoneRegistered = true;
            battery.LeaderReleasePointPrepared = true;
            battery.SharedReleasePoint = battery.RadarTarget;
            battery.SharedReleasePointEstablished = true;
            report = validationReport;
            return true;
        }

        private void EstablishSharedReleasePoint(BatteryOperation battery, TransportOperation leader)
        {
            if (battery == null || leader == null || leader.Aircraft == null
                || battery.SharedReleasePointEstablished)
                return;

            Vector3 point = leader.Aircraft.transform.position - leader.FormationOffset;
            point.y = battery.RadarTarget.y;
            battery.SharedReleasePoint = point;
            battery.SharedReleasePointEstablished = true;
            foreach (TransportOperation transport in battery.Transports)
            {
                RouteTransportThroughPoint(transport, point);
            }
            Announce("AIRLIFT-" + battery.Id
                + ": radar release confirmed the synchronized formation line at "
                + FormatVector(point) + ".");
            DebugLog("AIRLIFT-" + battery.Id
                + " synchronized release line confirmed by the native radar spawn.");
        }

        private static void RouteTransportThroughPoint(TransportOperation transport, Vector3 point)
        {
            Vector3 route = transport.EgressDirection;
            route.y = 0f;
            if (route.sqrMagnitude <= 1f) route = Vector3.forward;
            route.Normalize();
            Vector3 lanePoint = point + transport.FormationOffset;
            float remainingEgress = Mathf.Max(1000f,
                HorizontalDistance(transport.EgressPoint, lanePoint));
            transport.DropPoint = lanePoint;
            transport.EgressPoint = lanePoint + (route * remainingEgress);
            transport.EgressPoint = new Vector3(transport.EgressPoint.x,
                lanePoint.y + transport.TargetRadarAltitude, transport.EgressPoint.z);
            if (transport.Phase == TransportPhase.WaitingForSystems
                || transport.Phase == TransportPhase.Ingress
                || transport.Phase == TransportPhase.Releasing)
            {
                Vector3 ingress = transport.IngressDirection;
                ingress.y = 0f;
                if (ingress.sqrMagnitude <= 1f) ingress = Vector3.forward;
                ingress.Normalize();
                transport.FlightDestination = lanePoint + (ingress * 2000f);
            }
            else
            {
                transport.FlightDestination = transport.EgressPoint;
            }
        }

        private static bool PassedDropZone(TransportOperation transport, float distanceBeyond)
        {
            Vector3 route = transport.IngressDirection;
            route.y = 0f;
            Vector3 fromDrop = transport.Aircraft.transform.position - transport.DropPoint;
            fromDrop.y = 0f;
            return route.sqrMagnitude > 1f
                && Vector3.Dot(fromDrop, route.normalized) > Mathf.Max(40f, distanceBeyond);
        }

        private void ValidateCommand(Player owner, INetworkPlayer recipient,
            string requestedZone)
        {
            Dictionary<CargoRole, Vector3> targets = null;
            Vector3 forward = Vector3.zero;
            RecordedDropZone selectedZone = null;
            string zoneReport = "owner has no faction HQ";
            bool zoneValid = owner.HQ != null
                && TryResolveDropTargets(owner, requestedZone,
                    out targets, out forward, out selectedZone, out zoneReport);

            AircraftDefinition definition;
            List<TransportOperation> transports;
            string packageReport = "zone validation did not produce targets";
            bool packageValid = zoneValid && TryResolveBatteryManifest(owner.HQ, targets,
                out definition, out transports, out packageReport);
            SendPrivate(recipient, "AIRLIFT validation: zone=" + (zoneValid ? "PASS" : "FAIL")
                + " (" + zoneReport + "); four-aircraft manifest=" + (packageValid ? "PASS" : "FAIL")
                + " (" + packageReport + ").");
        }

        private static void SpawnUnitPostfix(UnitDefinition unit, Unit owner, Unit __result)
        {
            Plugin plugin = _instance;
            if (plugin == null || __result == null || owner == null || plugin._operation == null) return;
            BatteryOperation battery = plugin._operation;
            TransportOperation transport = battery.Transports.FirstOrDefault(item => item.Aircraft == owner);
            if (transport == null) return;
            string definitionKey = unit == null || string.IsNullOrEmpty(unit.jsonKey)
                ? "<unkeyed native cargo>" : unit.jsonKey;
            List<CargoAssignment> unconfirmed = transport.Cargo.Where(item => item != null
                && item.SpawnConfirmations < item.ExpectedCargoCount).ToList();
            CargoAssignment assignment = unconfirmed.FirstOrDefault(item =>
                item.ReleaseCommands > item.SpawnConfirmations
                && item.ExpectedUnitKeys.Contains(definitionKey));
            if (assignment == null)
                assignment = unconfirmed.FirstOrDefault(item =>
                    item.ExpectedUnitKeys.Contains(definitionKey));
            if (assignment == null)
                assignment = unconfirmed.FirstOrDefault(item =>
                    item.ReleaseCommands > item.SpawnConfirmations);
            if (assignment == null)
                assignment = unconfirmed.FirstOrDefault();
            if (assignment == null)
            {
                plugin.Logger.LogWarning(BatteryLabel(battery, transport)
                    + " observed surplus native cargo spawn '" + definitionKey
                    + "'; the formation and all existing cargo are being retained.");
                return;
            }
            if (!assignment.ExpectedUnitKeys.Contains(definitionKey))
            {
                assignment.ExpectedUnitKeys.Add(definitionKey);
                plugin.Logger.LogWarning(BatteryLabel(battery, transport)
                    + " learned live cargo alias '" + definitionKey + "' for "
                    + assignment.Role + "; release continues without operation cleanup.");
            }

            var record = new DeployedCargoRecord {
                Unit = __result,
                DefinitionKey = definitionKey,
                Role = assignment.Role,
                IntendedTarget = assignment.TargetPoint,
                SpawnedAt = Time.unscaledTime,
                RunwayDispersalRequired = battery.Kind
                    == AirliftOperationKind.CombatRunwayDrop
            };
            assignment.SpawnConfirmations++;
            transport.DeployedCargo.Add(record);
            battery.DeployedCargo.Add(record);
            plugin._trackedCargo.Add(record);
            plugin._nextPersistentUpdate = 0f;
            plugin.Logger.LogInfo(BatteryLabel(battery, transport) + " tracking " + assignment.Role
                + " cargo " + transport.DeployedCargo.Count + "/" + transport.ExpectedCargoCount
                + ": " + definitionKey + ".");
            if (assignment.Role == CargoRole.Radar)
                plugin.EstablishSharedReleasePoint(battery, transport);
        }

        private void UpdateCargoRecords(IEnumerable<DeployedCargoRecord> records)
        {
            foreach (DeployedCargoRecord record in records)
            {
                if (record == null || record.Unit == null || record.InspectionComplete) continue;
                if (!record.ParachuteObserved)
                    record.ParachuteObserved = record.Unit.GetComponentInChildren<CargoDeploymentSystem>(true) != null;
                GroundVehicle groundVehicle = record.Unit as GroundVehicle;
                Container container = record.Unit as Container;
                if (groundVehicle != null)
                {
                    if (!record.GroundActivationObserved)
                        record.GroundActivationObserved = groundVehicle.enabled && !groundVehicle.disabled;
                    if (!record.LandedObserved)
                        record.LandedObserved = groundVehicle.rb == null
                            || (Mathf.Abs(groundVehicle.rb.velocity.y) < 1f && groundVehicle.radarAlt < 3f);
                }
                else if (container != null)
                {
                    if (!record.GroundActivationObserved)
                        record.GroundActivationObserved = container.enabled && !container.disabled;
                    if (!record.LandedObserved)
                        record.LandedObserved = container.rb == null
                            || (Mathf.Abs(container.rb.velocity.y) < 1f && container.radarAlt < 3f);
                }
                if (record.RunwayDispersalRequired
                    && record.GroundActivationObserved && record.LandedObserved
                    && !record.RunwayDispersalIssued
                    && !record.RunwayDispersalFailed)
                {
                    record.RunwayDispersalAttempts++;
                    if (groundVehicle != null && groundVehicle.UnitCommand != null)
                    {
                        try
                        {
                            groundVehicle.SetHoldPosition(false);
                            groundVehicle.LeaveRoad(
                                AirliftPolicy.EffectiveRunwayDispersalDistance(
                                    _combatRunwayDispersalDistance.Value));
                            record.RunwayDispersalIssued = true;
                            Logger.LogInfo("AIRLIFT combat cargo " + record.Role
                                + " ordered off runway via native GroundVehicle.LeaveRoad().");
                        }
                        catch (Exception exception)
                        {
                            Logger.LogWarning("AIRLIFT combat cargo " + record.Role
                                + " runway dispersal attempt failed: "
                                + exception.GetType().Name + ".");
                        }
                    }
                    if (!record.RunwayDispersalIssued
                        && record.RunwayDispersalAttempts >= 5)
                    {
                        record.RunwayDispersalFailed = true;
                        Logger.LogWarning("AIRLIFT combat cargo " + record.Role
                            + " could not accept a native runway dispersal order; unit retained.");
                    }
                }
                record.InspectionComplete = record.GroundActivationObserved
                    && record.LandedObserved
                    && (!record.RunwayDispersalRequired
                        || record.RunwayDispersalIssued
                        || record.RunwayDispersalFailed);
            }
        }

        private bool TryValidateActualRadarLink(BatteryOperation battery, out string error)
        {
            error = null;
            DeployedCargoRecord radar = battery.DeployedCargo.FirstOrDefault(record =>
                record.Role == CargoRole.Radar && record.LandedObserved && record.Unit != null);
            DeployedCargoRecord alpha = battery.DeployedCargo.FirstOrDefault(record =>
                record.Role == CargoRole.R9Alpha && record.LandedObserved && record.Unit != null);
            DeployedCargoRecord bravo = battery.DeployedCargo.FirstOrDefault(record =>
                record.Role == CargoRole.R9Bravo && record.LandedObserved && record.Unit != null);
            DeployedCargoRecord charlie = battery.DeployedCargo.FirstOrDefault(record =>
                record.Role == CargoRole.R9Charlie && record.LandedObserved && record.Unit != null);
            if (radar == null || alpha == null || bravo == null || charlie == null) return false;

            float alphaDistance = HorizontalDistance(radar.Unit.transform.position, alpha.Unit.transform.position);
            float bravoDistance = HorizontalDistance(radar.Unit.transform.position, bravo.Unit.transform.position);
            float charlieDistance = HorizontalDistance(radar.Unit.transform.position,
                charlie.Unit.transform.position);
            DebugLog("AIRLIFT-" + battery.Id + " actual R9/radar touchdown distances: "
                + alphaDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m, "
                + bravoDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m, "
                + charlieDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m.");
            string alphaError = AirliftPolicy.ValidateR9RadarSpacing(alphaDistance, HardMaximumR9RadarDistance);
            string bravoError = AirliftPolicy.ValidateR9RadarSpacing(bravoDistance, HardMaximumR9RadarDistance);
            string charlieError = AirliftPolicy.ValidateR9RadarSpacing(charlieDistance,
                HardMaximumR9RadarDistance);
            if (alphaError != null || bravoError != null || charlieError != null)
            {
                error = "R9 touchdown exceeded the radar container's 17 km coverage radius ("
                    + alphaDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m, "
                    + bravoDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m, "
                    + charlieDistance.ToString("0.00", CultureInfo.InvariantCulture) + " m)";
                return false;
            }
            return true;
        }

        private void ExcludeFromNativeAiLimit(TransportOperation transport, int batteryId)
        {
            if (transport == null || transport.Aircraft == null
                || transport.NativeAiLimitExcluded) return;
            bool removed = ExcludeAircraftFromNativeAiLimit(transport.Aircraft);
            transport.NativeAiLimitExcluded = true;
            Logger.LogInfo("AIRLIFT-" + batteryId + "-" + transport.Callsign
                + (removed ? " excluded" : " already absent")
                + " from native AI-aircraft deployment accounting.");
        }

        private static bool ExcludeAircraftFromNativeAiLimit(Aircraft aircraft)
        {
            if (aircraft == null) return false;
            FactionHQ hq = aircraft.NetworkHQ;
            if (hq == null) return false;
            var active = ActiveAiAircraftField.GetValue(hq) as List<Aircraft>;
            if (active == null) throw new InvalidOperationException("FactionHQ.activeAIAircraft changed type.");
            bool removed = false;
            while (active.Remove(aircraft)) removed = true;
            return removed;
        }

        private void AbortOperation(string reason, bool cleanCargo)
        {
            BatteryOperation battery = _operation;
            if (battery == null) return;
            RefundUnlaunchedCombatPurchase(battery, reason);
            foreach (TransportOperation transport in battery.Transports)
            {
                transport.SetPhase(TransportPhase.Aborted, Time.unscaledTime);
                DestroyNetworkUnit(transport.Aircraft);
            }
            DeregisterDropZone(battery);
            if (cleanCargo)
            {
                foreach (DeployedCargoRecord cargo in battery.DeployedCargo.Distinct().ToArray())
                {
                    DestroyNetworkUnit(cargo == null ? null : cargo.Unit);
                    _trackedCargo.Remove(cargo);
                }
            }
            Logger.LogWarning("AIRLIFT-" + battery.Id + " aborted: " + reason + ".");
            Announce("AIRLIFT-" + battery.Id + ": battery operation aborted (" + reason + ").");
            _operation = null;
        }

        private void RefundUnlaunchedCombatPurchase(BatteryOperation battery,
            string reason)
        {
            if (battery == null || !battery.PurchaseCharged
                || !battery.PurchaseRefundEligible || battery.PurchaseCost <= 0f)
                return;
            Player purchaser = battery.PurchasingPlayer;
            if (purchaser == null || purchaser.SteamID != battery.PurchasingSteamId)
                purchaser = UnitRegistry.playerLookup.Values.FirstOrDefault(player =>
                    player != null && player.SteamID == battery.PurchasingSteamId);
            if (purchaser != null)
            {
                purchaser.AddAllocation(battery.PurchaseCost);
                Logger.LogWarning("AIRLIFT-" + battery.Id + " refunded "
                    + UnitConverter.ValueReading(battery.PurchaseCost)
                    + " because the purchased formation did not finish launching: "
                    + reason + ".");
            }
            else
            {
                Logger.LogError("AIRLIFT-" + battery.Id + " could not immediately refund Steam ID "
                    + battery.PurchasingSteamId.ToString(CultureInfo.InvariantCulture)
                    + " because that player is no longer present; manual refund required.");
            }
            if (battery.Hq != null) _combatPurchaseCooldownUntil.Remove(battery.Hq);
            battery.PurchaseRefundEligible = false;
            battery.PurchaseCharged = false;
        }

        private void FinishTransport(BatteryOperation battery, TransportOperation transport,
            string reason)
        {
            transport.SetPhase(TransportPhase.Complete, Time.unscaledTime);
            DebugLog(BatteryLabel(battery, transport) + " complete: " + reason + ".");
        }

        private void CompleteOffMapExtraction(BatteryOperation battery,
            TransportOperation transport)
        {
            if (battery == null || transport == null || transport.IsTerminal) return;
            Aircraft aircraft = transport.Aircraft;
            FinishTransport(battery, transport,
                "crossed original entry edge; transport despawned and deployed cargo retained");
            transport.JammerTarget = null;
            transport.JammerTargetReason = null;
            DestroyNetworkUnit(aircraft);
        }

        private void FinishOperation(string reason)
        {
            BatteryOperation battery = _operation;
            if (battery == null) return;
            DeregisterDropZone(battery);
            Logger.LogInfo("AIRLIFT-" + battery.Id + " complete: " + reason
                + "; tracked cargo=" + battery.DeployedCargo.Count + ".");
            Announce("AIRLIFT-" + battery.Id + ": operation complete (" + reason + ").");
            _operation = null;
        }

        private static void DeregisterDropZone(BatteryOperation battery)
        {
            if (battery == null || !battery.DropZoneRegistered || battery.Hq == null) return;
            battery.Hq.DeregisterDropZone(battery.ReservedDropPosition);
            battery.DropZoneRegistered = false;
        }

        private static void DestroyNetworkUnit(Unit unit)
        {
            if (unit == null) return;
            try
            {
                unit.ChangeUnitState(Unit.UnitState.Returned);
                ServerObjectManager manager = unit.ServerObjectManager;
                if (manager != null) manager.Destroy(unit.gameObject, true);
                else unit.DisableUnit();
            }
            catch
            {
                try { unit.DisableUnit(); } catch { }
            }
        }

        private bool UpdatePersistentlyTrackedUnits()
        {
            _trackedCargo.RemoveAll(record => record == null || record.Unit == null);
            UpdateCargoRecords(_trackedCargo);
            return _trackedCargo.Any(record => record != null && !record.InspectionComplete);
        }

        private void CleanupPersistentlyTrackedUnits(bool cleanCargo = true)
        {
            if (cleanCargo)
            {
                foreach (DeployedCargoRecord cargo in _trackedCargo)
                    DestroyNetworkUnit(cargo == null ? null : cargo.Unit);
                _trackedCargo.Clear();
            }
        }

        private string StatusText()
        {
            if (_operation == null)
            {
                return "AIRLIFT: idle; configured zone=" + _zoneName.Value
                    + ", manifest=4 Chimeras/14 cargo units, tracked deployed cargo=" + _trackedCargo.Count + ".";
            }

            string phases = string.Join(", ", _operation.Transports.Select(transport =>
                transport.Callsign + "=" + transport.Phase + " "
                + transport.ReleaseCommands + "/" + transport.ExpectedCargoCount).ToArray());
            return "AIRLIFT-" + _operation.Id + ": " + phases + "; spawned/tracked="
                + _operation.DeployedCargo.Count + "/14; radar-link="
                + (_operation.RadarLinkValidated ? "PASS"
                    : (_operation.RadarLinkCheckComplete ? "DEGRADED" : "PENDING")) + "; zone="
                + _operation.ZoneName + ".";
        }

        private static string CatalogueText()
        {
            return "AIRLIFT battery: ALPHA rear pallet-x4+SLMMR-A3"
                + " | BRAVO radar+R9 | CHARLIE R9+front pallet-x4"
                + " | DELTA SLMMR-A3+R9 | all Chimeras " + TransportJammerMountDefault
                + " | radar coverage maximum 17 km.";
        }

        private void DebugLog(string message)
        {
            if (_diagnostics.Value) Logger.LogInfo(message);
        }

        private static MissionManager SafeMissionManager()
        {
            try { return MissionManager.i; }
            catch { return null; }
        }

        private static string CurrentMissionIdentity()
        {
            if (MissionManager.CurrentMission == null) return null;
            return CurrentMapKey() + "|"
                + MissionManager.CurrentMission.GetHashCode().ToString(CultureInfo.InvariantCulture);
        }

        private static string CurrentMapKey()
        {
            Mission mission = MissionManager.CurrentMission;
            return mission == null ? string.Empty : mission.MapKey.Path ?? string.Empty;
        }

        private static string CurrentMapIdentity()
        {
            string mapKey = CurrentMapKey();
            if (!string.IsNullOrWhiteSpace(mapKey))
                return AirliftPolicy.CanonicalMapKey(mapKey);
            LevelInfo level = UnityEngine.Object.FindObjectOfType<LevelInfo>();
            MapSettings settings = level == null ? null : level.LoadedMapSettings;
            if (settings != null && !string.IsNullOrWhiteSpace(settings.name))
            {
                string settingsIdentity = settings.name.Replace("(Clone)", string.Empty).Trim();
                string canonical = AirliftPolicy.CanonicalMapKey(settingsIdentity);
                if (!string.Equals(canonical, settingsIdentity, StringComparison.Ordinal))
                    return canonical;
                return settingsIdentity + "-"
                    + settings.MapSize.x.ToString("0", CultureInfo.InvariantCulture) + "x"
                    + settings.MapSize.y.ToString("0", CultureInfo.InvariantCulture);
            }
            if (settings != null)
                return "LEGACY-"
                    + settings.MapSize.x.ToString("0", CultureInfo.InvariantCulture) + "x"
                    + settings.MapSize.y.ToString("0", CultureInfo.InvariantCulture);
            return "<unknown-map>";
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return AirliftPolicy.HorizontalDistance(a.x, a.z, b.x, b.z);
        }

        private static float HorizontalSquareDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return (x * x) + (z * z);
        }

        private static float HorizontalDistance(GlobalPosition a, GlobalPosition b)
        {
            return AirliftPolicy.HorizontalDistance(a.x, a.z, b.x, b.z);
        }

        private static Vector3 AverageTargets(IEnumerable<CargoAssignment> assignments)
        {
            CargoAssignment[] values = assignments.ToArray();
            if (values.Length == 0) return Vector3.zero;
            Vector3 total = Vector3.zero;
            foreach (CargoAssignment assignment in values) total += assignment.TargetPoint;
            return total / values.Length;
        }

        private static string FormatVector(Vector3 vector)
        {
            return "(" + vector.x.ToString("0.0", CultureInfo.InvariantCulture) + ", "
                + vector.y.ToString("0.0", CultureInfo.InvariantCulture) + ", "
                + vector.z.ToString("0.0", CultureInfo.InvariantCulture) + ")";
        }

        private static string FormatGlobalPosition(GlobalPosition position)
        {
            return "(" + position.x.ToString("0.0", CultureInfo.InvariantCulture) + ", "
                + position.y.ToString("0.0", CultureInfo.InvariantCulture) + ", "
                + position.z.ToString("0.0", CultureInfo.InvariantCulture) + ")";
        }

        private static string BatteryLabel(BatteryOperation battery, TransportOperation transport)
        {
            return "AIRLIFT-" + battery.Id + "-" + transport.Callsign;
        }

        private static void SendPrivate(INetworkPlayer recipient, string message)
        {
            if (recipient == null) return;
            try
            {
                ChatManager manager = ChatManager.i;
                if (manager != null) manager.RpcTargetServerMessage(recipient, message, false);
            }
            catch { }
        }

        private void Announce(string message,
            AirliftAnnouncementKind kind = AirliftAnnouncementKind.Internal)
        {
            Logger.LogInfo("AIRLIFT event: " + message);
            if (_announceMilestonesInGame == null
                || !AirliftPolicy.ShouldBroadcastOperationMessage(
                    _announceMilestonesInGame.Value, kind))
                return;
            try
            {
                ChatManager manager = ChatManager.i;
                if (manager != null) manager.RpcServerMessage(message, false);
                else Logger.LogWarning("ChatManager was unavailable for announcement: " + message);
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Announcement failed: " + exception.Message);
            }
        }
    }
}
