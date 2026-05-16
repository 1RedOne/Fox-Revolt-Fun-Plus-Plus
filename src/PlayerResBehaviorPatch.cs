using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FoxRevoltFunPlusPlus
{
     [HarmonyPatch(typeof(StayInAreaWorldEvent), nameof(StayInAreaWorldEvent.Update))]
    internal static class StayInAreaWorldEventUpdatePatch //resurrection logic, only tested locally, not online
    {
        private static bool Prefix(StayInAreaWorldEvent __instance, BattleCoreSimulation.SimulationState state, float deltaTime)
        {
            if (__instance.RewardType != BaseWorldEvent.RewardTypes.PlayerResurrect)
                return true;

            List<BattleSimulationUnit> playersInRange = state.Storage
                .GetUnitsInRange(__instance.Position, __instance.Config.Range)
                .Where(u => u.Faction == __instance.Config.Faction && u.IsAlive() && u is BattleSimulationPlayerUnit)
                .ToList();

            bool hasPlayerInRange = playersInRange.Any();
            if (__instance.Config.Time == 0f)
            {
                if (hasPlayerInRange)
                    __instance.Progress = 1f;
            }
            else
            {
                __instance.Progress += deltaTime / __instance.Config.Time * (hasPlayerInRange ? playersInRange.Count : -1f);
            }

            __instance.Progress = Mathf.Max(__instance.Progress, 0f);

            if (__instance.Progress >= 1f && !__instance.Done)
            {
                __instance.RewardFloatParam = Mathf.Clamp01(Plugin.CoopReviveHealthPercent);
                if (Plugin.VerboseLogging)
                    Plugin.Log.LogInfo($"[CoopRevive] Penalty negated. {playersInRange.Count} rescuer(s) kept their health; revived player will return with {__instance.RewardFloatParam:P0} max health.");

                if (playersInRange.Any())
                {
                    state.SimulationEvents.Add(new BattleCoreSimulation.SimulationEvent()
                    {
                        Type = BattleCoreSimulation.SimulationEvent.SimulationEventType.CollectWorldEventObject,
                        IntParam1 = __instance.Id,
                        IntParam2 = playersInRange.First().Id
                    });
                }
            }

            FinishReviveWorldEventUpdate(__instance, state);
            return false;
        }

        private static void FinishReviveWorldEventUpdate(BaseWorldEvent worldEvent, BattleCoreSimulation.SimulationState state)
        {
            if (worldEvent.Wrapping && state.World.WrappingMode != MapProperties.MapWrapping.None)
                worldEvent.Position = MapHelpers.GetClosestOffset2D(worldEvent.Position, state.ActiveAreaCenter, state.World.MapSize);

            if (worldEvent.Progress < 1f || worldEvent.Done || !Traverse.Create(worldEvent).Field<bool>("AutoDoneOnProgress").Value)
                return;

            worldEvent.Done = true;
            worldEvent.State = BaseWorldEvent.WorldEventState.Done;
            if (worldEvent.RewardType != BaseWorldEvent.RewardTypes.PlayerResurrect)
                return;

            BattleSimulationPlayerUnit playerUnit = state.Storage.GetAllPlayerUnits().Find(e => e.Id == worldEvent.RewardIntParam);
            if (playerUnit == null)
                return;

            float reviveHealth = playerUnit.GetMaxHealth() * worldEvent.RewardFloatParam;
            if (state.NetworkFrontend.NeedsToSendUpdates)
                state.NetworkEvents.SendMessage(new RevivePlayerMessage(worldEvent.RewardIntParam, state.Balancing.GlobalBattleBalancing.ReviveDelay, reviveHealth));

            playerUnit.TriggerRevive(state.Balancing.GlobalBattleBalancing.ReviveDelay, reviveHealth, false);
            state.SimulationEvents.Add(new BattleCoreSimulation.SimulationEvent()
            {
                Type = BattleCoreSimulation.SimulationEvent.SimulationEventType.ReviveSucceeded,
                IntParam1 = playerUnit.Id
            });
        }
    }
}
