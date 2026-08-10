# Nuclear Option 0.34 / Chimera research record

## Native runtime path

Inspection of the current 0.34 assemblies confirmed:

- `WeaponInfo.cargo` and `WeaponMount.Cargo` identify cargo stations.
- `WeaponStation.LaunchMount` advances and fires one mounted weapon.
- `MountedCargo.Fire` uses the server aircraft-launch RPC and rail path.
- `MountedCargo.RailLaunch` calls `Spawner.SpawnUnit`.
- cargo inherits aircraft point velocity plus rail velocity.
- `GroundVehicle.CheckAirdrop` creates its assigned `CargoDeploymentSystem`,
  which manages the parachute and landing activation.
- `AIHeloTransportState` continually chooses enemy/objective-derived
  destinations and is unsuitable for fixed configured zones.

## Chimera compatibility policy

Initial research used a 1.1.5 DLL whose embedded bundle was byte-identical to
the external 1.1.6 `.nobp`. The live installation later advanced to Chimera
1.1.7, proving that hash/version pinning would reject routine addon updates.

AIRLIFT 0.9.0 therefore does not pin or distribute a Chimera release. It
requires exactly one loaded assembly named `Aryx_MC260_Chimera` and exactly one
live Blueprinter manifest named `Aryx MC-260 Chimera`, regardless of reported
version. Compatibility is established from the actual bundle capabilities:

- exact `Aryx_MC260_Chimera_Definition` / `Aryx_CargoPlane1` aircraft;
- every exact required `WeaponMount` key;
- successful incremental Encyclopedia/network registration;
- exact spawned cargo stations, mounted cargo, jammer, hardpoints, and bay doors.

An update that preserves those contracts is accepted automatically. Missing,
renamed, duplicate, or type-incompatible content fails closed with a specific
diagnostic. The AIRLIFT release contains no Chimera DLL or `.nobp`, preventing
it from reinstalling a stale aircraft version.

The supplied definition asset is named `Aryx_MC260_Chimera_Definition`, but
its native network/Encyclopedia `jsonKey` is `Aryx_CargoPlane1`. Original
Chimera missions use `Aryx_CargoPlane1`. The later
`Aryx_MC260_Chimera` string came from a local mission-rebuild alias and is not
present in the supplied bundle, so AIRLIFT 0.3.4 migrates that obsolete config
value back to the native key.

Legacy Chimera bundles may use Blueprinter manifest schema 3 with patch
substitutions but no modern `OpAddToEncyclopedia` operations. AIRLIFT 0.9.0
registers non-opted-out `UnitDefinition` and `WeaponMount` assets from the
single compatible live bundle through Blueprinter's native incremental
Encyclopedia path before validating the battery manifest.

## Battery catalogue and hardpoints

| Exact mount key | Display | Rounds | Chimera hardpoints |
|---|---|---:|---|
| `MC260_RadarContainerx1` | Radar Container | 1 | Cargo Bay Rear, Cargo Bay Front |
| `MunitionsSmallPallet2x4` | Small Munitions Pallet x4 | 4 | Cargo Bay Rear, Cargo Bay Front |
| `Aryx_MC260_R9SAMLauncherx1` | R9 SAM Launcher | 1 | Mission Bay only |
| `Aryx_IRSAM_Turret_x1` | SLMMR-A3 SAM Launcher | 1 | Cargo Bay Rear, Cargo Bay Front |

The radar mount is enabled and visible in the researched Chimera bundles. The
small-pallet mount and its `MunitionsPallet2` deployed definition are base-game
0.34 content, so they are external references in a Chimera-only asset scan.
The current Nuclear Option 0.34 `resources.assets` identifies the exact mount as
`MunitionsSmallPallet2x4`; the shorter `MunitionsPallet2x4` key found in Supply
Buffet 2.1.2 belongs to a different content revision and is absent here. The
Chimera loadout UI exposes the current mount in both cargo bays. AIRLIFT still
verifies both named hardpoints, all four native rounds, cargo type, and
parachute system against the live installed content before spawning.

The Chimera has separate rear and front cargo hardpoint sets. However, the R9
x1 mount is offered only by the single Mission Bay set. There is no x2 R9
package in the bundle. The fully native battery therefore requires four
aircraft. AIRLIFT 0.9.0 retains two SLMMR launchers and replaces the former
10-tonne container plus one SLMMR with rear- and front-bay x4 pallet racks.

Disabled/hidden `Aryx_MC260_Airpad_x1` and `Aryx_MC260_FireControl` assets are
not forced into service.

## Named zone

`SOUTH_BOSCALI_FIELD` uses map key `Terrain1` at radar target X `-11800`, Z
`-4900`. The coordinate is an offline-vetted profile, not a bypass. Every
operation independently validates all eight derived points against live map
bounds, terrain, sea level, slope, static/exclusion obstruction, live-unit
clearance, faction, hostile proximity, and the native drop-zone reservation.
