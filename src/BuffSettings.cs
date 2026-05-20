using System;

namespace FoxRevoltFunPlusPlus
{
    [Serializable]
    public sealed class BuffSettings
    {
        public float HealingMultiplier = 3f;
        public float HealthRegenerationMultiplier = 2f;
        public float CoopReviveHealthPercent = 0.5f;
        public float PlayerSecretMoveRechargeTimeMultiplier = 0.5f;
        public string MoeWarriorIdContains = "Hero_Monk";
        [Obsolete("Use PlayerSecretMoveRechargeTimeMultiplier. Kept so older buffs.json files can migrate cleanly.")]
        public float MoeSecretMoveRechargeTimeMultiplier = 0.5f;
        [Obsolete("Moe's secret move id filter is only used by older builds. CheersAbilityIdContains controls Moe's HP/stat tweak.")]
        public string MoeSecretMoveAbilityIdContains = "Cheers,DrinkBrew";
        [Obsolete("Cooldown is no longer modified; only charges and recharge time are patched.")]
        public float MoeSecretMoveCooldownMultiplier = 0.5f;
        public string CheersAbilityIdContains = "Cheers,DrinkBrew";
        public float CheersBuffStatMultiplier = 3f;
        public float CheersBuffDurationMultiplier = 2f;
        public bool CheersRemoveHealthPenalty = true;
        public bool CheersBuffNegativeStats = false;
        public bool VerboseLogging = false;
        public bool HeartbeatLogging = false;
    }
}
