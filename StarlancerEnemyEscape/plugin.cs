using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EnemyEscape;
using EscapeTranspilers;
using HarmonyLib;


namespace StarlancerEnemyEscape
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency("AudioKnight.StarlancerAIFix", BepInDependency.DependencyFlags.HardDependency)]
    public class StarlancerEnemyEscapeBase : BaseUnityPlugin
    {
        private const string modGUID = "AudioKnight.StarlancerEnemyEscape";
        private const string modName = "Starlancer EnemyEscape";
        private const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);
        public static StarlancerEnemyEscapeBase Instance;
        internal static ManualLogSource logger;

        internal static ConfigFile EnemyEscapeConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, "AudioKnight.StarlancerEnemyEscape.cfg"), true);

        public enum ConfigPreset
        {
            Disabled,
            ReasonableDefaults,
            Minimal,
            Chaos,
        }

        public static ConfigEntry<ConfigPreset> configEscapePreset;

        internal static Dictionary<string, ConfigEntry<int>> EnemyEscapeConfigDictionary = new Dictionary<string, ConfigEntry<int>>();
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            EnemyEscapeConfig = Config;
            logger = Logger;

            logger.LogInfo("Starlancer is allowing enemies to roam.");

            configEscapePreset = Config.Bind("EnemyEscape", "EscapePreset", ConfigPreset.ReasonableDefaults, new ConfigDescription("Which preset to use. If an enemy has a value manually set below, it will take priority." +
                "\nReasonableDefaults: Blob:0, Bunker Spider:10, Butler:5, Butler Bees:5, Centipede:0, Crawler:10, Flowerman:10, Hoarding bug:10, Jester:1, Nutcracker:5, Puffer:10, Spring:5, Baboon hawk:15, Earth Leviathan:0, ForestGiant:0, Manticoil:5, MouthDog:10, RadMech:0, Red Locust Bees:5" +
                "\nDisabled: All 0s, Minimal: All 1s, Chaos: All 100s"));

            harmony.PatchAll(typeof(StarlancerEnemyEscapeBase));
            harmony.PatchAll(typeof(StarlancerEscapeComponent));
            harmony.PatchAll(typeof(StarlancerEscapeTranspilers));
            

        }

        internal static Dictionary<string, int> GetPresetValues(ConfigPreset preset)
        {
            switch (preset)
            {
                case ConfigPreset.Disabled:
                    {
                        return DisabledPresetValues;
                    }
                case ConfigPreset.ReasonableDefaults:
                    {
                        return DefaultPresetValues;
                    }
                case ConfigPreset.Minimal:
                    {
                        return MinimalPresetValues;
                    }
                case ConfigPreset.Chaos:
                    {
                        return ChaosPresetValues;
                    }
            }
            return DefaultPresetValues;
        }

        internal static Dictionary<string, int> GetCurrentlySelectedPreset()
        {
            return GetPresetValues(configEscapePreset.Value);
        }

        //================= Predefined Dictionaries =================

        internal static Dictionary<string, string> EnemyWhitelist = new Dictionary<string, string>  {
            { "Girl", "Unneeded"},
            { "Docile Locust Bees", "Unneeded"},
            { "Masked", "Unneeded"},
            { "Tulip Snake", "Unneeded"},
            { "Lasso", "Unimplemented"},
            { "Red pill", "Unimplemented"},
        };
        internal static Dictionary<string, string> VanillaEnemyList = new Dictionary<string, string>  {
            { "Blob", "" },
            { "Bunker Spider", "" },
            { "Butler", "" },
            { "Butler Bees", "" },
            { "Centipede", "" },
            { "Crawler", "" },
            { "Flowerman", "" },
            { "Hoarding bug", "" },
            { "Jester", "" },
            { "Nutcracker", "" },
            { "Puffer", "" },
            { "Spring", "" },
            { "Baboon hawk", "" },
            { "Earth Leviathan", "" },
            { "ForestGiant", "" },
            { "Manticoil", "" },
            { "MouthDog", "" },
            { "RadMech", "" },
            { "Red Locust Bees", "" },
        };

        internal static Dictionary<string, int> DefaultPresetValues = new Dictionary<string, int>  {
            { "Blob", 0 },
            { "Bunker Spider", 10 },
            { "Butler", 5 },
            { "Butler Bees", 5 },
            { "Centipede", 0 },
            { "Crawler", 10 },
            { "Flowerman", 10 },
            { "Hoarding bug", 10 },
            { "Jester", 1 },
            { "Nutcracker", 5 },
            { "Puffer", 10 },
            { "Spring", 5 },
            { "Baboon hawk", 15 },
            { "Earth Leviathan", 0 },
            { "ForestGiant", 0 },
            { "Manticoil", 5 },
            { "MouthDog", 10 },
            { "RadMech", 0 },
            { "Red Locust Bees", 5 },
        };
        internal static Dictionary<string, int> DisabledPresetValues = new Dictionary<string, int>  {
            { "Blob", 0 },
            { "Bunker Spider", 0 },
            { "Butler", 0 },
            { "Butler Bees", 0 },
            { "Centipede", 0 },
            { "Crawler", 0 },
            { "Flowerman", 0 },
            { "Hoarding bug", 0 },
            { "Jester", 0 },
            { "Nutcracker", 0 },
            { "Puffer", 0 },
            { "Spring", 0 },
            { "Baboon hawk", 0 },
            { "Earth Leviathan", 0 },
            { "ForestGiant", 0 },
            { "Manticoil", 0 },
            { "MouthDog", 0 },
            { "RadMech", 0 },
            { "Red Locust Bees", 0 },
        };
        internal static Dictionary<string, int> MinimalPresetValues = new Dictionary<string, int>  {
            { "Blob", 1 },
            { "Bunker Spider", 1 },
            { "Butler", 1 },
            { "Butler Bees", 1 },
            { "Centipede", 1 },
            { "Crawler", 1 },
            { "Flowerman", 1 },
            { "Hoarding bug", 1 },
            { "Jester", 1 },
            { "Nutcracker", 1 },
            { "Puffer", 1 },
            { "Spring", 1 },
            { "Baboon hawk", 1 },
            { "Earth Leviathan", 1 },
            { "ForestGiant", 1 },
            { "Manticoil", 1 },
            { "MouthDog", 1 },
            { "RadMech", 1 },
            { "Red Locust Bees", 1 },
        };
        internal static Dictionary<string, int> ChaosPresetValues = new Dictionary<string, int>  {
            { "Blob", 100 },
            { "Bunker Spider", 100 },
            { "Butler", 100 },
            { "Butler Bees", 100 },
            { "Centipede", 100 },
            { "Crawler", 100 },
            { "Flowerman", 100 },
            { "Hoarding bug", 100 },
            { "Jester", 100 },
            { "Nutcracker", 100 },
            { "Puffer", 100 },
            { "Spring", 100 },
            { "Baboon hawk", 100 },
            { "Earth Leviathan", 100 },
            { "ForestGiant", 100 },
            { "Manticoil", 100 },
            { "MouthDog", 100 },
            { "RadMech", 100 },
            { "Red Locust Bees", 100 },
        };
    }
}
