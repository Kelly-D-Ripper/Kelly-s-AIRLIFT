# Kelly's AIRLIFT 0.13.0

Combat airlifts can now suppress an enemy runway without turning the transports
into dogfighters. Each of the five combat Chimeras carries the native AGM-68 x3
load instead of a jamming pod. A shared, bounded server scan assigns tracked
hostile ground units in the selected runway corridor and fires only when the
native weapon envelope and `CombatAI` approve the shot. Hostile taxiing and
takeoff aircraft are also targets while they remain in the corridor and no
more than 35 m above runway elevation.

Aircraft taking off or landing do not block combat preflight. They cease being
suppression targets after leaving the runway corridor or climbing above the
35 m ceiling. The transports remain in the dedicated AIRLIFT navigation state
and do not chase targets. AA-battery flights are unchanged and retain their
radar jamming pods.

## Release files

- `KellysAIRLIFT-0.13.0-SERVER-NO034.zip` - clean dedicated-server install.
- `KellysAIRLIFT-0.13.0-PUBLIC-UI-NO034.zip` - optional player Donate-menu UI.
- `KellysAIRLIFT-0.13.0-LOCAL-TEST-NO034.zip` - listen-server test package.
- `KellysAIRLIFT-0.13.0-NO034.zip` - upgrade-safe general package.
- `KellysAIRLIFT-0.13.0-SOURCE.zip` - source-only GitHub archive.

The Aryx MC-260 Chimera addon, Nuclear Option assemblies and BepInEx binaries
are prerequisites and are not redistributed.
