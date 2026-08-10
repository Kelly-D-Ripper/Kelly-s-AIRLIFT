# Kelly's AIRLIFT 0.13.2

Combat runway selection now prefers the configured full-size runway but
automatically retries a compact operational runway down to 600m x 8m. Five
Chimera trail spacing contracts from the configured value to an 80m floor so
highway strips such as Dustbowl can fit the complete combat package.

Compact runways are not exempt from safety checks. All eight cargo release
points must still pass live terrain, water, slope, obstruction, faction and
native drop-zone validation. Combat formations retain the 0.13.1 off-map spawn
fix and the AGM-68 runway-suppression behaviour introduced in 0.13.0.

## Release files

- `KellysAIRLIFT-0.13.2-SERVER-NO034.zip` - clean dedicated-server install.
- `KellysAIRLIFT-0.13.2-PUBLIC-UI-NO034.zip` - optional player Donate-menu UI.
- `KellysAIRLIFT-0.13.2-LOCAL-TEST-NO034.zip` - listen-server test package.
- `KellysAIRLIFT-0.13.2-NO034.zip` - upgrade-safe general package.
- `KellysAIRLIFT-0.13.2-SOURCE.zip` - source-only GitHub archive.

The Aryx MC-260 Chimera addon, Nuclear Option assemblies and BepInEx binaries
are prerequisites and are not redistributed.
