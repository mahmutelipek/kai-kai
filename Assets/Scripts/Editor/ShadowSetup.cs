using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Sharper, steadier shadows: 4096 main-light map, three cascades over 90 m, soft shadows on. The old single
    /// 50 m cascade at 2048 gave blocky edges that crawled as the board moved. Reached through reflection like
    /// <see cref="UrpSetup"/> so a URP API difference only logs a warning. Idempotent.
    /// </summary>
    public static class ShadowSetup
    {
        [MenuItem("Downhill/Setup Shadows")]
        public static void Apply()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpSetup.PipelinePath);
            if (pipeline == null) { Debug.LogWarning("Downhill: no URP pipeline asset at " + UrpSetup.PipelinePath + " (run Downhill > Setup URP Pipeline first)."); return; }
            int changed = 0;
            changed += Set(pipeline, "mainLightShadowmapResolution", 4096);
            changed += Set(pipeline, "shadowDistance", 90f);
            changed += Set(pipeline, "shadowCascadeCount", 3);
            changed += Set(pipeline, "cascade3Split", new Vector2(0.12f, 0.35f));
            changed += Set(pipeline, "supportsSoftShadows", true);
            changed += Set(pipeline, "shadowDepthBias", 1.2f);
            changed += Set(pipeline, "shadowNormalBias", 1.0f);
            if (changed > 0)
            {
                EditorUtility.SetDirty(pipeline);
                AssetDatabase.SaveAssets();
            }
            if (changed > 0) Debug.Log("Downhill: shadow settings applied (" + changed + " value(s) changed).");
        }

        static int Set(object target, string property, object value)
        {
            PropertyInfo p = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite) { Debug.LogWarning("Downhill: URP property '" + property + "' not found, left as is."); return 0; }
            object old = p.GetValue(target);
            if (Equals(old, value)) return 0;
            try { p.SetValue(target, Convert.ChangeType(value, p.PropertyType)); }
            catch (Exception)
            {
                try { p.SetValue(target, value); }
                catch (Exception e) { Debug.LogWarning("Downhill: could not set URP '" + property + "': " + e.Message); return 0; }
            }
            return 1;
        }
    }
}
