using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    public static class ReleaseBuild
    {
        public static void PrepareArt()
        {
            const string folder = "Assets/Resources/Art/Feedback";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(UrpSetup.RendererPath);
            if (renderer != null)
            {
                var serialized = new SerializedObject(renderer);
                var data = serialized.FindProperty("postProcessData");
                data.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                if (data.objectReferenceValue == null) throw new System.InvalidOperationException("URP post-processing resources are missing.");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(folder + "/DaySky.mat") == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("KaiKai/DaySky")), folder + "/DaySky.mat");
            if (AssetDatabase.LoadAssetAtPath<Material>(folder + "/Sparks.mat") == null)
            {
                var texture = new Texture2D(64,64,TextureFormat.RGBA32,false) { name = "Spark", wrapMode = TextureWrapMode.Clamp };
                for (int y=0;y<64;y++) for(int x=0;x<64;x++)
                {
                    float radius = new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;
                    texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01((1-radius)*3)));
                }
                texture.Apply(); AssetDatabase.CreateAsset(texture,folder+"/Spark.asset");
                var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetTexture("_BaseMap",texture); material.SetFloat("_Surface",1);
                material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10); material.SetFloat("_ZWrite",0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
                AssetDatabase.CreateAsset(material,folder+"/Sparks.mat");
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(folder + "/Diamond.mat") == null)
            {
                var gem = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                gem.SetColor("_BaseColor", new Color(.68f,.035f,.95f)); gem.SetFloat("_Smoothness", .85f);
                gem.SetColor("_EmissionColor", new Color(.46f,.06f,.72f)); gem.EnableKeyword("_EMISSION");
                AssetDatabase.CreateAsset(gem, folder + "/Diamond.mat");
                var halo = new Material(AssetDatabase.LoadAssetAtPath<Material>(folder + "/Sparks.mat"));
                halo.SetColor("_BaseColor", new Color(.68f,.12f,1f,.18f));
                AssetDatabase.CreateAsset(halo, folder + "/DiamondGlow.mat");
            }
            var diamond = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Diamond.mat");
            diamond.SetColor("_EmissionColor", new Color(1.5f,.12f,2.8f));
            diamond.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            diamond.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(diamond);
            if (AssetDatabase.LoadAssetAtPath<Material>(folder + "/Asphalt.mat") == null)
            {
                var texture = new Texture2D(256,256,TextureFormat.RGB24,true) { name = "Asphalt", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
                var rng = new System.Random(10);
                for(int y=0;y<256;y++) for(int x=0;x<256;x++)
                {
                    float value = .27f + (float)rng.NextDouble()*.09f;
                    texture.SetPixel(x,y,new Color(value*.9f,value*.97f,value));
                }
                texture.Apply(); AssetDatabase.CreateAsset(texture,folder+"/Asphalt.asset");
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap",texture); material.SetFloat("_Smoothness",.15f);
                AssetDatabase.CreateAsset(material,folder+"/Asphalt.mat");
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        public static void BuildMac()
        {
            PrepareArt();
            PlayerSettings.companyName = "Kai Kai";
            PlayerSettings.productName = "kai kai";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone,"com.Kai-Kai.Kai-Kai");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory("Builds/Release");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ProjectSetup.ScenePath }, locationPathName = "Builds/Release/kai kai.app",
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            Debug.Log($"Kai Kai build: {report.summary.result}, {report.summary.totalSize} bytes.");
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Release build failed");
        }
    }
}
