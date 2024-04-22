using BepInEx.Configuration;
using HarmonyLib;
using StarlancerAIFix.Patches;
using UnityEngine;
using UnityEngine.AI;
using static StarlancerAIFix.Patches.AIFix;
using static StarlancerEnemyEscape.StarlancerEnemyEscapeBase;

namespace EnemyEscape
{
    internal class StarlancerEscapeComponent : MonoBehaviour
    {
        private const int UpdateInterval = 1;
        private const float TeleportCooldownTime = 5;
        private const float TeleportRange = 1;

        internal static EntranceTeleport[] entranceTeleports;
        internal static EntranceTeleport[] outsideTeleports;
        internal static EntranceTeleport[] insideTeleports;

        internal int chanceToEscape; //Config per enemy, -1 (preset) - 100
        internal int interiorPathRange = 20; //20 default, config range 10 - 9999
        internal int exteriorPathRange = 200; //200 default, config range 50 - 9999
        internal float PathCooldownTime = 60; //60 default, config int range 30 - 300 (Half a minute to 5 minutes)

        private EnemyAI enemy;
        private System.Random random;
        private NavMeshPath pathToTeleport;
        private EntranceTeleport closestTeleport;
        private bool pathingToTeleport;
        private bool teleportFound;
        private bool closeToTeleport;
        private int pathRange;
        private float lastTeleportCheck;
        private float lastTeleportTime;
        private float lastPathAttempt;
        private float prevPathDistance;

        private Vector3 closestTeleportPosition;
        private Vector3 randomEnemyDestination;
        private Transform insideFavoriteSpot;
        private Transform outsideFavoriteSpot;


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
            if (enemy.isOutside) 
            {
                outsideFavoriteSpot = enemy.favoriteSpot;
                insideFavoriteSpot = insideAINodes[random.Next(0, insideAINodes.Length - 1)].transform;
                pathRange = exteriorPathRange;
            }
            else
            {
                insideFavoriteSpot = enemy.favoriteSpot;
                outsideFavoriteSpot = insideAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform;
                pathRange = interiorPathRange;
            }
        }

        //====================================================================================================================================================================================

        private void Update()
        {
            if (enemy.isEnemyDead) { Destroy(enemy.GetComponent<StarlancerEscapeComponent>()); }

            if (enemy.currentBehaviourStateIndex != 0) { lastPathAttempt += Time.deltaTime; } //Pause the cooldown if not in state 0

            if ((Time.time - lastTeleportCheck) <= UpdateInterval) { return; }

            lastTeleportCheck = Time.time;
            logger.LogInfo($"Update check. PathCooldownTime remaining = {PathCooldownTime - (Time.time - lastPathAttempt)}. PathingToTeleport is {pathingToTeleport}. ");

            if ((Time.time - lastPathAttempt) > PathCooldownTime || pathingToTeleport) //Attempt to path to a nearby entrance.
            {
                //=========== EnemyType Specific ============
                if (enemy.GetType() == typeof(HoarderBugAI))
                {
                    if (enemy.GetComponent<HoarderBugAI>().heldItem != null) { return; }
                }
                else if (enemy.GetType() == typeof(FlowermanAI) )
                {
                    bool playerInArea = false;
                    for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                    {
                        if (StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled && StartOfRound.Instance.allPlayerScripts[i].isInsideFactory)
                        {
                            playerInArea = true;
                            break;
                        }
                    }
                    if (!playerInArea) { logger.LogInfo("Bracken !playerInArea"); enemy.SwitchToBehaviourState(1); }
                }


                    if (pathingToTeleport)
                {
                    enemy.SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(closestTeleportPosition));
                    enemy.agent.SetDestination(enemy.destination);                    
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
                        }
                    }
                    if (teleportFound)
                    {
                        pathingToTeleport = true;
                        logger.LogInfo($"{enemy.enemyType.name} is pathing to {closestTeleportPosition}.");
                        enemy.SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(closestTeleportPosition));
                        enemy.agent.SetDestination(enemy.destination);
                    }
                }
                lastPathAttempt = Time.time;
            }

            //====================================================================================================================================================================================


            if ((Time.time - lastTeleportTime) < TeleportCooldownTime) { return; }

            if (!pathingToTeleport)
            {
                if (enemy.isOutside)
                {
                    foreach (EntranceTeleport teleport in outsideTeleports)
                    {
                        if (Vector3.Distance(enemy.transform.position, teleport.entrancePoint.transform.position) < TeleportRange)
                        {
                            closeToTeleport = true;
                        }
                    }
                }if (!enemy.isOutside)
                {
                    foreach (EntranceTeleport teleport in insideTeleports)
                    {
                        if (Vector3.Distance(enemy.transform.position, teleport.entrancePoint.transform.position) < TeleportRange)
                        {
                            closeToTeleport = true;
                        }
                    }
                }
            }
            
            if (enemy.isOutside && ((Vector3.Distance(enemy.transform.position, closestTeleportPosition) <= TeleportRange) || closeToTeleport)) //Run through the list of teleporter IDs to warp to the matching inside teleport.
            {
                for (int i = 0; i < outsideTeleports.Length; i++)
                {
                    if (Vector3.Distance(outsideTeleports[i].entrancePoint.transform.position, enemy.transform.position) <= TeleportRange)
                    {
                        TeleportAndRefresh();
                        enemy.agent.Warp(insideTeleports[i].entrancePoint.transform.position);
                        enemy.SetEnemyOutside(false);
                        pathRange = interiorPathRange;
                        enemy.favoriteSpot = insideFavoriteSpot;
                        TeleportAndRefresh();


                        //=========== EnemyType Specific ============
                        if (enemy.GetType() == typeof(BaboonBirdAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<BaboonBirdAI>().scoutingSearchRoutine);
                            enemy.GetComponent<BaboonBirdAI>().scoutingSearchRoutine.unsearchedNodes = enemy.allAINodes.ToList();
                        }
                        else if (enemy.GetType() == typeof(HoarderBugAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<HoarderBugAI>().searchForItems);
                            enemy.GetComponent<HoarderBugAI>().searchForItems.unsearchedNodes = enemy.allAINodes.ToList();
                        }
                        else if (enemy.GetType() == typeof(CrawlerAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<CrawlerAI>().searchForPlayers);
                        }
                        else if (enemy.GetType() == typeof(SpringManAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<SpringManAI>().searchForPlayers);
                        }
                        else if (enemy.GetType() == typeof(BlobAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<BlobAI>().searchForPlayers);
                        }
                        else { enemy.StartSearch(enemy.transform.position); }

                        return;
                    }
                }
            }
            else if (!enemy.isOutside /*&& (Vector3.Distance(enemy.transform.position, closestTeleportPosition) <= TeleportRange) || closeToTeleport*/) //Run through the list of teleporter IDs to warp to the matching outside teleport.
            {
                for (int i = 0; i < insideTeleports.Length; i++)
                {
                    if (Vector3.Distance(insideTeleports[i].entrancePoint.transform.position, enemy.transform.position) <= TeleportRange)
                    {
                        enemy.agent.Warp(outsideTeleports[i].entrancePoint.transform.position);
                        enemy.SetEnemyOutside(true);
                        pathRange = exteriorPathRange;
                        enemy.favoriteSpot = outsideFavoriteSpot;
                        TeleportAndRefresh();

                        //=========== EnemyType Specific ============
                        if (enemy.GetType() == typeof(BaboonBirdAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<BaboonBirdAI>().scoutingSearchRoutine);
                            enemy.GetComponent<BaboonBirdAI>().scoutingSearchRoutine.unsearchedNodes = enemy.allAINodes.ToList();
                        }
                        else if (enemy.GetType() == typeof(HoarderBugAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<HoarderBugAI>().searchForItems);
                            enemy.GetComponent<HoarderBugAI>().searchForItems.unsearchedNodes = enemy.allAINodes.ToList();
                        }
                        else if (enemy.GetType() == typeof(CrawlerAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<CrawlerAI>().searchForPlayers);
                        }
                        else if (enemy.GetType() == typeof(SpringManAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<SpringManAI>().searchForPlayers);
                        }
                        else if (enemy.GetType() == typeof(BlobAI))
                        {
                            enemy.StartSearch(enemy.transform.position, enemy.GetComponent<BlobAI>().searchForPlayers);
                        }
                        else { enemy.StartSearch(enemy.transform.position); }

                        return;
                    }
                }
            }
        }

        //====================================================================================================================================================================================

        private void TeleportAndRefresh()
        {
            closeToTeleport = false;
            lastTeleportTime = Time.time;
            prevPathDistance = float.PositiveInfinity;
            pathingToTeleport = false;
            teleportFound = false;
            randomEnemyDestination = RoundManager.Instance.GetNavMeshPosition(enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform.position);
            enemy.SetDestinationToPosition(randomEnemyDestination);
            enemy.agent.SetDestination(randomEnemyDestination);
            enemy.DoAIInterval();
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
            
            /*IL.BaboonBirdAI.DoAIInterval += StarlancerEscapeTranspilers.HawkScrapDestinationChanger;*/

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

        //====================================================================================================================================================================================

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
        //====================================================================================================================================================================================
        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SetDestinationToPosition))]
        [HarmonyPrefix]

        private static void SetDestinationToPositionPrefix(EnemyAI __instance, ref Vector3 position, ref bool checkForPath)
        {
            bool destinationInOtherArea = false;
            NavMeshPath pathToTeleport = new NavMeshPath();
            float prevPathDistance = float.PositiveInfinity;

            if (__instance.isOutside)
            {
                foreach (Vector3 node in outsideNodePositions)
                {
                    if (Vector3.Distance(node, position) < 10)
                    {
                        destinationInOtherArea = false;
                        break;
                    }
                    else { destinationInOtherArea = true; }
                }
                if (destinationInOtherArea)
                {
                    foreach (EntranceTeleport teleport in outsideTeleports)
                    {
                        NavMesh.CalculatePath(__instance.transform.position, teleport.entrancePoint.transform.position, __instance.agent.areaMask, pathToTeleport);
                        if (pathToTeleport.status != NavMeshPathStatus.PathComplete) { continue; }

                        var corners = pathToTeleport.corners;
                        var pathDistance = 0f;

                        for (int i = 1; i < corners.Length; i++)
                        {
                            pathDistance += Vector3.Distance(corners[i - 1], corners[i]);
                        }
                        if (pathDistance < prevPathDistance)
                        {
                            prevPathDistance = pathDistance;
                            checkForPath = false;
                            position = teleport.entrancePoint.transform.position;
                            __instance.destination = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, -1f);
                        }
                    }
                }
            }
            if (!__instance.isOutside)
            {
                foreach (Vector3 node in insideNodePositions)
                {
                    if (Vector3.Distance(node, position) < 10)
                    {
                        destinationInOtherArea = false;
                        break;
                    }
                    else { destinationInOtherArea = true; }
                }
                if (destinationInOtherArea)
                {
                    foreach (EntranceTeleport teleport in insideTeleports)
                    {
                        NavMesh.CalculatePath(__instance.transform.position, teleport.entrancePoint.transform.position, __instance.agent.areaMask, pathToTeleport);
                        if (pathToTeleport.status != NavMeshPathStatus.PathComplete) { continue; }

                        var corners = pathToTeleport.corners;
                        var pathDistance = 0f;

                        for (int i = 1; i < corners.Length; i++)
                        {
                            pathDistance += Vector3.Distance(corners[i - 1], corners[i]);
                        }
                        if (pathDistance < prevPathDistance)
                        {
                            prevPathDistance = pathDistance;
                            checkForPath = false;
                            position = teleport.entrancePoint.transform.position;
                            __instance.destination = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, -1f);
                        }
                    }
                }
            }
        }
    }
}

