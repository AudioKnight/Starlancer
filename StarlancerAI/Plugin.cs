using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using StarlancerAIFix.Patches;

namespace StarlancerAIFix
{
    [BepInDependency("xCeezy.LethalEscape", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(modGUID, modName, modVersion)]
    public class StarlancerAIFixBase : BaseUnityPlugin
    {
        private const string modGUID = "AudioKnight.StarlancerAIFix";
        private const string modName = "Starlancer AI Fix";
        private const string modVersion = "3.6.0";

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

            foreach (var plugin in Chainloader.PluginInfos)
            {
                var metadata = plugin.Value.Metadata;
                if (metadata.GUID.Equals("xCeezy.LethalEscape"))
                {
                    logger.LogInfo("LethalEscape is active, disabling LEsc's JesterAI.Update() Postfix to ensure compatibility with SLAI.");
                    harmony.Unpatch(typeof(JesterAI).GetMethod("Update"), HarmonyPatchType.Postfix, "LethalEscape");
                    break;
                }
            }
        }
    }
}