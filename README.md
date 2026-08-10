# Kelly's AIRLIFT

Kelly's AIRLIFT 0.14.0 is a standalone BepInEx 5 plugin for Nuclear Option
0.34. It does not modify, reference, or depend on KellysHOUNDS. The Revoker
escort experiment has been removed. The normal AA operation uses four
MC-260 Chimeras; **RAPID (Runway Assault Package Insertion & Deployment)**
uses five for the combat-runway operation.

| Transport | Exact native cargo order |
|---|---|
| ALPHA | Small Munitions Pallet x4 rear, SLMMR-A3 front |
| BRAVO | Radar rear, R9 Mission Bay |
| CHARLIE | R9 Mission Bay, Small Munitions Pallet x4 front |
| DELTA | SLMMR-A3 rear, R9 Mission Bay |

Every AA-battery Chimera also carries the native `JammingPod1` Radar Jamming
Pod on its `Wing Pylons`. The manifest resolves the live Chimera definition, exact
hardpoints, cargo mounts, `MountedCargo`, jammer, bay doors, native parachute
systems, and faction loadout before the first aircraft is spawned.

### RAPID runway assault

The owner-only `/airlift rapid <BDF|PALA> <airbase> <runway-number|random>`
variant enumerates the running mission's real `Airbase.Runway` objects. It
prefers an operational, level runway at least 1,200 m by 25 m, then retries a
compact highway strip down to 600 m by 8 m with dynamically compressed trail
spacing. It chooses the runway direction that approaches from the nearest other
friendly airport and validates eight centreline targets against live terrain,
water, slope, physical runway obstructions, enemy ownership, and the native
drop-zone check.

| Combat transport | Ground-level cargo |
|---|---|
| ALPHA | Type-12 MBT, Mission Bay |
| BRAVO | Type-12 MBT, Mission Bay |
| CHARLIE | AFV6 IFV rear, AFV6 AA front |
| DELTA | AFV6 IFV rear, AFV6 AA front |
| ECHO | FRCV-105 LT rear and front |

Every RAPID Chimera replaces the jammer with the native three-round
`AGM-68` Wing Pylons load. During ingress, AIRLIFT uses one shared 2 Hz runway
snapshot to assign tracked hostile ground units inside the selected runway
corridor without sending multiple aircraft at the same target simultaneously.
Launches use the native `WeaponStation.LaunchMount` missile path and obey the
weapon's range, alignment, speed, line-of-sight, safety and `CombatAI`
opportunity checks for ground units. Hostile aircraft taxiing or taking off are
also eligible while they remain inside the runway corridor and no more than
35 m above runway elevation. The transports never chase targets or leave their
drop route.

Aircraft taking off or landing are ignored by combat-runway preflight and are
valid AGM-68 targets while they remain in that ground-level runway envelope.
Once an aircraft leaves the corridor or climbs above 35 m it is removed from
suppression targeting. Native autopilot collision handling remains active
during the low pass. Friendly or non-targetable units physically occupying a
cargo release point still reject the operation; hostile units are admitted as
suppression targets.

The design brief's “AFV6 IFC” is interpreted as the game's `AFV6 IFV`. The
FRCV key is deliberately not guessed: AIRLIFT requires exactly one live
Chimera cargo option whose displayed identity is `FRCV-105 LT`, or an explicit
configured key. The five aircraft fly in trail along the runway rather than
abreast, lower gear during the descent, and must be at or below 5 m radar
altitude before the two synchronized native cargo waves can begin. They raise
gear, climb promptly, egress together, and use the same cargo-safe off-map
extraction as the AA variant. RAPID should be proven
on each map/runway before public-server automation is considered.

As soon as each combat vehicle is both active and landed, AIRLIFT issues one
native `GroundVehicle.LeaveRoad()` order. The vehicle builds perpendicular
off-road waypoints and drives 180 m clear of the runway by default, using the
game's own obstruction, terrain, slope and water checks. This applies only to
the combat package; AA-battery cargo remains at its intended positions.

### Public Donate-menu purchase

The optional `KellysAIRLIFTPublicUI` client addon adds `RAPID Combat Drop` under
the native `Donate > Vehicles` list. Clicking it opens a dropdown of live
enemy-held airports. It displays a $400m default price and sends only the hidden
`/airlift purchase rapid <enemy-airport>` request. It has no spawning or
charging authority.

The server identifies the actual sender and independently checks faction,
allocation, active-operation state, map catalogue, hostile airport ownership,
runway, terrain and the complete manifest. The selected airport must still be
held by an opposing faction when the server accepts the request. Allocation is
charged only after preflight succeeds. A
five-minute cooldown is enforced per faction; rejected requests are free. If
formation assembly fails before all five aircraft launch, the purchase is
refunded and its cooldown is cleared. Once the full formation launches, later
losses are part of the purchase and are not refundable.

Every player who should see the button must install the small public UI addon;
an unmodified client cannot receive a new local menu control from a server-only
BepInEx plugin. Players can also type
`/airlift purchase rapid <enemy-airport>`; this does not bypass any server
check.

## Operation

Recorded zones live in
`BepInEx/config/kelly.nuclearoption.airlift.zones.tsv`. `/addzone` records the
global terrain point below the authorized owner's aircraft, its inbound
heading, current map, nearest friendly airport, and BDF/PALA ownership. Every
selected zone is revalidated for terrain, water, slope, obstruction, faction,
hostile separation, and native drop-zone availability.

The formation cruises at 1,200 m radar altitude, descends together to 450 m,
opens its cargo doors during ingress, and releases two synchronized cargo
waves through the native `WeaponStation.LaunchMount` / `MountedCargo.Fire` /
`Spawner.SpawnUnit` pipeline. Native parachutes and ground activation are not
reimplemented. At release, each survivor calls the game's explicit native
`CountermeasureManager.PopFlares()` helper ten times at 0.5-second intervals.
That selects flare station zero directly instead of trusting the previously
active countermeasure. All four transports and all fourteen deployed cargo units are
tracked. Actual radar-to-R9 touchdown distances are checked against the
published 17 km radar range; a miss is reported but never deletes the battery.

One Chimera loss affects only that aircraft and its unreleased cargo. Surviving
aircraft continue to the drop and recover normally. After any cargo release,
the operation timeout cannot clean away transports that are still recovering.
Explicit owner abort, mission change, plugin unload, and configured
empty-server cleanup remain authoritative.

## Off-map extraction

Production uses `PostDrop.Behavior = Return`. Before extracting, all survivors
preserve their parallel lanes and complete the same configurable 30-degree
right-bank egress. A destroyed aircraft is excluded from this barrier, and a
60-second fail-safe prevents a damaged survivor from holding the formation.

Each Chimera then climbs to cruise altitude and flies directly back to its own
original off-map spawn point. Reaching that point server-despawns only the
transport. Recovery is forbidden until all of that aircraft's native cargo
spawns are confirmed, and the deployed units are separate network objects in
the persistent cargo tracker; transport extraction never deletes them.

`PostDrop.Behavior = Land` remains available as an explicit alternative using
the accelerated native landing implementation. The legacy `Despawn` value is
now a compatibility alias for safe off-map extraction, not immediate deletion.

## Dedicated-server airport capture mode

The AIRLIFT plugin DLL is server-authoritative and does not need to be installed
as a client-side control plugin. The separate Chimera content must still be
available wherever Nuclear Option/Blueprinter requires that aircraft asset.

Automatic capture launches are disabled by default. Use the included
`examples/kelly.nuclearoption.airlift.server.cfg` as the production template.
With `EnableAirportCaptureAirlifts = true`, a dedicated server:

1. enables on any identified running map when validated dynamic fallback is
   configured, while still preferring its recorded-zone catalogue;
2. snapshots live `Airbase.CurrentHQ` ownership without launching anything;
3. checks ownership once per second;
4. detects a later capture by either BDF or PALA;
5. selects a random recorded zone matching the map, new faction, and airport,
   or searches safe live terrain around an uncatalogued airport;
6. queues one operation at a time, with an eight-request bound and per
   airport/faction cooldown that begins only after a dispatch starts;
7. extends the selected inbound line beyond the first map edge by
   `OffMapSpawnMargin`, then flies the Chimeras in at full configured speed;
8. announces the confirmed launch, each individual Chimera shot down, and the
   successful drop; all other operational phases remain in the server log.

`Messaging.AnnounceMilestonesInGame = true` enables that exact three-event
allowlist. Setting it false silences even those milestones. Manual command
responses remain private to the authorized administrator regardless.

Automatic launches do not use a chat sender or owner authorization. A live
player in the captured faction supplies only the faction/HQ context required
by the game's spawn and loadout APIs. A capture request expires if that context
does not become available within two minutes. Listen-server capture testing is
separately gated and remains off in the server template.

Automatic AA captures and authorized AA starts on a dedicated server use the
map-boundary calculation plus `OffMapSpawnMargin`. Every combat runway drop
uses that off-map calculation in every host mode, including public purchases
and owner commands on a listen server. `SpawnDistance` is now used only for a
normal four-aircraft AA graphical local test, so a production operation
cannot accidentally create the formation 5–6 km from the selected zone.

The bundled zone catalogue covers `Terrain1`: K92 Highway Strip, Dustbowl
Highway Strip, and South Boscali General Aviation for both BDF and PALA.
Recorded zones remain the first choice. Uncatalogued airports, including
mission-specific names such as Island FOB, use the same fail-closed terrain,
slope, obstruction, hostile-separation, cluster, and map-boundary validation.
The nearest other friendly-controlled operational airport supplies the ingress
corridor; if no safe cluster or friendly source exists, AIRLIFT does not spawn.

## Commands

The generic/public default sets `OwnerSteamId = 0`, disabling manual commands.
The included Kelly dedicated-server and local-test templates explicitly authorize
Steam ID `76561197999424821`. The configured owner may use:

- `/addzone`
- `/zones`
- `/airlift validate <airbase> <number|random>`
- `/airlift start <airbase> <number|random>`
- `/airlift wave <BDF|PALA> <airbase> <number|random>`
- `/airlift rapid <BDF|PALA> <airbase> <runway-number|random>`
- `/airlift status`
- `/airlift catalog`
- `/airlift abort`
- `/airlift auto set <on|off> <cooldown-seconds>`
- `/airlift auto status`

All faction players may use `/airlift purchase rapid <enemy-airport>` when
public purchases are enabled. Every other AIRLIFT command remains owner-only.

Examples: `/airlift start k92 2`, `/airlift start dustbowl random`.

## Combined owner console

Kelly's combined HOUNDS + AIRLIFT Operations Console 1.0.0 replaces the former
AIRLIFT-only owner companion. Press F8 in a running mission to open a menu
containing every live airport, a BDF/PALA team selector, an AA/combat operation
selector, recorded zones, every live runway exposed by the selected airbase,
and automatic airport-capture settings. The menu sends a normal authenticated
chat command; it never spawns a unit locally or bypasses the
server's map, airport ownership, faction, terrain, manifest, or active-operation
checks.

Install the combined console only on the owner's game client. Remove the older
`KellysAIRLIFTOwner.dll`. Do not install the console on the dedicated server or
distribute it to ordinary players. The main AIRLIFT DLL remains authoritative.

## Runtime cost and spawning

The coordinator runs at 2 Hz while idle, 10 Hz in ordinary flight, and 20 Hz
only around precision release or active jamming. Player presence and airport
ownership are sampled at 1 Hz; persistent cargo state at 2 Hz. Threat aircraft
come from the native registry and incoming missiles from each Chimera's native
warning system. Stations, doors, and jammers are resolved once and cached.

The four or five heavyweight Chimeras are assembled sequentially. Unity
`InstantiateAsync` uses a configurable 2 ms integration budget before native
Mirage server publication, with a synchronous `Spawner.SpawnAircraft` fallback.
AIRLIFT transports are excluded from native AI-aircraft deployment accounting.
The combat-only runway target scan runs at 2 Hz and is shared by all five
transports; AA operations never pay that scan cost.

## Chimera compatibility

AIRLIFT does not pin a Chimera version. It accepts one loaded addon retaining
the stable Aryx identity and live `Aryx_CargoPlane1` capabilities. Duplicate or
incomplete Chimera installations fail closed. The AIRLIFT archive contains no
Chimera DLL or `.nobp`.

## Build and package

```powershell
$env:NUCLEAR_OPTION_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option'
dotnet build .\KellysAIRLIFT.csproj -c Release
dotnet run --project .\KellysAIRLIFT.Tests\KellysAIRLIFT.Tests.csproj -c Release
.\package-release.ps1
.\package-server.ps1
.\package-local-test.ps1
.\package-public-ui.ps1
.\package-source.ps1
.\verify-release.ps1
```

The general package contains only the DLL in its live BepInEx path; configs and
zones are under `examples`, making it safe to inspect during an upgrade. The
`SERVER` package installs automatic production configuration and `Terrain1`
zones directly and is intended for a clean server or a carefully reviewed
upgrade. The `LOCAL-TEST` package enables listen-server testing.

Follow [INSTALL-CHECKLIST.md](INSTALL-CHECKLIST.md) before production use.
