using HarmonyLib;
using I2.Loc;
using System;
using System.Collections.Generic;

namespace FoxRevoltFunPlusPlus
{
    [HarmonyPatch(typeof(StatInjector), nameof(StatInjector.GetText), new Type[] { typeof(LocalizedString), typeof(List<Stat>) })]
    internal static class StatInjectorGetTextPatch
    {
        private static bool Prefix(LocalizedString loca, List<Stat> stats, ref string __result)
        {
            if (stats == null)
                return true;

            string format = loca;
            if (string.IsNullOrEmpty(format))
                return true;

            if (IsProgressiveSpeedAreaText(format, stats))
            {
                __result = string.Format(
                    format,
                    TextFormatter.WholePercent(stats[0].value, forcePlus: true),
                    TextFormatter.WholePercent(stats[1].value, forcePlus: true));
                return false;
            }

            if (IsSpecialMoveChargeText(format, stats))
            {
                __result = string.Format(format, TextFormatter.RelativeFloat(stats[0].value));
                return false;
            }

            return true;
        }

        private static bool IsProgressiveSpeedAreaText(string format, List<Stat> stats)
        {
            return format.Contains("attack speed", StringComparison.OrdinalIgnoreCase)
                && format.Contains("attack area", StringComparison.OrdinalIgnoreCase)
                && stats.Count >= 2
                && stats[0]?.type == Stat.StatType.AttackSpeedMultiplier
                && stats[1]?.type == Stat.StatType.AttackAreaMultiplier;
        }

        private static bool IsSpecialMoveChargeText(string format, List<Stat> stats)
        {
            return format.Contains("special move charge", StringComparison.OrdinalIgnoreCase)
                && stats.Count >= 1
                && stats[0]?.type == Stat.StatType.BonusAbilityCharges;
        }
    }
}
