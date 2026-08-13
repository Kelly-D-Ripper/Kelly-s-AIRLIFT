# Kelly's AIRLIFT Public Purchase UI

Optional client-side companion for Kelly's AIRLIFT server 0.14.0 or newer.
Public UI 0.14.1 restores the `RAPID Combat Drop` entry after Nuclear Option
0.34.2 changed convoy pricing, and grows the vehicle donation panel to fit the
expanded list without overlapping the funds summary. Install it on every
player client that should see the entry under `Donate > Vehicles`.

Clicking the entry opens a dropdown containing live enemy-held airports. The
addon sends `/airlift purchase rapid <enemy-airport>` and cannot charge funds
or spawn aircraft. The dedicated server independently identifies the sending
player and validates faction allocation, the active operation, the five-minute
faction cooldown, map catalogue, current hostile ownership, live runway
geometry, terrain and the complete Chimera manifest.

RAPID transports carry three AGM-68s each. The server may engage tracked
hostile ground units and hostile taxiing/takeoff aircraft in the selected
runway corridor. Aircraft stop being eligible after leaving the corridor or
climbing more than 35 m above runway elevation. The transports never leave
their ingress route to pursue a target.

The displayed price defaults to $400m and should match the server's
`PublicPurchase.CombatDropCost`. A mismatched client price does not affect the
amount enforced by the server.
