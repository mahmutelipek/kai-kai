using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
namespace Game.EditorTools
{
    public static class RiderPortraitSetup
    {
        public static void CaptureBatch()=>BoardArtPreview.QueueBatch(Capture);
        [MenuItem("Downhill/Art/Capture Character Heads")]
        public static void Capture()
        {
            if(Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var oldTarget=RenderTexture.active;bool oldAsync=ShaderUtil.allowAsyncCompilation;
            var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(256,256,TextureFormat.RGBA32,false);
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);RenderSettings.fog=false;
                var light=new GameObject("Portrait Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(25,-150,0);
                var camera=new GameObject("Portrait Camera").AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.targetTexture=target;
                Directory.CreateDirectory("Assets/Resources/Art/Portraits");
                foreach(string name in RiderArtRig.AssetNames)
                {
                    var rider=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiderArtSetup.PrefabPathFor(name)));
                    var head=rider.GetComponent<RiderArtRig>().head;
                    foreach(var t in rider.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                    foreach(var renderer in rider.GetComponentsInChildren<Renderer>())renderer.enabled=renderer.transform.IsChildOf(head);
                    var renderers=head.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x)*.59f;
                    camera.transform.position=bounds.center+new Vector3(.15f,.05f,3);camera.transform.LookAt(bounds.center);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    RenderTexture.active=target;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                    string path="Assets/Resources/Art/Portraits/"+name+".png";File.WriteAllBytes(path,image.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                    Object.DestroyImmediate(rider);
                }
                Debug.Log("Captured six character head portraits.");
            }
            finally
            {
                RenderTexture.active=oldTarget;ShaderUtil.allowAsyncCompilation=oldAsync;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);
                EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
            }
        }
    }
}
