using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>Captures the imported prefab in Unity without editing the gameplay scene.</summary>
    public static class BoardArtPreview
    {
        public static void CaptureBatch() => QueueBatch(Capture);

        public static void CaptureBlueRiderBatch() => QueueBatch(CaptureBlueRider);

        [MenuItem("Downhill/Art/Capture Blue Rider Review")]
        public static void CaptureBlueRider()
        {
            Capture(true);
            Capture(false, true);
        }

        public static void CaptureAllRidersBatch() => QueueBatch(CaptureAllRiders);

        [MenuItem("Downhill/Art/Capture All Riders Review")]
        public static void CaptureAllRiders()
        {
            Capture(false, true, true);
            Capture(false, false, true);
        }

        internal static void QueueBatch(Action capture)
        {
            double started = EditorApplication.timeSinceStartup;
            EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                if (EditorApplication.timeSinceStartup - started < 3 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                EditorApplication.update -= callback;
                try { capture(); EditorApplication.Exit(0); }
                catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
            };
            EditorApplication.update += callback;
        }

        [MenuItem("Downhill/Art/Capture Board Review")]
        public static void Capture() => Capture(false);

        static void Capture(bool riderOnly, bool riderOnBoard = false, bool allRiders = false)
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            Scene previous = SceneManager.GetActiveScene();
            Scene review = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Light[] existingLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            bool[] lightStates = new bool[existingLights.Length];
            for (int i = 0; i < existingLights.Length; i++)
            {
                lightStates[i] = existingLights[i].enabled;
                existingLights[i].enabled = false;
            }
            SceneManager.SetActiveScene(review);
            RenderTexture target = null;
            Texture2D image = null;
            Material floorMaterial = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(riderOnly ? RiderArtSetup.PrefabPath : BoardArtSetup.PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Build the board prefab first.");
                GameObject subject = allRiders && !riderOnBoard ? null : UnityEngine.Object.Instantiate(prefab);
                if (riderOnly) subject.GetComponent<RiderArtRig>().PoseArms(22f, 0f);
                if (allRiders)
                {
                    for (int i = 0; i < RiderArtRig.AssetNames.Length; i++)
                    {
                        var riderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiderArtSetup.PrefabPathFor(RiderArtRig.AssetNames[i]));
                        if (riderPrefab == null) throw new InvalidOperationException("Missing rider prefab: " + RiderArtRig.AssetNames[i]);
                        var rider = UnityEngine.Object.Instantiate(riderPrefab);
                        if (riderOnBoard) rider.transform.localScale = Vector3.one * PlayerView.VisualScale;
                        rider.transform.position = riderOnBoard
                            ? new Vector3(i % 2 == 0 ? -0.5f : 0.5f, 0.862f, (i / 2 - 1) * 1.5f)
                            : new Vector3((i - 2.5f) * 1.25f, 0f, 0f);
                        rider.GetComponent<RiderArtRig>().PoseArms(riderOnBoard ? 35f : 15f, 0f);
                    }
                }
                else if (riderOnBoard)
                {
                    var riderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiderArtSetup.PrefabPath);
                    var rider = UnityEngine.Object.Instantiate(riderPrefab);
                    if (riderOnBoard) rider.transform.localScale = Vector3.one * PlayerView.VisualScale;
                    rider.transform.position = new Vector3(0f, 0.862f, -0.8f);
                    rider.GetComponent<RiderArtRig>().PoseArms(30f, 0f);
                }
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.localScale = Vector3.one * 10f;
                floor.transform.position = Vector3.down * 0.015f;
                floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                floorMaterial.SetColor("_BaseColor", new Color(0.16f, 0.20f, 0.27f));
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                var light = new GameObject("Key").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
                if (riderOnly || riderOnBoard || allRiders)
                {
                    var fill = new GameObject("Rider Fill").AddComponent<Light>();
                    fill.type = LightType.Directional;
                    fill.intensity = 0.55f;
                    fill.color = new Color(0.85f, 0.92f, 1f);
                    fill.transform.rotation = Quaternion.Euler(20f, 160f, 0f);
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.25f);
                var camera = new GameObject("Review Camera").AddComponent<Camera>();
                camera.transform.position = allRiders && !riderOnBoard ? new Vector3(3f, 2f, 10f) : riderOnly ? new Vector3(2f, 1.4f, 3.5f) : new Vector3(6f, 5f, riderOnBoard ? 7f : -7f);
                camera.transform.LookAt(new Vector3(0f, riderOnly ? 0.67f : allRiders ? 1f : 0.45f, 0f));
                camera.orthographic = true;
                camera.orthographicSize = allRiders && !riderOnBoard ? 3.1f : riderOnly ? 0.88f : 3.6f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.13f, 0.19f);
                target = new RenderTexture(1400, 1000, 24);
                camera.targetTexture = target;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0);
                image.Apply();
                string folder = riderOnly || riderOnBoard || allRiders ? "ArtSource/Riders" : "ArtSource/Board";
                string filename = allRiders ? (riderOnBoard ? "Unity_AllRiders_OnBoard.png" : "Unity_AllRiders_Lineup.png") : riderOnly ? "Unity_BlueRider_Review.png" : riderOnBoard ? "Unity_BlueRider_OnBoard.png" : "Unity_Board_Review.png";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(folder + "/" + filename, image.EncodeToPNG());
                Debug.Log("Downhill: Unity art review captured: " + folder + "/" + filename);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (floorMaterial != null) UnityEngine.Object.DestroyImmediate(floorMaterial);
                EditorSceneManager.CloseScene(review, true);
                SceneManager.SetActiveScene(previous);
                for (int i = 0; i < existingLights.Length; i++)
                    if (existingLights[i] != null) existingLights[i].enabled = lightStates[i];
            }
        }
    }
}
