# Public release checklist

## Required before publishing

- Choose and add a source/distribution `LICENSE`. No licence has been assumed.
- Publish source from a clean standalone repository; do not include `bin`, `obj`,
  backups, game assemblies, Chimera files, Steam server files, or private logs.
- State that Nuclear Option, BepInEx, Aryx MC-260 Chimera, and their names/assets
  belong to their respective owners; AIRLIFT does not redistribute them.
- Confirm the public download contains only AIRLIFT's DLL, configuration examples,
  zone data, documentation, and checksums.
- Publish the optional public purchase UI as a clearly labelled separate
  client-only ZIP; never place its DLL in the dedicated-server package.
- Publish the ZIP SHA-256 and tag the exact source revision used to build it.
- Keep the generic `OwnerSteamId = 0`. Server operators must set their own ID;
  do not publish a personal administrator identity as the universal fallback.
- Clarify that the public UI has no authority: price, player, faction, target,
  cooldown and spawning are independently enforced by the server.

## Compatibility statement

- Target: Nuclear Option 0.34 and BepInEx 5.x.
- Required content: one compatible Aryx MC-260 Chimera installation.
- Recorded-zone coverage: `Terrain1`, K92 Highway Strip, Dustbowl Highway Strip,
  and South Boscali General Aviation, for BDF and PALA. Automatic mode can also
  serve other live airports through fail-closed validated dynamic fallback.
- The plugin DLL is server-authoritative. Whether each client needs the separate
  Chimera content must be verified in the intended Blueprinter/add-on deployment;
  do not claim a client content requirement until that test is complete.

## Production acceptance test

- Use a private/staging server first and preserve the existing config and zone TSV.
- Verify all 15 items in `INSTALL-CHECKLIST.md` for both factions.
- Test a capture during an active airlift to verify bounded queueing.
- Test a capture with no player in the capturing faction, then join within two
  minutes and confirm dispatch; repeat without joining and confirm clean expiry.
- Test one destroyed Chimera before release, one after release, and an empty server.
- Test purchase rejection for insufficient allocation, an active operation,
  the five-minute cooldown, unsupported map, friendly/neutral/recaptured
  airport, ambiguous airport name and invalid runway.
- Test pre-launch failure refund and confirm no refund occurs for combat losses
  after all five transports have launched.
- Confirm every combat transport resolves AGM-68 x3, ignores runway aircraft as
  preflight obstructions, attacks tracked hostile ground units and hostile
  aircraft below the 35 m runway ceiling inside the corridor, stops considering
  aircraft after departure, and never diverts from the transport route.
- Confirm public and owner-triggered RAPID drops on both listen and dedicated
  servers spawn beyond the map boundary plus `OffMapSpawnMargin`; the log must
  report the planned global spawn, enemy runway and calculated distance.
- Confirm every landed combat vehicle begins native perpendicular runway
  dispersal promptly, receives only one order, and AA-battery cargo does not
  receive a movement order.
- Record frame time, server tick stability, network traffic, and log volume during
  formation spawn, synchronized release, parachute deployment, and recovery.
- Run at least one full match after enabling automatic mode; restart the server and
  verify the config and zone file survive unchanged.

## Recommended publication material

- One short installation video and one complete capture-to-off-map-extraction demonstration.
- Screenshots of a valid manifest, synchronized drop, persistent deployed battery,
  and empty transport formation after extraction.
- A known-issues section listing unsupported maps/airports and the separate Chimera
  prerequisite.
- A single issue template asking for game version, AIRLIFT version, Chimera version,
  map, faction, selected zone, BepInEx log excerpt, and reproduction steps.
