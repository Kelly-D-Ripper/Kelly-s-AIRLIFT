# Kelly's AIRLIFT Public Purchase UI

Optional client-side companion for Kelly's AIRLIFT server 0.17.1. Install it
on every player client that should see `Request Support` under
`Donate > Vehicles`; do not install it on a dedicated server.

The selector offers the AA battery, RAPID runway package, ammo-pallet
prototype, light-tank, mortar, AFV6 and AA Gun Container drops. No nuclear
support option is included. Every choice requires a marker on the native map.

The addon sends only an authenticated `/airlift purchase support ...` request.
It cannot charge allocation or spawn aircraft. The server independently
validates the player, faction, price, cooldown, target, friendly-territory
ingress route, terrain/runway and complete Chimera manifest before charging.

Displayed defaults match the server: AA and RAPID $400m, light tanks $300m,
mortars $200m, AFV6 $160m, AA Gun Containers $200m and the ammo-pallet
prototype $100m. Server configuration remains authoritative.

