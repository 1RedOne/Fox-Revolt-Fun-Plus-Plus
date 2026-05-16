using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoxRevoltFunPlusPlus
{    

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "fox.foxrevoltfunplusplus";
        public const string PluginName = "Fox Revolt Fun++";
        public const string PluginVersion = "0.1.26";
        public const string BuildMarker = "plugin-layout-cleanup-2026-05-16-a";

        internal static ManualLogSource Log;
        internal static BuffSettings Buffs;
        internal static string BuffsJsonPath;
        internal static float HealingMultiplier;
        internal static float BasePlayerAttackSpeedBonus;
        internal static float EarlyLevelPickSpeedAreaBonus;
        internal static float LaterLevelPickSpeedAreaBonus;
        internal static float HealthRegenerationMultiplier;
        internal static float CoopReviveHealthPercent;
        internal static string MoeWarriorIdContains;
        internal static string MoeSecretMoveAbilityIdContains;
        internal static int MoeSecretMoveExtraCharges;
        internal static float MoeSecretMoveRechargeTimeMultiplier;
        internal static float MoeSecretMoveCooldownMultiplier;
        internal static string CheersAbilityIdContains;
        internal static float CheersBuffStatMultiplier;
        internal static float CheersBuffDurationMultiplier;
        internal static bool CheersRemoveHealthPenalty;
        internal static bool CheersBuffNegativeStats;
        internal static bool VerboseLogging;
        internal static bool HeartbeatLogging;
        internal static readonly Dictionary<int, float> LastLoggedLevelSpeedBonusByPlayer = new Dictionary<int, float>();
        internal static readonly HashSet<int> LoggedBaseSpeedPlayers = new HashSet<int>();
        internal static readonly HashSet<string> LoggedScreens = new HashSet<string>();
        internal static readonly Dictionary<int, string> LastGameStateBaseStates = new Dictionary<int, string>();
        internal static readonly HashSet<int> LoggedManualAbilityInstances = new HashSet<int>();
        internal static readonly HashSet<int> PatchedMoeSecretMoveInstances = new HashSet<int>();
        internal static readonly HashSet<int> PatchedCheersAbilityConfigs = new HashSet<int>();

        private static bool _initialized;
        private static Harmony _sharedHarmony;
        private Harmony _harmony;
                
        private int _unityLogCallbackCount;
        private int _activeSceneLogCount;
        private bool _isQuitting;

        private void Awake()
        {
            Log = Logger;
            UnityEngine.Object.DontDestroyOnLoad(this.gameObject);

            if (_initialized)
            {
                Log.LogInfo($"{PluginName} {PluginVersion} duplicate Awake observed. Existing patches remain active. BuildMarker={BuildMarker}");
                return;
            }

            _initialized = true;
            BuffsJsonPath = Path.Combine(Paths.ConfigPath, "buffs.json");
            Buffs = LoadBuffSettings(BuffsJsonPath);
            ApplyBuffSettings(Buffs);

            _sharedHarmony = new Harmony(PluginGuid);
            _harmony = _sharedHarmony;
            _sharedHarmony.PatchAll();
            Application.logMessageReceived += HandleUnityLogMessageReceived;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. BuildMarker={BuildMarker}");
            Log.LogInfo($"Buffs loaded from {BuffsJsonPath}: HealingMultiplier={HealingMultiplier:0.###}, BasePlayerAttackSpeedBonus={BasePlayerAttackSpeedBonus:0.###}, EarlyLevelPickSpeedAreaBonus={EarlyLevelPickSpeedAreaBonus:0.###}, LaterLevelPickSpeedAreaBonus={LaterLevelPickSpeedAreaBonus:0.###}, HealthRegenerationMultiplier={HealthRegenerationMultiplier:0.###}, CoopReviveHealthPercent={CoopReviveHealthPercent:0.###}, MoeSecretMoveExtraCharges={MoeSecretMoveExtraCharges}, MoeSecretMoveRechargeTimeMultiplier={MoeSecretMoveRechargeTimeMultiplier:0.###}, MoeSecretMoveCooldownMultiplier={MoeSecretMoveCooldownMultiplier:0.###}, CheersBuffStatMultiplier={CheersBuffStatMultiplier:0.###}, CheersBuffDurationMultiplier={CheersBuffDurationMultiplier:0.###}, CheersRemoveHealthPenalty={CheersRemoveHealthPenalty}, VerboseLogging={VerboseLogging}, HeartbeatLogging={HeartbeatLogging}");
            LogPatchedMethods();
        }

        private void OnDestroy()
        {
            if (!_isQuitting)
                return;

            Application.logMessageReceived -= HandleUnityLogMessageReceived;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

    [HarmonyPatch(typeof(BattleSimulationUnit), nameof(BattleSimulationUnit.AddHealth))]
    internal static class BattleSimulationUnitAddHealthPatch
    {
        private static void Prefix(BattleSimulationUnit __instance, ref float amount)
        {
            if (amount <= 0f)
                return;

            float originalAmount = amount;
            amount *= Plugin.HealingMultiplier;

            if (Plugin.VerboseLogging)
                Plugin.Log.LogInfo($"[Healing] UnitId={__instance.GetId()} healing {originalAmount:0.###} -> {amount:0.###} (x{Plugin.HealingMultiplier:0.###}).");
        }
    }

    [HarmonyPatch(typeof(PlayerLoadout), nameof(PlayerLoadout.GetAdditionalStatList))]
    internal static class PlayerLoadoutGetAdditionalStatListPatch
    {
        private static void Postfix(PlayerLoadout __instance, ref List<Stat> __result)
        {
            int playerIndex = __instance.PlayerIndex;
            int levelPickCount = Math.Max(0, __instance.GetLevel() - 1);
            int effectivePickCount = levelPickCount;
            float baseBonus = Plugin.BasePlayerAttackSpeedBonus;
            float levelSpeedAreaBonus = Plugin.CalculateLevelPickSpeedAreaBonus(effectivePickCount);
            float totalSpeedBonus = baseBonus + levelSpeedAreaBonus;
            float regenMultiplier = Math.Max(0f, Plugin.HealthRegenerationMultiplier);
            float regenBonus = regenMultiplier - 1f;

            if (Math.Abs(totalSpeedBonus) <= 0.0001f && Math.Abs(levelSpeedAreaBonus) <= 0.0001f && Math.Abs(regenBonus) <= 0.0001f)
                return;

            if (Math.Abs(totalSpeedBonus) > 0.0001f)
            {
                __result.Add(new Stat()
                {
                    type = Stat.StatType.AttackSpeedMultiplier,
                    value = totalSpeedBonus
                });
            }

            if (Math.Abs(levelSpeedAreaBonus) > 0.0001f)
            {
                __result.Add(new Stat()
                {
                    type = Stat.StatType.AttackAreaMultiplier,
                    value = levelSpeedAreaBonus
                });
            }

            if (Math.Abs(regenBonus) > 0.0001f)
            {
                __result.Add(new Stat()
                {
                    type = Stat.StatType.HealthRegenMultiplier,
                    value = regenBonus
                });
            }

            if (!Plugin.LoggedBaseSpeedPlayers.Contains(playerIndex))
            {
                Plugin.LoggedBaseSpeedPlayers.Add(playerIndex);
                Plugin.Log.LogInfo($"[StatInjection] Player={playerIndex} via PlayerLoadout.GetAdditionalStatList: AttackSpeed +{totalSpeedBonus:P0} ({baseBonus:P0} base + {levelSpeedAreaBonus:P0} level), AttackArea +{levelSpeedAreaBonus:P0}, HealthRegenMultiplier x{regenMultiplier:0.###}. Level={__instance.GetLevel()}, effective picks={effectivePickCount}.");
            }

            Plugin.LastLoggedLevelSpeedBonusByPlayer.TryGetValue(playerIndex, out float previousBonus);
            if (levelSpeedAreaBonus > previousBonus + 0.0001f)
            {
                Plugin.LastLoggedLevelSpeedBonusByPlayer[playerIndex] = levelSpeedAreaBonus;
                if (Plugin.VerboseLogging)
                    Plugin.Log.LogInfo($"[LevelPickStats] SPEED/AREA INCREASED: Player={playerIndex} Level={__instance.GetLevel()} effective picks={effectivePickCount} level bonus {previousBonus:P0} -> {levelSpeedAreaBonus:P0}; total speed injected +{totalSpeedBonus:P0}.");
            }
        }
    }

        private static BuffSettings LoadBuffSettings(string path)
        {
            BuffSettings settings = new BuffSettings();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, JsonUtility.ToJson(settings, true));
                    Log.LogInfo($"[BuffsJson] Created default buffs.json at {path}.");
                    return settings;
                }

                string json = File.ReadAllText(path);
                JsonUtility.FromJsonOverwrite(json, settings);
                File.WriteAllText(path, JsonUtility.ToJson(settings, true));
                Log.LogInfo($"[BuffsJson] Loaded buffs.json from {path}.");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[BuffsJson] Failed to load {path}; using built-in defaults. {ex.GetType().Name}: {ex.Message}");
            }

            return settings;
        }

        private static void ApplyBuffSettings(BuffSettings settings)
        {
            HealingMultiplier = settings.HealingMultiplier;
            BasePlayerAttackSpeedBonus = settings.BasePlayerAttackSpeedBonus;
            EarlyLevelPickSpeedAreaBonus = settings.EarlyLevelPickSpeedAreaBonus;
            LaterLevelPickSpeedAreaBonus = settings.LaterLevelPickSpeedAreaBonus;
            HealthRegenerationMultiplier = settings.HealthRegenerationMultiplier;
            CoopReviveHealthPercent = settings.CoopReviveHealthPercent;
            MoeWarriorIdContains = settings.MoeWarriorIdContains ?? "";
            MoeSecretMoveAbilityIdContains = settings.MoeSecretMoveAbilityIdContains ?? "";
            MoeSecretMoveExtraCharges = settings.MoeSecretMoveExtraCharges;
            MoeSecretMoveRechargeTimeMultiplier = settings.MoeSecretMoveRechargeTimeMultiplier;
            MoeSecretMoveCooldownMultiplier = settings.MoeSecretMoveCooldownMultiplier;
            CheersAbilityIdContains = settings.CheersAbilityIdContains ?? "";
            CheersBuffStatMultiplier = settings.CheersBuffStatMultiplier;
            CheersBuffDurationMultiplier = settings.CheersBuffDurationMultiplier;
            CheersRemoveHealthPenalty = settings.CheersRemoveHealthPenalty;
            CheersBuffNegativeStats = settings.CheersBuffNegativeStats;
            VerboseLogging = settings.VerboseLogging;
            HeartbeatLogging = settings.HeartbeatLogging;
        }

        internal static float CalculateLevelPickSpeedAreaBonus(int effectivePickCount)
        {
            int earlyPickCount = Math.Min(Math.Max(0, effectivePickCount), 3);
            int laterPickCount = Math.Max(0, effectivePickCount - 3);
            return earlyPickCount * EarlyLevelPickSpeedAreaBonus + laterPickCount * LaterLevelPickSpeedAreaBonus;
        }

        internal static bool ContainsIgnoreCase(string value, string needle)
        {
            if (string.IsNullOrWhiteSpace(needle))
                return true;

            return !string.IsNullOrEmpty(value) && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool ContainsAnyConfiguredToken(string value, string configuredTokens, bool blankMatches)
        {
            if (string.IsNullOrWhiteSpace(configuredTokens))
                return blankMatches;

            foreach (string token in configuredTokens.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = token.Trim();
                if (trimmed.Length > 0 && ContainsIgnoreCase(value, trimmed))
                    return true;
            }

            return false;
        }

        internal static int GetInstanceKey(object instance) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(instance);

        
        private void HandleUnityLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (!HeartbeatLogging)
                return;

            _unityLogCallbackCount++;
            bool interesting =
                _unityLogCallbackCount <= 20 ||
                condition.Contains("GameState", StringComparison.OrdinalIgnoreCase) ||
                condition.Contains("TimeDif", StringComparison.OrdinalIgnoreCase) ||
                condition.Contains("Gracefully stopped", StringComparison.OrdinalIgnoreCase) ||
                condition.Contains("Level ", StringComparison.OrdinalIgnoreCase);

            if (interesting)
                Log.LogInfo($"[UnityLogCallback] #{_unityLogCallbackCount} Type={type} Message={condition}");
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HeartbeatLogging)
                Log.LogInfo($"[SceneLifecycle] sceneLoaded name='{scene.name}' mode={mode} buildIndex={scene.buildIndex}.");
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            if (HeartbeatLogging)
            {
                bool renderBoothSwap = previous.name == "RenderBoothEnvironment" || next.name == "RenderBoothEnvironment";
                if (renderBoothSwap && _activeSceneLogCount >= 6)
                    return;

                _activeSceneLogCount++;
                Log.LogInfo($"[SceneLifecycle] activeSceneChanged '{previous.name}' -> '{next.name}'.");
            }
        }

        private static void LogPatchedMethods()
        {
            if (!HeartbeatLogging)
                return;

            int count = 0;
            foreach (MethodBase method in Harmony.GetAllPatchedMethods())
            {
                Patches patches = Harmony.GetPatchInfo(method);
                bool ownedByUs = patches?.Owners != null && patches.Owners.Contains(PluginGuid);
                if (!ownedByUs)
                    continue;

                count++;
                Log.LogInfo($"[PatchCheck] Patched {method.DeclaringType?.FullName}.{method.Name}");
            }

            Log.LogInfo($"[PatchCheck] Total patched methods owned by {PluginGuid}: {count}");
        }
    }

    [HarmonyPatch(typeof(Debug), nameof(Debug.Log), new Type[] { typeof(object) })]
    internal static class UnityDebugLogPatch
    {
        private static int _interestingLogCount;

        private static void Prefix(object message)
        {
            if (!Plugin.HeartbeatLogging || message == null)
                return;

            string text = message.ToString();
            if (string.IsNullOrEmpty(text))
                return;

            bool interesting =
                text.Contains("GameState", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("TimeDif at end of GameStateBattle", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Gracefully stopped", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Level ", StringComparison.OrdinalIgnoreCase);

            if (!interesting)
                return;

            _interestingLogCount++;
            if (_interestingLogCount <= 80)
                Plugin.Log.LogInfo($"[UnityLogProbe] Unity Debug.Log observed: {text}");
        }
    }

    [HarmonyPatch(typeof(GameSimulation), nameof(GameSimulation.Initialize))]
    internal static class GameSimulationInitializePatch
    {
        private static void Postfix(GameSimulation __instance)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            int loadoutCount = __instance.GetLoadouts()?.Count ?? 0;
            Plugin.Log.LogInfo($"[RunStart] GameSimulation.Initialize fired. BuildMarker={Plugin.BuildMarker}; loadouts={loadoutCount}.");
        }
    }

    [HarmonyPatch(typeof(BattleSimulation), nameof(BattleSimulation.Initialize))]
    internal static class BattleSimulationInitializePatch
    {
        private static void Postfix()
        {
            if (!Plugin.HeartbeatLogging)
                return;

            Plugin.Log.LogInfo($"[RunStart] BattleSimulation.Initialize fired. BuildMarker={Plugin.BuildMarker}.");
        }
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.RequestGameState))]
    internal static class GameStateManagerRequestGameStatePatch
    {
        private static void Prefix(GameData.GameStateId id)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            Plugin.Log.LogInfo($"[GameStateProbe] GameStateManager.RequestGameState({id}) fired. BuildMarker={Plugin.BuildMarker}.");
        }
    }

    [HarmonyPatch(typeof(GameStateManager), "CreateNewGameState")]
    internal static class GameStateManagerCreateNewGameStatePatch
    {
        private static void Postfix(GameStateManager __instance)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            GameStateBase current = Traverse.Create(__instance).Field<GameStateBase>("_currentGameState").Value;
            Plugin.Log.LogInfo($"[GameStateProbe] CreateNewGameState postfix: Current={(current == null ? "null" : current.GetType().FullName)}.");
        }
    }

    [HarmonyPatch(typeof(GameStateBase), nameof(GameStateBase.Update))]
    internal static class GameStateBaseUpdatePatch
    {
        private static int _idleLogCount;

        private static void Prefix(GameStateBase __instance)
        {
            if (!Plugin.HeartbeatLogging || __instance == null)
                return;

            int id = __instance.GetInstanceID();
            string state = Traverse.Create(__instance).Field("_state").GetValue()?.ToString() ?? "unknown";
            string key = __instance.GetType().FullName + ":" + state;

            Plugin.LastGameStateBaseStates.TryGetValue(id, out string previous);
            if (previous != key)
            {
                Plugin.LastGameStateBaseStates[id] = key;
                Plugin.Log.LogInfo($"[GameStateProbe] {__instance.GetType().FullName}.Update entering state {state}; GameStateId={__instance.GetGameStateId()}.");
            }
            else if (state == "Idle" && _idleLogCount < 10)
            {
                _idleLogCount++;
                Plugin.Log.LogInfo($"[GameStateProbe] {__instance.GetType().FullName}.Update idle heartbeat {_idleLogCount}/10.");
            }
        }
    }

    [HarmonyPatch(typeof(UIScreen), nameof(UIScreen.UpdateAndInitialize))]
    internal static class UIScreenUpdateAndInitializePatch
    {
        private static void Prefix(UIScreen __instance)
        {
            if (!Plugin.HeartbeatLogging || __instance == null)
                return;

            string screenName = __instance.GetType().FullName;
            if (!Plugin.LoggedScreens.Add(screenName))
                return;

            Plugin.Log.LogInfo($"[UIHeartbeat] First UpdateAndInitialize for {screenName}. BuildMarker={Plugin.BuildMarker}.");
        }
    }

    [HarmonyPatch(typeof(MainMenuScreen), "InitializeScreen")]
    internal static class MainMenuScreenInitializeScreenPatch
    {
        private static void Postfix()
        {
            if (!Plugin.HeartbeatLogging)
                return;

            Plugin.Log.LogInfo($"[UIHeartbeat] MainMenuScreen.InitializeScreen fired. BuildMarker={Plugin.BuildMarker}.");
        }
    }

    [HarmonyPatch(typeof(UpgradesScreen), "InitializeScreen")]
    internal static class UpgradesScreenInitializeScreenPatch
    {
        private static void Postfix()
        {
            if (!Plugin.HeartbeatLogging)
                return;

            int gold = Singleton<GameData>.Instance.PlayerData.MetaUpgrades.GetCurrentGold();
            Plugin.Log.LogInfo($"[UIHeartbeat] UpgradesScreen.InitializeScreen fired. CurrentGold={gold}. BuildMarker={Plugin.BuildMarker}.");
        }
    }

    [HarmonyPatch(typeof(ResultScreen), "InitializeScreen")]
    internal static class ResultScreenInitializeScreenPatch
    {
        private static void Postfix(ResultScreen __instance)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            BattleStateData battleStateData = Singleton<GameData>.Instance.BattleStateData;
            float time = battleStateData?.currentSimulationState?.stepTime ?? -1f;
            float gold = battleStateData?.currentSimulationState?.CurrentGold ?? -1f;
            Plugin.Log.LogInfo($"[ResultProbe] ResultScreen.InitializeScreen fired. Victory={__instance.ScreenData.HasAchievedVictory}; Time={time:0.###}; Gold={gold:0.###}; BuildMarker={Plugin.BuildMarker}.");
        }
    }

    [HarmonyPatch(typeof(GameStateBattle), "InitializePreScenes")]
    internal static class GameStateBattleInitializePreScenesPatch
    {
        private static void Prefix()
        {
            Plugin.LastLoggedLevelSpeedBonusByPlayer.Clear();
            Plugin.LoggedBaseSpeedPlayers.Clear();
            GameStateBattleUpdateGameStatePatch.Reset();
        }
    }

    //region : logging related
    [HarmonyPatch(typeof(GameStateBattle), "RequestPickLevelUpOption")]
    internal static class GameStateBattleRequestPickLevelUpOptionPatch
    {
        private static void Prefix(int player, int option)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            Plugin.Log.LogInfo($"[LevelUpScreen] GameStateBattle.RequestPickLevelUpOption fired: Player={player} OptionIndex={option} BuildMarker={Plugin.BuildMarker}.");
        }
    }

    [HarmonyPatch(typeof(LevelUpScreen), "InitializeScreen")]
    internal static class LevelUpScreenInitializeScreenPatch
    {
        private static void Postfix(LevelUpScreen __instance)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            LevelUpScreen.LevelUpScreenData data = __instance.ScreenData;
            Plugin.Log.LogInfo($"[LevelUpScreen] InitializeScreen fired. Source={data.UpgradeSource}; upgrades={data.upgrades?.Count ?? 0}; players={data.PlayerLoadouts?.Count ?? 0}; BuildMarker={Plugin.BuildMarker}.");
        }
    }

    [HarmonyPatch(typeof(LevelUpScreen), "SelectUpgrade")]
    internal static class LevelUpScreenSelectUpgradePatch
    {
        private static void Prefix(LevelUpScreen __instance, int upgradeIndex, int playerIndex)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            UpgradeEntry upgrade = default;
            bool hasUpgrade = __instance.ScreenData?.upgrades != null && upgradeIndex >= 0 && upgradeIndex < __instance.ScreenData.upgrades.Count;
            if (hasUpgrade)
                upgrade = __instance.ScreenData.upgrades[upgradeIndex];

            Plugin.Log.LogInfo($"[LevelUpScreen] SelectUpgrade prefix fired: Player={playerIndex} OptionIndex={upgradeIndex} HasUpgrade={hasUpgrade} Type={(hasUpgrade ? upgrade.UpgradeType.ToString() : "n/a")} TypeId={(hasUpgrade ? upgrade.TypeId : 0)} SubType={(hasUpgrade ? upgrade.SubTypeIndex : 0)} Level={(hasUpgrade ? upgrade.Level : 0)}.");
        }

        private static void Postfix(LevelUpScreen __instance, int upgradeIndex, int playerIndex)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            if (__instance.ScreenData?.PickedOptions == null)
                return;

            bool picked = __instance.ScreenData.PickedOptions.Exists(e => e.PlayerIndex == playerIndex && e.OptionIndex == upgradeIndex);
            Plugin.Log.LogInfo($"[LevelUpScreen] SelectUpgrade postfix fired: Player={playerIndex} OptionIndex={upgradeIndex} PickedNow={picked} PickedOptions={__instance.ScreenData.PickedOptions.Count}.");
        }
    }

    [HarmonyPatch(typeof(GameStateBattle), "InitializePostScenes")]
    internal static class GameStateBattleInitializePostScenesPatch
    {
        private static void Postfix(GameStateBattle __instance)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            List<BattlePlayer> battlePlayers = Traverse.Create(__instance).Field<List<BattlePlayer>>("_battlePlayers").Value;
            int playerCount = battlePlayers?.Count ?? 0;
            Plugin.Log.LogInfo($"[GameStateBattle] InitializePostScenes fired. BattlePlayers={playerCount}; BasePlayerAttackSpeedBonus={Plugin.BasePlayerAttackSpeedBonus:P0}; regen x{Plugin.HealthRegenerationMultiplier:0.###}.");

            if (battlePlayers == null)
                return;

            foreach (BattlePlayer battlePlayer in battlePlayers)
            {
                PlayerLoadout loadout = battlePlayer.currentLoadout;
                if (loadout == null)
                    continue;

                BattleSimulationUnitStats stats = loadout.GetStats();
                Plugin.Log.LogInfo($"[GameStateBattle] Player={loadout.PlayerIndex} loadout stats now AttackSpeedMultiplier={stats.AttackSpeedMultiplier:0.###}, TotalAttackCooldown={stats.TotalAttackCooldown:0.###}, HealthRegenMultiplier={stats.HealthRegenerationMultiplier:0.###}, TotalHealthRegeneration={stats.TotalHealthRegeneration:0.###}.");
            }
        }
    }

    [HarmonyPatch(typeof(GameStateBattle), nameof(GameStateBattle.FinishBattle))]
    internal static class GameStateBattleFinishBattlePatch
    {
        private static void Prefix(BattleCoreSimulation.BattleState outcome)
        {
            if (!Plugin.HeartbeatLogging)
                return;

            BattleStateData battleStateData = Singleton<GameData>.Instance.BattleStateData;
            float time = battleStateData?.currentSimulationState?.stepTime ?? -1f;
            float gold = battleStateData?.currentSimulationState?.CurrentGold ?? -1f;
            Plugin.Log.LogInfo($"[ResultProbe] GameStateBattle.FinishBattle prefix fired. Outcome={outcome}; Time={time:0.###}; Gold={gold:0.###}.");
        }
    }

    [HarmonyPatch(typeof(GameStateBattle), nameof(GameStateBattle.Destroy))]
    internal static class GameStateBattleDestroyPatch
    {
        private static void Prefix()
        {
            if (!Plugin.HeartbeatLogging)
                return;

            Plugin.Log.LogInfo($"[ResultProbe] GameStateBattle.Destroy prefix fired before Unity TimeDif log. deltaTimeDif={BattleVisualization.deltaTimeDif:0.###}.");
        }
    }

    [HarmonyPatch(typeof(GameStateBattle), "UpdateGameState")]
    internal static class GameStateBattleUpdateGameStatePatch
    {
        private static int _loggedTicks;

        internal static void Reset()
        {
            _loggedTicks = 0;
        }

        private static void Prefix()
        {
            if (!Plugin.HeartbeatLogging)
                return;

            if (_loggedTicks >= 5)
                return;

            _loggedTicks++;
            Plugin.Log.LogInfo($"[GameStateBattle] UpdateGameState tick {_loggedTicks}/5. Mod is alive in Battle state. BuildMarker={Plugin.BuildMarker}.");
        }
    }
    //endregion : logging related
    
}
