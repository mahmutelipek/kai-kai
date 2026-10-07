using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class RiderArtImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Art/Models/Riders/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.preserveHierarchy = true;
        }
    }

    [InitializeOnLoad]
    public static class RiderArtSetup
    {
        public const string ModelPath = "Assets/Art/Models/Riders/BlueRider.fbx";
        public const string PrefabPath = "Assets/Resources/Art/Riders/BlueRider.prefab";

        static RiderArtSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (string name in RiderArtRig.AssetNames)
                    if (File.Exists(ModelPathFor(name)) && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathFor(name)) == null)
                        BuildRider(name);
            };
        }

        [MenuItem("Downhill/Art/Rebuild Blue Rider Prefab")]
        public static void Build() => BuildRider("BlueRider");

        public static string ModelPathFor(string name) => "Assets/Art/Models/Riders/" + name + ".fbx";
        public static string PrefabPathFor(string name) => "Assets/Resources/Art/Riders/" + name + ".prefab";

        [MenuItem("Downhill/Art/Rebuild All Rider Prefabs")]
        public static void BuildAll()
        {
            foreach (string name in RiderArtRig.AssetNames) BuildRider(name);
        }

        // Open the baked crouch while keeping shoes and all part seams aligned.
        // Originals stay in the FBX; these derived meshes can be rebuilt at any time.
        static Vector3 StandingPoint(Vector3 p)
        {
            p.y += Mathf.Clamp01((p.y - .13f) / .23f) * .22f;
            return p;
        }

        static void BuildStandingMeshes(GameObject root, GameObject visual, string name)
        {
            const string folder = "Assets/Art/Models/Riders/Standing";
            ProjectSetup.EnsureFolder(folder);
            var filters = visual.GetComponentsInChildren<MeshFilter>();
            var positions = new Vector3[filters.Length][];
            for (int i = 0; i < filters.Length; i++)
            {
                var vertices = filters[i].sharedMesh.vertices;
                positions[i] = new Vector3[vertices.Length];
                for (int v = 0; v < vertices.Length; v++)
                    positions[i][v] = root.transform.InverseTransformPoint(filters[i].transform.TransformPoint(vertices[v]));
            }
            foreach (Transform pivot in visual.GetComponentsInChildren<Transform>())
                if (pivot.name.EndsWith("Pivot", StringComparison.Ordinal))
                    pivot.position = root.transform.TransformPoint(StandingPoint(root.transform.InverseTransformPoint(pivot.position)));
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = UnityEngine.Object.Instantiate(filters[i].sharedMesh);
                mesh.name = name + "_Standing_" + filters[i].name;
                var vertices = positions[i];
                for (int v = 0; v < vertices.Length; v++)
                    vertices[v] = filters[i].transform.InverseTransformPoint(root.transform.TransformPoint(
                        filters[i].name.StartsWith("Arm", StringComparison.Ordinal) ? vertices[v] + Vector3.up * .22f : StandingPoint(vertices[v])));
                mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string path = folder + "/" + mesh.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                filters[i].sharedMesh = mesh;
            }
        }

        static void BuildRider(string name)
        {
            string modelPath = ModelPathFor(name);
            string prefabPath = PrefabPathFor(name);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException(name + " FBX is missing.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            var materials = BoardArtSetup.CreateMaterials("Assets/Art/Models/Riders/" + name + ".materials.json", "Assets/Art/Materials/Riders/" + name.Replace("Rider", ""), shader);
            ProjectSetup.EnsureFolder("Assets/Resources/Art/Riders");
            var prefab = new GameObject(name);
            try
            {
                GameObject visual = UnityEngine.Object.Instantiate(model, prefab.transform);
                visual.name = "BlenderVisual";
                Transform forward = BoardArtSetup.Find(visual.transform, "ForwardMarker");
                if (forward == null) throw new InvalidOperationException("Forward anchor missing.");
                if (prefab.transform.InverseTransformPoint(forward.position).z < 0f)
                    visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * visual.transform.localRotation;
                BoardArtSetup.AssignMaterials(visual, materials);
                BuildStandingMeshes(prefab, visual, name);
                var rig = prefab.AddComponent<RiderArtRig>();
                rig.head = BoardArtSetup.Find(visual.transform, "HeadPivot");
                rig.armLeft = BoardArtSetup.Find(visual.transform, "ArmLPivot");
                rig.armRight = BoardArtSetup.Find(visual.transform, "ArmRPivot");
                rig.legLeft = BoardArtSetup.Find(visual.transform, "LegLPivot");
                rig.legRight = BoardArtSetup.Find(visual.transform, "LegRPivot");
                if (rig.head == null || rig.armLeft == null || rig.armRight == null || rig.legLeft == null || rig.legRight == null)
                    throw new InvalidOperationException("Rider articulation pivots are missing.");
                Bounds bounds = new Bounds();
                bool first = true;
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                Vector3 nose = prefab.transform.InverseTransformPoint(forward.position);
                if (Mathf.Abs(bounds.min.y) > 0.02f || Mathf.Abs(bounds.size.y - 1.57f) > 0.04f || nose.z < 0.9f)
                    throw new InvalidOperationException($"Rider scale/axes mismatch: bounds {bounds}, forward {nose}");
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Downhill: articulated rider prefab ready: " + prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(prefab); }
        }
    }
}
