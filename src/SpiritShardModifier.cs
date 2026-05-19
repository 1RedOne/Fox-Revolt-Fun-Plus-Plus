using System;
using UnityEngine;

namespace FoxRevoltFunPlusPlus
{
    public static class SpiritShardModifier
    {
        private static readonly Color WinterShardCore = new Color(4.972062f, 2.1011283f, 9.972529f, 1f);
        private static readonly Color WinterShardGlow = new Color(8.114428f, 6.131323f, 15.012922f, 1f);
        private static readonly Color WinterShardRim = new Color(9.463949f, 0f, 14.074751f, 1f);
        private static readonly Color WinterShardFallback = new Color(0.95f, 0.25f, 1.25f, 1f);

        private static readonly int[] WinterShardColorProperties =
        {
            Shader.PropertyToID("Color_001022c0c2a9415aa85e09c2652cf65e"),
            Shader.PropertyToID("Color_12d3a14dfbdf41a7a51a1596238d7fa1"),
            Shader.PropertyToID("Color_90034542145f42ec9506e97aa87faf71"),
            Shader.PropertyToID("_Color"),
            Shader.PropertyToID("_BaseColor"),
            Shader.PropertyToID("_EmissionColor")
        };

        public static void ApplyModificationIfWinterStage(BattleVisualizationValueBlob spiritShard)
        {
            if (spiritShard == null || !IsWinterStage() || !LooksLikeExperienceShard(spiritShard))
                return;

            GameObject shardObject = spiritShard.gameObject;
            if (shardObject.GetComponent<WinterSpiritShardModificationMarker>() != null)
                return;

            shardObject.AddComponent<WinterSpiritShardModificationMarker>();
            ApplyPurpleShardTint(shardObject);
        }

        private static bool IsWinterStage()
        {
            try
            {
                MapBalancing map = Singleton<GameData>.Instance?.Session?.runningBattleSetupConfig?.GetMapBalancing();
                if (map == null)
                    return false;

                return ContainsWinterToken(map.MapId) || ContainsWinterToken(map.MapSceneName);
            }
            catch (Exception ex)
            {
                if (Plugin.VerboseLogging)
                    Plugin.Log.LogWarning($"[SpiritShard] Failed to resolve current map; leaving XP shard unchanged. {ex.GetType().Name}: {ex.Message}");

                return false;
            }
        }

        private static bool ContainsWinterToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("Glacial", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Northland", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Winter", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Snow", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeExperienceShard(BattleVisualizationValueBlob spiritShard)
        {
            string objectName = spiritShard.name ?? "";
            if (objectName.IndexOf("XPShard", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (Renderer renderer in spiritShard.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    string materialName = material == null ? "" : material.name;
                    if (materialName.IndexOf("XP_Shard", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        private static void ApplyPurpleShardTint(GameObject shardObject)
        {
            foreach (Renderer renderer in shardObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsShadowRenderer(renderer))
                    continue;

                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(WinterShardColorProperties[0], WinterShardCore);
                propertyBlock.SetColor(WinterShardColorProperties[1], WinterShardGlow);
                propertyBlock.SetColor(WinterShardColorProperties[2], WinterShardRim);
                propertyBlock.SetColor(WinterShardColorProperties[3], WinterShardFallback);
                propertyBlock.SetColor(WinterShardColorProperties[4], WinterShardFallback);
                propertyBlock.SetColor(WinterShardColorProperties[5], WinterShardGlow);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static bool IsShadowRenderer(Renderer renderer)
        {
            string objectName = renderer.gameObject.name ?? "";
            return objectName.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class WinterSpiritShardModificationMarker : MonoBehaviour
        {
        }
    }
}
