using KellysAirlift;
using System;
using System.Collections.Generic;

internal static class Program
{
    private static readonly List<string> Failures = new List<string>();
    private static int Checks;

    private static int Main()
    {
        Check("RAPID canonical command accepted",
            AirliftPolicy.IsRapidCommandToken("rapid"));
        Check("RAPID command is case insensitive",
            AirliftPolicy.IsRapidCommandToken("RAPID"));
        Check("legacy combat command remains accepted",
            AirliftPolicy.IsRapidCommandToken("combat"));
        Check("unrelated command rejected",
            !AirliftPolicy.IsRapidCommandToken("battery"));
        Check("routine operation phases stay out of public chat",
            !AirliftPolicy.ShouldBroadcastOperationMessage(true,
                AirliftAnnouncementKind.Internal));
        Check("launch milestone can be broadcast",
            AirliftPolicy.ShouldBroadcastOperationMessage(true,
                AirliftAnnouncementKind.Launch));
        Check("aircraft loss milestone can be broadcast",
            AirliftPolicy.ShouldBroadcastOperationMessage(true,
                AirliftAnnouncementKind.AircraftShotDown));
        Check("successful drop milestone can be broadcast",
            AirliftPolicy.ShouldBroadcastOperationMessage(true,
                AirliftAnnouncementKind.DropSucceeded));
        Check("milestone switch suppresses every public event",
            !AirliftPolicy.ShouldBroadcastOperationMessage(false,
                AirliftAnnouncementKind.AircraftShotDown));
        Check("owner accepted", AirliftPolicy.IsAuthorized(76561197999424821UL, 76561197999424821UL));
        Check("datum X rebase follows Nuclear Option global-plus-origin convention",
            AirliftPolicy.DatumRebaseDelta(-13248f, -13568f) == -320f);
        Check("datum Y rebase follows Nuclear Option global-plus-origin convention",
            AirliftPolicy.DatumRebaseDelta(-128f, -256f) == -128f);
        Check("datum Z rebase follows Nuclear Option global-plus-origin convention",
            AirliftPolicy.DatumRebaseDelta(-20992f, -19968f) == 1024f);
        Check("K92 remains globally anchored after the observed datum shift",
            8683.5f + AirliftPolicy.DatumRebaseDelta(-13248f, -13568f) - (-13568f)
                == 21931.5f);
        Check("non-owner rejected", !AirliftPolicy.IsAuthorized(1UL, 76561197999424821UL));
        Check("zero owner rejected", !AirliftPolicy.IsAuthorized(0UL, 0UL));
        Check("legacy rebuilt-mission Chimera alias normalized",
            AirliftPolicy.NormalizeChimeraDefinitionKey("Aryx_MC260_Chimera") == "Aryx_CargoPlane1");
        Check("native Chimera key preserved",
            AirliftPolicy.NormalizeChimeraDefinitionKey("Aryx_CargoPlane1") == "Aryx_CargoPlane1");
        Check("unrelated aircraft key preserved",
            AirliftPolicy.NormalizeChimeraDefinitionKey("Fighter1") == "Fighter1");
        Check("GroundVehicle cargo type accepted",
            AirliftPolicy.IsSupportedNativeCargoType(true, false));
        Check("Container cargo type accepted",
            AirliftPolicy.IsSupportedNativeCargoType(false, true));
        Check("unknown cargo type rejected",
            !AirliftPolicy.IsSupportedNativeCargoType(false, false));
        Check("transport drop altitude raised to 450 metres",
            AirliftPolicy.EffectiveDropRadarAltitude(260f) == 450f);
        Check("450 metre drop altitude accepted",
            AirliftPolicy.EffectiveDropRadarAltitude(450f) == 450f);
        Check("higher configured transport altitude preserved",
            AirliftPolicy.EffectiveDropRadarAltitude(1200f) == 1200f);
        Check("flatter terrain candidate preferred",
            AirliftPolicy.ShouldPreferTerrainCandidate(2f, 4f));
        Check("steeper terrain candidate not preferred",
            !AirliftPolicy.ShouldPreferTerrainCandidate(5f, 4f));
        Check("equal terrain candidate preserves first result",
            !AirliftPolicy.ShouldPreferTerrainCandidate(4f, 4f));
        Check("cruise floor remains 300 metres above a 450 metre drop",
            AirliftPolicy.EffectiveCruiseRadarAltitude(700f, 450f) == 750f);
        Check("cruise altitude remains above the drop profile",
            AirliftPolicy.EffectiveCruiseRadarAltitude(700f, 600f) == 900f);
        Check("configured 1200 metre cruise altitude accepted",
            AirliftPolicy.EffectiveCruiseRadarAltitude(1200f, 600f) == 1200f);
        Check("release lead has safe floor",
            AirliftPolicy.EffectiveReleaseLeadDistance(0f) == 400f);
        Check("1400 metre release lead accepted",
            AirliftPolicy.EffectiveReleaseLeadDistance(1400f) == 1400f);
        Check("release lead remains bounded",
            AirliftPolicy.EffectiveReleaseLeadDistance(9000f) == 4000f);
        Check("drop profile starts inside descent range",
            AirliftPolicy.ShouldStartDropProfile(5000f, 5000f));
        Check("drop profile waits outside descent range",
            !AirliftPolicy.ShouldStartDropProfile(5000.1f, 5000f));
        Check("leader release corridor enlarged",
            AirliftPolicy.EffectiveLeaderReleaseRadius(30f) == 600f);
        Check("leader release corridor accepts 750 metres",
            AirliftPolicy.EffectiveLeaderReleaseRadius(750f) == 750f);
        Check("leader release corridor remains bounded",
            AirliftPolicy.EffectiveLeaderReleaseRadius(2000f) == 1200f);
        Check("altitude envelope enlarged",
            AirliftPolicy.EffectiveAltitudeTolerance(60f) == 200f);
        Check("formation spawn interval has anti-hitch floor",
            AirliftPolicy.EffectiveFormationSpawnInterval(0f) == 0.25f);
        Check("formation spawn interval preserves staged default",
            AirliftPolicy.EffectiveFormationSpawnInterval(0.75f) == 0.75f);
        Check("formation spawn interval remains bounded",
            AirliftPolicy.EffectiveFormationSpawnInterval(5f) == 2f);
        Check("two live Chimera jammer pods accepted",
            AirliftPolicy.HasExactTransportJammerPodCount(2));
        Check("single prefab jammer template rejected as runtime station",
            !AirliftPolicy.HasExactTransportJammerPodCount(1));
        Check("unexpected extra Chimera jammer pod rejected",
            !AirliftPolicy.HasExactTransportJammerPodCount(3));
        Check("line-abreast spacing has collision floor",
            AirliftPolicy.EffectiveFormationLateralSpacing(20f) == 80f);
        Check("line-abreast spacing preserves 120 metre default",
            AirliftPolicy.EffectiveFormationLateralSpacing(120f) == 120f);
        Check("line-abreast spacing remains bounded",
            AirliftPolicy.EffectiveFormationLateralSpacing(500f) == 250f);
        Check("all four aircraft accepted at synchronized release barrier",
            AirliftPolicy.SynchronizedReleaseReady(4, 4));
        Check("partial readiness cannot start synchronized release",
            !AirliftPolicy.SynchronizedReleaseReady(3, 4));
        Check("three surviving aircraft may synchronize",
            AirliftPolicy.SynchronizedReleaseReady(3, 3));
        Check("zero-aircraft release is rejected",
            !AirliftPolicy.SynchronizedReleaseReady(0, 0));
        Check("mixed x1/x4 cargo wave waits for all pallet rounds",
            !AirliftPolicy.SynchronizedCargoWaveComplete(
                new[] { 3, 1, 1, 1 }, new[] { 4, 1, 1, 1 }));
        Check("mixed x1/x4 cargo wave completes after all pallet rounds",
            AirliftPolicy.SynchronizedCargoWaveComplete(
                new[] { 4, 1, 1, 1 }, new[] { 4, 1, 1, 1 }));
        Check("empty cargo wave is rejected",
            !AirliftPolicy.SynchronizedCargoWaveComplete(
                new int[0], new int[0]));
        Check("precision predictor waits before parachute lead window",
            !AirliftPolicy.PrecisionReleaseReady(7.6f, 7.5f, 1.5f, 2f, 25f));
        Check("precision predictor releases at parachute lead boundary",
            AirliftPolicy.PrecisionReleaseReady(7.5f, 7.5f, 1.5f, 2f, 25f));
        Check("precision predictor accepts slightly late release",
            AirliftPolicy.PrecisionReleaseReady(-1.4f, 7.5f, 1.5f, 2f, 25f));
        Check("precision predictor rejects missed release window",
            !AirliftPolicy.PrecisionReleaseReady(-1.5f, 7.5f, 1.5f, 2f, 25f));
        Check("precision predictor identifies missed window",
            AirliftPolicy.PrecisionReleaseMissed(-1.5f, 1.5f));
        Check("precision predictor rejects poor alignment",
            !AirliftPolicy.PrecisionReleaseReady(3f, 7.5f, 1.5f, 25f, 25f));
        Check("requested cargo order accepted",
            AirliftPolicy.CargoSequenceMatchesRequestedOrder(new[] { 0, 1 }));
        Check("reversed cargo order rejected",
            !AirliftPolicy.CargoSequenceMatchesRequestedOrder(new[] { 1, 0 }));
        Check("follower accepts ALPHA gate centre",
            AirliftPolicy.WithinSharedReleaseGate(0f, 0f));
        Check("follower accepts near shared gate",
            AirliftPolicy.WithinSharedReleaseGate(39.9f, 39.9f));
        Check("follower rejects early release away from ALPHA",
            !AirliftPolicy.WithinSharedReleaseGate(41f, 0f));
        Check("follower rejects lateral miss from ALPHA",
            !AirliftPolicy.WithinSharedReleaseGate(0f, 41f));
        int[] cargoOrder;
        Check("shared mixed cargo station follows native sequence",
            AirliftPolicy.TryOrderCargoAssignments(
                new[] { "MunitionsContainer1", "Aryx_RadarContainer1" },
                new[] { "Aryx_RadarContainer1", "MunitionsContainer1" },
                out cargoOrder)
            && cargoOrder[0] == 1 && cargoOrder[1] == 0);
        Check("shared duplicate cargo station maps both assignments",
            AirliftPolicy.TryOrderCargoAssignments(
                new[] { "Aryx_SLMMR_A3", "Aryx_SLMMR_A3" },
                new[] { "Aryx_SLMMR_A3", "Aryx_SLMMR_A3" },
                out cargoOrder)
            && cargoOrder[0] == 0 && cargoOrder[1] == 1);
        Check("missing shared cargo rejected",
            !AirliftPolicy.TryOrderCargoAssignments(
                new[] { "Aryx_RadarContainer1" },
                new[] { "Aryx_RadarContainer1", "MunitionsContainer1" },
                out cargoOrder));
        Check("unexpected shared cargo rejected",
            !AirliftPolicy.TryOrderCargoAssignments(
                new[] { "Aryx_RadarContainer1", "UnexpectedCargo" },
                new[] { "Aryx_RadarContainer1", "MunitionsContainer1" },
                out cargoOrder));
        Check("dedicated authority", AirliftPolicy.CanRun(true, true, true, false));
        Check("listen server opt-in", AirliftPolicy.CanRun(true, true, false, true));
        Check("client rejected", !AirliftPolicy.CanRun(false, true, true, true));
        Check("stopped mission rejected", !AirliftPolicy.CanRun(true, false, true, true));

        PostDropBehavior behavior;
        Check("parse return", AirliftPolicy.TryParsePostDrop("return", out behavior) && behavior == PostDropBehavior.Return);
        Check("parse land", AirliftPolicy.TryParsePostDrop("LAND", out behavior) && behavior == PostDropBehavior.Land);
        Check("parse despawn", AirliftPolicy.TryParsePostDrop("Despawn", out behavior) && behavior == PostDropBehavior.Despawn);
        Check("reject unknown postdrop", !AirliftPolicy.TryParsePostDrop("orbit", out behavior));

        ZoneFacts goodZone = ValidZone();
        Check("valid zone", AirliftPolicy.ValidateZone(goodZone, 5f) == null);
        ZoneFacts water = ValidZone();
        water.AboveWater = false;
        Check("water rejected", Contains(AirliftPolicy.ValidateZone(water, 5f), "sea-level"));
        ZoneFacts slope = ValidZone();
        slope.SlopeDegrees = 5.1f;
        Check("slope rejected", Contains(AirliftPolicy.ValidateZone(slope, 5f), "slope"));
        ZoneFacts sevenDegreeSlope = ValidZone();
        sevenDegreeSlope.SlopeDegrees = 7f;
        Check("seven degree slope accepted at new ceiling",
            AirliftPolicy.ValidateZone(sevenDegreeSlope, 7f) == null);
        sevenDegreeSlope.SlopeDegrees = 7.1f;
        Check("slope above seven degree ceiling rejected",
            Contains(AirliftPolicy.ValidateZone(sevenDegreeSlope, 7f), "slope"));
        ZoneFacts obstruction = ValidZone();
        obstruction.Obstructed = true;
        Check("obstruction rejected", Contains(AirliftPolicy.ValidateZone(obstruction, 5f), "obstructed"));
        ZoneFacts hostile = ValidZone();
        hostile.HostileNearby = true;
        Check("hostile rejected", Contains(AirliftPolicy.ValidateZone(hostile, 5f), "hostile"));
        ZoneFacts faction = ValidZone();
        faction.FactionValid = false;
        Check("faction rejected", Contains(AirliftPolicy.ValidateZone(faction, 5f), "faction"));
        ZoneFacts occupied = ValidZone();
        occupied.NativeZoneClear = false;
        Check("native zone reservation rejected", Contains(AirliftPolicy.ValidateZone(occupied, 5f), "native"));

        Check("release window accepted", AirliftPolicy.WithinReleaseWindow(
            200f, 650f, 260f, 260f, 90f, 145f, 75f, 190f, 4f, 18f, 3f, 30f));
        Check("release distance rejected", !AirliftPolicy.WithinReleaseWindow(
            651f, 650f, 260f, 260f, 90f, 145f, 75f, 190f, 4f, 18f, 3f, 30f));
        Check("release bank rejected", !AirliftPolicy.WithinReleaseWindow(
            200f, 650f, 260f, 260f, 90f, 145f, 75f, 190f, 18.1f, 18f, 3f, 30f));
        Check("first sequential release due", AirliftPolicy.ReleaseDue(0, 2, 10f, 10f));
        Check("interval enforced", !AirliftPolicy.ReleaseDue(1, 2, 11f, 12.5f));
        Check("package completion stops release", !AirliftPolicy.ReleaseDue(2, 2, 20f, 0f));
        Check("close R9 placement accepted", AirliftPolicy.ValidateR9RadarSpacing(20f, 17000f) == null);
        Check("R9 inside 17 km coverage accepted",
            AirliftPolicy.ValidateR9RadarSpacing(16999f, 17000f) == null);
        Check("R9 beyond 17 km rejected",
            Contains(AirliftPolicy.ValidateR9RadarSpacing(17000.1f, 17000f), "outside"));
        Check("configured link cannot exceed 17 km hard cap",
            Contains(AirliftPolicy.ValidateR9RadarSpacing(17000.1f, 50000f), "outside"));
        Check("negative R9 distance rejected", Contains(AirliftPolicy.ValidateR9RadarSpacing(-1f, 20f), "negative"));
        Check("all assignments released", AirliftPolicy.BatteryReleaseComplete(2, 2));
        Check("pending assignment not complete", !AirliftPolicy.BatteryReleaseComplete(1, 2));
        Check("default coordinated egress banks right",
            AirliftPolicy.EffectiveEgressTurnDegrees(30f) == 30f);
        Check("egress right bank is capped",
            AirliftPolicy.EffectiveEgressTurnDegrees(90f) == 60f);
        Check("egress left bank is capped",
            AirliftPolicy.EffectiveEgressTurnDegrees(-90f) == -60f);
        Check("ten flare pulses accepted",
            AirliftPolicy.EffectiveFlareBurstCount(10) == 10);
        Check("flare pulse count has safe floor",
            AirliftPolicy.EffectiveFlareBurstCount(0) == 1);
        Check("flare pulse count is capped",
            AirliftPolicy.EffectiveFlareBurstCount(100) == 20);
        Check("half-second flare spacing accepted",
            Math.Abs(AirliftPolicy.EffectiveFlareBurstInterval(0.5f) - 0.5f) < 0.001f);
        Check("flare spacing has safe floor",
            Math.Abs(AirliftPolicy.EffectiveFlareBurstInterval(0.01f) - 0.2f) < 0.001f);
        Check("initial airport ownership snapshot does not trigger",
            !AirliftPolicy.AirportCaptureDetected(false, true, true));
        Check("unchanged airport owner does not trigger",
            !AirliftPolicy.AirportCaptureDetected(true, false, true));
        Check("neutralized airport does not trigger",
            !AirliftPolicy.AirportCaptureDetected(true, true, false));
        Check("new non-null airport owner triggers",
            AirliftPolicy.AirportCaptureDetected(true, true, true));
        Check("capture cooldown blocks before expiry",
            !AirliftPolicy.AirportCaptureCooldownElapsed(99f, 100f));
        Check("capture cooldown opens at expiry",
            AirliftPolicy.AirportCaptureCooldownElapsed(100f, 100f));
        Check("recorded map enables automatic director",
            AirliftPolicy.AutomaticMapHasRecordedZones("Terrain1",
                new[] { "Terrain1", "Terrain2" }));
        Check("unrecorded map disables automatic director",
            !AirliftPolicy.AutomaticMapHasRecordedZones("Terrain3",
                new[] { "Terrain1", "Terrain2" }));
        Check("unknown map disables automatic director",
            !AirliftPolicy.AutomaticMapHasRecordedZones(" ", new[] { "Terrain1" }));
        Check("missing zone catalogue disables automatic director",
            !AirliftPolicy.AutomaticMapHasRecordedZones("Terrain1", null));
        Check("automatic capture uses off-map spawn",
            AirliftPolicy.UseOffMapSpawn(true, false));
        Check("dedicated manual command uses off-map spawn",
            AirliftPolicy.UseOffMapSpawn(false, true));
        Check("dedicated automatic capture uses off-map spawn",
            AirliftPolicy.UseOffMapSpawn(true, true));
        Check("local manual test retains close spawn",
            !AirliftPolicy.UseOffMapSpawn(false, false));
        Check("off-map return completes inside configured radius",
            AirliftPolicy.OffMapReturnReached(599f, 600f));
        Check("off-map return waits outside configured radius",
            !AirliftPolicy.OffMapReturnReached(601f, 600f));
        Check("off-map return radius has safe floor",
            AirliftPolicy.OffMapReturnReached(200f, 10f));
        Check("off-map return radius is capped",
            !AirliftPolicy.OffMapReturnReached(1201f, 5000f));
        Check("invalid negative off-map distance is rejected",
            !AirliftPolicy.OffMapReturnReached(-1f, 600f));
        string ownerFaction;
        Check("owner wave accepts BDF faction",
            AirliftPolicy.TryNormalizeOwnerFaction("bdf", out ownerFaction)
                && ownerFaction == "BDF");
        Check("owner wave accepts PALA faction",
            AirliftPolicy.TryNormalizeOwnerFaction(" PALA ", out ownerFaction)
                && ownerFaction == "PALA");
        Check("owner wave rejects unknown faction",
            !AirliftPolicy.TryNormalizeOwnerFaction("neutral", out ownerFaction));
        string waveAirport;
        string waveDrop;
        Check("owner wave parses a spaced airport and numbered zone",
            AirliftPolicy.TryParseOwnerWaveSelection("South Boscali General Aviation 2",
                out waveAirport, out waveDrop)
                && waveAirport == "South Boscali General Aviation" && waveDrop == "2");
        Check("owner wave parses a random airport selection",
            AirliftPolicy.TryParseOwnerWaveSelection("Dustbowl Highway Strip RANDOM",
                out waveAirport, out waveDrop)
                && waveAirport == "Dustbowl Highway Strip" && waveDrop == "random");
        Check("owner wave rejects a missing airport",
            !AirliftPolicy.TryParseOwnerWaveSelection("random", out waveAirport, out waveDrop));
        Check("owner wave rejects an invalid drop selection",
            !AirliftPolicy.TryParseOwnerWaveSelection("K92 Highway Strip zero",
                out waveAirport, out waveDrop));
        Check("capture airlift announcement names the faction",
            AirliftPolicy.CaptureAirliftAnnouncement("BDF")
                == "An airbase has been captured by BDF, an airdropped AA battery will arrive soon.");
        Check("capture airlift announcement has a safe faction fallback",
            AirliftPolicy.CaptureAirliftAnnouncement("  ")
                == "An airbase has been captured by an unknown faction, an airdropped AA battery will arrive soon.");
        Check("eastbound route spawns west of map with margin",
            Math.Abs(AirliftPolicy.RequiredOffMapSpawnDistance(0f, 0f, 1f, 0f,
                20000f, 15000f, 3000f, 6000f) - 23000f) < 0.01f);
        Check("northbound route spawns south of map with margin",
            Math.Abs(AirliftPolicy.RequiredOffMapSpawnDistance(0f, 1000f, 0f, 1f,
                20000f, 15000f, 3000f, 6000f) - 19000f) < 0.01f);
        Check("diagonal route uses first crossed map edge",
            AirliftPolicy.RequiredOffMapSpawnDistance(0f, 0f, 1f, 1f,
                20000f, 15000f, 3000f, 6000f) > 25000f);
        Check("configured spawn distance remains a floor",
            AirliftPolicy.RequiredOffMapSpawnDistance(0f, 0f, 1f, 0f,
                2000f, 2000f, 1000f, 9000f) == 9000f);
        Check("zero inbound vector uses safe off-map fallback",
            AirliftPolicy.RequiredOffMapSpawnDistance(0f, 0f, 0f, 0f,
                20000f, 15000f, 3000f, 6000f) == 15000f);
        Check("off-map margin is clamped to ten kilometres",
            Math.Abs(AirliftPolicy.RequiredOffMapSpawnDistance(0f, 0f, 1f, 0f,
                20000f, 15000f, 50000f, 6000f) - 30000f) < 0.01f);
        Check("exact Heartland map key accepted",
            AirliftPolicy.MapKeyMatches("Terrain1", "Terrain1"));
        Check("exact Ignus map key accepted",
            AirliftPolicy.MapKeyMatches("Terrain_naval", "Terrain_naval"));
        Check("auto map accepts Heartland",
            AirliftPolicy.MapKeyMatches("Terrain1", "Auto"));
        Check("auto map accepts Ignus",
            AirliftPolicy.MapKeyMatches("Terrain_naval", "Auto"));
        Check("auto map accepts legacy empty runtime key",
            AirliftPolicy.MapKeyMatches("", "Auto"));
        Check("wrong explicit map rejected",
            !AirliftPolicy.MapKeyMatches("Terrain_naval", "Terrain1"));
        Check("Heartland display name canonicalized",
            AirliftPolicy.CanonicalMapKey("Heartland-40000x40000") == "Terrain1");
        Check("Terrain1 path canonicalized",
            AirliftPolicy.CanonicalMapKey("Maps/Terrain1") == "Terrain1");
        Check("Ignus display name canonicalized",
            AirliftPolicy.CanonicalMapKey("Ignus-50000x50000") == "Terrain_naval");
        Check("Heartland alias matches Terrain1 zone",
            AirliftPolicy.ZoneMapMatches("Terrain1", "Heartland-40000x40000"));
        Check("Ignus does not match Heartland",
            !AirliftPolicy.ZoneMapMatches("Terrain1", "Ignus-50000x50000"));
        Check("Boscali canonicalized to BDF",
            AirliftPolicy.CanonicalFactionKey("Boscali") == "BDF");
        Check("BDF tag remains BDF",
            AirliftPolicy.CanonicalFactionKey("BDF") == "BDF");
        Check("Primeva canonicalized to PALA",
            AirliftPolicy.CanonicalFactionKey("Primeva") == "PALA");
        Check("PALA tag remains PALA",
            AirliftPolicy.CanonicalFactionKey("PALA") == "PALA");
        Check("BDF zone matches Boscali faction",
            AirliftPolicy.ZoneFactionMatches("BDF", "Boscali", ""));
        Check("BDF zone matches BDF tag",
            AirliftPolicy.ZoneFactionMatches("BDF", "", "BDF"));
        Check("BDF zone rejects PALA faction",
            !AirliftPolicy.ZoneFactionMatches("BDF", "Primeva", "PALA"));
        Check("PALA zone matches Primeva faction",
            AirliftPolicy.ZoneFactionMatches("PALA", "Primeva", ""));
        Check("unscoped legacy zone rejected",
            !AirliftPolicy.ZoneFactionMatches("", "Boscali", "BDF"));
        Check("Dustbowl numbered selector resolves exact row",
            AirliftPolicy.ZoneSelectorMatches("Dustbowl Strip-BDF-LZ-01",
                "Dustbowl Strip", "dustbowl 1"));
        Check("Dustbowl selector rejects another sequence",
            !AirliftPolicy.ZoneSelectorMatches("Dustbowl Strip-BDF-LZ-02",
                "Dustbowl Strip", "dustbowl 1"));
        Check("K92 numbered selector preserves number in airbase name",
            AirliftPolicy.ZoneSelectorMatches("K92 Highway Strip-PALA-LZ-03",
                "K92 Highway Strip", "k92 3"));
        Check("South Boscali shorthand resolves",
            AirliftPolicy.ZoneSelectorMatches(
                "South Boscali General Aviation-BDF-LZ-02",
                "South Boscali General Aviation", "southboscali 2"));
        Check("exact generated zone name resolves",
            AirliftPolicy.ZoneSelectorMatches(
                "South Boscali General Aviation-BDF-LZ-02",
                "South Boscali General Aviation",
                "South Boscali General Aviation-BDF-LZ-02"));
        Check("wrong airbase selector rejected",
            !AirliftPolicy.ZoneSelectorMatches("Dustbowl Strip-BDF-LZ-01",
                "Dustbowl Strip", "k92 1"));
        Check("airbase-only selector matches for ambiguity detection",
            AirliftPolicy.ZoneSelectorMatches("Dustbowl Strip-BDF-LZ-03",
                "Dustbowl Strip", "dustbowl"));
        Check("recorded Dustbowl name matches runtime highway name",
            AirliftPolicy.ZoneSelectorMatches("Dustbowl Strip-BDF-LZ-01",
                "Dustbowl Strip", "Dustbowl Highway Strip"));
        Check("runtime Dustbowl name matches recorded highway name",
            AirliftPolicy.ZoneSelectorMatches("Dustbowl Highway Strip-BDF-LZ-01",
                "Dustbowl Highway Strip", "Dustbowl Strip"));
        Check("airport descriptors canonicalize without changing identity",
            AirliftPolicy.CanonicalAirbaseKey("South Boscali General Aviation")
                == "southboscali");
        Check("FOB descriptor canonicalizes without changing identity",
            AirliftPolicy.CanonicalAirbaseKey("Island FOB") == "island");
        Check("unrelated airports remain distinct after canonicalization",
            AirliftPolicy.CanonicalAirbaseKey("Island FOB")
                != AirliftPolicy.CanonicalAirbaseKey("Dustbowl Highway Strip"));
        Check("GLOBAL coordinate-space marker accepted",
            AirliftPolicy.IsGlobalCoordinateSpace("GLOBAL"));
        Check("GLOBAL coordinate-space marker is case-insensitive",
            AirliftPolicy.IsGlobalCoordinateSpace(" global "));
        Check("LOCAL coordinate-space marker rejected",
            !AirliftPolicy.IsGlobalCoordinateSpace("LOCAL"));
        Check("missing coordinate-space marker rejected",
            !AirliftPolicy.IsGlobalCoordinateSpace(""));
        Check("approach-heading convention accepted",
            AirliftPolicy.IsApproachHeadingConvention("APPROACH_HEADING"));
        Check("approach-heading convention is case-insensitive",
            AirliftPolicy.IsApproachHeadingConvention(" approach_heading "));
        Check("obsolete spawn-bearing convention rejected",
            !AirliftPolicy.IsApproachHeadingConvention("SPAWN_BEARING"));
        Check("K92 dynamic selector parsed",
            AirliftPolicy.TryParseDynamicZoneSelector("k92 random", out string k92Selector)
                && k92Selector == "k92");
        Check("Dustbowl dynamic selector parsed",
            AirliftPolicy.TryParseDynamicZoneSelector("dustbowl random",
                out string dustbowlSelector) && dustbowlSelector == "dustbowl");
        Check("numbered selector is not dynamic",
            !AirliftPolicy.TryParseDynamicZoneSelector("k92 2", out _));
        Check("bare random selector is rejected",
            !AirliftPolicy.TryParseDynamicZoneSelector("random", out _));
        Check("recovery allowed after every package is confirmed",
            AirliftPolicy.RecoveryAllowed(2, 2, 2, 2));
        Check("recovery forbidden before every release command",
            !AirliftPolicy.RecoveryAllowed(1, 2, 2, 2));
        Check("recovery forbidden before cargo spawn confirmation",
            !AirliftPolicy.RecoveryAllowed(2, 2, 1, 2));
        Check("ALPHA and BRAVO form recovery pair zero",
            AirliftPolicy.RecoveryPairIndex(0) == 0
                && AirliftPolicy.RecoveryPairIndex(1) == 0);
        Check("CHARLIE and DELTA form recovery pair one",
            AirliftPolicy.RecoveryPairIndex(2) == 1
                && AirliftPolicy.RecoveryPairIndex(3) == 1);
        Check("combat drop altitude is hard capped at five metres",
            AirliftPolicy.EffectiveCombatDropAltitude(25f) == 5f);
        Check("combat drop altitude retains safe five metre setting",
            AirliftPolicy.EffectiveCombatDropAltitude(5f) == 5f);
        Check("combat drop altitude has two metre lower clamp",
            AirliftPolicy.EffectiveCombatDropAltitude(0f) == 2f);
        Check("level operational 1200 by 25 runway accepted",
            AirliftPolicy.CombatRunwayEligible(true, true, true,
                1200f, 25f, 1200f, 25f));
        Check("short combat runway rejected",
            !AirliftPolicy.CombatRunwayEligible(true, true, true,
                1199f, 25f, 1200f, 25f));
        Check("non-level combat runway rejected",
            !AirliftPolicy.CombatRunwayEligible(true, true, false,
                2000f, 40f, 1200f, 25f));
        Check("compact highway strip accepted by fallback floor",
            AirliftPolicy.CombatRunwayEligible(true, true, true,
                700f, 10f, 600f, 8f));
        Check("runway below compact fallback rejected",
            !AirliftPolicy.CombatRunwayEligible(true, true, true,
                599f, 10f, 600f, 8f));
        Check("trail spacing compresses for compact runway",
            AirliftPolicy.EffectiveCombatTrailSpacing(140f, 700f) == 110f);
        Check("trail spacing retains configured value on long runway",
            AirliftPolicy.EffectiveCombatTrailSpacing(140f, 1200f) == 140f);
        Check("runway dispersal distance has safe floor",
            AirliftPolicy.EffectiveRunwayDispersalDistance(10f) == 80f);
        Check("runway dispersal distance keeps configured default",
            AirliftPolicy.EffectiveRunwayDispersalDistance(180f) == 180f);
        Check("runway dispersal distance has bounded ceiling",
            AirliftPolicy.EffectiveRunwayDispersalDistance(900f) == 400f);
        Check("five aircraft trail is symmetric",
            AirliftPolicy.CombatTrailOffset(0, 5, 140f) == -280f
                && AirliftPolicy.CombatTrailOffset(2, 5, 140f) == 0f
                && AirliftPolicy.CombatTrailOffset(4, 5, 140f) == 280f);
        Check("combat purchase cost has one-million-dollar minimum",
            AirliftPolicy.EffectiveCombatPurchaseCost(0f) == 1f);
        Check("combat purchase cost keeps configured four hundred million",
            AirliftPolicy.EffectiveCombatPurchaseCost(400f) == 400f);
        Check("combat purchase cost has one-billion-dollar maximum",
            AirliftPolicy.EffectiveCombatPurchaseCost(2000f) == 1000f);
        Check("combat purchase cooldown enforces five minutes",
            AirliftPolicy.EffectiveCombatPurchaseCooldown(60f) == 300f);
        Check("combat purchase cooldown keeps five minutes",
            AirliftPolicy.EffectiveCombatPurchaseCooldown(300f) == 300f);
        Check("combat purchase cooldown remaining never negative",
            AirliftPolicy.CombatPurchaseCooldownRemaining(500f, 400f) == 0f);
        Check("combat purchase cooldown reports remaining seconds",
            AirliftPolicy.CombatPurchaseCooldownRemaining(100f, 400f) == 300f);
        Check("enemy-held combat purchase target accepted",
            AirliftPolicy.CombatPurchaseTargetIsEnemy(true, true, false));
        Check("friendly combat purchase target rejected",
            !AirliftPolicy.CombatPurchaseTargetIsEnemy(true, true, true));
        Check("neutral combat purchase target rejected",
            !AirliftPolicy.CombatPurchaseTargetIsEnemy(false, false, false));
        Check("ground unit on runway is inside suppression corridor",
            AirliftPolicy.RunwaySuppressionCorridorContains(500f, 10f,
                600f, 15f));
        Check("ground unit beyond runway is outside suppression corridor",
            !AirliftPolicy.RunwaySuppressionCorridorContains(900f, 10f,
                600f, 15f));
        Check("ground unit far beside runway is outside suppression corridor",
            !AirliftPolicy.RunwaySuppressionCorridorContains(0f, 60f,
                600f, 15f));
        Check("taxiing aircraft are eligible for runway suppression",
            AirliftPolicy.RunwayAircraftSuppressionEligible(3f));
        Check("aircraft in the takeoff roll remain eligible",
            AirliftPolicy.RunwayAircraftSuppressionEligible(30f));
        Check("departed aircraft are outside the suppression envelope",
            !AirliftPolicy.RunwayAircraftSuppressionEligible(36f));

        if (Failures.Count == 0)
        {
            Console.WriteLine("KellysAIRLIFT policy tests: PASS (" + Checks + " checks)");
            return 0;
        }
        foreach (string failure in Failures) Console.Error.WriteLine("FAIL: " + failure);
        return 1;
    }

    private static ZoneFacts ValidZone()
    {
        return new ZoneFacts {
            MapMatches = true,
            InMapBounds = true,
            TerrainFound = true,
            AboveWater = true,
            SlopeDegrees = 2f,
            Obstructed = false,
            FactionValid = true,
            HostileNearby = false,
            NativeZoneClear = true
        };
    }

    private static bool Contains(string value, string expected)
    {
        return value != null && value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void Check(string name, bool passed)
    {
        Checks++;
        if (!passed) Failures.Add(name);
    }
}
