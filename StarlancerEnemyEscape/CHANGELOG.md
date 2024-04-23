- 2.0.0
  - Almost a complete overhaul from v1.0.0
  - Instead of only allowing enemies a chance to use the EntranceTeleports if they happen to wander close enough, a system has been implemented to make an enemy path directly to an EntranceTeleport and warp to its match on the other half of the level. The range at which they may search for a teleport and the cooldown for an attempted search are configurable per enemy.
    - "Chance To Escape" now represents the chance for them to initiate pathing to the teleport.
  - Implemented a HarmonyPrefix to EnemyAI.SetDestinationToPosition() in order to prevent enemies from getting stuck if the position they want to path to is on the other half of the level.

- 1.0.0  
  - Initial Release.