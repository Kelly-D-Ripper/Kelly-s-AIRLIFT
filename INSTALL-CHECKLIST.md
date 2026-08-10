# Kelly's AIRLIFT 0.14.0 installation checklist

1. Stop Nuclear Option and back up `BepInEx/plugins` and `BepInEx/config`.
2. Confirm Nuclear Option 0.34 and BepInEx 5.x.
3. Keep exactly one current Aryx MC-260 Chimera installation. Remove duplicate
   or superseded Chimera DLL/`.nobp` copies.
4. For an upgrade-safe install, extract `KellysAIRLIFT-0.14.0-NO034.zip` and
   copy the server config and zone catalogue from `examples` after comparing
   them with the server's existing files. For a clean dedicated server, use
   `KellysAIRLIFT-0.14.0-SERVER-NO034.zip`.
5. Confirm `BepInEx/plugins/KellysAIRLIFT/KellysAIRLIFT.dll` exists.
6. For manual testing, copy the packaged default config from `examples`. For
   production capture triggers, copy
   `examples/kelly.nuclearoption.airlift.server.cfg` over
   `BepInEx/config/kelly.nuclearoption.airlift.cfg`. The clean `SERVER` package
   has already placed the production config there.
   Set `Authorization.OwnerSteamId` to the server owner's Steam ID if owner-only
   manual commands are required; every public package intentionally ships `0`.
7. Preserve/copy faction-scoped recorded zones into
   `BepInEx/config/kelly.nuclearoption.airlift.zones.tsv`. Recorded zones are
   preferred; with `AllowValidatedDynamicZoneFallback = true`, uncatalogued
   captured airports use a fully validated live-terrain search instead.
8. Remove any obsolete `[Escort]` section from an older config. AIRLIFT 0.14.0
   has no escort manifest or escort aircraft.
9. Confirm `[Messaging] AnnounceMilestonesInGame = true`, then start the server
   and confirm `Kelly's AIRLIFT 0.14.0` loads without API or
   Chimera-manifest errors.
10. Join as the authorized owner and run `/airlift validate <airport> 1`.
    Confirm the zone and four-aircraft manifest both report `PASS`.
11. Run one manual test and verify:
    - four Chimeras spawn sequentially; no Revoker or fifth aircraft appears;
    - all four carry `JammingPod1`;
    - every surviving Chimera visibly emits ten flare pulses, approximately
      0.5 seconds apart, beginning with synchronized cargo release;
    - two synchronized waves produce fourteen tracked cargo units;
    - cargo doors open during ingress and native parachutes activate;
    - surviving aircraft complete the drop after another Chimera is destroyed;
    - before splitting, every survivor banks right through the same 30-degree
      egress while preserving its parallel lane;
    - each survivor climbs and returns through its own original off-map entry
      corridor instead of entering an airport landing pattern;
    - each Chimera disappears only after reaching its off-map return point;
    - the fourteen deployed cargo units remain present and tracked after all
      transports have despawned;
    - only launch, each individual Chimera loss, and successful drop are
      broadcast globally; every `AIRLIFT event:` remains in the server log.
12. Enable automatic capture mode only after the manual test. Capture a test
    airport and verify exactly one request is logged after the ownership change
    (never during the initial ownership snapshot), the selected zone matches
    the new faction, and the flight spawns beyond the map edge.
13. Repeat for the other faction, then verify the configured cooldown blocks an
    immediate repeat capture at the same airport.
14. Test owner abort, mission change, and all players disconnecting. Confirm
    active transports, reservations, and configured tracked cargo are cleaned.
15. Return `VerboseLogging = false` after commissioning. Enable it temporarily
    when diagnosing a manifest, zone, cargo, or landing failure.

## Public Donate-menu purchase

1. Extract `KellysAIRLIFT-0.14.0-PUBLIC-UI-NO034.zip` on every player client
   that should see the purchase entry. Do not install this UI DLL on the server.
2. Confirm `RAPID Combat Drop` appears under `Donate > Vehicles` and displays the
   same price as `PublicPurchase.CombatDropCost` on the server.
3. Open `RAPID Combat Drop`, choose an enemy-held airport from the dropdown, and
   verify the server charges exactly once and starts five Chimeras only after
   hostile-ownership, runway, map and manifest validation.
4. While the selector is open, capture the selected airport before confirming;
   verify the now-friendly destination is rejected without a charge.
5. Attempt a second purchase immediately and confirm no funds are charged and
   the response reports roughly 300 seconds remaining.
6. After five minutes, buy again. Then perform a controlled pre-launch failure
   and verify allocation is refunded and the cooldown is cleared.
7. Verify five Chimeras stay in trail, lower gear, remain at or below 5 m for
   release, climb without collision, extract off-map, and leave eight units.
8. Confirm all five combat Chimeras carry `AGM-68 x3`, aircraft using the
   runway do not block preflight, and tracked hostile ground units plus hostile
   taxiing/takeoff aircraft below the 35 m runway ceiling receive deconflicted
   native missile attacks without transports leaving the ingress route. Confirm
   each landed combat vehicle receives one native perpendicular off-runway
   movement order while AA-battery cargo remains stationary.

The combined Kelly's HOUNDS + AIRLIFT Operations Console remains the separate
owner-only administrative UI. It is not replaced by the public purchase addon.

The AIRLIFT plugin itself is server-side. The current Chimera content remains a
separate prerequisite and is not bundled or updated by AIRLIFT.
