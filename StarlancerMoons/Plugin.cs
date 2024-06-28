using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLevelLoader;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace StarlancerMoons
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency("imabatby.lethallevelloader", BepInDependency.DependencyFlags.HardDependency)]
    public class StarlancerMoonsBase : BaseUnityPlugin
    {
        private const string modGUID = "AudioKnight.StarlancerMoons";
        private const string modName = "Starlancer Moons";
        private const string modVersion = "2.2.1";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static StarlancerMoonsBase Instance;

        internal static ManualLogSource logger;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            logger = Logger;

            logger.LogInfo("StarlancerMoons scripts loaded.");

            harmony.PatchAll(typeof(StarlancerMoonsBase));
            harmony.PatchAll(typeof(TerminalManagerPatch));
        }
    }


    public class DamageTrigger : MonoBehaviour
    {
        public float cooldownTime;
        public int damageAmount;
        public CauseOfDeath causeOfDeath;
        private float timeSincePlayerDamaged = 0f;

        private void OnTriggerStay(Collider other)
        {
            PlayerControllerB victim = other.gameObject.GetComponent<PlayerControllerB>();
            if (!other.gameObject.CompareTag("Player"))
            {
                return;
            }
            if ((timeSincePlayerDamaged < cooldownTime))
            {
                timeSincePlayerDamaged += Time.deltaTime;
                return;
            }
            if (victim != null && !victim.isInsideFactory)
            {
                timeSincePlayerDamaged = 0f;
                victim.DamagePlayer(damageAmount, hasDamageSFX: true, callRPC: true, causeOfDeath);
            }
        }
    }

    public class lookAtPlayerXZ : MonoBehaviour
    {
        private Vector3 playerPositionXZ;
        private AudioListener target = StartOfRound.Instance.audioListener;

        private void Update()
        {
            if (StartOfRound.Instance.audioListener != null)
            {
                playerPositionXZ = new Vector3(target.transform.position.x, transform.position.y, target.transform.position.z);
                transform.LookAt(playerPositionXZ);
            }
        }
    }

    [HarmonyPatch(typeof(TerminalManager))]
    static class TerminalManagerPatch
    {
        //internal static ManualLogSource logger;
        static readonly Dictionary<string, string> KeywordReplacements = new() { { "starlancerzero", "anomaly" } };

        static void EditTerminalKeyword(ExtendedLevel level, TerminalKeyword keyword)
        {
            //logger.LogInfo($"Performing edits on keyword: {keyword.word}, for level: {level.NumberlessPlanetName}");
            if (!KeywordReplacements.TryGetValue(keyword.word, out string newWord)) return;
            //logger.LogDebug($"Changing keyword from '{keyword.word}' -> '{newWord}'");
            keyword.word = newWord;
        }

        [HarmonyTranspiler, HarmonyPatch("CreateLevelTerminalData")]
        static IEnumerable<CodeInstruction> DebugLogging(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                   .MatchForward(true,
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(TerminalManager), "routeKeyword")),
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(TerminalKeyword), "defaultVerb"))
                   )
                   .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TerminalManagerPatch), nameof(EditTerminalKeyword)))
                   )
                .InstructionEnumeration();
        }
    }
}
