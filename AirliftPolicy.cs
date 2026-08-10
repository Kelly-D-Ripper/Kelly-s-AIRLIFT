using System;
using System.Collections.Generic;
using System.Linq;

namespace KellysAirlift
{
    public enum PostDropBehavior
    {
        Return,
        Land,
        Despawn
    }

    public enum AirliftAnnouncementKind
    {
        Internal,
        Launch,
        AircraftShotDown,
        DropSucceeded
    }

    public sealed class ZoneFacts
    {
        public bool MapMatches;
        public bool InMapBounds;
        public bool TerrainFound;
        public bool AboveWater;
        public float SlopeDegrees;
        public bool Obstructed;
        public bool FactionValid;
        public bool HostileNearby;
        public bool NativeZoneClear;
    }

    public static class AirliftPolicy
    {
        public static bool IsRapidCommandToken(string value)
        {
            return string.Equals(value, "rapid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "combat", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldBroadcastOperationMessage(bool milestonesEnabled,
            AirliftAnnouncementKind kind)
        {
            return milestonesEnabled && kind != AirliftAnnouncementKind.Internal;
        }

        public static float DatumRebaseDelta(float previousOrigin, float currentOrigin)
        {
            // Nuclear Option stores local coordinates as global + Datum.originPosition.
            return currentOrigin - previousOrigin;
        }

        public static string NormalizeChimeraDefinitionKey(string configuredKey)
        {
            string key = (configuredKey ?? string.Empty).Trim();
            return key.Equals("Aryx_MC260_Chimera", StringComparison.Ordinal)
                ? "Aryx_CargoPlane1"
                : key;
        }

        public static bool IsSupportedNativeCargoType(bool isGroundVehicle, bool isContainer)
        {
            return isGroundVehicle || isContainer;
        }

        public static float EffectiveDropRadarAltitude(float configuredMetres)
        {
            return Math.Max(450f, configuredMetres);
        }

        public static float EffectiveCruiseRadarAltitude(float configuredMetres, float dropMetres)
        {
            return Math.Max(EffectiveDropRadarAltitude(dropMetres) + 300f, configuredMetres);
        }

        public static bool ShouldPreferTerrainCandidate(float candidateMaximumSlope,
            float currentMaximumSlope)
        {
            return candidateMaximumSlope >= 0f
                && candidateMaximumSlope < currentMaximumSlope;
        }

        public static float EffectiveReleaseLeadDistance(float configuredMetres)
        {
            return Math.Min(4000f, Math.Max(400f, configuredMetres));
        }

        public static bool ShouldStartDropProfile(float distanceToReleaseLine,
            float configuredDescentDistance)
        {
            return distanceToReleaseLine >= 0f
                && distanceToReleaseLine <= Math.Max(1000f, configuredDescentDistance);
        }

        public static float EffectiveLeaderReleaseRadius(float configuredMetres)
        {
            return Math.Min(1200f, Math.Max(600f, configuredMetres));
        }

        public static float EffectiveAltitudeTolerance(float configuredMetres)
        {
            return Math.Max(200f, configuredMetres);
        }

        public static float EffectiveFormationSpawnInterval(float configuredSeconds)
        {
            return Math.Min(2f, Math.Max(0.25f, configuredSeconds));
        }

        public static bool HasExactTransportJammerPodCount(int livePodCount)
        {
            return livePodCount == 2;
        }

        public static float EffectiveFormationLateralSpacing(float configuredMetres)
        {
            return Math.Min(250f, Math.Max(80f, configuredMetres));
        }

        public static bool SynchronizedReleaseReady(int readyAircraft, int expectedAircraft)
        {
            return expectedAircraft > 0 && readyAircraft == expectedAircraft;
        }

        public static bool SynchronizedCargoWaveComplete(
            IReadOnlyList<int> releasedCounts, IReadOnlyList<int> expectedCounts)
        {
            if (releasedCounts == null || expectedCounts == null
                || releasedCounts.Count == 0 || releasedCounts.Count != expectedCounts.Count)
                return false;
            for (int index = 0; index < releasedCounts.Count; index++)
                if (expectedCounts[index] <= 0 || releasedCounts[index] < expectedCounts[index])
                    return false;
            return true;
        }

        public static int RecoveryPairIndex(int recoverySequence)
        {
            return Math.Max(0, recoverySequence) / 2;
        }

        public static bool PrecisionReleaseReady(float timeDifference,
            float parachuteLeadTime, float lateTolerance, float alignmentDegrees,
            float maximumAlignmentDegrees)
        {
            if (float.IsNaN(timeDifference) || float.IsInfinity(timeDifference)
                || float.IsNaN(alignmentDegrees) || float.IsInfinity(alignmentDegrees))
                return false;
            float lead = Math.Min(15f, Math.Max(0f, parachuteLeadTime));
            float late = Math.Min(5f, Math.Max(0.25f, lateTolerance));
            float alignment = Math.Min(45f, Math.Max(5f, maximumAlignmentDegrees));
            return timeDifference <= lead && timeDifference > -late
                && alignmentDegrees < alignment;
        }

        public static bool PrecisionReleaseMissed(float timeDifference,
            float lateTolerance)
        {
            if (float.IsNaN(timeDifference) || float.IsInfinity(timeDifference))
                return false;
            float late = Math.Min(5f, Math.Max(0.25f, lateTolerance));
            return timeDifference <= -late;
        }

        public static bool CargoSequenceMatchesRequestedOrder(IReadOnlyList<int> assignmentIndices)
        {
            if (assignmentIndices == null || assignmentIndices.Count == 0) return false;
            for (int index = 0; index < assignmentIndices.Count; index++)
                if (assignmentIndices[index] != index) return false;
            return true;
        }

        public static bool WithinSharedReleaseGate(
            float alongTrackDistance,
            float crossTrackDistance)
        {
            return Math.Abs(alongTrackDistance) <= 40f
                && crossTrackDistance >= 0f
                && crossTrackDistance <= 40f;
        }

        public static bool TryOrderCargoAssignments(
            IReadOnlyList<string> liveCargoKeys,
            IReadOnlyList<string> expectedAssignmentKeys,
            out int[] assignmentIndices)
        {
            assignmentIndices = null;
            if (liveCargoKeys == null || expectedAssignmentKeys == null
                || liveCargoKeys.Count == 0
                || liveCargoKeys.Count != expectedAssignmentKeys.Count)
                return false;

            var used = new bool[expectedAssignmentKeys.Count];
            var ordered = new int[liveCargoKeys.Count];
            for (int liveIndex = 0; liveIndex < liveCargoKeys.Count; liveIndex++)
            {
                string liveKey = liveCargoKeys[liveIndex] ?? string.Empty;
                int match = -1;
                for (int expectedIndex = 0; expectedIndex < expectedAssignmentKeys.Count; expectedIndex++)
                {
                    if (!used[expectedIndex]
                        && string.Equals(liveKey, expectedAssignmentKeys[expectedIndex] ?? string.Empty,
                            StringComparison.Ordinal))
                    {
                        match = expectedIndex;
                        break;
                    }
                }
                if (match < 0) return false;
                used[match] = true;
                ordered[liveIndex] = match;
            }

            assignmentIndices = ordered;
            return true;
        }

        public static bool IsAuthorized(ulong actualSteamId, ulong configuredSteamId)
        {
            return configuredSteamId != 0UL && actualSteamId == configuredSteamId;
        }

        public static bool CanRun(bool isServer, bool missionRunning, bool isBatchMode, bool allowListenServer)
        {
            return isServer && missionRunning && (isBatchMode || allowListenServer);
        }

        public static bool TryParsePostDrop(string value, out PostDropBehavior behavior)
        {
            return Enum.TryParse(value ?? string.Empty, true, out behavior);
        }

        public static bool RecoveryAllowed(int releaseCursor, int assignmentCount,
            int deployedCargoCount, int expectedCargoCount)
        {
            return BatteryReleaseComplete(releaseCursor, assignmentCount)
                && deployedCargoCount >= Math.Max(0, expectedCargoCount);
        }

        public static string ValidateZone(ZoneFacts facts, float maximumSlopeDegrees)
        {
            if (facts == null) return "zone facts were not supplied";
            if (!facts.MapMatches) return "configured map does not match the running map";
            if (!facts.InMapBounds) return "zone is outside the loaded map bounds";
            if (!facts.TerrainFound) return "terrain was not found below the configured point";
            if (!facts.AboveWater) return "zone is at or below the protected sea-level margin";
            if (facts.SlopeDegrees > Math.Max(0f, maximumSlopeDegrees))
                return "terrain slope exceeds the configured limit";
            if (facts.Obstructed) return "zone clearance volume is obstructed";
            if (!facts.FactionValid) return "requesting faction is not valid for this zone";
            if (facts.HostileNearby) return "a hostile unit is inside the configured separation radius";
            if (!facts.NativeZoneClear) return "the faction has another active native drop zone nearby";
            return null;
        }

        public static bool WithinReleaseWindow(
            float horizontalDistance,
            float releaseRadius,
            float radarAltitude,
            float targetAltitude,
            float altitudeTolerance,
            float speed,
            float minimumSpeed,
            float maximumSpeed,
            float absoluteRoll,
            float maximumAbsoluteRoll,
            float absoluteVerticalSpeed,
            float maximumAbsoluteVerticalSpeed)
        {
            return horizontalDistance <= Math.Max(1f, releaseRadius)
                && Math.Abs(radarAltitude - targetAltitude) <= Math.Max(0f, altitudeTolerance)
                && speed >= Math.Max(0f, minimumSpeed)
                && speed <= Math.Max(minimumSpeed, maximumSpeed)
                && absoluteRoll <= Math.Max(0f, maximumAbsoluteRoll)
                && absoluteVerticalSpeed <= Math.Max(0f, maximumAbsoluteVerticalSpeed);
        }

        public static bool ReleaseDue(int releasedCount, int expectedCount, float now, float nextReleaseAt)
        {
            return releasedCount < expectedCount && now >= nextReleaseAt;
        }

        public static float HorizontalDistance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }

        public static string ValidateR9RadarSpacing(float distanceMetres, float maximumDistanceMetres)
        {
            float maximum = Math.Min(17000f, Math.Max(0f, maximumDistanceMetres));
            if (maximum <= 0f) return "R9/radar maximum distance must be greater than zero";
            if (distanceMetres < 0f) return "R9/radar distance cannot be negative";
            if (distanceMetres > maximum)
                return "R9 is outside the maximum radar-link distance";
            return null;
        }

        public static bool BatteryReleaseComplete(int cursor, int assignmentCount)
        {
            return assignmentCount > 0 && cursor >= assignmentCount;
        }

        public static float EffectiveEgressTurnDegrees(float configuredDegrees)
        {
            return Math.Min(60f, Math.Max(-60f, configuredDegrees));
        }

        public static int EffectiveFlareBurstCount(int configuredCount)
        {
            return Math.Min(20, Math.Max(1, configuredCount));
        }

        public static float EffectiveFlareBurstInterval(float configuredSeconds)
        {
            return Math.Min(2f, Math.Max(0.2f, configuredSeconds));
        }

        public static bool AirportCaptureDetected(bool baselineKnown,
            bool ownerChanged, bool hasNewOwner)
        {
            return baselineKnown && ownerChanged && hasNewOwner;
        }

        public static bool AirportCaptureCooldownElapsed(float now, float cooldownUntil)
        {
            return now >= cooldownUntil;
        }

        public static bool AutomaticMapHasRecordedZones(string runningMapIdentity,
            IEnumerable<string> recordedMapIdentities)
        {
            if (string.IsNullOrWhiteSpace(runningMapIdentity)
                || recordedMapIdentities == null)
                return false;
            return recordedMapIdentities.Any(recordedMapIdentity =>
                ZoneMapMatches(recordedMapIdentity, runningMapIdentity));
        }

        public static bool UseOffMapSpawn(bool automaticStart, bool dedicatedServer)
        {
            return automaticStart || dedicatedServer;
        }

        public static bool OffMapReturnReached(float horizontalDistance,
            float configuredArrivalRadius)
        {
            float radius = Math.Min(1200f, Math.Max(200f, configuredArrivalRadius));
            return horizontalDistance >= 0f && horizontalDistance <= radius;
        }

        public static bool TryNormalizeOwnerFaction(string value, out string factionKey)
        {
            factionKey = CanonicalFactionKey(value);
            return factionKey == "BDF" || factionKey == "PALA";
        }

        public static bool TryParseOwnerWaveSelection(string value,
            out string airportSelector, out string dropSelector)
        {
            airportSelector = null;
            dropSelector = null;
            string[] tokens = (value ?? string.Empty).Trim().Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) return false;
            string requestedDrop = tokens[tokens.Length - 1];
            int sequence;
            bool validDrop = requestedDrop.Equals("random",
                    StringComparison.OrdinalIgnoreCase)
                || (int.TryParse(requestedDrop, out sequence) && sequence > 0);
            if (!validDrop) return false;
            string requestedAirport = string.Join(" ", tokens, 0, tokens.Length - 1).Trim();
            if (requestedAirport.Length == 0) return false;
            airportSelector = requestedAirport;
            dropSelector = requestedDrop.ToLowerInvariant();
            return true;
        }

        public static string CaptureAirliftAnnouncement(string factionName)
        {
            string displayName = (factionName ?? string.Empty).Trim();
            if (displayName.Length == 0) displayName = "an unknown faction";
            return "An airbase has been captured by " + displayName
                + ", an airdropped AA battery will arrive soon.";
        }

        public static float RequiredOffMapSpawnDistance(float releaseX, float releaseZ,
            float inboundX, float inboundZ, float halfMapX, float halfMapZ,
            float margin, float configuredDistance)
        {
            float configured = Math.Max(1000f, configuredDistance);
            float outboundX = -inboundX;
            float outboundZ = -inboundZ;
            float magnitude = (float)Math.Sqrt((outboundX * outboundX)
                + (outboundZ * outboundZ));
            if (magnitude < 0.001f) return Math.Max(configured, 15000f);
            outboundX /= magnitude;
            outboundZ /= magnitude;
            float edgeX = float.PositiveInfinity;
            float edgeZ = float.PositiveInfinity;
            float safeHalfX = Math.Max(2000f, halfMapX);
            float safeHalfZ = Math.Max(2000f, halfMapZ);
            float safeMargin = Math.Min(10000f, Math.Max(1000f, margin));
            if (outboundX > 0.001f)
                edgeX = ((safeHalfX + safeMargin) - releaseX) / outboundX;
            else if (outboundX < -0.001f)
                edgeX = ((-safeHalfX - safeMargin) - releaseX) / outboundX;
            if (outboundZ > 0.001f)
                edgeZ = ((safeHalfZ + safeMargin) - releaseZ) / outboundZ;
            else if (outboundZ < -0.001f)
                edgeZ = ((-safeHalfZ - safeMargin) - releaseZ) / outboundZ;
            float firstEdge = Math.Min(edgeX > 0f ? edgeX : float.PositiveInfinity,
                edgeZ > 0f ? edgeZ : float.PositiveInfinity);
            if (float.IsInfinity(firstEdge) || float.IsNaN(firstEdge))
                firstEdge = 15000f;
            return Math.Max(configured, firstEdge);
        }

        public static string CanonicalFactionKey(string value)
        {
            string key = (value ?? string.Empty).Trim();
            if (key.Equals("BDF", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Boscali", StringComparison.OrdinalIgnoreCase))
                return "BDF";
            if (key.Equals("PALA", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Primeva", StringComparison.OrdinalIgnoreCase))
                return "PALA";
            return key.ToUpperInvariant();
        }

        public static bool ZoneFactionMatches(string zoneFaction,
            string factionName, string factionTag)
        {
            string required = CanonicalFactionKey(zoneFaction);
            return required.Length > 0
                && (required == CanonicalFactionKey(factionName)
                    || required == CanonicalFactionKey(factionTag));
        }

        public static string CanonicalMapKey(string value)
        {
            string key = (value ?? string.Empty).Trim();
            if (key.IndexOf("Terrain_naval", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("Ignus", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("Ignis", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Terrain_naval";
            if (key.IndexOf("Terrain1", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("Heartland", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Terrain1";
            return key;
        }

        public static bool ZoneMapMatches(string zoneMapKey, string runningMapIdentity)
        {
            string zone = CanonicalMapKey(zoneMapKey);
            string running = CanonicalMapKey(runningMapIdentity);
            return zone.Length > 0 && running.Length > 0
                && zone.Equals(running, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGlobalCoordinateSpace(string value)
        {
            return string.Equals((value ?? string.Empty).Trim(), "GLOBAL",
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsApproachHeadingConvention(string value)
        {
            return string.Equals((value ?? string.Empty).Trim(), "APPROACH_HEADING",
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseDynamicZoneSelector(string selector, out string airbaseSelector)
        {
            airbaseSelector = null;
            string[] tokens = (selector ?? string.Empty).Trim().Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2
                || !tokens[tokens.Length - 1].Equals("random",
                    StringComparison.OrdinalIgnoreCase))
                return false;
            airbaseSelector = string.Join(" ", tokens, 0, tokens.Length - 1).Trim();
            return airbaseSelector.Length > 0;
        }

        public static bool ZoneSelectorMatches(string zoneName, string airbaseName,
            string selector)
        {
            string query = (selector ?? string.Empty).Trim();
            if (query.Length == 0) return true;
            string compactQuery = CompactZoneText(query);
            if (compactQuery.Length == 0) return false;
            if (compactQuery == CompactZoneText(zoneName)) return true;

            string[] tokens = query.Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            int requestedSequence = 0;
            bool hasSequence = tokens.Length > 1
                && int.TryParse(tokens[tokens.Length - 1],
                    out requestedSequence) && requestedSequence > 0;
            string requestedBase = hasSequence
                ? string.Join(" ", tokens, 0, tokens.Length - 1)
                : query;
            string compactBase = CompactZoneText(requestedBase);
            string compactAirbase = CompactZoneText(airbaseName);
            string canonicalBase = CanonicalAirbaseKey(requestedBase);
            string canonicalAirbase = CanonicalAirbaseKey(airbaseName);
            bool airbaseMatches = compactBase.Length > 0
                && (compactAirbase.Contains(compactBase)
                    || compactBase.Contains(compactAirbase)
                    || (canonicalBase.Length > 0
                        && canonicalBase == canonicalAirbase));
            if (!airbaseMatches) return false;
            if (!hasSequence) return true;

            int zoneSequence;
            return TryReadZoneSequence(zoneName, out zoneSequence)
                && zoneSequence == requestedSequence;
        }

        private static string CompactZoneText(string value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
        }

        public static string CanonicalAirbaseKey(string value)
        {
            string[] descriptors = {
                "airport", "airbase", "airfield", "highway", "strip",
                "general", "aviation", "fob", "base"
            };
            var descriptorSet = new HashSet<string>(descriptors,
                StringComparer.OrdinalIgnoreCase);
            string[] tokens = (value ?? string.Empty)
                .Split(new[] { ' ', '-', '_', '/', '\\' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !descriptorSet.Contains(token))
                .ToArray();
            return CompactZoneText(string.Join(" ", tokens));
        }

        private static bool TryReadZoneSequence(string zoneName, out int sequence)
        {
            sequence = 0;
            string name = zoneName ?? string.Empty;
            int marker = name.LastIndexOf("-LZ-", StringComparison.OrdinalIgnoreCase);
            return marker >= 0 && int.TryParse(name.Substring(marker + 4), out sequence)
                && sequence > 0;
        }

        public static bool MapKeyMatches(string runningMapKey, string configuredMapKey)
        {
            string configured = (configuredMapKey ?? string.Empty).Trim();
            if (configured.Equals("Auto", StringComparison.OrdinalIgnoreCase) || configured == "*")
                return true;
            return ZoneMapMatches(configured, runningMapKey);
        }

        public static float EffectiveCombatDropAltitude(float configured)
        {
            return Math.Max(2f, Math.Min(5f, configured));
        }

        public static float EffectiveCombatPurchaseCost(float configured)
        {
            return Math.Max(1f, Math.Min(1000f, configured));
        }

        public static float EffectiveCombatPurchaseCooldown(float configured)
        {
            return Math.Max(300f, Math.Min(3600f, configured));
        }

        public static float CombatPurchaseCooldownRemaining(float now,
            float cooldownUntil)
        {
            return Math.Max(0f, cooldownUntil - now);
        }

        public static bool CombatPurchaseTargetIsEnemy(bool hasOwner,
            bool ownerHasFaction, bool ownerIsPurchaserFaction)
        {
            return hasOwner && ownerHasFaction && !ownerIsPurchaserFaction;
        }

        public static bool RunwaySuppressionCorridorContains(float alongDistance,
            float crossDistance, float runwayHalfLength, float runwayHalfWidth,
            float alongPadding = 250f, float crossPadding = 30f)
        {
            return Math.Abs(alongDistance) <= Math.Max(0f, runwayHalfLength)
                    + Math.Max(0f, alongPadding)
                && Math.Abs(crossDistance) <= Math.Max(0f, runwayHalfWidth)
                    + Math.Max(0f, crossPadding);
        }

        public static bool RunwayAircraftSuppressionEligible(
            float heightAboveRunway, float maximumHeight = 35f)
        {
            return heightAboveRunway >= -15f
                && heightAboveRunway <= Math.Max(5f, maximumHeight);
        }

        public static bool CombatRunwayEligible(bool hasEndpoints, bool operational,
            bool level, float length, float width, float minimumLength,
            float minimumWidth)
        {
            return hasEndpoints && operational && level
                && length >= Math.Max(600f, minimumLength)
                && width >= Math.Max(8f, minimumWidth);
        }

        public static float EffectiveCombatTrailSpacing(float configuredSpacing,
            float runwayLength, float endMargin = 130f)
        {
            float requested = Math.Max(80f, Math.Min(200f, configuredSpacing));
            float available = Math.Max(0f,
                ((Math.Max(0f, runwayLength) * 0.5f) - Math.Max(0f, endMargin))
                / 2f);
            return Math.Max(80f, Math.Min(requested, available));
        }

        public static float EffectiveRunwayDispersalDistance(float configured)
        {
            return Math.Max(80f, Math.Min(400f, configured));
        }

        public static float CombatTrailOffset(int index, int count, float spacing)
        {
            if (count <= 0 || index < 0 || index >= count) return 0f;
            float effectiveSpacing = Math.Max(80f, Math.Min(200f, spacing));
            return (index - ((count - 1) * 0.5f)) * effectiveSpacing;
        }
    }
}
