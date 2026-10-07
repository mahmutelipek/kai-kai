using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    public static class EnvironmentArtPreview
    {
        public static void CaptureBatch() => BoardArtPreview.QueueBatch(Capture);

        [MenuItem("Downhill/Art/Capture Coastal Environment Review")]
        public static void Capture()
        {
            EnvironmentArtSetup.BuildAll();
            CaptureAt(40f, "Unity_Coastal_Review.png");
            CaptureAt(210f, "Unity_Coastal_Curve.png");
        }

        static void CaptureAt(float distance, string filename)
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            Scene previous = SceneManager.GetActiveScene();
            Scene review = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var states = new bool[lights.Length];
            for (int i = 0; i < lights.Length; i++) { states[i] = lights[i].enabled; lights[i].enabled = false; }
            SceneManager.SetActiveScene(review);
            RenderTexture target = null; Texture2D image = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var parent = new GameObject("Coastal Review");
                var road = TestRoad.Build(parent.transform);
                if (road.GetComponentInChildren<CoastalScenery>() == null) throw new InvalidOperationException("Environment prefabs are missing.");
                road.Path.Sample(distance, out Vector3 center, out float yaw);
                Quaternion heading = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
                var board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BoardArtSetup.PrefabPath));
                board.transform.SetPositionAndRotation(center, heading * Quaternion.Euler(Mathf.Atan(TestRoad.Grade) * Mathf.Rad2Deg, 0, 0));
                for (int i = 0; i < 6; i++)
                {
                    var rider = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiderArtSetup.PrefabPathFor(RiderArtRig.AssetNames[i])), board.transform);
                    rider.transform.localPosition = new Vector3(i % 2 == 0 ? -0.5f : 0.5f, 0.862f, (i / 2 - 1) * 1.5f);
                    rider.GetComponent<RiderArtRig>().PoseArms(40f, 0f);
                }
                var sun = new GameObject("Coastal Sun").AddComponent<Light>(); sun.type = LightType.Directional;
                sun.intensity = 1.1f; sun.color = new Color(1f, 0.95f, 0.83f); sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(40, -35, 0);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.22f, 0.28f, 0.36f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.61f, 0.77f, 0.9f); RenderSettings.fogDensity = 0.00065f;
                var camera = new GameObject("Coastal Review Camera").AddComponent<Camera>();
                camera.transform.position = center + heading * new Vector3(-7f, 5.5f, -10f);
                camera.transform.LookAt(center + heading * new Vector3(0, 1f, 7f));
                camera.fieldOfView = 65; camera.nearClipPlane = 0.2f; camera.farClipPlane = 1800f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
                target = new RenderTexture(1600, 1000, 24); camera.targetTexture = target;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
                Directory.CreateDirectory("ArtSource/Environment");
                File.WriteAllBytes("ArtSource/Environment/" + filename, image.EncodeToPNG());
                Debug.Log("Downhill: coastal review captured: " + filename);
            }
            finally
            {
                RenderTexture.active = previousTarget; ShaderUtil.allowAsyncCompilation = previousAsync;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.CloseScene(review, true); SceneManager.SetActiveScene(previous);
                for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].enabled = states[i];
            }
        }
    }
}
