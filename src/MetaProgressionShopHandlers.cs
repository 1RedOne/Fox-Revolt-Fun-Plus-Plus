using I2.Loc;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FoxRevoltFunPlusPlus
{
    public static class MetaProgressionShopHandlers
    {
        public const string InitialAttackSpeedBoostId = "fox_mod_initial_attack_speed_boost";
        public const string ProgressiveSpeedAreaTrainingId = "fox_mod_progressive_speed_area_training";
        public const string SpecialMoveChargeSystemId = "fox_mod_special_move_charge_system";

        public static void AddMetaShopItems(bool createPlayerSaveEntries = false)
        {
            try
            {
                GameData gameData = Singleton<GameData>.Instance;
                MetaUpgradesBalancing metaBalancing = gameData?.Balancing?.MetaBalancing;
                if (metaBalancing?.MetaUpgrades == null)
                {
                    Plugin.Log?.LogWarning("[MetaShop] Could not add mod meta upgrades because MetaBalancing is not ready.");
                    return;
                }

                AddMetaShopItems(
                    metaBalancing,
                    createPlayerSaveEntries ? gameData.PlayerData?.MetaUpgrades : null);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[MetaShop] Failed to add mod meta upgrades. {ex.GetType().Name}: {ex.Message}");
            }
        }

        public static void AddMetaShopItems(
            MetaUpgradesBalancing metaBalancing,
            PlayerDataMetaUpgrades playerMetaUpgrades = null)
        {
            if (metaBalancing?.MetaUpgrades == null)
                return;

            EnsureLocalizationTerms();

            Sprite fallbackIcon = metaBalancing.MetaUpgrades.FirstOrDefault(u => u?.Icon != null)?.Icon;

            AddMetaShopItem(
                metaBalancing,
                playerMetaUpgrades,
                CreateInitialAttackSpeedBoost(fallbackIcon));

            AddMetaShopItem(
                metaBalancing,
                playerMetaUpgrades,
                CreateProgressiveSpeedAreaTraining(fallbackIcon));

            AddMetaShopItem(
                metaBalancing,
                playerMetaUpgrades,
                CreateSpecialMoveChargeSystem(fallbackIcon));
        }

        private static void AddMetaShopItem(
            MetaUpgradesBalancing metaBalancing,
            PlayerDataMetaUpgrades playerMetaUpgrades,
            MetaUpgradeBalancing upgrade)
        {
            MetaUpgradeBalancing activeUpgrade = metaBalancing.MetaUpgrades.Find(existing => existing != null && existing.TypeId == upgrade.TypeId);
            if (activeUpgrade == null)
            {
                activeUpgrade = upgrade;
                metaBalancing.MetaUpgrades.Add(activeUpgrade);
            }

            if (playerMetaUpgrades == null)
                return;

            var playerUpgrade = playerMetaUpgrades.Upgrades.Find(existing => existing.TypeId == activeUpgrade.TypeId);
            
            if (playerUpgrade == null)
            {
                playerUpgrade = new PlayerDataMetaUpgrade()
                {
                    TypeId = activeUpgrade.TypeId,
                    Level = 0,
                    Unlocked = activeUpgrade.UnlockedInitially,
                };
                playerMetaUpgrades.Upgrades.Add(playerUpgrade);
                playerMetaUpgrades.SetDirty();
            }

            playerUpgrade.balancing = activeUpgrade;
            playerUpgrade.parent = playerMetaUpgrades;
            ModMetaProgressionStore.ApplyProgress(playerUpgrade, activeUpgrade);
            playerMetaUpgrades.SetDirty();
        }

        public static ModInjectedBuffValues CalculateInjectedBuffs(int effectiveLevelPickCount)
        {
            effectiveLevelPickCount = Math.Max(0, effectiveLevelPickCount);

            List<Stat> initialSpeedStats = GetPurchasedStats(InitialAttackSpeedBoostId);
            List<Stat> progressiveStats = GetPurchasedStats(ProgressiveSpeedAreaTrainingId);

            float baseAttackSpeedBonus = SumStats(initialSpeedStats, Stat.StatType.AttackSpeedMultiplier);
            float speedPerLevelPick = SumStats(progressiveStats, Stat.StatType.AttackSpeedMultiplier);
            float areaPerLevelPick = SumStats(progressiveStats, Stat.StatType.AttackAreaMultiplier);

            return new ModInjectedBuffValues(
                baseAttackSpeedBonus,
                effectiveLevelPickCount * speedPerLevelPick,
                effectiveLevelPickCount * areaPerLevelPick,
                speedPerLevelPick,
                areaPerLevelPick);
        }

        public static float GetPurchasedSpecialMoveExtraChargeProgress()
        {
            List<Stat> chargeStats = GetPurchasedStats(SpecialMoveChargeSystemId);
            return Math.Max(0f, SumStats(chargeStats, Stat.StatType.BonusAbilityCharges));
        }

        public static int GetPurchasedSpecialMoveWholeExtraCharges()
        {
            return Math.Max(0, (int)Math.Floor(GetPurchasedSpecialMoveExtraChargeProgress() + 0.0001f));
        }

        public static List<Stat> GetPurchasedStats(string typeId)
        {
            try
            {
                PlayerDataMetaUpgrades playerMetaUpgrades = Singleton<GameData>.Instance?.PlayerData?.MetaUpgrades;
                PlayerDataMetaUpgrade playerUpgrade = playerMetaUpgrades?.Upgrades?.Find(existing => existing?.TypeId == typeId);
                if (playerUpgrade == null)
                    return new List<Stat>();

                if (playerUpgrade.balancing == null)
                {
                    MetaUpgradeBalancing balancing = Singleton<GameData>.Instance?.Balancing?.MetaBalancing?.MetaUpgrades?.Find(existing => existing?.TypeId == typeId);
                    if (balancing != null)
                    {
                        playerUpgrade.balancing = balancing;
                        playerUpgrade.parent = playerMetaUpgrades;
                        ModMetaProgressionStore.ApplyProgress(playerUpgrade, balancing);
                    }
                }

                return playerUpgrade.GetCurrentLevelStats() ?? new List<Stat>();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[MetaShop] Failed to read purchased stats for '{typeId}'. {ex.GetType().Name}: {ex.Message}");
                return new List<Stat>();
            }
        }

        private static float SumStats(List<Stat> stats, Stat.StatType statType)
        {
            if (stats == null)
                return 0f;

            return stats.Where(stat => stat != null && stat.type == statType).Sum(stat => stat.value);
        }

        private static MetaUpgradeBalancing CreateInitialAttackSpeedBoost(Sprite icon)
        {
            var upgrade = CreateBaseUpgrade(
                InitialAttackSpeedBoostId,
                "FoxRevoltFunPlusPlus/Meta/InitialAttackSpeedBoost/Name",
                icon,
                UIStatType.AttackSpeed);

            upgrade.Levels = new List<MetaUpgradeLevelBalancing>()
            {
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/InitialAttackSpeedBoost/Benefit",
                    250,
                    Stat.StatType.AttackSpeedMultiplier,
                    0.05f),
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/InitialAttackSpeedBoost/Benefit",
                    750,
                    Stat.StatType.AttackSpeedMultiplier,
                    0.05f),
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/InitialAttackSpeedBoost/Benefit",
                    1500,
                    Stat.StatType.AttackSpeedMultiplier,
                    0.05f),
            };

            return upgrade;
        }

        private static MetaUpgradeBalancing CreateProgressiveSpeedAreaTraining(Sprite icon)
        {
            var upgrade = CreateBaseUpgrade(
                ProgressiveSpeedAreaTrainingId,
                "FoxRevoltFunPlusPlus/Meta/ProgressiveSpeedAreaTraining/Name",
                icon,
                UIStatType.AttackArea);

            upgrade.Levels = new List<MetaUpgradeLevelBalancing>()
            {
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/ProgressiveSpeedAreaTraining/Benefit",
                    500,
                    new Stat() { type = Stat.StatType.AttackSpeedMultiplier, value = 0.005f},
                    new Stat() { type = Stat.StatType.AttackAreaMultiplier, value = 0.005f }),
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/ProgressiveSpeedAreaTraining/Benefit",
                    1000,
                    new Stat() { type = Stat.StatType.AttackSpeedMultiplier, value = 0.005f},
                    new Stat() { type = Stat.StatType.AttackAreaMultiplier, value = 0.005f }),
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/ProgressiveSpeedAreaTraining/Benefit",
                    2000,
                    new Stat() { type = Stat.StatType.AttackSpeedMultiplier, value = 0.01f },
                    new Stat() { type = Stat.StatType.AttackAreaMultiplier, value = 0.01f }),
            };

            return upgrade;
        }

        private static MetaUpgradeBalancing CreateSpecialMoveChargeSystem(Sprite icon)
        {
            var upgrade = CreateBaseUpgrade(
                SpecialMoveChargeSystemId,
                "FoxRevoltFunPlusPlus/Meta/SpecialMoveChargeSystem/Name",
                icon,
                UIStatType.CooldownRecoveryBonus);

            upgrade.Levels = new List<MetaUpgradeLevelBalancing>()
            {
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/SpecialMoveChargeSystem/Benefit",
                    750,
                    Stat.StatType.BonusAbilityCharges,
                    0.5f),
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/SpecialMoveChargeSystem/Benefit",
                    1500,
                    Stat.StatType.BonusAbilityCharges,
                    0.5f),
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/SpecialMoveChargeSystem/Benefit",
                    3000,
                    Stat.StatType.BonusAbilityCharges,
                    0.5f),
                CreateLevel(
                    "FoxRevoltFunPlusPlus/Meta/SpecialMoveChargeSystem/Benefit",
                    5000,
                    Stat.StatType.BonusAbilityCharges,
                    0.5f),
            };

            return upgrade;
        }

        private static MetaUpgradeBalancing CreateBaseUpgrade(
            string typeId,
            LocalizedString nameTerm,
            Sprite icon,
            UIStatType uiStatType)
        {
            MetaUpgradeBalancing upgrade = ScriptableObject.CreateInstance<MetaUpgradeBalancing>();
            upgrade.name = typeId;
            upgrade.TypeId = typeId;
            upgrade.NameLoca = nameTerm;
            upgrade.Icon = icon;
            upgrade.UIStatType = uiStatType;
            upgrade.GenericUpgradeLoca = "MetaUpgrades/TotalBonus";
            upgrade.UnlockedInitially = true;
            upgrade.Levels = new List<MetaUpgradeLevelBalancing>();
            return upgrade;
        }

        private static MetaUpgradeLevelBalancing CreateLevel(
            LocalizedString benefitTerm,
            int goldCost,
            Stat.StatType statType,
            float value)
        {
            return CreateLevel(
                benefitTerm,
                goldCost,
                new Stat()
                {
                    type = statType,
                    value = value,
                });
        }

        private static MetaUpgradeLevelBalancing CreateLevel(
            LocalizedString benefitTerm,
            int goldCost,
            params Stat[] stats)
        {
            return new MetaUpgradeLevelBalancing()
            {
                UpgradeLoca = benefitTerm,
                GoldCost = goldCost,
                UpgradeStats = stats.ToList(),
            };
        }

        private static void EnsureLocalizationTerms()
        {
            try
            {
                LocalizationManager.InitializeIfNeeded();

                if (LocalizationManager.Sources == null || LocalizationManager.Sources.Count == 0)
                {
                    LocalizationManager.UpdateSources();
                }

                if (LocalizationManager.Sources == null || LocalizationManager.Sources.Count == 0)
                {
                    Plugin.Log?.LogWarning("[MetaShop] Could not register mod localization terms because I2 has no language sources.");
                    return;
                }

                LanguageSourceData source = LocalizationManager.Sources[0];
                AddOrUpdateTerm(source, "FoxRevoltFunPlusPlus/Meta/InitialAttackSpeedBoost/Name", "Opening Tempo");
                AddOrUpdateTerm(source, "FoxRevoltFunPlusPlus/Meta/InitialAttackSpeedBoost/Benefit", "Initial base {0} attack speed boost");
                AddOrUpdateTerm(source, "FoxRevoltFunPlusPlus/Meta/ProgressiveSpeedAreaTraining/Name", "Foxfire Footwork");
                AddOrUpdateTerm(source, "FoxRevoltFunPlusPlus/Meta/ProgressiveSpeedAreaTraining/Benefit", "Applies {0} attack speed and {1} attack area per level up");
                AddOrUpdateTerm(source, "FoxRevoltFunPlusPlus/Meta/SpecialMoveChargeSystem/Name", "Special Move Charge System");
                AddOrUpdateTerm(source, "FoxRevoltFunPlusPlus/Meta/SpecialMoveChargeSystem/Benefit", "Adds {0} special move charge progress");
                source.UpdateDictionary(force: true);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[MetaShop] Failed to register mod localization terms. {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void AddOrUpdateTerm(LanguageSourceData source, string term, string text)
        {
            TermData termData = source.GetTermData(term) ?? source.AddTerm(term, eTermType.Text, SaveSource: false);
            if (termData.Languages == null || termData.Languages.Length != source.mLanguages.Count)
            {
                Array.Resize(ref termData.Languages, source.mLanguages.Count);
            }

            if (termData.Flags == null || termData.Flags.Length != source.mLanguages.Count)
            {
                Array.Resize(ref termData.Flags, source.mLanguages.Count);
            }

            for (int i = 0; i < source.mLanguages.Count; i++)
            {
                termData.Languages[i] = text;
            }
        }
    }

    public readonly struct ModInjectedBuffValues
    {
        public readonly float BaseAttackSpeedBonus;
        public readonly float LevelAttackSpeedBonus;
        public readonly float LevelAttackAreaBonus;
        public readonly float AttackSpeedPerLevelPick;
        public readonly float AttackAreaPerLevelPick;

        public ModInjectedBuffValues(
            float baseAttackSpeedBonus,
            float levelAttackSpeedBonus,
            float levelAttackAreaBonus,
            float attackSpeedPerLevelPick,
            float attackAreaPerLevelPick)
        {
            BaseAttackSpeedBonus = baseAttackSpeedBonus;
            LevelAttackSpeedBonus = levelAttackSpeedBonus;
            LevelAttackAreaBonus = levelAttackAreaBonus;
            AttackSpeedPerLevelPick = attackSpeedPerLevelPick;
            AttackAreaPerLevelPick = attackAreaPerLevelPick;
        }
    }
}
