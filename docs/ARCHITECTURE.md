# Kelly's AIRLIFT architecture

AIRLIFT is standalone. It has no KellysHOUNDS source, assembly, runtime lookup,
or shared-state dependency. Version 0.14.0 contains no escort aircraft and
supports a four-transport AA operation plus the five-transport RAPID runway assault.

## Authority and automatic director

Manual commands are intercepted at the Nuclear Option 0.34 server chat RPC,
resolved to a player, and restricted to the configured Steam ID. All mutation
requires the active authoritative mission server. Listen-server testing is an
explicit opt-in.

The optional `KellysAIRLIFTOwner` client companion discovers live `Airbase`
display names for its dropdowns and sends ordinary chat commands. It has no
reference to the AIRLIFT assembly and no mutation path. The server's `wave`
command resolves the requested BDF/PALA `FactionHQ`, requires that faction to
control the named airport, and runs the same map, zone, terrain, Chimera,
cargo, concurrency, and authority checks as every other manual operation.

The optional dedicated-server capture director first checks whether the running
map has recorded zones or validated dynamic fallback is enabled. With neither,
it remains dormant and does not scan airports, queue requests, or announce. On
an eligible map it polls live `Airbase.CurrentHQ` at 1 Hz. The initial set is a
baseline, not a capture. A later non-null owner
transition for either faction is matched to a recorded map/faction/airport
zone, de-duplicated, rate-limited by airport and faction, and placed in a queue
bounded at eight requests. Only one `BatteryOperation` runs at a time. The
airport/faction cooldown begins after a successful operation start, so an
expired player-context wait or failed manifest validation does not consume the
next legitimate recapture.

Automatic spawn geometry walks backwards from the release line along the
configured inbound heading to the first map boundary, adds the configured
off-map margin, and retains full configured spawn velocity. This applies to
automatic and authorized manual operations on a dedicated server. Only a
graphical local/listen-server manual test retains the short configured spawn
distance.

## Battery and native cargo

One `BatteryOperation` owns four `TransportOperation` records:

1. ALPHA: pallet x4 rear, SLMMR-A3 front.
2. BRAVO: radar rear, R9 Mission Bay.
3. CHARLIE: R9 Mission Bay, pallet x4 front.
4. DELTA: SLMMR-A3 rear, R9 Mission Bay.

Each AA transport also receives `JammingPod1` on `Wing Pylons`. Combat
transports instead require the unique native `AGM-68` option on that hardpoint,
three physical pylons and three runtime `MountedMissile` instances. The manifest proves exact
live mounts, hardpoints, faction acceptance, cargo type, native parachute
support, jammer capability, station ordering, and cargo doors before spawning.

`AirliftTransportState` owns fixed route navigation only. Release calls native
`WeaponStation.LaunchMount`; `MountedCargo.Fire`, `Spawner.SpawnUnit`,
`GroundVehicle.CheckAirdrop`, and `CargoDeploymentSystem` retain their original
server, parachute, and activation responsibilities. A postfix attributes and
tracks all fourteen spawned units.

## Recovery and extraction

Immediately after release, every survivor targets a parallel point along one
`EgressTurnDegrees` vector, 30 degrees right by default. The battery waits for
all live aircraft to clear this common bank before releasing their individual
return routes. The drop also schedules ten native `PopFlares()` pulses at
0.5-second intervals; that game helper explicitly selects flare station zero.

The production `Return` path directs each aircraft to its original per-lane
spawn point beyond the same map edge used for ingress and restores cruise
altitude. Within `ReturnArrivalRadius`, AIRLIFT marks the transport complete and
destroys only its aircraft network object. The cargo-release confirmation gate
must already prove every expected deployed unit exists. Those units remain in
the independent persistent tracker and are not passed to transport cleanup.

Optional `Land` mode retains the native airbase selection, runway registration,
and accelerated `AIPilotLandingState.FinalTurn` path. The legacy `Despawn`
configuration is an alias for `Return`, preventing immediate post-drop deletion.

## RAPID combat runway variant

The combat variant selects directly from live `Airbase.runways` rather than
recorded point coordinates. Exact runway endpoints define its centreline and
heading; the nearest other friendly airbase determines the permitted direction
of approach. Five longitudinal slots keep the large transports out of an
unsafe abreast formation. The release state has its own 2-5 m altitude target,
hard 5 m ceiling, gear-down flag, and variable one/two-package synchronized
waves. Combat cargo is required to be a native `GroundVehicle`; parachute
capability is intentionally not required because the native release occurs at
ground level.

## Public purchase boundary

`KellysAIRLIFTPublicUI` patches only the local convoy-list refresh and clones
the game's own `ConvoyPurchaseOption` prefab. Its display-only convoy object is
never added to the faction catalogue or sent to `CmdPurchaseConvoy`; clicking
it opens a local enemy-airport selector and sends the intercepted
`/airlift purchase rapid <enemy-airport>` chat request. The legacy `combat`
token remains a compatibility alias.

The server resolves the authenticated chat sender to `Player`, checks that
player's current `FactionHQ` and replicated allocation, resolves the named
airport against live opposing-faction ownership, and executes the same combat
preflight used by owner commands. Only an operation successfully placed
into the coordinator is debited. Until staged formation assembly completes,
the operation retains purchaser/cost metadata so an abort can refund allocation
and clear the faction cooldown. The selected airport name is only a request:
no client-supplied price, faction, ownership, runway or cooldown value is
trusted.

Transport loss is isolated. Once any transport has committed cargo release or
post-drop recovery, whole-operation timeout cleanup is suppressed. Explicit
abort and mission/empty-server lifecycle cleanup remain available.

## Runtime budget

Formation assembly is staged and async-integrated with a per-frame budget.
The coordinator uses adaptive 2/10/20 Hz cadence. Airport ownership and player
presence scan at 1 Hz, persistent cargo at 2 Hz, and one cached hostile-aircraft
snapshot serves all four jammers. Incoming missiles are read from native
warning lists; no global missile scan is used. All transports are removed from
`FactionHQ.activeAIAircraft` accounting while remaining normal network units.
