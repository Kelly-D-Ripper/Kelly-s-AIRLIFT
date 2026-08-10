using System;
using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace KellysAirlift
{
    internal enum AirliftOperationKind
    {
        AntiAirBattery,
        CombatRunwayDrop
    }

    internal enum TransportPhase
    {
        WaitingForSystems,
        Ingress,
        Releasing,
        Egress,
        RecoveryHolding,
        Returning,
        Landing,
        Despawning,
        Complete,
        Aborted
    }

    internal enum CargoRole
    {
        Radar,
        MunitionsRear,
        MunitionsFront,
        R9Alpha,
        R9Bravo,
        R9Charlie,
        SlmmrAlpha,
        SlmmrCharlie,
        Type12Alpha,
        Type12Bravo,
        AfvIfvCharlie,
        AfvAaCharlie,
        AfvIfvDelta,
        AfvAaDelta,
        FrcvRear,
        FrcvFront
    }

    internal sealed class RecordedDropZone
    {
        public string Name;
        public string MapIdentity;
        public string NearestAirbaseName;
        public string FactionKey;
        public GlobalPosition GlobalPoint;
        public float ApproachHeading;
    }

    internal sealed class CaptureAirliftRequest
    {
        public Airbase Airbase;
        public FactionHQ Hq;
        public string AirbaseName;
        public string ZoneSelector;
        public string CooldownKey;
        public float QueuedAt;
    }

    internal sealed class CargoCatalogueEntry
    {
        public string Key;
        public string DisplayName;
        public int PublishedRounds;
        public bool CertifiedForParachuteDrop;
        public string ResearchNote;
    }

    internal sealed class CargoAssignment
    {
        public CargoRole Role;
        public string MountKey;
        public string ExpectedDisplayName;
        public string HardpointSetName;
        public WeaponMount Mount;
        public WeaponStation Station;
        public MountedCargo MountedCargo;
        public Vector3 TargetPoint;
        public GlobalPosition NetworkTarget;
        public int ExpectedCargoCount;
        public int ReleaseCommands;
        public int SpawnConfirmations;
        public bool RequiresParachute = true;
        public bool RequiresGroundVehicle;
        public readonly List<string> ExpectedUnitKeys = new List<string>();
    }

    internal sealed class DeployedCargoRecord
    {
        public Unit Unit;
        public string DefinitionKey;
        public CargoRole Role;
        public Vector3 IntendedTarget;
        public float SpawnedAt;
        public bool ParachuteObserved;
        public bool GroundActivationObserved;
        public bool LandedObserved;
        public bool RunwayDispersalRequired;
        public bool RunwayDispersalIssued;
        public bool RunwayDispersalFailed;
        public int RunwayDispersalAttempts;
        public bool InspectionComplete;
    }

    internal sealed class TransportOperation
    {
        public string Callsign;
        public Aircraft Aircraft;
        public Pilot Pilot;
        public AirliftTransportState TransportState;
        public FactionHQ Hq;
        public Vector3 DropPoint;
        public Vector3 SpawnPoint;
        public Vector3 EgressPoint;
        public Vector3 ReturnPoint;
        public Vector3 FlightDestination;
        public Vector3 FormationOffset;
        public Vector3 IngressDirection;
        public Vector3 EgressDirection;
        public Airbase RecoveryAirbase;
        public string RecoveryAirbaseName;
        public int RecoverySequence;
        public float RecoveryHoldAngle;
        public float TargetRadarAltitude;
        public float ReleaseAltitudeTolerance;
        public float MaximumReleaseRadarAltitude;
        public float CruiseThrottle;
        public float StartedAt;
        public float PhaseStartedAt;
        public float NextReleaseAt;
        public float DespawnAt;
        public float NextJammerScanAt;
        public float NextCargoDoorRefreshAt;
        public float NextRecoveryUpdateAt;
        public float NextLandingRetryAt;
        public float CountermeasureHoldUntil;
        public float NextSuppressionLaunchAt;
        public int ReleaseCursor;
        public int SuppressionLaunches;
        public bool ImmediateRelease;
        public bool PackageDeploymentConfirmed;
        public bool EgressReady;
        public bool NativeAiLimitExcluded;
        public bool GearDownForDrop;
        public WeaponMount JammerMount;
        public WeaponMount SuppressionMount;
        public WeaponStation SuppressionStation;
        public Unit SuppressionTarget;
        public Unit JammerTarget;
        public string JammerTargetReason;
        public TransportPhase Phase;
        public PostDropBehavior PostDropBehavior;
        public readonly List<JammingPod> JammingPods = new List<JammingPod>();
        public readonly List<WeaponStation> JammerStations = new List<WeaponStation>();
        public readonly List<BayDoor> CargoDoors = new List<BayDoor>();
        public readonly List<CargoAssignment> Cargo = new List<CargoAssignment>();
        public readonly List<DeployedCargoRecord> DeployedCargo = new List<DeployedCargoRecord>();

        public int ExpectedCargoCount
        {
            get
            {
                int count = 0;
                foreach (CargoAssignment assignment in Cargo) count += assignment.ExpectedCargoCount;
                return count;
            }
        }

        public int ReleaseCommands
        {
            get
            {
                int count = 0;
                foreach (CargoAssignment assignment in Cargo) count += assignment.ReleaseCommands;
                return count;
            }
        }

        public bool IsTerminal
        {
            get { return Phase == TransportPhase.Complete || Phase == TransportPhase.Aborted; }
        }

        public bool IsActive
        {
            get { return !IsTerminal && Aircraft != null && !Aircraft.disabled; }
        }

        public void SetPhase(TransportPhase phase, float now)
        {
            Phase = phase;
            PhaseStartedAt = now;
        }
    }

    internal sealed class BatteryOperation
    {
        public int Id;
        public AirliftOperationKind Kind;
        public string ZoneName;
        public string MapKey;
        public FactionHQ Hq;
        public Vector3 RadarTarget;
        public GlobalPosition ReservedDropPosition;
        public Vector3 DatumOrigin;
        public bool DropZoneRegistered;
        public bool FormationSpawnInProgress;
        public bool LeaderReleasePointPrepared;
        public bool SharedReleasePointEstablished;
        public Vector3 SharedReleasePoint;
        public bool RadarLinkCheckComplete;
        public bool RadarLinkValidated;
        public bool SynchronizedReleaseStarted;
        public bool DropProfileStarted;
        public bool FlaresTriggered;
        public bool HasTransportLoss;
        public bool TimeoutSuppressionAnnounced;
        public bool CoordinatedEgressStarted;
        public bool CoordinatedEgressReleased;
        public int SynchronizedReleaseWave;
        public int ReleaseWaveCount = 2;
        public int FlareBurstsRemaining;
        public float NextSynchronizedReleaseAt;
        public float NextFlareBurstAt;
        public float CruiseRadarAltitude;
        public float DropRadarAltitude;
        public float StartedAt;
        public Vector3 SuppressionRunwayCenter;
        public Vector3 SuppressionRunwayForward;
        public float SuppressionRunwayHalfLength;
        public float SuppressionRunwayHalfWidth;
        public float NextSuppressionScanAt;
        public Player PurchasingPlayer;
        public ulong PurchasingSteamId;
        public float PurchaseCost;
        public bool PurchaseCharged;
        public bool PurchaseRefundEligible;
        public readonly List<Aircraft> ThreatAircraft = new List<Aircraft>();
        public float NextThreatSnapshotAt;
        public readonly List<TransportOperation> Transports = new List<TransportOperation>();
        public readonly List<DeployedCargoRecord> DeployedCargo = new List<DeployedCargoRecord>();
        public readonly List<Unit> SuppressionTargets = new List<Unit>();
        public readonly Dictionary<Unit, float> SuppressionRetryAt =
            new Dictionary<Unit, float>();

        public bool IsActive
        {
            get
            {
                foreach (TransportOperation transport in Transports)
                    if (!transport.IsTerminal) return true;
                return false;
            }
        }
    }
}
