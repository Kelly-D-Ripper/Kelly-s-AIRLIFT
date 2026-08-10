# Changelog

## 0.14.0 - 2026-08-10

- Name the five-Chimera runway assault package **RAPID: Runway Assault
  Package Insertion & Deployment** across the Donate menu, owner controls,
  commands, announcements and release documentation.
- Make `/airlift rapid` and `/airlift purchase rapid` the canonical commands
  while retaining the former `combat` tokens as backward-compatible aliases.
- Retain the 0.13.x runway-assault behaviour: off-map ingress, native AGM-68
  suppression, compact-runway fallback and native post-touchdown dispersal.

## 0.13.3 - 2026-08-10

- Order every surviving combat-drop `GroundVehicle` to leave the runway as
  soon as native airdrop activation and touchdown are both observed.
- Use the game's native `GroundVehicle.LeaveRoad()` path so perpendicular
  off-road waypoints retain terrain, obstruction, slope and water checks.
- Default to 180m of runway dispersal, configurable from 80-400m, and never
  apply the order to AA-battery containers or SAM positions.

## 0.13.2 - 2026-08-10

- Stop inspecting deployed cargo at 2 Hz after both touchdown and native ground
  activation have been confirmed.
- Keep only lightweight unit references for later mission/server-empty cleanup;
  settled batteries are checked for destroyed references every 30 seconds.

## 0.13.1 - 2026-08-10

- Make every combat runway drop use boundary-derived off-map ingress, including
  public purchases and owner-triggered operations on listen servers. The former
  6 km close spawn remains available only to normal AA local testing.
- Log the planned combat spawn and runway coordinates plus calculated off-map
  distance for deployment diagnostics.

## 0.13.0 - 2026-08-10

- Replace combat-drop Chimera jamming pods with the native three-round AGM-68
  Wing Pylons load while leaving the four-aircraft AA battery manifest unchanged.
- Add server-authoritative runway suppression during combat ingress. One shared
  2 Hz snapshot finds tracked hostile ground units inside the selected runway
  corridor, deconflicts assignments, and launches only inside the native weapon
  range, alignment, speed, line-of-sight, safety and `CombatAI` envelope.
- Ignore aircraft taking off or landing during combat-runway preflight, but
  admit hostile runway traffic as AGM-68 targets while it remains inside the
  corridor and no more than 35 m above runway elevation. Native autopilot
  collision handling remains active and transports never divert to chase.
- Make project references and packaging scripts standalone-repository friendly,
  using `NUCLEAR_OPTION_DIR`, optional `BEPINEX_DIR`, and a system `dotnet`
  fallback without depending on another Kelly project.

## 0.12.2 - 2026-08-08

- Correct Nuclear Option allocation units for public combat-drop purchases:
  the configured value is measured in millions, so `400` now correctly means
  $400m rather than treating `20,000,000` as $20tn.
- Change the default combat-drop price to $400m and clamp valid server/client
  values to $1m-$1b using the native `1-1000` allocation range.
- Format the optional client selector price through the game's native value
  formatter so its label matches the Donate menu and server rejection message.

## 0.12.1 - 2026-08-08

- Replace automatic latest-capture combat targeting with an explicit dropdown
  of live enemy-held airports in the public Donate-menu companion.
- Require the named destination to remain controlled by an opposing faction at
  server acceptance time; friendly, neutral, recaptured and ambiguous targets
  fail without charging allocation or consuming cooldown.
- Permit expected nearby defenders during an assault drop while retaining
  physical runway obstruction, map, terrain, slope, runway and native drop-zone
  validation.

## 0.12.0 - 2026-08-08

- Add a public, server-authoritative paid combat-drop request with a five-minute
  per-faction cooldown and a configurable $400m default player-allocation cost.
- Prefer the requesting faction's latest captured friendly airport, with a
  nearest-friendly-airport fallback, then reuse full map/runway/manifest
  validation before accepting payment.
- Charge only an accepted operation and automatically refund/cancel the
  cooldown when five-aircraft formation assembly fails before launch.
- Add a separate lightweight client addon that inserts `Combat Airlift` under
  the native `Donate > Vehicles` list without receiving spawn authority.

## 0.11.0 - 2026-08-08

- Added owner-authenticated runtime controls for automatic airport-capture
  enable/disable and the 60-3600 second cooldown.
- Added automatic director status output and safe snapshot reset whenever the
  runtime automatic settings change.
- Replaced the AIRLIFT-only player UI distribution with Kelly's combined
  HOUNDS + AIRLIFT Operations Console 1.0.0.

## 0.10.0 - 2026-08-08

- Add an owner-triggered five-Chimera combat runway drop with two Type-12 MBTs,
  two AFV6 IFV/AA pairs, and one dual FRCV-105 LT transport.
- Enumerate live `Airbase.Runway` geometry and fail closed on runway ownership,
  level-state, length, width, terrain, water, slope, obstruction, hostile
  separation, map catalogue, or native drop-zone validation.
- Use a five-aircraft longitudinal centreline formation, gear-down descent, a
  hard 5 m AGL release ceiling, variable synchronized cargo waves, prompt
  climb-out, and cargo-safe off-map extraction.
- Extend the optional owner console with AA/combat operation and live runway
  selection.

## 0.9.0 - 2026-08-08

- Add the owner-authorized `/airlift wave <BDF|PALA> <airport>
  <number|random>` server command while retaining all authoritative validation.
- Add a separate optional owner-client console with live airport, team, and
  drop-site selectors, opened with F8.
- Keep the companion UI out of the dedicated-server package and give it no
  direct spawning authority or dependency on the AIRLIFT server assembly.

## 0.8.0 - 2026-08-08

- Replace production landing with a direct return through each Chimera's
  original per-lane off-map entry corridor.
- Despawn only the transport after it reaches that off-map point; deployed
  cargo remains independently tracked and is not touched by extraction.
- Map the legacy `Despawn` setting to the same safe off-map extraction path.
- Retain native landing as an explicit optional configuration mode.

## 0.7.9 - 2026-08-07

- Use the off-map boundary calculation for manual commands issued on a dedicated
  server, not only for automatic airport-capture dispatches.
- Preserve the short configured spawn distance exclusively for local/listen-server
  testing.

## 0.7.8 - 2026-08-06

- Broadcast exactly three player-facing milestone types: AIRLIFT launch, each
  individual Chimera shot down, and successful drop completion.
- Keep formation assembly, descent, flares, release waves, egress, landing,
  abort, and routine recovery messages in the server log only.
- Replace the all-or-nothing messaging switch with the production-default
  `Messaging.AnnounceMilestonesInGame` setting.

## 0.7.7 - 2026-08-06

- Keep automatic AIRLIFT phase, loss, release, recovery, and completion events
  out of global in-game chat by default.
- Preserve every operation event in the BepInEx server log for diagnostics.
- Keep manual command responses private to the authorized administrator.
- Add an interim opt-in operation-announcement setting.

## 0.7.6 - 2026-08-06

- Raise the hard landing-cluster slope ceiling from 5 to 7 degrees.
- Evaluate dynamic landing candidates and select the valid eight-target cluster
  with the lowest maximum slope rather than accepting the first valid result.
- Lower the drop-run radar-altitude default and enforced floor from 600 to
  450 metres while retaining the 1,200-metre terrain-following ingress profile.

## 0.7.5 - 2026-08-06

- Generalize automatic capture dispatch to every live airport instead of the
  three names present in the recorded Terrain1 catalogue.
- Prefer a faction-scoped recorded zone when available; otherwise use a
  fail-closed dynamic search around the captured airport.
- Choose the nearest other friendly-controlled operational airport as the
  dynamic ingress source and reject dispatch when no friendly source exists.
- Retain full terrain, water, slope, obstruction, faction, hostile-separation,
  cluster, map-boundary, and aircraft/loadout validation for dynamic dispatches.
- Canonicalize harmless airport descriptors such as `Highway Strip`, `Airport`,
  and `FOB` so recorded and runtime names resolve consistently.

## 0.7.4 - 2026-08-06

- Gate the automatic capture director by the recorded-zone map catalogue once
  per mission. Unsupported maps perform no airport scanning or dispatch work.
- Keep production manual starts fail-closed through `RequireRecordedZones = true`.

## 0.7.3 - 2026-08-06

- Added server-ready and upgrade-safe public package layouts.
- Included the prevalidated `Terrain1` zone catalogue with release packages.
- Made production verbose diagnostics opt-in.
- Disabled manual commands in the generic/public fallback config; Kelly's server
  and local-test templates explicitly retain the authorized owner Steam ID.
- Start the airport/faction cooldown only after an automatic operation starts.
- Log automatic requests that expire without a live faction context or fail to start.

## 0.7.2 - 2026-08-05

- Added the global airport-capture airlift announcement.

## 0.7.1 - 2026-08-05

- Removed the Revoker escort.
- Added synchronized parallel egress and ten native flare pulses per surviving Chimera.
- Accelerated native landing by safely skipping the oversized join pattern.
- Added dedicated-server airport-capture dispatch support.
