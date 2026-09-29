using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates a URP pipeline asset (with a Universal Renderer) and makes it the active render pipeline,
    /// so the project never falls back to the deprecated Built-In pipeline.
    /// URP is reached through reflection on purpose: an API difference between URP versions then only
    /// produces a warning with manual steps, instead of a compile error that would block Play mode.
    /// </summary>
    public static class UrpSetup
    {
        public const string PipelinePath = "Assets/Settings/URP_Pipeline.asset";
        public const string RendererPath = "Assets/Settings/URP_Renderer.asset";

        const string UrpAssembly = "Unity.RenderPipelines.Universal.Runtime";
        const string ManualSteps =
            "Manual fix: Assets > Create > Rendering > URP Asset (with Universal Renderer), then assign it in " +
            "Project Settings > Graphics > Default Render Pipeline (and Project Settings > Quality).";

        [MenuItem("Downhill/Setup URP Pipeline")]
        public static void SetupFromMenu() => EnsureUrpActive(force: true);

        /// <summary>Returns true if a render pipeline is active afterwards.</summary>
        public static bool EnsureUrpActive(bool force = false)
        {
            if (!force && GraphicsSettings.defaultRenderPipeline != null) return true;

            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
            if (pipeline == null) pipeline = CreatePipelineAsset();
            if (pipeline == null) return false;

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("Downhill: URP is now the active render pipeline (" + PipelinePath + ").");
            return true;
        }

        static RenderPipelineAsset CreatePipelineAsset()
        {
            try
            {
                Type assetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " + UrpAssembly);
                Type rendererType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, " + UrpAssembly);
                Type rendererBase = Type.GetType("UnityEngine.Rendering.Universal.ScriptableRendererData, " + UrpAssembly);
                if (assetType == null || rendererType == null || rendererBase == null)
                {
                    Debug.LogWarning("Downhill: URP package not found. " + ManualSteps);
                    return null;
                }

                MethodInfo create = assetType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { rendererBase }, null);
                if (create == null)
                {
                    Debug.LogWarning("Downhill: UniversalRenderPipelineAsset.Create not found in this URP version. " + ManualSteps);
                    return null;
                }

                ProjectSetup.EnsureFolder("Assets/Settings");
                var rendererData = (ScriptableObject)ScriptableObject.CreateInstance(rendererType);
                AssetDatabase.CreateAsset(rendererData, RendererPath);

                var pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { rendererData });
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                AssetDatabase.SaveAssets();
                return pipeline;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Downhill: automatic URP setup failed (" + e.Message + "). " + ManualSteps);
                return null;
            }
        }
    }
}
