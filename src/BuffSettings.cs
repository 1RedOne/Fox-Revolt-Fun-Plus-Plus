using System;

namespace FoxRevoltFunPlusPlus
{
    [Serializable]
    public sealed class BuffSettings
    {
        public float HealingMultiplier = 3f;
        public float BasePlayerAttackSpeedBonus = 0.15f;
        public float EarlyLevelPickSpeedAreaBonus = 0.03f;
        public float LaterLevelPickSpeedAreaBonus = 0.01f;
        public float HealthRegenerationMultiplier = 2f;
        public float CoopReviveHealthPercent = 0.5f;
        public string MoeWarriorIdContains = "Hero_Monk";
        public string MoeSecretMoveAbilityIdContains = "Cheers,DrinkBrew";
        public int MoeSecretMoveExtraCharges = 1;
        public float MoeSecretMoveRechargeTimeMultiplier = 0.5f;
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
