using HarmonyLib;
using System.Reflection.Emit;
using static StarlancerEnemyEscape.StarlancerEnemyEscapeBase;
using UnityEngine.AI;
using EnemyEscape;
using StarlancerAIFix.Patches;
using UnityEngine;

//======================================================================= UNUSED IN CURRENT VERSION OF ENEMYESCAPE =======================================================================

namespace EscapeTranspilers
{

    internal class StarlancerEscapeTranspilers 
    {
        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SetDestinationToPosition))]
        [HarmonyTranspiler]

        static IEnumerable<CodeInstruction> SetDestinationToPositionTranspiler(ref Vector3 __position, IEnumerable<CodeInstruction> instructions)
        {
            Vector3 position = __position;

            return new CodeMatcher(instructions)
                .MatchForward(true,
                new CodeMatch(OpCodes.Ldarg_2),
                new CodeMatch(OpCodes.Ldsfld, typeof(BaboonBirdAI).GetField(nameof(BaboonBirdAI.baboonCampPosition))),
                new CodeMatch(OpCodes.Ldc_I4_0))
                .Insert(new CodeInstruction(OpCodes.Ldarg_0,
                CodeInstruction.Call(typeof(EnemyAI), nameof(EnemyAI.SetDestinationToPosition))),
                Transpilers.EmitDelegate<Action<EnemyAI>>((enemy) =>
                {
                    logger.LogWarning("SetDestinationToPositionTranspiler is trying to do something!");

                    bool nodeInOtherArea = false;
                    NavMeshPath pathToTeleport = new NavMeshPath();

                    if (enemy.isOutside)
                    {
                        foreach (Vector3 node in AIFix.outsideNodePositions)
                        {
                            if (Vector3.Distance(node, position) < 10)
                            {
                                nodeInOtherArea = false;
                                break;
                            }
                            else { nodeInOtherArea = true; }
                        }
                        if (nodeInOtherArea)
                        {
                            logger.LogWarning("Target position is inside, checking for reachable teleport.");
                            float closestTeleportDistance = float.PositiveInfinity;
                            foreach (EntranceTeleport teleport in StarlancerEscapeComponent.insideTeleports)
                            {
                                NavMesh.CalculatePath(enemy.transform.position, teleport.transform.position, enemy.agent.areaMask, pathToTeleport);
                                if (pathToTeleport.status != NavMeshPathStatus.PathComplete && Vector3.Distance(enemy.transform.position, teleport.transform.position) < closestTeleportDistance)
                                {
                                    position = teleport.transform.position;
                                    logger.LogWarning($"Teleport found, changing destination to {position}.");
                                }
                                
                            }
                        }
                    }
                    if (!enemy.isOutside)
                    {
                        foreach (Vector3 node in AIFix.insideNodePositions)
                        {
                            if (Vector3.Distance(node, position) < 10)
                            {
                                nodeInOtherArea = false;
                                break;
                            }
                            else { nodeInOtherArea = true; }
                        }
                        if (nodeInOtherArea)
                        {
                            logger.LogWarning("Target position is outside, checking for reachable teleport.");
                            float closestTeleportDistance = float.PositiveInfinity;
                            foreach (EntranceTeleport teleport in StarlancerEscapeComponent.insideTeleports)
                            {
                                NavMesh.CalculatePath(enemy.transform.position, teleport.transform.position, enemy.agent.areaMask, pathToTeleport);
                                if (pathToTeleport.status != NavMeshPathStatus.PathComplete && Vector3.Distance(enemy.transform.position, teleport.transform.position) < closestTeleportDistance)
                                {
                                    position = teleport.transform.position;
                                    logger.LogWarning($"Teleport found, changing destination to {position}.");
                                }
                                
                            }
                        }
                    }
                }))
                         .InstructionEnumeration();
        }

/*        static IEnumerable<CodeInstruction> HawkDestinationTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchForward(true,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldsfld, typeof(BaboonBirdAI).GetField(nameof(BaboonBirdAI.baboonCampPosition))),
                new CodeMatch(OpCodes.Ldc_I4_0))
                .Insert(new CodeInstruction(OpCodes.Ldarg_0,
                CodeInstruction.Call(typeof(EnemyAI), nameof(EnemyAI.SetDestinationToPosition))),
                Transpilers.EmitDelegate<Action<BaboonBirdAI>>((hawk) =>
                {
                    logger.LogWarning("Baboon hawk is trying to carry scrap back to the nest!");

                    var pathToTeleport = new NavMeshPath();
                    var pathToCamp = new NavMeshPath();
                    NavMesh.CalculatePath(hawk.transform.position, BaboonBirdAI.baboonCampPosition, hawk.agent.areaMask, pathToCamp);
                    if (pathToCamp.status != NavMeshPathStatus.PathComplete)
                    {
                        foreach (EntranceTeleport teleport in StarlancerEscapeComponent.entranceTeleports)
                        {
                            NavMesh.CalculatePath(hawk.transform.position, teleport.entrancePoint.transform.position, hawk.agent.areaMask, pathToTeleport); //Check for a valid path to this entrance.

                            if (pathToTeleport.status == NavMeshPathStatus.PathComplete)
                            {
                                hawk.SetDestinationToPosition(teleport.transform.position);
                                break;
                            }
                        }
                    }
                    else { hawk.SetDestinationToPosition(BaboonBirdAI.baboonCampPosition); }

                }))
                         .InstructionEnumeration();
        }
*/
        /*internal static void HawkScrapDestinationChanger(ILContext il)
        {

            logger.LogInfo("Attempting to do IL shenanigans");
            
            ILCursor c = new(il);
            

            c.GotoNext(
                x => x.MatchLdarg(0),
                x => x.MatchLdsfld<BaboonBirdAI>(nameof(BaboonBirdAI.baboonCampPosition)),
                x => x.MatchLdcI4(0),
                x => x.MatchCall<EnemyAI>(nameof(EnemyAI.SetDestinationToPosition))
                );
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<BaboonBirdAI>>((self) =>
            {
                logger.LogWarning("Baboon hawk is trying to carry scrap back to the nest!");

                var pathToTeleport = new NavMeshPath();

                foreach (EntranceTeleport teleport in StarlancerEscapeComponent.entranceTeleports)
                {
                    NavMesh.CalculatePath(self.transform.position, teleport.entrancePoint.transform.position, self.agent.areaMask, pathToTeleport); //Check for a valid path to this entrance.

                    if (pathToTeleport.status == NavMeshPathStatus.PathComplete)
                    {
                        self.SetDestinationToPosition(teleport.transform.position);
                        break;
                    }

                }
            });
            logger.LogInfo(il);
        }*/
    }
}
