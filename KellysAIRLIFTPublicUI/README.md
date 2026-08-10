# Kelly's AIRLIFT Public Purchase UI

Optional client-side companion for Kelly's AIRLIFT 0.14.0. Install it on every
player client that should see the `RAPID Combat Drop` entry under
`Donate > Vehicles`.

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
