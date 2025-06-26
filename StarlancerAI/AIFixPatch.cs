using UnityEngine;
using HarmonyLib;
using static StarlancerAIFix.StarlancerAIFixBase;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic;
using System;

namespace StarlancerAIFix.Patches
{
    public class AIFix
    {
        public static GameObject[] outsideAINodes;
        public static GameObject[] insideAINodes;
        public static Vector3[] outsideNodePositions;
        public static Vector3[] insideNodePositions;
        public static string[] enemyWhitelist = ["Blob", "Butler", "Centipede", "Crawler", "Flowerman", "HoarderBug", "Nutcracker", "SandSpider"];
        public static string[] hiveMoons = ["Asteroid14Scene", "CollateralScene"];

        private static GameObject[] FindOutsideAINodes()
        {
            if (outsideAINodes == null || outsideAINodes.Length == 0 || outsideAINodes[0] == null)
            {
                outsideAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
                logger.LogInfo("Finding outside AI nodes.");
                outsideNodePositions = new Vector3[outsideAINodes.Length];
                
                for (int i = 0; i < outsideAINodes.Length; i++)
                {
                    outsideNodePositions[i] = outsideAINodes[i].transform.position;
                }
            }
            return outsideAINodes;
        }

        private static GameObject[] FindInsideAINodes()
        {
            if (insideAINodes == null || insideAINodes.Length == 0 || insideAINodes[0] == null)
            {
                insideAINodes = GameObject.FindGameObjectsWithTag("AINode");
                logger.LogInfo("Finding inside AI nodes.");
                insideNodePositions = new Vector3[insideAINodes.Length];
                for (int i = 0; i < insideAINodes.Length; i++)
                {
                    insideNodePositions[i] = insideAINodes[i].transform.position;
                }
            }
            return insideAINodes;
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]

        private static void AIFixPatch(EnemyAI __instance)
        {
            FindOutsideAINodes();
            FindInsideAINodes();
            __instance.removedPowerLevel = true;

            if (StartOfRound.Instance.currentLevelID == 3) //If at the company building, enemy is always set to Outside.
            {
                __instance.SetEnemyOutside(true);
            }

            else
            {
                Vector3 enemyPos = __instance.transform.position;
                Vector3 closestOutsideNode = Vector3.positiveInfinity;
                Vector3 closestInsideNode = Vector3.positiveInfinity;

                for (int i = 0; i < outsideNodePositions.Length; i++) //Cache outside node positions.
                {
                    if ((outsideNodePositions[i] - enemyPos).sqrMagnitude < (closestOutsideNode - enemyPos).sqrMagnitude)
                    {
                        closestOutsideNode = outsideNodePositions[i];
                    }
                }
                for (int i = 0; i < insideAINodes.Length; i++) //Cache inside node positions.
                {
                    if ((insideNodePositions[i] - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude)
                    {
                        closestInsideNode = insideNodePositions[i];
                    }
                }

                if (!__instance.isOutside && ((closestOutsideNode - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude)) //Set isOutside true if the enemy is outside.
                {
                    __instance.SetEnemyOutside(true);
                    int nodeIndex = UnityEngine.Random.Range(0, __instance.allAINodes.Length - 1);
                    __instance.favoriteSpot = __instance.allAINodes[nodeIndex].transform;
                    logger.LogInfo($"{__instance.gameObject.name} spawned outside; Switching to exterior AI. Setting Favorite Spot to {__instance.favoriteSpot}.");
                }
                else if (__instance.isOutside && ((closestOutsideNode - enemyPos).sqrMagnitude > (closestInsideNode - enemyPos).sqrMagnitude)) //Set isOutside false if the enemy is inside.
                {
                    __instance.SetEnemyOutside(false);
                    int nodeIndex = UnityEngine.Random.Range(0, __instance.allAINodes.Length - 1);
                    __instance.favoriteSpot = __instance.allAINodes[nodeIndex].transform;
                    logger.LogInfo($"{__instance.gameObject.name} spawned inside; Switching to interior AI. Setting Favorite Spot to {__instance.favoriteSpot}.");
                }
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "SubtractFromPowerLevel")]
        [HarmonyPostfix]

        private static void SubtractPowerLevelPatch(EnemyAI __instance) //When an enemy dies, subtracts its power level from the correct list.
        {
            if (RoundManager.Instance.currentLevel.OutsideEnemies.Any(enemy => enemy.enemyType == __instance.enemyType) /*|| RoundManager.Instance.WeedEnemies.Any(enemy => enemy.enemyType == __instance.enemyType)*/) //Outside & Weeds
            {
                logger.LogInfo($"{__instance.gameObject.name} from the exterior enemy list has died; \nPrevious exterior power level is {RoundManager.Instance.currentOutsideEnemyPower}");

                RoundManager.Instance.currentOutsideEnemyPower = Mathf.Max(RoundManager.Instance.currentOutsideEnemyPower - __instance.enemyType.PowerLevel, 0);

                logger.LogInfo($"Removing {__instance.gameObject.name}'s power ({__instance.enemyType.PowerLevel}) from the RoundManager; \nCurrent exterior power level is {RoundManager.Instance.currentOutsideEnemyPower}");
            }

            else if (RoundManager.Instance.currentLevel.Enemies.Any(enemy => enemy.enemyType == __instance.enemyType)) //Inside
            {
                logger.LogInfo($"{__instance.gameObject.name} from the interior enemy list has died; \nPrevious interior power level is {RoundManager.Instance.currentEnemyPower}");

                RoundManager.Instance.currentEnemyPower = Mathf.Max(RoundManager.Instance.currentEnemyPower - __instance.enemyType.PowerLevel, 0);
                RoundManager.Instance.cannotSpawnMoreInsideEnemies = false;

                logger.LogInfo($"Removing {__instance.gameObject.name}'s power ({__instance.enemyType.PowerLevel}) from the RoundManager; \nCurrent interior power level is {RoundManager.Instance.currentEnemyPower}");
            }

            else if (RoundManager.Instance.currentLevel.DaytimeEnemies.Any(enemy => enemy.enemyType == __instance.enemyType)) //Daytime
            {
                logger.LogInfo($"{__instance.gameObject.name} from the daytime enemy list has died; \nPrevious daytime power level is {RoundManager.Instance.currentDaytimeEnemyPower}");

                RoundManager.Instance.currentDaytimeEnemyPower = Mathf.Max(RoundManager.Instance.currentDaytimeEnemyPower - __instance.enemyType.PowerLevel, 0);

                logger.LogInfo($"Removing {__instance.gameObject.name}'s power ({__instance.enemyType.PowerLevel}) from the RoundManager; \nCurrent daytime power level is {RoundManager.Instance.currentDaytimeEnemyPower}");
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(JesterAI), "Update")]
        [HarmonyPostfix]

        private static void JesterAIPatch(JesterAI __instance, ref bool ___targetingPlayer, ref float ___noPlayersToChaseTimer, ref int ___previousState)
        {
            switch (__instance.currentBehaviourStateIndex)
            {
                case 0:
                    if (__instance.isOutside)
                    {
                        if (__instance.targetPlayer == null && !__instance.roamMap.inProgress)
                        {
                            __instance.StartSearch(__instance.transform.position, __instance.roamMap);
                            __instance.SwitchToBehaviourState(0);
                        }
                    }
                    break;

                case 2:

                    if (__instance.targetPlayer != null)
                    {
                        if (__instance.targetingPlayer && ((!__instance.isOutside && !__instance.targetPlayer.isInsideFactory) || (__instance.isOutside && __instance.targetPlayer.isInsideFactory))) //Allows the Jester to end hostilities if there are no players in its area.
                        {
                            __instance.targetPlayer = null;
                        }
                    }

                    if (__instance.isOutside) //A copy of some vanilla Jester code, but with flipped logic so that it works outside.
                    {
                        if (___previousState != 2)
                        {
                            ___previousState = 2;
                            __instance.farAudio.Stop();
                            __instance.creatureAnimator.SetBool("poppedOut", value: true);
                            __instance.creatureAnimator.SetFloat("CrankSpeedMultiplier", 1f);
                            __instance.creatureSFX.PlayOneShot(__instance.popUpSFX);
                            WalkieTalkie.TransmitOneShotAudio(__instance.creatureSFX, __instance.popUpSFX);
                            __instance.creatureVoice.clip = __instance.screamingSFX;
                            __instance.creatureVoice.Play();
                            __instance.agent.speed = 0f;
                            __instance.mainCollider.isTrigger = true;
                            __instance.agent.stoppingDistance = 0f;
                        }

                        ___targetingPlayer = false;

                        for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                        {
                            if (StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled && !StartOfRound.Instance.allPlayerScripts[i].isInsideFactory)
                            {
                                ___targetingPlayer = true;
                                break;
                            }
                        }
                        if (!___targetingPlayer)
                        {
                            ___noPlayersToChaseTimer -= Time.deltaTime;
                            if (___noPlayersToChaseTimer <= 0f)
                            {
                                __instance.SwitchToBehaviourState(0);
                            }
                        }
                        else
                        {
                            ___noPlayersToChaseTimer = 5f;
                        }
                    }
                    break;
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(SandWormAI), "StartEmergeAnimation")]
        [HarmonyPostfix]

        private static void SandwormResetPatch(SandWormAI __instance) //Sets the position of an interior sandworm to a random node after it attacks.
        {
            if (!__instance.isOutside)
            {
                int nodeIndex = UnityEngine.Random.Range(0, __instance.allAINodes.Length - 1);
                __instance.endOfFlightPathPosition = __instance.allAINodes[nodeIndex].transform.position;
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(SandWormAI), "StartEmergeAnimation")]
        [HarmonyPostfix]

        private static void SandwormAttackPatch(SandWormAI __instance) //A simple postfix that makes the sandworm ignore its normal logic while inside. If it's close enough to trigger StartEmergeAnimation(), it will always follow through.
        {
            if (!__instance.isOutside)
            {
                __instance.EmergeServerRpc((int)RoundManager.Instance.YRotationThatFacesTheFarthestFromPosition(__instance.transform.position + Vector3.up * 1.5f, 30f));
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(SpringManAI), "DoAIInterval")]
        [HarmonyPostfix]

        private static void SpringManAnimPatch(SpringManAI __instance) //If it walks, it walks.
        {
            switch (__instance.currentBehaviourStateIndex)
            {
                case 0:
                    if (__instance.isOutside)
                    {
                        if (__instance.searchForPlayers.inProgress && __instance.agent.speed >= 1f)
                        {
                            __instance.creatureAnimator.SetFloat("walkSpeed", 1f);
                        }
                        break;
                    }
                    break;
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(PufferAI), "Start")]
        [HarmonyPostfix]

        private static void PufferPrefabPatch(PufferAI __instance) //Bizarre fix for a bizarre creature.
        {
            if (__instance.isOutside)
            {
                __instance.currentBehaviourStateIndex = 1;
            }
        }

        //====================================================================================================================================================================================
        /*[HarmonyPatch(typeof(SandSpiderAI), "Update")]
        [HarmonyPostfix]

        private static void SandSpiderMeshReposition(SandSpiderAI __instance) //Body and Mind become One
        {
            if (__instance.agent.speed > 0)
            {
                __instance.meshContainerPosition = __instance.agent.transform.position;
                __instance.meshContainerTarget = __instance.meshContainerPosition;
            }
        }*/
        //Disabled in favor of Fandovec03's SpiderPositionFix. Also it was breaking things apparently.

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(ButlerEnemyAI), "Update")]
        [HarmonyPostfix]

        private static void ButlerMusicOutside(ButlerEnemyAI __instance) //Flipped logic to allow butler music to play outside.
        {
            switch (__instance.currentBehaviourStateIndex)
            {
                case 2:
                    {
                        if (__instance.isOutside && !__instance.startedMurderMusic)
                        {
                            if (!GameNetworkManager.Instance.localPlayerController.isInsideFactory && GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 0.7f, 100f, 18, 1f))
                            {
                                __instance.startedMurderMusic = true;
                            }
                            break;
                        }
                        __instance.ambience1.volume = Mathf.Lerp(__instance.ambience1.volume, 0f, Time.deltaTime * 7f);
                        if (__instance.isOutside && !GameNetworkManager.Instance.localPlayerController.isInsideFactory)
                        {
                            if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 0.7f, 100f, 18, 1f))
                            {
                                ButlerEnemyAI.murderMusicVolume = Mathf.Max(ButlerEnemyAI.murderMusicVolume, Mathf.Lerp(ButlerEnemyAI.murderMusicVolume, 0.7f, Time.deltaTime * 3f));
                            }
                            else
                            {
                                ButlerEnemyAI.murderMusicVolume = Mathf.Max(ButlerEnemyAI.murderMusicVolume, Mathf.Lerp(ButlerEnemyAI.murderMusicVolume, 0.36f, Time.deltaTime * 3f));
                            }
                            ButlerEnemyAI.increaseMurderMusicVolume = true;
                        }
                        if (__instance.ambience1.isPlaying && __instance.ambience1.volume <= 0.01f)
                        {
                            __instance.ambience1.Stop();
                        }
                        if (!ButlerEnemyAI.murderMusicAudio.isPlaying)
                        {
                            ButlerEnemyAI.murderMusicAudio.Play();
                        }
                    }
                    break;
            }
            
        }

        //====================================================================================================================================================================================
        
        [HarmonyPatch(typeof(RedLocustBees), "Start")]
        [HarmonyPostfix]

        private static void DoNotGrabHive(RedLocustBees __instance)
        {
            if (!hiveMoons.Contains(RoundManager.Instance.currentLevel.sceneName) && !configLootBugHives.Value) //So Wesley can have aggressive loot bugs on Hyve.
            {
                Debug.LogWarning($"Making this hive ungrabbable by enemies");
                __instance.hive.grabbableToEnemies = false;
            }
            /*if (RoundManager.Instance.currentLevel.sceneName != "Asteroid14Scene") //So Wesley can have aggressive loot bugs on Hyve.
            {
                __instance.hive.grabbableToEnemies = false;
            }*/
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "EnableEnemyMesh")]
        [HarmonyPrefix]

        private static bool EnemyMeshPatch(EnemyAI __instance, bool enable, bool overrideDoNotSet = false) //Redundant with ButteryFixes, but the method is under EnemyAI so I'll just leave this here.
        {
            int skinNull = 0;
            int meshNull = 0;
            int layer = ((!enable) ? 23 : 19);

            for (int i = 0; i < __instance.skinnedMeshRenderers.Length; i++)
            {
                if (__instance.skinnedMeshRenderers[i] != null && (!__instance.skinnedMeshRenderers[i].CompareTag("DoNotSet") || overrideDoNotSet))
                {
                    __instance.skinnedMeshRenderers[i].gameObject.layer = layer;
                }
                else if (__instance.skinnedMeshRenderers[i] == null)
                {
                    List<SkinnedMeshRenderer> skinList = new List<SkinnedMeshRenderer>(__instance.skinnedMeshRenderers);
                    for (int j = 0; j < skinList.Count;)
                    {
                        if (skinList[j] == null)
                        {
                            skinNull++;
                            skinList.RemoveAt(j);
                        }
                        else j++;
                    }
                    logger.LogWarning($"Found and removed {skinNull} null SkinnedMeshRenderers in {__instance.gameObject.name} ({__instance.thisEnemyIndex}) to prevent potential null reference exceptions.");
                    __instance.skinnedMeshRenderers = skinList.ToArray();
                }
            }
            for (int i = 0; i < __instance.meshRenderers.Length; i++)
            {
                if (__instance.meshRenderers[i] != null && (!__instance.meshRenderers[i].CompareTag("DoNotSet") || overrideDoNotSet))
                {
                    __instance.meshRenderers[i].gameObject.layer = layer;
                }
                else if ((__instance.meshRenderers[i] == null))
                {
                    List<MeshRenderer> meshList = new List<MeshRenderer>(__instance.meshRenderers);
                    for (int j = 0; j < meshList.Count;)
                    {
                        if (meshList[j] == null)
                        {
                            meshNull++;
                            meshList.RemoveAt(j);
                        }
                        else j++;
                    }
                    logger.LogWarning($"Found and removed {meshNull} null MeshRenderers in {__instance.gameObject.name} ({__instance.thisEnemyIndex}) to prevent potential null reference exceptions.");
                    __instance.meshRenderers = meshList.ToArray();
                }
            }
            return false;
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "PlayerIsTargetable")] //Thanks 1A3!
        [HarmonyPrefix]
        private static void PlayerIsTargetablePatch(ref bool overrideInsideFactoryCheck)
        {
            if (StartOfRound.Instance.currentLevelID == 3) //If at the company building, ignore the inside factory check.
            {
                overrideInsideFactoryCheck = true;
            }
        }

        //====================================================================================================================================================================================
        internal class ThreatComponent : MonoBehaviour, IVisibleThreat //A dummy IVisibleThreat component to allow enemies like the RadMech to target interior enemies like the Bracken.
        {
            public EnemyAI thisEnemy;
            public Transform eye;
            public float visibility;
            public int threatLevel;
            public int interestLevel;
            public Transform enemyTransform;
            public NavMeshAgent agent;
            public Vector3 agentLocalVelocity;
            public ThreatType Generic = ThreatType.BaboonHawk; //Must not be null. Enemies looking into ThreatType gets NullReferenceException. Put BaboonHawk as a temporary solution.

            ThreatType IVisibleThreat.type => Generic;

            int IVisibleThreat.SendSpecialBehaviour(int id)
            {
                return 0;
            }

            int IVisibleThreat.GetThreatLevel(Vector3 seenByPosition)
            {
                return threatLevel;
            }

            int IVisibleThreat.GetInterestLevel()
            {
                return interestLevel;
            }

            Transform IVisibleThreat.GetThreatLookTransform()
            {
                return eye;
            }

            Transform IVisibleThreat.GetThreatTransform()
            {
                return enemyTransform;
            }

            Vector3 IVisibleThreat.GetThreatVelocity()
            {
                if (thisEnemy.IsOwner)
                {
                    return agent.velocity;
                }
                return Vector3.zero;
            }

            float IVisibleThreat.GetVisibility()
            {
                if (thisEnemy.isEnemyDead)
                {
                    return 0f;
                }
                else
                {
                    visibility = 1f; //Visibility should not be based on velocity
                }
                return visibility;
            }

            GrabbableObject IVisibleThreat.GetHeldObject()
            {
                if (thisEnemy is HoarderBugAI)
                {
                    if ((thisEnemy as HoarderBugAI).heldItem == null) return null;
                    else return (thisEnemy as HoarderBugAI).heldItem.itemGrabbableObject; // Allows enemies through IVisibleThreat what item is Hoarding bug holding.
                }
                return null;
            }

            bool IVisibleThreat.IsThreatDead()
            {
                return thisEnemy.isEnemyDead;
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "Start")] // Adds a dummy collider so enemies using OverlapSpheres for LOS can see the enemy
        [HarmonyPostfix]

        private static void DummyCollider(EnemyAI __instance)
        {
            BoxCollider collider = __instance.gameObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = __instance.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.enabled = true;
                collider.size = Vector3.zero;
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]

        private static void ThreatPatch(EnemyAI __instance)
        {
            if (Array.IndexOf(enemyWhitelist, __instance.enemyType.name) != -1)
            {
                logger.LogInfo($"The Enemy Whitelist contains: {string.Join(", ", enemyWhitelist)}.");

                IVisibleThreat threatAlreadyExists = __instance.GetComponentInChildren<IVisibleThreat>();
                if (threatAlreadyExists != null) { return; } //Do nothing if an IVisibleThreat component somehow exists already.

                if (threatAlreadyExists == null)
                {
                    logger.LogInfo($"Adding IVisibleThreat component to {__instance.gameObject.name}.");

                    ThreatComponent IVS = __instance.gameObject.AddComponent<ThreatComponent>();

                    IVS.thisEnemy = __instance;
                    IVS.eye = __instance.eye;
                    IVS.visibility = 0.5f;
                    IVS.threatLevel = 3;
                    IVS.interestLevel = 0;
                    IVS.enemyTransform = __instance.transform;
                    IVS.agent = __instance.agent;

                }
            }
        }
    }
}