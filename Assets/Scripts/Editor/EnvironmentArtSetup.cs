using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class EnvironmentArtImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Art/Models/Environment/", StringComparison.Ordinal) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.isReadable = true; // Runtime static batching of repeated scenery.
            importer.importAnimation = importer.importLights = importer.importCameras = importer.addCollider = false;
        }
    }

    [InitializeOnLoad]
    public static class EnvironmentArtSetup
    {
        public static string PrefabPath(string name) => "Assets/Resources/Art/Environment/" + name + ".prefab";
        static EnvironmentArtSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (string name in CoastalScenery.AssetNames)
                    if (File.Exists("Assets/Art/Models/Environment/" + name + ".fbx") && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)) == null) Build(name);
            };
        }

        [MenuItem("Downhill/Art/Rebuild Environment Prefabs")]
        public static void BuildAll()
        {
            foreach (string name in CoastalScenery.AssetNames) Build(name);
        }

        static void Build(string name)
        {
            string source = "Assets/Art/Models/Environment/" + name + ".fbx";
            AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (model == null) throw new InvalidOperationException("Missing environment model: " + name);
            var materials = BoardArtSetup.CreateMaterials(source.Replace(".fbx", ".materials.json"), "Assets/Art/Materials/Environment", Shader.Find("Universal Render Pipeline/Lit"));
            ProjectSetup.EnsureFolder("Assets/Resources/Art/Environment");
            var root = new GameObject(name);
            try
            {
                var visual = UnityEngine.Object.Instantiate(model, root.transform);
                visual.name = "BlenderVisual";
                Transform forward = BoardArtSetup.Find(visual.transform, "ForwardMarker");
                Transform right = BoardArtSetup.Find(visual.transform, "RightMarker");
                if (forward == null || right == null) throw new InvalidOperationException("Missing environment axes.");
                if (forward.position.z < 0) visual.transform.localRotation = Quaternion.Euler(0, 180, 0);
                if (forward.position.z < 0.9f || right.position.x < 0.9f) throw new InvalidOperationException("Environment axes mismatch: " + name);
                BoardArtSetup.AssignMaterials(visual, materials);
                foreach (Material material in materials.Values) material.enableInstancing = true;
                if (name.StartsWith("CoastalHouse", StringComparison.Ordinal)) AddWindows(root, name);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
                AssetDatabase.SaveAssets();
                Debug.Log("Downhill: environment prefab ready: " + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Material DetailMaterial(string name, Color color)
        {
            string path = "Assets/Art/Materials/Environment/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Source houses contain a solid body and roof; add reusable facade detail in Unity.
        static void AddWindows(GameObject root, string name)
        {
            Color[] walls = { new Color(0.78f, 0.57f, 0.38f), new Color(0.72f, 0.34f, 0.24f), new Color(0.4f, 0.6f, 0.55f) };
            var body = root.GetComponentInChildren<Renderer>();
            var bodyMaterials = body.sharedMaterials;
            bodyMaterials[0] = DetailMaterial(name + "Walls", walls[int.Parse(name.Substring(name.Length - 1))]);
            body.sharedMaterials = bodyMaterials;
            Bounds bounds = root.GetComponentInChildren<Renderer>().bounds;
            var vertices = new List<Vector3>();
            var windows = new List<int>();
            var trims = new List<int>();
            void Quad(Vector3 center, Vector3 right, Vector3 up, List<int> triangles)
            {
                int i = vertices.Count;
                vertices.Add(center - right - up); vertices.Add(center + right - up);
                vertices.Add(center + right + up); vertices.Add(center - right + up);
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            int floors = Mathf.Max(1, Mathf.FloorToInt(bounds.size.y / 2.7f));
            void Wall(Vector3 normal, Vector3 horizontal, float width)
            {
                int columns = Mathf.Max(1, Mathf.FloorToInt(width / 1.8f));
                for (int floor = 0; floor < floors; floor++)
                for (int col = 0; col < columns; col++)
                {
                    float lateral = (col + 0.5f) * width / columns - width * 0.5f;
                    Vector3 center = new Vector3(bounds.center.x, 1.4f + floor * 2.7f, bounds.center.z)
                        + horizontal * lateral + normal * (Mathf.Abs(normal.x) > 0.5f ? bounds.extents.x + 0.025f : bounds.extents.z + 0.025f);
                    Quad(center, horizontal * 0.46f, Vector3.up * 0.62f, trims);
                    Quad(center + normal * 0.012f, horizontal * 0.38f, Vector3.up * 0.54f, windows);
                    Quad(center + normal * 0.016f, horizontal * 0.025f, Vector3.up * 0.54f, trims);
                }
            }
            Wall(Vector3.forward, Vector3.right, bounds.size.x);
            Wall(Vector3.back, Vector3.left, bounds.size.x);
            Wall(Vector3.right, Vector3.back, bounds.size.z);
            Wall(Vector3.left, Vector3.forward, bounds.size.z);
            var mesh = new Mesh { name = name + "Windows" };
            mesh.SetVertices(vertices); mesh.subMeshCount = 2;
            mesh.SetTriangles(windows, 0); mesh.SetTriangles(trims, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            ProjectSetup.EnsureFolder("Assets/Art/Models/Environment/Details");
            string meshPath = "Assets/Art/Models/Environment/Details/" + name + "Windows.asset";
            var previous = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (previous == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, previous); UnityEngine.Object.DestroyImmediate(mesh); mesh = previous; }
            var detail = new GameObject("Facade Windows"); detail.transform.SetParent(root.transform, false);
            detail.AddComponent<MeshFilter>().sharedMesh = mesh;
            detail.AddComponent<MeshRenderer>().sharedMaterials = new[]
                { DetailMaterial("WindowGlass", new Color(0.12f, 0.27f, 0.34f)), DetailMaterial("WindowTrim", new Color(0.95f, 0.9f, 0.75f)) };
        }
    }
}
