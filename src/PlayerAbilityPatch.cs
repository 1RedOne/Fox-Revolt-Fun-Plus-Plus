using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FoxRevoltFunPlusPlus
{
    [HarmonyPatch(typeof(BattleSimulationManualAbility), nameof(BattleSimulationManualAbility.Initialize))]
    internal static class BattleSimulationManualAbilityInitializePatch
    {
        private static void Postfix(BattleSimulationManualAbility __instance, BattleSimulationUnit sourceUnit)
        {
            int instanceKey = Plugin.GetInstanceKey(__instance);
            string warriorId = GetWarriorId(sourceUnit);
            string abilityId = GetAbilityId(__instance);
            string configTypeId = GetConfigTypeId(__instance);
            int abilityHash = SafeGetAbilityHash(__instance);
            bool chargeBased = __instance is BattleSimulationManualMultiUseAbility;
            bool playerOwned = sourceUnit is BattleSimulationPlayerUnit;

            if (Plugin.HeartbeatLogging && !Plugin.LoggedManualAbilityInstances.Contains(instanceKey))
            {
                Plugin.LoggedManualAbilityInstances.Add(instanceKey);
                Plugin.Log.LogInfo($"[ManualAbilityProbe] WarriorId='{warriorId}' AbilityId='{abilityId}' ConfigTypeId='{configTypeId}' AbilityHash={abilityHash} ChargeBased={chargeBased} Type={__instance.GetType().Name}.");
            }

            if (playerOwned && chargeBased)
                PatchPlayerSecretMoveCharges(__instance, instanceKey, warriorId, abilityId, configTypeId, abilityHash);

            // Moe's Cheers/DrinkBrew gets additional HP/stat cleanup. Other classes only get the shared charge/recharge tweak above.
            PatchCheersBuffConfig(__instance, warriorId, abilityId, configTypeId, abilityHash);
        }

        private static void PatchPlayerSecretMoveCharges(BattleSimulationManualAbility ability, int instanceKey, string warriorId, string abilityId, string configTypeId, int abilityHash)
        {
            if (Plugin.PatchedPlayerSecretMoveInstances.Contains(instanceKey))
                return;

            Plugin.PatchedPlayerSecretMoveInstances.Add(instanceKey);
            try
            {
                Traverse traverse = Traverse.Create(ability);
                int oldMaxUses = traverse.Field<int>("_defaultMaxUses").Value;
                float oldRechargeTime = traverse.Field<float>("_rechargeTime").Value;
                float oldCurrentCharges = traverse.Field<float>("_currentCharges").Value;

                int extraCharges = Math.Max(0, Plugin.PlayerSecretMoveExtraCharges);
                int newMaxUses = Math.Max(1, oldMaxUses + extraCharges);
                float newRechargeTime = Math.Max(0.01f, oldRechargeTime * Math.Max(0.01f, Plugin.PlayerSecretMoveRechargeTimeMultiplier));
                float newCurrentCharges = Math.Max(oldCurrentCharges, newMaxUses);

                traverse.Field("_defaultMaxUses").SetValue(newMaxUses);
                traverse.Field("_rechargeTime").SetValue(newRechargeTime);
                traverse.Field("_currentCharges").SetValue(newCurrentCharges);

                Plugin.Log.LogInfo($"[PlayerSecretMove] Patched WarriorId='{warriorId}' AbilityId='{abilityId}' ConfigTypeId='{configTypeId}' Hash={abilityHash}. Charges {oldMaxUses}->{newMaxUses}, CurrentCharges {oldCurrentCharges:0.###}->{newCurrentCharges:0.###}, Recharge {oldRechargeTime:0.###}->{newRechargeTime:0.###}.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[PlayerSecretMove] Failed to patch WarriorId='{warriorId}' AbilityId='{abilityId}' ConfigTypeId='{configTypeId}' Hash={abilityHash}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        internal static bool IsMoeWarrior(string warriorId) => Plugin.ContainsAnyConfiguredToken(warriorId, Plugin.MoeWarriorIdContains, blankMatches: false);

        private static void PatchCheersBuffConfig(BattleSimulationManualAbility ability, string warriorId, string abilityId, string configTypeId, int abilityHash)
        {
            if (!IsMoeWarrior(warriorId) || !IsCheersAbility(abilityId, configTypeId))
                return;

            AbilityConfig config = ability.Config;
            if (config == null)
                return;

            int configKey = Plugin.GetInstanceKey(config);
            if (Plugin.PatchedCheersAbilityConfigs.Contains(configKey))
                return;

            Plugin.PatchedCheersAbilityConfigs.Add(configKey);

            try
            {
                List<BattleSimulationBaseConfigNode> nodes = Traverse.Create(config).Field<List<BattleSimulationBaseConfigNode>>("_allNodes").Value;
                if (nodes == null)
                {
                    Plugin.Log.LogWarning($"[CheersBuff] Could not inspect config nodes for WarriorId='{warriorId}' AbilityId='{abilityId}' ConfigTypeId='{configTypeId}'.");
                    return;
                }

                float statMultiplier = Math.Max(0f, Plugin.CheersBuffStatMultiplier);
                float durationMultiplier = Math.Max(0f, Plugin.CheersBuffDurationMultiplier);
                int statConfigCount = 0;
                int statCount = 0;
                int durationCount = 0;
                int healthPenaltyCount = 0;
                List<string> changes = new List<string>();

                foreach (BattleSimulationStatEffectConfig statConfig in nodes.OfType<BattleSimulationStatEffectConfig>())
                {
                    statConfigCount++;

                    if (statConfig.Duration > 0f && Math.Abs(durationMultiplier - 1f) > 0.0001f)
                    {
                        float oldDuration = statConfig.Duration;
                        statConfig.Duration *= durationMultiplier;
                        durationCount++;
                        changes.Add($"Duration {oldDuration:0.###}->{statConfig.Duration:0.###}");
                    }

                    foreach (Stat stat in statConfig.Stats)
                    {
                        if (stat == null)
                            continue;

                        if (Plugin.CheersRemoveHealthPenalty && stat.value < 0f && IsHealthPenaltyStat(stat.type))
                        {
                            float oldPenaltyValue = stat.value;
                            stat.value = 0f;
                            healthPenaltyCount++;
                            changes.Add($"removed {stat.type} penalty {oldPenaltyValue:0.###}->0");
                            continue;
                        }

                        bool shouldMultiply = stat.value > 0f || (Plugin.CheersBuffNegativeStats && stat.value < 0f);
                        if (!shouldMultiply || Math.Abs(statMultiplier - 1f) <= 0.0001f)
                            continue;

                        float oldValue = stat.value;
                        stat.value *= statMultiplier;
                        statCount++;
                        changes.Add($"{stat.type} {oldValue:0.###}->{stat.value:0.###}");
                    }
                }

                string changeSummary = changes.Count == 0 ? "no numeric changes applied" : string.Join("; ", changes.Take(8));
                if (changes.Count > 8)
                    changeSummary += $"; +{changes.Count - 8} more";

                Plugin.Log.LogInfo($"[CheersBuff] Patched WarriorId='{warriorId}' AbilityId='{abilityId}' ConfigTypeId='{configTypeId}' Hash={abilityHash}. StatConfigs={statConfigCount}, StatsChanged={statCount}, HealthPenaltiesRemoved={healthPenaltyCount}, DurationsChanged={durationCount}, StatMultiplier={statMultiplier:0.###}, DurationMultiplier={durationMultiplier:0.###}. {changeSummary}.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[CheersBuff] Failed to patch WarriorId='{warriorId}' AbilityId='{abilityId}' ConfigTypeId='{configTypeId}' Hash={abilityHash}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool IsHealthPenaltyStat(Stat.StatType type)
        {
            return type == Stat.StatType.BaseHealthRegeneration
                || type == Stat.StatType.HealthRegenMultiplier
                || type == Stat.StatType.BaseHealth
                || type == Stat.StatType.HealthMultiplier
                || type == Stat.StatType.DamageReduction;
        }

        private static bool IsCheersAbility(string abilityId, string configTypeId)
        {
            return MatchesAbilityTokens(abilityId, configTypeId, Plugin.CheersAbilityIdContains, blankMatches: false);
        }

        private static bool MatchesAbilityTokens(string abilityId, string configTypeId, string configuredTokens, bool blankMatches)
        {
            return Plugin.ContainsAnyConfiguredToken(abilityId, configuredTokens, blankMatches)
                || Plugin.ContainsAnyConfiguredToken(configTypeId, configuredTokens, blankMatches);
        }

        internal static string GetWarriorId(BattleSimulationUnit sourceUnit)
        {
            if (sourceUnit is BattleSimulationPlayerUnit playerUnit)
                return playerUnit.GetLoadout()?.GetStaticLoadout()?.WarriorBalancing?.TypeId ?? "";

            return sourceUnit?.GetBalancing()?.TypeId ?? "";
        }

        internal static string GetAbilityId(BattleSimulationManualAbility ability)
        {
            AbilityBalancing balancing = Traverse.Create(ability).Field<AbilityBalancing>("_balancing").Value;
            if (!string.IsNullOrWhiteSpace(balancing?.TypeId?.StringId))
                return balancing.TypeId.StringId;

            if (!string.IsNullOrWhiteSpace(ability.OverrideTypeId?.StringId))
                return ability.OverrideTypeId.StringId;

            if (!string.IsNullOrWhiteSpace(ability.Config?.TypeId?.StringId))
                return ability.Config.TypeId.StringId;

            return "";
        }

        internal static string GetConfigTypeId(BattleSimulationManualAbility ability) => ability.Config?.TypeId?.StringId ?? "";

        private static int SafeGetAbilityHash(BattleSimulationManualAbility ability)
        {
            try
            {
                return ability.GetId();
            }
            catch
            {
                return 0;
            }
        }
    }
}
