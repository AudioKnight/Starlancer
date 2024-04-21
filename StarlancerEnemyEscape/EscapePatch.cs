using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using static StarlancerEnemyEscape.StarlancerEnemyEscapeBase;

namespace EnemyEscape
{
    internal class StarlancerEscapeComponent : MonoBehaviour
    {
        private const int UpdateInterval = 1;
        private const float TeleportCooldownTime = 5;
        private const float TeleportRange = 1;
        private const float PathCooldownTime = 20; //60
        //private const float chanceToPath = 100; //After test, change to 20.

        private const int interiorPathRange = 500; //50 after test
        private const int exteriorPathRange = 500; //200 after test

        internal static EntranceTeleport[] entranceTeleports;
        internal static EntranceTeleport[] outsideTeleports;
        internal static EntranceTeleport[] insideTeleports;

        private int pathRange;
        private int chanceToEscape;
        private float lastTeleportCheck;
        private float lastTeleportTime;
        private float lastPathAttempt;
        private EnemyAI enemy;
        private System.Random random;
        private NavMeshPath pathToTeleport;
        private EntranceTeleport closestTeleport;
        private Vector3 closestTeleportPosition;
        private bool pathingToTeleport;
        private float prevPathDistance;
        private bool teleportFound;
        private Vector3 randomEnemyDestination;

        private bool closeToTeleport;

        private void Awake()
        {
            enemy = GetComponent<EnemyAI>();
            pathToTeleport = new NavMeshPath();
            lastTeleportCheck = Time.time;
            lastTeleportTime = Time.time;
            lastPathAttempt = Time.time;
            closestTeleportPosition = Vector3.negativeInfinity;
            prevPathDistance = float.PositiveInfinity;
            random = new System.Random(StartOfRound.Instance.randomMapSeed);

            if (EnemyEscapeConfigDictionary[enemy.enemyType.enemyName].Value == -1)
            {
                chanceToEscape = GetCurrentlySelectedPreset().GetValueOrDefault(enemy.enemyType.enemyName, 0);
            }
            else
            {
                chanceToEscape = EnemyEscapeConfigDictionary[enemy.enemyType.enemyName].Value;
            }
            if (enemy.isOutside) { pathRange = exteriorPathRange; }
            else { pathRange = interiorPathRange; }
        }

        //====================================================================================================================================================================================

        private void Update()
        {
            if (enemy.isEnemyDead) { Destroy(enemy.GetComponent<StarlancerEscapeComponent>()); }

            //if (enemy.currentBehaviourStateIndex != 0) { lastPathAttempt += Time.deltaTime; }

            if ((Time.time - lastTeleportCheck) <= UpdateInterval) { return; }

            lastTeleportCheck = Time.time;
            logger.LogInfo($"Update check. PathCooldownTime remaining = {PathCooldownTime - (Time.time - lastPathAttempt)}. PathingToTeleport is {pathingToTeleport}. ");

            if ((Time.time - lastPathAttempt) > PathCooldownTime || pathingToTeleport) //Attempt to path to a nearby entrance.
            {
                if (pathingToTeleport)
                {
                    enemy.SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(closestTeleportPosition));
                    enemy.agent.SetDestination(enemy.destination);

                    if (enemy.GetType() == typeof(FlowermanAI)) 
                    {
                        enemy.SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(enemy.destination));
                        enemy.agent.SetDestination(enemy.destination);
                    }
                    else
                    {
                        enemy.SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(closestTeleportPosition));
                        enemy.agent.SetDestination(enemy.destination);
                    }
                    
                }
                else if (random.Next(0, 100) <= chanceToEscape) 
                {
                    foreach (EntranceTeleport teleport in entranceTeleports)
                    {
                        NavMesh.CalculatePath(enemy.transform.position, teleport.entrancePoint.transform.position, enemy.agent.areaMask, pathToTeleport); //Check for a valid path to this entrance.

                        if (pathToTeleport.status != NavMeshPathStatus.PathComplete) { continue; }

                        var corners = pathToTeleport.corners;
                        var pathDistance = 0f;

                        for (int i = 1; i < corners.Length; i++)
                        {
                            pathDistance += Vector3.Distance(corners[i - 1], corners[i]);
                        }

                        if (pathDistance > pathRange) { continue; } //Check if this entrance is within range.

                        if (pathDistance < prevPathDistance)
                        {
                            teleportFound = true;
                            prevPathDistance = pathDistance;
                            closestTeleport = teleport;
                            closestTeleportPosition = teleport.entrancePoint.transform.position;
                            logger.LogInfo($"{teleport.name} is now the closest teleport.");
                        }
                    }

                    if (teleportFound)
                    {
                        pathingToTeleport = true;
                        logger.LogInfo($"{enemy.enemyType.name} is pathing to {closestTeleportPosition}.");

                        if (enemy.GetType() == typeof(FlowermanAI)) 
                        {
                            enemy.favoriteSpot = closestTeleport.transform;
                            enemy.destination = enemy.favoriteSpot.position;
                            enemy.SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(enemy.destination));
                            enemy.agent.SetDestination(enemy.destination);
                        }
                        else
                        {
                            enemy.SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(closestTeleportPosition));
                            enemy.agent.SetDestination(enemy.destination);
                        }
                        logger.LogInfo($"{enemy.gameObject.name} destination is {enemy.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} navmesh destination is {enemy.agent.destination}.");
                    }
                    else { logger.LogInfo($"No path available to any teleports."); }
                }
                lastPathAttempt = Time.time;
            }

            //====================================================================================================================================================================================


            if ((Time.time - lastTeleportTime) < TeleportCooldownTime) { return; }

            if (!pathingToTeleport)
            {
                foreach (EntranceTeleport teleport in entranceTeleports)
                {
                    if (Vector3.Distance(enemy.transform.position, teleport.transform.position) < TeleportRange)
                    {
                        closeToTeleport = true;
                    }
                }
            }
            

            if (enemy.isOutside && ((Vector3.Distance(enemy.transform.position, closestTeleportPosition) <= TeleportRange) || closeToTeleport)) //Run through the list of teleporter IDs to warp to the matching inside teleport.
            {
                for (int i = 0; i < outsideTeleports.Length; i++)
                {
                    if (Vector3.Distance(outsideTeleports[i].entrancePoint.transform.position, enemy.transform.position) <= TeleportRange)
                    {
                        closeToTeleport = false;
                        lastTeleportTime = Time.time;
                        prevPathDistance = float.PositiveInfinity;
                        pathingToTeleport = false;
                        teleportFound = false;
                        enemy.agent.Warp(insideTeleports[i].entrancePoint.transform.position);
                        enemy.SetEnemyOutside(false);
                        pathRange = interiorPathRange;
                        enemy.favoriteSpot = enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform;
                        enemy.DoAIInterval();
                        randomEnemyDestination = RoundManager.Instance.GetNavMeshPosition(enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform.position);
                        enemy.SetDestinationToPosition(randomEnemyDestination);
                        enemy.agent.SetDestination(randomEnemyDestination);

                        //======== Search ==============
                        if (enemy.GetType() == typeof(BaboonBirdAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<BaboonBirdAI>().scoutingSearchRoutine);
                        }
                        else if (enemy.GetType() == typeof(HoarderBugAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<HoarderBugAI>().searchForItems);
                            enemy.GetComponent<HoarderBugAI>().searchForItems.unsearchedNodes = enemy.allAINodes.ToList();
                        }
                        else { enemy.StartSearch(enemy.transform.position); }

                        //======== Logging ============================                        
                        logger.LogInfo($"{enemy.gameObject.name} teleported inside; Switching to interior AI. Setting Favorite Spot to {enemy.favoriteSpot}.");
                        logger.LogInfo($"{enemy.gameObject.name} destination is {enemy.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} navmesh destination is {enemy.agent.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} outside status is {enemy.isOutside}.");
                        return;

                    }
                }
            }
            else if (!enemy.isOutside && (Vector3.Distance(enemy.transform.position, closestTeleportPosition) <= TeleportRange) || closeToTeleport) //Run through the list of teleporter IDs to warp to the matching outside teleport.
            {
                for (int i = 0; i < insideTeleports.Length; i++)
                {
                    if (Vector3.Distance(insideTeleports[i].entrancePoint.transform.position, enemy.transform.position) <= TeleportRange)
                    {
                        closeToTeleport = false;
                        lastTeleportTime = Time.time;
                        prevPathDistance = float.PositiveInfinity;
                        pathingToTeleport = false;
                        teleportFound = false;
                        enemy.agent.Warp(outsideTeleports[i].entrancePoint.transform.position);
                        enemy.SetEnemyOutside(true);
                        pathRange = exteriorPathRange;
                        enemy.favoriteSpot = enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform;
                        enemy.DoAIInterval();
                        randomEnemyDestination = RoundManager.Instance.GetNavMeshPosition(enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform.position);
                        enemy.SetDestinationToPosition(randomEnemyDestination);
                        enemy.agent.SetDestination(randomEnemyDestination);

                        //=========== Search ============
                        if (enemy.GetType() == typeof(BaboonBirdAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<BaboonBirdAI>().scoutingSearchRoutine);
                        }
                        else if (enemy.GetType() == typeof(HoarderBugAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<HoarderBugAI>().searchForItems);
                            enemy.GetComponent<HoarderBugAI>().searchForItems.unsearchedNodes = enemy.allAINodes.ToList();
                        }
                        else { enemy.StartSearch(enemy.transform.position); }

                        //======== Logging ============================                        
                        logger.LogInfo($"{enemy.gameObject.name} teleported outside; Switching to exterior AI. Setting Favorite Spot to {enemy.favoriteSpot}.");
                        logger.LogInfo($"{enemy.gameObject.name} destination is {enemy.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} navmesh destination is {enemy.agent.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} outside status is {enemy.isOutside}.");
                        return;
                    }
                }
            }



            /*if (enemy.isOutside && (Vector3.Distance(enemy.transform.position, closestTeleportPosition) <= TeleportRange)) //Run through the list of teleporter IDs to warp to the matching inside teleport.
            {
                for (int i = 0; i < outsideTeleports.Length; i++)
                {
                    if (Vector3.Distance(outsideTeleports[i].entrancePoint.transform.position, enemy.transform.position) <= TeleportRange)
                    {
                        lastTeleportTime = Time.time;
                        prevPathDistance = float.PositiveInfinity;
                        pathingToTeleport = false;
                        teleportFound = false;
                        agent.Warp(insideTeleports[i].entrancePoint.transform.position);
                        enemy.SetEnemyOutside(false);
                        pathRange = interiorPathRange;
                        enemy.favoriteSpot = enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform;
                        enemy.DoAIInterval();
                        randomEnemyDestination = RoundManager.Instance.GetNavMeshPosition(enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform.position);
                        enemy.SetDestinationToPosition(randomEnemyDestination);
                        enemy.agent.SetDestination(randomEnemyDestination);

                       //======== Search ==============
                        if (enemy.GetType() == typeof(BaboonBirdAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<BaboonBirdAI>().scoutingSearchRoutine);
                        }
                        else if (enemy.GetType() == typeof(HoarderBugAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<HoarderBugAI>().searchForItems);
                            enemy.GetComponent<HoarderBugAI>().searchForItems.unsearchedNodes = enemy.allAINodes.ToList();
                        }
                        else { enemy.StartSearch(enemy.transform.position); } 

                        //======== Logging ============================                        
                        logger.LogInfo($"{enemy.gameObject.name} teleported inside; Switching to interior AI. Setting Favorite Spot to {enemy.favoriteSpot}.");
                        logger.LogInfo($"{enemy.gameObject.name} destination is {enemy.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} navmesh destination is {agent.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} outside status is {enemy.isOutside}.");
                        return;

                    }
                }
            }
            else if (!enemy.isOutside && (Vector3.Distance(enemy.transform.position, closestTeleportPosition) <= TeleportRange)) //Run through the list of teleporter IDs to warp to the matching outside teleport.
            {
                for (int i = 0; i < insideTeleports.Length; i++)
                {
                    if (Vector3.Distance(insideTeleports[i].entrancePoint.transform.position, enemy.transform.position) <= TeleportRange)
                    {
                        lastTeleportTime = Time.time;
                        prevPathDistance = float.PositiveInfinity;
                        pathingToTeleport = false;
                        teleportFound = false;
                        agent.Warp(outsideTeleports[i].entrancePoint.transform.position);
                        enemy.SetEnemyOutside(true);
                        pathRange = exteriorPathRange;
                        enemy.favoriteSpot = enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform;
                        enemy.DoAIInterval();
                        randomEnemyDestination = RoundManager.Instance.GetNavMeshPosition(enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform.position);
                        enemy.SetDestinationToPosition(randomEnemyDestination);
                        enemy.agent.SetDestination(randomEnemyDestination);

                        //=========== Search ============
                        if (enemy.GetType() == typeof(BaboonBirdAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<BaboonBirdAI>().scoutingSearchRoutine);
                        }
                        else if (enemy.GetType() == typeof(HoarderBugAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<HoarderBugAI>().searchForItems);
                            enemy.GetComponent<HoarderBugAI>().searchForItems.unsearchedNodes = enemy.allAINodes.ToList();
                        }
                        else { enemy.StartSearch(enemy.transform.position); }

                        //======== Logging ============================                        
                        logger.LogInfo($"{enemy.gameObject.name} teleported outside; Switching to exterior AI. Setting Favorite Spot to {enemy.favoriteSpot}.");
                        logger.LogInfo($"{enemy.gameObject.name} destination is {enemy.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} navmesh destination is {agent.destination}.");
                        logger.LogInfo($"{enemy.gameObject.name} outside status is {enemy.isOutside}.");
                        return;
                    }
                }
            }*/
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]

        private static void EscapeSetup(EnemyAI __instance)
        {
            if (EnemyWhitelist.ContainsKey(__instance.enemyType.enemyName)) { return; }

            if (!EnemyEscapeConfigDictionary.ContainsKey(__instance.enemyType.enemyName))
            {
                if (VanillaEnemyList.ContainsKey(__instance.enemyType.enemyName))
                {
                    logger.LogInfo($"Adding {__instance.enemyType.enemyName} to the StarlancerEnemyEscape config.");
                    EnemyEscapeConfigDictionary[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{__instance.enemyType.enemyName.Replace("[^0-9A-Za-z _-]", "")}", -1,
                        new ConfigDescription($"Chance for {__instance.enemyType.enemyName} to go into or out of the facility. Set to -1 to use the value from the chosen preset.",
                        new AcceptableValueRange<int>(-1, 100)));
                }
                else
                {
                    logger.LogInfo($"Adding {__instance.enemyType.enemyName} to the StarlancerEnemyEscape config.");
                    EnemyEscapeConfigDictionary[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{__instance.enemyType.enemyName.Replace("[^0-9A-Za-z _-]", "")}", -1,
                        new ConfigDescription($"Chance for {__instance.enemyType.enemyName} to go into or out of the facility. Set to -1 to use the value from the chosen preset.",
                        new AcceptableValueRange<int>(-1, 100)));
                }
                
            }
            if (__instance.gameObject.GetComponent<StarlancerEscapeComponent>() == null)
            {
                StarlancerEscapeComponent EscapeComponent = __instance.gameObject.AddComponent<StarlancerEscapeComponent>();
                logger.LogInfo($"Adding EscapeComponent to {__instance.gameObject.name}. It may now roam freely.");

                if (EscapeComponent.chanceToEscape == 0)
                {
                    logger.LogInfo($"ChanceToEscape is 0, removing EscapeComponent from {__instance.gameObject.name}");
                    Destroy(EscapeComponent);
                }
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(GameNetworkManager), "Start")]
        [HarmonyPostfix]
        [HarmonyPriority(0)]

        private static void BindRegisteredEnemies()
        {
            EnemyAI[] enemyAIs = Resources.FindObjectsOfTypeAll<EnemyAI>();

            foreach (EnemyAI enemy in enemyAIs)
            {
                if (EnemyWhitelist.ContainsKey(enemy.enemyType.enemyName)) { continue; }
                if (EnemyEscapeConfigDictionary.ContainsKey(enemy.enemyType.enemyName)) { continue; }
                if (VanillaEnemyList.ContainsKey(enemy.enemyType.enemyName))
                {
                    EnemyEscapeConfigDictionary[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{enemy.enemyType.enemyName.Replace("[^0-9A-Za-z _-]", "")}", -1,
                        new ConfigDescription($"Chance for {enemy.enemyType.enemyName} to go into or out of the facility. Set to -1 to use the value from the chosen preset.",
                        new AcceptableValueRange<int>(-1, 100)));
                    continue;
                }
                //Mod Enemy Binding
                EnemyEscapeConfigDictionary[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{enemy.enemyType.enemyName.Replace("[^0-9A-Za-z _-]", "")}", -1,
                    new ConfigDescription($"Chance for {enemy.enemyType.enemyName} to go into or out of the facility. Set to -1 to use the value from the chosen preset.",
                    new AcceptableValueRange<int>(-1, 100)));
            }
        }

        [HarmonyPatch(typeof(RoundManager), "SetLevelObjectVariables")]
        [HarmonyPostfix]

        private static void EntranceAINodes()
        {
            if (entranceTeleports == null || entranceTeleports.Length == 0 || entranceTeleports[0] == null)
            {
                entranceTeleports = FindObjectsOfType<EntranceTeleport>();
                outsideTeleports = new EntranceTeleport[entranceTeleports.Length / 2];
                insideTeleports = new EntranceTeleport[entranceTeleports.Length / 2];
                for (int i = 0; i < entranceTeleports.Length; i++)
                {
                    int entranceID = entranceTeleports[i].entranceId;

                    if (entranceTeleports[i].isEntranceToBuilding)
                    {
                        outsideTeleports[entranceID] = entranceTeleports[i];
                        logger.LogInfo("Finding exterior EntranceTeleports.");
                    }
                    else
                    {
                        insideTeleports[entranceID] = entranceTeleports[i];
                        logger.LogInfo("Finding interior EntranceTeleports.");
                    }
                }
            }

            for (int i = 0; i < entranceTeleports.Length; i++)
            {
                if (entranceTeleports[i].isEntranceToBuilding)
                {
                    var entranceNode = new GameObject();
                    entranceNode.name = $"EnemyEscapeEntranceNode ({entranceTeleports[i].name})";
                    entranceNode.tag = "OutsideAINode";
                    entranceNode.transform.SetParent(entranceTeleports[i].entrancePoint, false);
                    RoundManager.Instance.outsideAINodes.AddItem(entranceNode);

                    var spawnDenial = new GameObject();
                    spawnDenial.name = $"EnemyEscapeEntranceSpawnDenial ({entranceTeleports[i].name})";
                    spawnDenial.transform.SetParent(entranceNode.transform, false);
                    spawnDenial.tag = "SpawnDenialPoint";
                    RoundManager.Instance.spawnDenialPoints.AddItem(spawnDenial);
                }
                else
                {
                    var entranceNode = new GameObject();
                    entranceNode.name = $"EnemyEscapeEntranceNode ({entranceTeleports[i].name})";
                    entranceNode.tag = "AINode";
                    entranceNode.transform.SetParent(entranceTeleports[i].entrancePoint, false);
                    RoundManager.Instance.insideAINodes.AddItem(entranceNode);
                }
                
            }
        }
    }
}

