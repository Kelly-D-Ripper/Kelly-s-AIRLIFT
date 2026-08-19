# Kelly's AIRLIFT Public UI 0.17.1

This release replaces the old RAPID-only donation entry with the AIRLIFT support
selector and native tactical-map drop-zone picker.

Available public requests are AA Battery, RAPID Runway Combat Drop,
Ammo-Pallet Prototype, Light Tank, Mortar, AFV6 and AA Gun Container. No
Darkreach or nuclear support option is included.

The tactical map can now open from aircraft selection and behaves as a modal
layer, preventing clicks from passing through to the aircraft and donation UI.

The addon remains client-only and non-authoritative. It sends authenticated
purchase requests; AIRLIFT 0.17.1 on the server independently validates the
player, faction, funds, price, cooldown, marker, terrain/runway,
friendly-territory ingress route and cargo manifest before charging or spawning.

Requires Nuclear Option 0.34.2, BepInEx 5 and a server running Kelly's AIRLIFT
0.17.1.

