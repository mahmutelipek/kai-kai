using System;
using System.Reflection;
using Game.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Sunny coastal afternoon: warm sun with soft shadows, sky / equator / ground ambient, distance fog, a
    /// procedural sky, and (URP only) a global post-processing volume with ACES tone mapping, gentle bloom for
    /// the glowing pickups, a little saturation and a vignette. Colours come from Game.Art.Atmosphere, shared
    /// with the headless preview. URP types are reached by reflection so a URP version difference only logs a
    /// warning instead of breaking the build.
    /// </summary>
    public static class SceneAtmosphere
    {
        const string UrpAssembly = "Unity.RenderPipelines.Universal.Runtime";
        const string CoreAssembly = "Unity.RenderPipelines.Core.Runtime";

        static Color C(ArtColor c) => new Color(c.R, c.G, c.B);

        /// <summary>Finds or creates the sun and applies the look. Returns the sun.</summary>
        public static Light Apply()
        {
            Light sun = null;
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = C(Atmosphere.SunColor);
            sun.intensity = Atmosphere.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            System.Numerics.Vector3 f = Atmosphere.SunForward;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(f.X, f.Y, f.Z));
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = C(Atmosphere.AmbientSky) * 0.95f;
            RenderSettings.ambientEquatorColor = C(Atmosphere.AmbientEquator) * 0.8f;
            RenderSettings.ambientGroundColor = C(Atmosphere.AmbientGround) * 0.6f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = C(Atmosphere.FogColor);
            RenderSettings.fogStartDistance = Atmosphere.FogStart;
            RenderSettings.fogEndDistance = Atmosphere.FogEnd;

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "Coastal Sky" };
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", C(Atmosphere.SkyTop));
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", C(Atmosphere.SkyHorizon));
                if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.75f);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.25f);
                if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.03f);
                RenderSettings.skybox = sky;
            }
            ConfigurePipeline();
            return sun;
        }

        /// <summary>Camera side: far plane for the backdrop, HDR, post-processing on (URP).</summary>
        public static void SetupCamera(Camera cam)
        {
            cam.farClipPlane = 1700f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            try
            {
                Type data = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " + UrpAssembly);
                if (data == null) return;
                Component c = cam.GetComponent(data);
                if (c == null) c = cam.gameObject.AddComponent(data);
                data.GetProperty("renderPostProcessing")?.SetValue(c, true);
                PropertyInfo aa = data.GetProperty("antialiasing");
                if (aa != null) aa.SetValue(c, Enum.ToObject(aa.PropertyType, 1)); // FXAA
            }
            catch (Exception e)
            {
                Debug.LogWarning("Downhill: could not enable URP post-processing on the camera: " + e.Message);
            }
        }

        static void ConfigurePipeline()
        {
            try
            {
                RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
                if (asset == null) return;
                SetProperty(asset, "shadowDistance", 90f);
                SetProperty(asset, "supportsHDR", true);
                CreateVolume();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Downhill: URP atmosphere setup skipped: " + e.Message);
            }
        }

        static void SetProperty(object target, string name, object value)
        {
            PropertyInfo p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) p.SetValue(target, value);
        }

        static void CreateVolume()
        {
            Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, " + CoreAssembly);
            Type profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile, " + CoreAssembly);
            if (volumeType == null || profileType == null) return;
            if (UnityEngine.Object.FindFirstObjectByType(volumeType) != null) return;

            var go = new GameObject("Post Processing");
            Component volume = go.AddComponent(volumeType);
            volumeType.GetField("isGlobal")?.SetValue(volume, true);
            var profile = ScriptableObject.CreateInstance(profileType);
            volumeType.GetProperty("profile")?.SetValue(volume, profile);

            // ACES tone mapping, gentle bloom (glowing pickups, lamps), a touch more saturation, soft vignette,
            // and a little motion blur for speed (camera motion only)
            Override(profile, "Tonemapping", ("mode", 2));
            Override(profile, "Bloom", ("threshold", 0.95f), ("intensity", 0.6f), ("scatter", 0.65f));
            Override(profile, "ColorAdjustments", ("saturation", 12f), ("contrast", 8f), ("postExposure", 0.15f));
            Override(profile, "Vignette", ("intensity", 0.2f));
            Override(profile, "MotionBlur", ("intensity", 0.18f));
        }

        /// <summary>profile.Add(type, true) and sets each parameter's value (enums by int).</summary>
        static void Override(object profile, string component, params (string field, object value)[] values)
        {
            Type t = Type.GetType("UnityEngine.Rendering.Universal." + component + ", " + UrpAssembly);
            if (t == null) return;
            MethodInfo add = profile.GetType().GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            object comp = add?.Invoke(profile, new object[] { t, true });
            if (comp == null) return;
            foreach ((string field, object value) in values)
            {
                FieldInfo f = t.GetField(field);
                object parameter = f?.GetValue(comp);
                if (parameter == null) continue;
                parameter.GetType().GetProperty("overrideState")?.SetValue(parameter, true);
                PropertyInfo v = parameter.GetType().GetProperty("value");
                if (v == null) continue;
                object converted = v.PropertyType.IsEnum ? Enum.ToObject(v.PropertyType, value) : Convert.ChangeType(value, v.PropertyType);
                v.SetValue(parameter, converted);
            }
        }
    }
}
