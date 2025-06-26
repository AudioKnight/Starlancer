using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
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
        private const string modVersion = "3.11.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static StarlancerAIFixBase Instance;

        internal static ManualLogSource logger;

        internal static ConfigFile AIFixConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, "AudioKnight.StarlancerAIFix.cfg"), true);

        public static ConfigEntry<bool> configLootBugHives;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            configLootBugHives = Config.Bind("General",
                                            "Hoarding Bugs Grab Hives",
                                            false,
                                            "Whether or not Hoarding Bugs can pick up Circuit Bee Hives. This has no effect on the following moons:\n--- Wesley's 58 Hyve \n--- Generic's 72 Collateral");

            logger = Logger;

            logger.LogInfo("Starlancer AI Fix Online.");

            harmony.PatchAll(typeof(StarlancerAIFixBase));
            harmony.PatchAll(typeof(AIFix));
        }
    }
}