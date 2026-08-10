# Kelly's AIRLIFT 0.13.3

Combat vehicles now clear the runway immediately after native airdrop
activation and touchdown. AIRLIFT issues the game's own
`GroundVehicle.LeaveRoad()` order once per surviving vehicle, producing
perpendicular off-road waypoints with native terrain, obstruction, slope and
water checks. The default dispersal distance is 180m and can be configured from
80-400m.

The movement order applies only to the eight combat-package vehicles. Radar,
munitions and SAM cargo from the normal AA-battery operation remains at its
validated drop positions. Version 0.13.3 retains the compact Dustbowl runway
fallback and unconditional combat off-map ingress from 0.13.2 and 0.13.1.

## Release files

- `KellysAIRLIFT-0.13.3-SERVER-NO034.zip` - clean dedicated-server install.
- `KellysAIRLIFT-0.13.3-PUBLIC-UI-NO034.zip` - optional player Donate-menu UI.
- `KellysAIRLIFT-0.13.3-LOCAL-TEST-NO034.zip` - listen-server test package.
- `KellysAIRLIFT-0.13.3-NO034.zip` - upgrade-safe general package.
- `KellysAIRLIFT-0.13.3-SOURCE.zip` - source-only GitHub archive.

The Aryx MC-260 Chimera addon, Nuclear Option assemblies and BepInEx binaries
are prerequisites and are not redistributed.
