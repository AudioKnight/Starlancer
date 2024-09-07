﻿using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.Utilities;
using static StarlancerAIFix.Patches.AIFix;
using static StarlancerEnemyEscape.StarlancerEnemyEscapeBase;
using Random = System.Random;

namespace EnemyEscape
{
    internal class StarlancerEscapeComponent : MonoBehaviour
    {
        private const int UpdateInterval = 1;
        private const float TeleportCooldownTime = 5;
        private const float TeleportRange = 1;

        private const int intPathRangeDefault = 20;
        private const int extPathRangeDefault = 200;
        private const int cooldownTimeDefault = 30;

        internal static EntranceTeleport[] entranceTeleports;
        internal static List<EntranceTeleport> outsideTeleports = [];
        internal static List<EntranceTeleport> insideTeleports = [];
        internal static List<EnemyAI> enemiesThatCanEscape = [];

        internal int chanceToEscape;    //Config per enemy, [-1(preset) - 100]
        internal int interiorPathRange; //20 default, config range [10 - 9999]
        internal int exteriorPathRange; //200 default, config range [50 - 9999]
        internal int pathCooldownTime;  //30 default, config int range [10 - 300] (10 seconds up to 5 minutes)

        private bool ignoreStateCheck;
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
        private Random random;
        private EnemyAI enemy;
        private NavMeshPath pathToTeleport;
        private EntranceTeleport closestTeleport;
        private Transform insideFavoriteSpot;
        private Transform outsideFavoriteSpot;

        //====================================================================================================================================================================================

        private void Awake()
        {
            enemy = GetComponent<EnemyAI>();
            pathToTeleport = new NavMeshPath();
            lastTeleportCheck = Time.time;
            lastTeleportTime = Time.time;
            lastPathAttempt = Time.time;
            closestTeleportPosition = Vector3.negativeInfinity;
            prevPathDistance = float.PositiveInfinity;
            random = new Random(StartOfRound.Instance.randomMapSeed);
            interiorPathRange = configEscapeInteriorRange[enemy.enemyType.enemyName].Value;
            exteriorPathRange = configEscapeExteriorRange[enemy.enemyType.enemyName].Value;
            pathCooldownTime = configEscapeCooldownTime[enemy.enemyType.enemyName].Value;

            logger.LogInfo($"Adding EscapeComponent to {enemy.gameObject.name}. It may now roam freely.");

            if (EnemyEscapeConfigDictionary[enemy.enemyType.enemyName].Value == -1)
            {
                chanceToEscape = GetCurrentlySelectedPreset().GetValueOrDefault(enemy.enemyType.enemyName, 0);
            }
            else
            {
                chanceToEscape = EnemyEscapeConfigDictionary[enemy.enemyType.enemyName].Value;
            }

            if (chanceToEscape == 0)
            {
                logger.LogInfo($"ChanceToEscape is 0, removing EscapeComponent from {gameObject.name}");
                Destroy(this);
            }
            else
            {
                enemiesThatCanEscape.Add(enemy);
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
                outsideFavoriteSpot = outsideAINodes[random.Next(0, outsideAINodes.Length - 1)].transform;
                pathRange = interiorPathRange;
            }

            //== CIRCUIT BEES == : Rework later
            //Ignore (currentBehaviourStateIndex != 0) check
            //if (enemy is RedLocustBees) { ignoreStateCheck = true; }
        }


        //====================================================================================================================================================================================

        private void Update()
        {
            if (enemy.isEnemyDead) { Destroy(this); }

            if ((Time.time - lastTeleportCheck) <= UpdateInterval) { return; }

            lastTeleportCheck = Time.time;

            if (!ignoreStateCheck && enemy.currentBehaviourStateIndex != 0) { lastPathAttempt += Time.deltaTime; } //Pause the cooldown if not in state 0 and ignoreStateCheck is false

            if ((Time.time - lastPathAttempt) > pathCooldownTime || pathingToTeleport) //Attempt to path to a nearby entrance.
            {
                //=========== EnemyType Specific ============

                if (enemy is FlowermanAI) { return; } //The Bracken doesn't want to path to an entrance when it's alone in the facility. Until or unless I transpile its DoAIInterval(), I'd rather the random pathing not eat up CPU.
                else if (enemy is HoarderBugAI hoarderBugAI) //Prevents random pathing when a lootbug is holding an item so it doesn't get confused.
                {
                    if (hoarderBugAI.heldItem != null)
                    {
                        enemy.SetDestinationToPosition(hoarderBugAI.nestPosition);
                        lastPathAttempt += Time.deltaTime;
                        return;
                    }
                }

                //===========================================

                if (pathingToTeleport)
                {
                    enemy.SetDestinationToPosition(closestTeleportPosition);
                    enemy.agent.SetDestination(closestTeleportPosition);

                    if (enemy is SandSpiderAI sandSpiderAI)
                    {
                        Vector3 vector = sandSpiderAI.meshContainerPosition;
                        sandSpiderAI.meshContainerPosition = Vector3.MoveTowards(sandSpiderAI.meshContainerPosition, closestTeleportPosition, sandSpiderAI.spiderSpeed * Time.deltaTime);
                        sandSpiderAI.refVel = vector - sandSpiderAI.meshContainerPosition;
                        sandSpiderAI.meshContainer.position = sandSpiderAI.meshContainerPosition;
                        sandSpiderAI.meshContainer.rotation = Quaternion.Lerp(sandSpiderAI.meshContainer.rotation, sandSpiderAI.meshContainerTargetRotation, 8f * Time.deltaTime);
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
                        }
                    }
                    if (teleportFound)
                    {
                        pathingToTeleport = true;
                        //logger.LogInfo($"{enemy.enemyType.name} is pathing to {closestTeleportPosition}.");
                        enemy.SetDestinationToPosition(closestTeleportPosition);
                        enemy.agent.SetDestination(closestTeleportPosition);

                        if (enemy is SandSpiderAI sandSpiderAI)
                        {
                            Vector3 vector = sandSpiderAI.meshContainerPosition;
                            sandSpiderAI.meshContainerPosition = Vector3.MoveTowards(sandSpiderAI.meshContainerPosition, closestTeleportPosition, sandSpiderAI.spiderSpeed * Time.deltaTime);
                            sandSpiderAI.refVel = vector - sandSpiderAI.meshContainerPosition;
                            sandSpiderAI.meshContainer.position = sandSpiderAI.meshContainerPosition;
                            sandSpiderAI.meshContainer.rotation = Quaternion.Lerp(sandSpiderAI.meshContainer.rotation, sandSpiderAI.meshContainerTargetRotation, 8f * Time.deltaTime);
                        }

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
                }
                if (!enemy.isOutside)
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
                for (int i = 0; i < outsideTeleports.Count; i++)
                {
                    if (Vector3.Distance(outsideTeleports[i].entrancePoint.transform.position, enemy.transform.position) <= TeleportRange)
                    {
                        enemy.agent.Warp(insideTeleports[i].entrancePoint.transform.position);
                        enemy.SetEnemyOutside(false);
                        pathRange = interiorPathRange;
                        enemy.favoriteSpot = insideFavoriteSpot;

                        //=========== EnemyType Specific ============
                        if (enemy is BlobAI blobAI)
                        {
                            blobAI.centerPoint.position = insideTeleports[i].entrancePoint.transform.position; 
                            for (int j = 0; j < blobAI.maxDistanceForSlimeRays.Length; j++)
                            {
                                blobAI.maxDistanceForSlimeRays[j] = 3.7f;
                                blobAI.SlimeBonePositions[j] = blobAI.SlimeBones[j].transform.position;
                            }
                        }
                        else if (enemy is SandSpiderAI sandSpiderAI)
                        {
                            logger.LogInfo($"Spider-specific code is running.");
                            sandSpiderAI.meshContainerPosition = insideTeleports[i].entrancePoint.transform.position; 
                            sandSpiderAI.meshContainerTarget = sandSpiderAI.meshContainerPosition;
                        }

                        //===========================================

                        TeleportAndRefresh();
                        return;
                    }
                }
            }
            else if (!enemy.isOutside && (Vector3.Distance(enemy.transform.position, closestTeleportPosition) <= TeleportRange) || closeToTeleport) //Run through the list of teleporter IDs to warp to the matching outside teleport.
            {
                for (int i = 0; i < insideTeleports.Count; i++)
                {
                    if (Vector3.Distance(insideTeleports[i].entrancePoint.transform.position, enemy.transform.position) <= TeleportRange)
                    {
                        enemy.agent.Warp(outsideTeleports[i].entrancePoint.transform.position);
                        enemy.SetEnemyOutside(true);
                        pathRange = exteriorPathRange;
                        enemy.favoriteSpot = outsideFavoriteSpot;

                        //=========== EnemyType Specific ============
                        if (enemy is BlobAI blobAI)
                        {
                            blobAI.centerPoint.position = outsideTeleports[i].entrancePoint.transform.position; 
                            for (int j = 0; j < blobAI.maxDistanceForSlimeRays.Length; j++)
                            {
                                blobAI.maxDistanceForSlimeRays[j] = 3.7f;
                                blobAI.SlimeBonePositions[j] = blobAI.SlimeBones[j].transform.position;
                            }
                        }
                        else if (enemy is SandSpiderAI sandSpiderAI)
                        {
                            logger.LogInfo($"Spider-specific code is running.");
                            sandSpiderAI.meshContainerPosition = outsideTeleports[i].entrancePoint.transform.position; 
                            sandSpiderAI.meshContainerTarget = sandSpiderAI.meshContainerPosition; 
                        }

                        //===========================================

                        TeleportAndRefresh();
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
            randomEnemyDestination = enemy.allAINodes[random.Next(0, enemy.allAINodes.Length - 1)].transform.position;
            enemy.SetDestinationToPosition(randomEnemyDestination);
            enemy.agent.SetDestination(randomEnemyDestination);
            enemy.DoAIInterval();

            //=========== EnemyType Specific ============

            AISearchRoutine newRoutine = enemy switch
            {
                BaboonBirdAI baboonBirdAI => baboonBirdAI.scoutingSearchRoutine,
                HoarderBugAI hoarderBugAI => hoarderBugAI.searchForItems,
                BlobAI blobAI => blobAI.searchForPlayers,
                SandSpiderAI sandSpiderAI => sandSpiderAI.patrolHomeBase,
                //RedLocustBees redLocustBeesAI => redLocustBeesAI.searchForHive, //Unimplemented for now, reworking later
                _ => new AISearchRoutine()
            };

            if (enemy is BaboonBirdAI baboon)
            {
                baboon.scoutingSearchRoutine.unsearchedNodes = baboon.allAINodes.ToList();
            }
            else if (enemy is HoarderBugAI hoarder)
            {
                hoarder.searchForItems.unsearchedNodes = hoarder.allAINodes.ToList();
            }
            else if (enemy is CrawlerAI crawlerAI)
            {
                crawlerAI.StartSearch(enemy.transform.position, crawlerAI.searchForPlayers);
            }
            else if (enemy is SpringManAI springManAI)
            {
                springManAI.StartSearch(enemy.transform.position, springManAI.searchForPlayers);
            }
            else if (enemy is SandSpiderAI spider)
            {
                spider.homeNode = enemy.favoriteSpot;
            }
            enemy.StartSearch(enemy.transform.position, newRoutine);

            //===========================================
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]

        private static void EscapeSetup(EnemyAI __instance)
        {
            if (EnemyBlacklist.ContainsKey(__instance.enemyType.enemyName)) { return; }

            if (!EnemyEscapeConfigDictionary.ContainsKey(__instance.enemyType.enemyName)) //Secondary config binding in case the GNM patch doesn't catch something.
            {
                //Vanilla Enemy Binding
                if (VanillaEnemyList.ContainsKey(__instance.enemyType.enemyName))
                {
                    logger.LogInfo($"Adding {__instance.enemyType.enemyName} to the StarlancerEnemyEscape config.");
                    EnemyEscapeConfigDictionary[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{__instance.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")}", -1,
                        new ConfigDescription($"Chance for {__instance.enemyType.enemyName} to go into or out of the facility. Set to -1 to use the value from the chosen preset.",
                        new AcceptableValueRange<int>(-1, 100)));logger.LogInfo($"Adding {__instance.enemyType.enemyName} to the StarlancerEnemyEscape config.");
                    configEscapeExteriorRange[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{__instance.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Exterior Range", extPathRangeDefault,
                        new ConfigDescription($"Range at which {__instance.enemyType.enemyName} can detect a teleport while outside.",
                        new AcceptableValueRange<int>(20, 9999)));
                    configEscapeInteriorRange[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{__instance.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Interior Range", intPathRangeDefault,
                        new ConfigDescription($"Range at which {__instance.enemyType.enemyName} can detect a teleport while inside.",
                        new AcceptableValueRange<int>(10, 9999)));
                    configEscapeCooldownTime[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{__instance.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Cooldown Time", cooldownTimeDefault,
                        new ConfigDescription($"Length of the cooldown between attempts at pathing to a nearby EntranceTeleport for {__instance.enemyType.enemyName}.",
                        new AcceptableValueRange<int>(10, 9999)));
                }
                else
                //Mod Enemy Binding
                {
                    logger.LogInfo($"Adding {__instance.enemyType.enemyName} to the StarlancerEnemyEscape config.");
                    EnemyEscapeConfigDictionary[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{__instance.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")}", -1,
                        new ConfigDescription($"Chance for {__instance.enemyType.enemyName} to go into or out of the facility. Set to -1 to use the value from the chosen preset.",
                        new AcceptableValueRange<int>(20, 100)));
                    configEscapeExteriorRange[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{__instance.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Exterior Range", extPathRangeDefault,
                        new ConfigDescription($"Range at which {__instance.enemyType.enemyName} can detect a teleport while outside.",
                        new AcceptableValueRange<int>(20, 9999)));
                    configEscapeInteriorRange[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{__instance.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Interior Range", intPathRangeDefault,
                        new ConfigDescription($"Range at which {__instance.enemyType.enemyName} can detect a teleport while inside.",
                        new AcceptableValueRange<int>(10, 9999)));
                    configEscapeCooldownTime[__instance.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{__instance.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Cooldown Time", cooldownTimeDefault,
                        new ConfigDescription($"Length of the cooldown between attempts at pathing to a nearby EntranceTeleport for {__instance.enemyType.enemyName}.",
                        new AcceptableValueRange<int>(10, 9999)));
                }
            }

            if (insideAINodes.Length != 0 && __instance.gameObject.GetComponent<StarlancerEscapeComponent>() == null) //Add the Escape Component
            {
                StarlancerEscapeComponent EscapeComponent = __instance.gameObject.AddComponent<StarlancerEscapeComponent>();
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
                if (enemy == null || enemy.enemyType == null || enemy.enemyType.enemyName == null) { continue; }
                if (EnemyBlacklist.ContainsKey(enemy.enemyType.enemyName)) { continue; }
                if (EnemyEscapeConfigDictionary.ContainsKey(enemy.enemyType.enemyName)) { continue; }

                //Vanilla Enemy Binding
                if (VanillaEnemyList.ContainsKey(enemy.enemyType.enemyName))
                {
                    
                    EnemyEscapeConfigDictionary[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{enemy.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")}", -1,
                        new ConfigDescription($"Chance for {enemy.enemyType.enemyName} to go into or out of the facility. Set to -1 to use the value from the chosen preset.",
                        new AcceptableValueRange<int>(-1, 100)));
                    configEscapeExteriorRange[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{enemy.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Exterior Range", extPathRangeDefault,
                        new ConfigDescription($"Range at which {enemy.enemyType.enemyName} can detect a teleport while outside.",
                        new AcceptableValueRange<int>(20, 9999)));
                    configEscapeInteriorRange[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{enemy.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Interior Range", intPathRangeDefault,
                        new ConfigDescription($"Range at which {enemy.enemyType.enemyName} can detect a teleport while inside.",
                        new AcceptableValueRange<int>(10, 9999)));
                    configEscapeCooldownTime[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Vanilla Enemies", $"{enemy.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Cooldown Time", cooldownTimeDefault,
                        new ConfigDescription($"Length of the cooldown between attempts at pathing to a nearby EntranceTeleport for {enemy.enemyType.enemyName}.",
                        new AcceptableValueRange<int>(10, 9999)));
                    continue;
                }
                //Mod Enemy Binding
                EnemyEscapeConfigDictionary[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{enemy.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")}", -1,
                    new ConfigDescription($"Chance for {enemy.enemyType.enemyName} to go into or out of the facility. Set to -1 to use the value from the chosen preset.",
                    new AcceptableValueRange<int>(-1, 100)));
                configEscapeExteriorRange[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{enemy.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Exterior Range", extPathRangeDefault,
                        new ConfigDescription($"Range at which {enemy.enemyType.enemyName} can detect a teleport while outside.",
                        new AcceptableValueRange<int>(20, 9999)));
                configEscapeInteriorRange[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{enemy.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Interior Range", intPathRangeDefault,
                    new ConfigDescription($"Range at which {enemy.enemyType.enemyName} can detect a teleport while inside.",
                    new AcceptableValueRange<int>(10, 9999)));
                configEscapeCooldownTime[enemy.enemyType.enemyName] = EnemyEscapeConfig.Bind("Mod Enemies", $"{enemy.enemyType.enemyName.Replace("=", "").Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "")} Cooldown Time", cooldownTimeDefault,
                        new ConfigDescription($"Length of the cooldown between attempts at pathing to a nearby EntranceTeleport for {enemy.enemyType.enemyName}.",
                        new AcceptableValueRange<int>(10, 9999)));
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(RoundManager), "SetLevelObjectVariables")]
        [HarmonyPostfix]

        private static void EntranceTeleportsAndAINodes()
        {
            enemiesThatCanEscape.Clear();
            outsideTeleports.Clear();
            insideTeleports.Clear();

            entranceTeleports = FindObjectsByType<EntranceTeleport>(FindObjectsSortMode.None); //Find all EntranceTeleports
            for (int i = 0; i < entranceTeleports.Length; i++)
            {
                if (entranceTeleports[i].isEntranceToBuilding)
                {
                    outsideTeleports.Add(entranceTeleports[i]);
                    logger.LogInfo("Finding exterior EntranceTeleports.");
                }
                else
                {
                    insideTeleports.Add(entranceTeleports[i]);
                    logger.LogInfo("Finding interior EntranceTeleports.");
                }
            }
        }

        //====================================================================================================================================================================================
        
        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SetDestinationToPosition))]
        [HarmonyPrefix]

        private static void SetDestinationToPositionPrefix(EnemyAI __instance, ref Vector3 position, ref bool checkForPath)
        {
            if (enemiesThatCanEscape.Contains(__instance))
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
                                __instance.destination = position;
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
                                __instance.destination = position; ;
                            }
                        }
                    }
                }
            }
            
        }
    }
}

