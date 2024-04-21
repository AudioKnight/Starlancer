using HarmonyLib;
using GameNetcodeStuff;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using static StarlancerEnemyEscape.StarlancerEnemyEscapeBase;
using UnityEngine.AI;
using EnemyEscape;
using UnityEngine;



namespace EscapeTranspilers
{

    internal class EscapeTranspilers 
    {

        /*// Somewhere in our code we subscribe to the event once:

        // ...
        private static void PlayerControllerB_Jump_performed(ILContext il)
        {
            IL.GameNetcodeStuff.PlayerControllerB.Jump_performed += PlayerControllerB_Jump_performed;
            // We use ILCursor to make modifications to the il code
            ILCursor c = new(il);

            // Find a place inside the if statement which makes us jump.
            // We know the following C# line is inside the if statement:
            // this.playerSlidingTimer = 0f;
            // So we locate it from IL code:
            c.GotoNext(
                // IL_00af: ldarg.0         // load argument 0 'this' onto stack
                // IL_00b0: ldc.r4 0.0      // push 0 onto the stack as float32
                // IL_00b5: stfld float32 GameNetcodeStuff.PlayerControllerB::playerSlidingTimer // replace the value of 'playerSlidingTimer' with value from stack
                x => x.MatchLdarg(0),
                x => x.MatchLdcR4(0.0f),
                // Note that nameof gives the name of a variable, type, or member as a string constant
                // so this is the same as "playerSlidingTimer" but we can more easily change this
                // if the game changes the name of that variable/type/member.
                x => x.MatchStfld<PlayerControllerB>(nameof(PlayerControllerB.playerSlidingTimer))
            // The reason we have multiple things to match is to make sure
            // that even if the original IL code changes, we will find the
            // exact place if it still exists. If GotoNext doesn't match everything,
            // it will throw an exception and this code won't run.
            // If you don't want it to throw an exception, use TryGotoNext instead.
            );
            // Our IL cursor is now located before the first instruction we matched against in GotoNext.
            // The IL cursor will always be between an above and below instruction.
            // If we want to move it, we could for example do c.Index += 3; to move it after the stfld instruction.

            // To insert our C# logic from before, we will do the following:
            // We will emit a delegate Method of type void (Action) which
            // takes an instance of PlayerControllerB as an argument.
            // Because this is IL code, we have to load 'this' (PlayerControllerB) onto
            // stack first, with ldarg.0
            // Any non-static method has 'this' as the first argument
            c.Emit(OpCodes.Ldarg_0); // load argument 0 'this' onto stack
            c.EmitDelegate<Action<PlayerControllerB>>((self) =>
            {
                logger.LogInfo("Hello from C# code in IL!");

                if (self.isSprinting)
                    self.jumpForce = 30f;
                else
                    self.jumpForce = 13f; // this is the default value of jumpForce
            });
            // Plugin.Logger.LogInfo(il.ToString()); // uncomment to print the modified IL code to console
        }*/

        private static void HawkScrapDestinationChanger(ILContext il)
        {
            IL.BaboonBirdAI.DoAIInterval += HawkScrapDestinationChanger;
            ILCursor c = new(il);
            c.GotoNext(
                x => x.MatchLdarg(0),
                x => x.MatchLdcI4(0),
                x => x.MatchCall<EnemyAI>(nameof(EnemyAI.SetDestinationToPosition))
                );
            c.Emit(OpCodes.Ldarg_0);
            c.EmitPop();
            c.EmitDelegate<Action<EnemyAI>>((self) =>
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
        }
    }
}

