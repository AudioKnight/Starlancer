using UnityEngine;
using HarmonyLib;
using static StarlancerAIFix.StarlancerAIFixBase;

namespace StarlancerAIFix.Patches
{
    /*public class IndoorVisibilitySpoofer : MonoBehaviour, IVisibleThreat      //Future implementation.
    {
        public EnemyAI thisEnemy;
        public Transform eye;
        public float visibility;
        public int threatLevel;
        public int interestLevel;
        public Transform enemyTransform;
        public NavMeshAgent agent;
        public Vector3 agentLocalVelocity;
        public ThreatType Generic;
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
            if (agent.velocity.sqrMagnitude > 0f)
            {
                return 1f;
            }
            return visibility;
        }
    }*/


    public class AIFix
    {
        public static GameObject[] outsideAINodes;
        public static GameObject[] insideAINodes;
        public static Vector3[] outsideNodePositions;
        public static Vector3[] insideNodePositions;

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

        [HarmonyPatch(typeof(EnemyAI), "Awake")]
        [HarmonyPostfix]
        private static void AIAwakePatch()
        {
            FindOutsideAINodes();
            FindInsideAINodes();
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]

        private static void AIFixPatch(EnemyAI __instance)
        {

            Vector3 enemyPos = __instance.transform.position;
            Vector3 closestOutsideNode = Vector3.positiveInfinity;
            Vector3 closestInsideNode = Vector3.positiveInfinity;

            for (int i = 0; i < outsideNodePositions.Length; i++)
            {
                if ((outsideNodePositions[i] - enemyPos).sqrMagnitude < (closestOutsideNode - enemyPos).sqrMagnitude)
                {
                    closestOutsideNode = outsideNodePositions[i];
                }
            }
            for (int i = 0; i < insideAINodes.Length; i++)
            {
                if ((insideNodePositions[i] - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude)
                {
                    closestInsideNode = insideNodePositions[i];
                }
            }

            if (!__instance.isOutside && ((closestOutsideNode - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude))
            {
                __instance.SetEnemyOutside(true);
                int nodeIndex = UnityEngine.Random.Range(0, __instance.allAINodes.Length - 1);
                __instance.favoriteSpot = __instance.allAINodes[nodeIndex].transform;
                logger.LogInfo($"{__instance.gameObject.name} spawned outside; Switching to exterior AI. Setting Favorite Spot to {__instance.favoriteSpot}.");
            }
            else if (__instance.isOutside && ((closestOutsideNode - enemyPos).sqrMagnitude > (closestInsideNode - enemyPos).sqrMagnitude))
            {
                __instance.SetEnemyOutside(false);
                int nodeIndex = UnityEngine.Random.Range(0, __instance.allAINodes.Length - 1);
                __instance.favoriteSpot = __instance.allAINodes[nodeIndex].transform;
                logger.LogInfo($"{__instance.gameObject.name} spawned inside; Switching to interior AI. Setting Favorite Spot to {__instance.favoriteSpot}.");
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

                    if (__instance.isOutside)
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

        private static void SandwormResetPatch(SandWormAI __instance)
        {
            if (!__instance.isOutside)
            {
                int nodeIndex = UnityEngine.Random.Range(0, __instance.allAINodes.Length - 1);
                __instance.endOfFlightPathPosition = __instance.allAINodes[nodeIndex].transform.position;
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(SpringManAI), "DoAIInterval")]
        [HarmonyPostfix]

        private static void SpringManAnimPatch(SpringManAI __instance)
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

        private static void PufferPrefabPatch(PufferAI __instance)
        {
            if (__instance.isOutside)
            {
                __instance.currentBehaviourStateIndex = 1;
            }
        }

        //====================================================================================================================================================================================

        [HarmonyPatch(typeof(EnemyAI), "EnableEnemyMesh")]
        [HarmonyPrefix]

        private static bool EnemyMeshPatch(EnemyAI __instance, bool enable, bool overrideDoNotSet = false)
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

        /*public static string[] enemyWhitelist = {"Blob", "Butler", "Centipede", "Crawler", "Flowerman", "Hoarding bug", "Nutcracker", "Bunker Spider"};

        [HarmonyPatch(typeof(EnemyAI), "Start")]          //Future implementation.
        [HarmonyPostfix]

        private static void ThreatPatch(EnemyAI __instance)
        {
            logger.LogInfo($"The whitelist contains: {enemyWhitelist}");

            if (Array.IndexOf(enemyWhitelist, __instance.enemyType.name) != -1)
            {
                *//*IVisibleThreat[] threats = __instance.GetComponentsInChildren<IVisibleThreat>();
                if (threats != null) { return; }*//*

                IVisibleThreat threatExists = __instance.GetComponentInChildren<IVisibleThreat>();
                if (threatExists != null) { return; }

                Collider[] threatColliders = __instance.GetComponentsInChildren<Collider>();
                logger.LogInfo($"Making {__instance.gameObject.name} threatening.");

                foreach (var threatCollider in threatColliders)
                {
                    if (threatCollider.gameObject.layer == 19)
                    {
                        IndoorVisibilitySpoofer iVS = threatCollider.gameObject.AddComponent<IndoorVisibilitySpoofer>();

                        iVS.thisEnemy = __instance;
                        iVS.eye = __instance.eye;
                        iVS.visibility = 1f; //0.5 in release, 1f is for Xu Giant testing
                        iVS.threatLevel = 18; //5 in release, 18 is for Xu Giant testing
                        iVS.interestLevel = 0;
                        iVS.enemyTransform = __instance.transform;
                        iVS.agent = __instance.agent;

                        logger.LogInfo($"Adding IVisibleThreat component to {__instance.gameObject.name}'s {threatCollider.gameObject.name}.");
                    }
                }
            }
        }*/
    }
}