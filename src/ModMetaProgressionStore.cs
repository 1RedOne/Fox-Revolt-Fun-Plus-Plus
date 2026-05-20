using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FoxRevoltFunPlusPlus
{
    internal static class ModMetaProgressionStore
    {
        private const string FileName = "foxrevoltfunplusplus_meta_progress.json";

        private static readonly HashSet<string> ModTypeIds = new HashSet<string>(StringComparer.Ordinal)
        {
            MetaProgressionShopHandlers.InitialAttackSpeedBoostId,
            MetaProgressionShopHandlers.ProgressiveSpeedAreaTrainingId,
            MetaProgressionShopHandlers.SpecialMoveChargeSystemId,
        };

        private static ModMetaProgressionData _data;

        private static string ProgressPath => Path.Combine(Paths.ConfigPath, FileName);

        internal static bool IsModMetaUpgrade(string typeId)
        {
            return !string.IsNullOrEmpty(typeId) && ModTypeIds.Contains(typeId);
        }

        internal static void ApplyProgress(PlayerDataMetaUpgrade playerUpgrade, MetaUpgradeBalancing balancing)
        {
            if (playerUpgrade == null || balancing == null || !IsModMetaUpgrade(balancing.TypeId))
                return;

            ModMetaProgressionEntry entry = GetOrCreateEntry(playerUpgrade, balancing);
            playerUpgrade.Level = Mathf.Clamp(entry.Level, 0, balancing.Levels?.Count ?? 0);
            playerUpgrade.Unlocked = entry.Unlocked || balancing.UnlockedInitially;
        }

        internal static void SaveFromRuntime(PlayerDataMetaUpgrade playerUpgrade)
        {
            if (playerUpgrade == null || !IsModMetaUpgrade(playerUpgrade.TypeId))
                return;

            ModMetaProgressionData data = Load();
            ModMetaProgressionEntry entry = GetOrCreateEntry(data, playerUpgrade.TypeId);
            entry.Level = Mathf.Max(0, playerUpgrade.Level);
            entry.Unlocked = playerUpgrade.Unlocked;
            Save(data);
        }

        internal static void SaveAllFromRuntime()
        {
            SaveFromRuntime(Singleton<GameData>.Instance?.PlayerData?.MetaUpgrades);
        }

        internal static void SaveFromRuntime(PlayerDataMetaUpgrades playerMetaUpgrades)
        {
            if (playerMetaUpgrades?.Upgrades == null)
                return;

            bool changed = false;
            bool sawModUpgrade = false;
            ModMetaProgressionData data = Load();
            foreach (PlayerDataMetaUpgrade playerUpgrade in playerMetaUpgrades.Upgrades)
            {
                if (playerUpgrade == null || !IsModMetaUpgrade(playerUpgrade.TypeId))
                    continue;

                sawModUpgrade = true;
                ModMetaProgressionEntry entry = GetOrCreateEntry(data, playerUpgrade.TypeId);
                int level = Mathf.Max(0, playerUpgrade.Level);
                bool unlocked = playerUpgrade.Unlocked;
                if (entry.Level == level && entry.Unlocked == unlocked)
                    continue;

                entry.Level = level;
                entry.Unlocked = unlocked;
                changed = true;
            }

            if (changed || sawModUpgrade)
                Save(data);
        }

        internal static RemovedModMetaUpgradeState RemoveRuntimeEntriesBeforeVanillaSave(PlayerData playerData)
        {
            PlayerDataMetaUpgrades playerMetaUpgrades = playerData?.MetaUpgrades;
            if (playerMetaUpgrades?.Upgrades == null)
                return RemovedModMetaUpgradeState.Empty;

            SaveFromRuntime(playerMetaUpgrades);

            var removed = new List<RemovedModMetaUpgrade>();
            for (int i = playerMetaUpgrades.Upgrades.Count - 1; i >= 0; i--)
            {
                PlayerDataMetaUpgrade playerUpgrade = playerMetaUpgrades.Upgrades[i];
                if (playerUpgrade == null || !IsModMetaUpgrade(playerUpgrade.TypeId))
                    continue;

                removed.Add(new RemovedModMetaUpgrade(i, playerUpgrade));
                playerMetaUpgrades.Upgrades.RemoveAt(i);
            }

            return new RemovedModMetaUpgradeState(playerMetaUpgrades, removed);
        }

        internal static void RestoreRuntimeEntriesAfterVanillaSave(RemovedModMetaUpgradeState state)
        {
            if (state.PlayerMetaUpgrades?.Upgrades == null || state.RemovedEntries == null)
                return;

            for (int i = state.RemovedEntries.Count - 1; i >= 0; i--)
            {
                RemovedModMetaUpgrade removed = state.RemovedEntries[i];
                int index = Mathf.Clamp(removed.Index, 0, state.PlayerMetaUpgrades.Upgrades.Count);
                state.PlayerMetaUpgrades.Upgrades.Insert(index, removed.Upgrade);
            }
        }

        private static ModMetaProgressionEntry GetOrCreateEntry(PlayerDataMetaUpgrade playerUpgrade, MetaUpgradeBalancing balancing)
        {
            ModMetaProgressionData data = Load();
            ModMetaProgressionEntry entry = data.Upgrades.FirstOrDefault(e => e.TypeId == balancing.TypeId);
            if (entry != null)
                return entry;

            entry = new ModMetaProgressionEntry
            {
                TypeId = balancing.TypeId,
                Level = Mathf.Clamp(playerUpgrade.Level, 0, balancing.Levels?.Count ?? 0),
                Unlocked = playerUpgrade.Unlocked || balancing.UnlockedInitially,
            };
            data.Upgrades.Add(entry);
            Save(data);
            return entry;
        }

        private static ModMetaProgressionEntry GetOrCreateEntry(ModMetaProgressionData data, string typeId)
        {
            ModMetaProgressionEntry entry = data.Upgrades.FirstOrDefault(e => e.TypeId == typeId);
            if (entry != null)
                return entry;

            entry = new ModMetaProgressionEntry
            {
                TypeId = typeId,
                Level = 0,
                Unlocked = true,
            };
            data.Upgrades.Add(entry);
            return entry;
        }

        private static ModMetaProgressionData Load()
        {
            if (_data != null)
                return _data;

            try
            {
                string path = ProgressPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _data = JsonConvert.DeserializeObject<ModMetaProgressionData>(json);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[MetaProgress] Failed to load mod meta progress; starting fresh. {ex.GetType().Name}: {ex.Message}");
            }

            if (_data == null)
                _data = new ModMetaProgressionData();

            if (_data.Upgrades == null)
                _data.Upgrades = new List<ModMetaProgressionEntry>();

            return _data;
        }

        private static void Save(ModMetaProgressionData data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ProgressPath));
                File.WriteAllText(ProgressPath, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[MetaProgress] Failed to save mod meta progress. {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    [Serializable]
    internal sealed class ModMetaProgressionData
    {
        public int Version = 1;
        public List<ModMetaProgressionEntry> Upgrades = new List<ModMetaProgressionEntry>();
    }

    [Serializable]
    internal sealed class ModMetaProgressionEntry
    {
        public string TypeId;
        public int Level;
        public bool Unlocked = true;
    }

    internal readonly struct RemovedModMetaUpgrade
    {
        internal readonly int Index;
        internal readonly PlayerDataMetaUpgrade Upgrade;

        internal RemovedModMetaUpgrade(int index, PlayerDataMetaUpgrade upgrade)
        {
            Index = index;
            Upgrade = upgrade;
        }
    }

    internal readonly struct RemovedModMetaUpgradeState
    {
        internal static readonly RemovedModMetaUpgradeState Empty = new RemovedModMetaUpgradeState(null, null);

        internal readonly PlayerDataMetaUpgrades PlayerMetaUpgrades;
        internal readonly List<RemovedModMetaUpgrade> RemovedEntries;

        internal RemovedModMetaUpgradeState(PlayerDataMetaUpgrades playerMetaUpgrades, List<RemovedModMetaUpgrade> removedEntries)
        {
            PlayerMetaUpgrades = playerMetaUpgrades;
            RemovedEntries = removedEntries;
        }
    }
}
