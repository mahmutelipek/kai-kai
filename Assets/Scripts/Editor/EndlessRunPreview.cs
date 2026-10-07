using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    public static class EndlessRunPreview
    {
        public static void CaptureBatch() => BoardArtPreview.QueueBatch(Capture);
        [MenuItem("Downhill/Art/Capture Endless Run Review")]
        public static void Capture()
        {
            ReleaseBuild.PrepareArt();
            RiderArtSetup.BuildAll();
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            Scene previous = SceneManager.GetActiveScene();
            Scene review = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var states = new bool[lights.Length];
            for (int i = 0; i < lights.Length; i++) { states[i] = lights[i].enabled; lights[i].enabled = false; }
            SceneManager.SetActiveScene(review);
            RenderTexture target = null; Texture2D image = null;
            var wakeMeshes = new List<Mesh>();
            RenderTexture previousTarget = RenderTexture.active;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var parent = new GameObject("Endless Review");
                var road = TestRoad.Build(parent.transform, endless: true);
                road.Path.Sample(90f, out Vector3 center, out float yaw);
                Quaternion heading = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
                var board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BoardArtSetup.PrefabPath));
                road.Path.Sample(87.5f,out Vector3 rear,out _); road.Path.Sample(92.5f,out Vector3 front,out _);
                float pitch = Mathf.Atan2(front.y-rear.y,Vector2.Distance(new Vector2(front.x,front.z),new Vector2(rear.x,rear.z)));
                board.transform.SetPositionAndRotation(center, heading * Quaternion.Euler(-pitch*Mathf.Rad2Deg,0,0));
                board.transform.localScale = new Vector3(3.4f / 2.4f, 1, 7.2f / 6f);
                var crew = new GameObject("Review Crew").transform; crew.SetPositionAndRotation(board.transform.position, board.transform.rotation);
                for (int i = 0; i < 6; i++)
                {
                    var rider = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiderArtSetup.PrefabPathFor(RiderArtRig.AssetNames[i])), crew);
                    rider.transform.localScale = Vector3.one * PlayerView.VisualScale;
                    rider.transform.localPosition = new Vector3(i % 2 == 0 ? -.68f : .68f, .862f, (i / 2 - 1) * 1.98f);
                    rider.GetComponent<RiderArtRig>().PoseArms(32f + i * 2f, 0f);
                }
                board.AddComponent<WheelFeedback>().Initialize(board.transform);
                board.GetComponent<WheelFeedback>().PreviewWake();
                var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
                sun.intensity = 1.1f; sun.color = new Color(1f, 0.95f, 0.83f); sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(40, -35, 0);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.3f, 0.35f, 0.42f);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.61f, 0.77f, 0.9f); RenderSettings.fogDensity = 0.003f;
                var camera = new GameObject("Review Camera").AddComponent<Camera>();
                camera.transform.position = center + heading * new Vector3(-5.8f, 3.8f, -7f);
                camera.transform.LookAt(center + heading * new Vector3(0, 1.1f, 1.4f));
                camera.fieldOfView = 58; camera.nearClipPlane = 0.2f; camera.farClipPlane = 550f;
                RoadAtmosphere.Apply();
                RunVisualPolish.Apply(parent.transform, camera);
                foreach (var ps in board.GetComponentsInChildren<ParticleSystem>())
                {
                    var mesh = new Mesh(); wakeMeshes.Add(mesh);
                    ps.GetComponent<ParticleSystemRenderer>().BakeMesh(mesh, camera, true);
                    var wake = new GameObject("Review " + ps.name); wake.AddComponent<MeshFilter>().sharedMesh = mesh;
                    wake.AddComponent<MeshRenderer>().sharedMaterial = ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                    ps.GetComponent<ParticleSystemRenderer>().enabled = false;
                }
                target = new RenderTexture(1600, 1000, 24); camera.targetTexture = target;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
                Directory.CreateDirectory("ArtSource/Endless");
                File.WriteAllBytes("ArtSource/Endless/Unity_Endless_Review.png", image.EncodeToPNG());
                var chase = camera.gameObject.AddComponent<CameraController>();
                camera.transform.position = center + heading * new Vector3(chase.lateralOffset, chase.heightAbove-Mathf.Tan(pitch)*chase.distanceBehind*.55f, -chase.distanceBehind);
                camera.transform.LookAt(center + heading * new Vector3(0, chase.lookHeight+Mathf.Tan(pitch)*chase.lookAhead, chase.lookAhead));
                camera.fieldOfView = Mathf.Lerp(chase.fovMin, chase.fovMax, 25f / 35f);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
                File.WriteAllBytes("ArtSource/Endless/Unity_GameCamera_Review.png", image.EncodeToPNG());
                Debug.Log("Downhill: endless road and chase-camera reviews captured.");
            }
            finally
            {
                RenderTexture.active = previousTarget; ShaderUtil.allowAsyncCompilation = previousAsync;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.CloseScene(review, true); SceneManager.SetActiveScene(previous);
                foreach (var mesh in wakeMeshes) UnityEngine.Object.DestroyImmediate(mesh);
                for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].enabled = states[i];
            }
        }
    }
}
