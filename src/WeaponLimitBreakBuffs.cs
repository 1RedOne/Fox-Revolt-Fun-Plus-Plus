using System;
using System.Collections.Generic;

namespace FoxRevoltFunPlusPlus
{
    public static class WeaponLimitBreakBuffs
    {
        private const float LimitBreakStatMultiplier = 3f;
        private static bool _applied;

        public static void ApplyWeaponLimitBreakBuffs()
        {
            if (_applied)
                return;

            try
            {
                List<WeaponBalancing> weapons = Singleton<GameData>.Instance?.Balancing?.WeaponsBalancing?.Weapons;
                if (weapons == null || weapons.Count == 0)
                    return;

                int weaponCount = 0;
                int statCount = 0;

                foreach (WeaponBalancing weapon in weapons)
                {
                    if (weapon?.WeaponLimitStats == null || weapon.WeaponLimitStats.Count == 0)
                        continue;

                    bool changedWeapon = false;
                    foreach (WeaponLimitStatBalancing limitStat in weapon.WeaponLimitStats)
                    {
                        if (limitStat?.BonusStats == null)
                            continue;

                        foreach (Stat stat in limitStat.BonusStats)
                        {
                            if (stat == null)
                                continue;

                            stat.value *= LimitBreakStatMultiplier;
                            statCount++;
                            changedWeapon = true;
                        }
                    }

                    if (changedWeapon)
                        weaponCount++;
                }

                _applied = true;
                Plugin.Log?.LogInfo($"[LimitBreakBuffs] Applied x{LimitBreakStatMultiplier:0.###} Limit Break stat multiplier to {statCount} stats across {weaponCount} weapons.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[LimitBreakBuffs] Failed to apply Limit Break buffs. {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
