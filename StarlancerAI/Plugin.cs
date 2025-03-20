using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using StarlancerAIFix.Patches;

namespace StarlancerAIFix
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class StarlancerAIFixBase : BaseUnityPlugin
    {
        private const string modGUID = "AudioKnight.StarlancerAIFix";
        private const string modName = "Starlancer AI Fix";
        private const string modVersion = "3.8.4";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static StarlancerAIFixBase Instance;

        internal static ManualLogSource logger;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            logger = Logger;

            logger.LogInfo("Starlancer AI Fix Online.");

            harmony.PatchAll(typeof(StarlancerAIFixBase));
            harmony.PatchAll(typeof(AIFix));
        }
    }
}