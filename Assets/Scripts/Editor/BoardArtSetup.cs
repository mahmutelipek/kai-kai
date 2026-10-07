using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class BoardArtImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath != BoardArtSetup.ModelPath) return;
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

    /// <summary>Turns the isolated Blender export into a runtime prefab with native URP materials.</summary>
    [InitializeOnLoad]
    public static class BoardArtSetup
    {
        public const string ModelPath = "Assets/Art/Models/Board/PartyBoard.fbx";
        public const string PrefabPath = "Assets/Resources/Art/PartyBoard.prefab";
        const string MaterialFolder = "Assets/Art/Materials/Board";
        const string MaterialDataPath = "Assets/Art/Models/Board/PartyBoard.materials.json";

        [Serializable] public sealed class ExportData { public MaterialData[] materials; }
        [Serializable] public sealed class MaterialData
        {
            public string name;
            public float[] linearColor;
            public float roughness;
            public float metallic;
        }

        static BoardArtSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ModelPath) ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
                Build();
            };
        }

        [MenuItem("Downhill/Art/Rebuild Board Prefab")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Board FBX could not be imported.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            ProjectSetup.EnsureFolder("Assets/Resources/Art");
            var materials = CreateMaterials(MaterialDataPath, MaterialFolder, shader);

            var prefab = new GameObject("PartyBoard");
            try
            {
                GameObject visual = UnityEngine.Object.Instantiate(model, prefab.transform);
                visual.name = "BlenderVisual";
                Transform nose = Find(visual.transform, "NoseMarker");
                Transform deckTop = Find(visual.transform, "DeckTop");
                if (nose == null || deckTop == null) throw new InvalidOperationException("Board export anchors are missing.");
                if (prefab.transform.InverseTransformPoint(nose.position).z < 0f)
                    visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * visual.transform.localRotation;
                AssignMaterials(visual, materials);
                Validate(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Downhill: Blender board prefab ready: " + PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(prefab); }
        }

        public static Dictionary<string, Material> CreateMaterials(string dataPath, string folder, Shader shader)
        {
            ProjectSetup.EnsureFolder(folder);
            var data = JsonUtility.FromJson<ExportData>(File.ReadAllText(dataPath));
            var materials = new Dictionary<string, Material>();
            foreach (MaterialData source in data.materials)
            {
                string path = folder + "/" + source.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = source.name };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                // Blender stores the BSDF input in linear space; Unity's color property is authored in sRGB.
                var linear = new Color(source.linearColor[0], source.linearColor[1], source.linearColor[2], source.linearColor[3]);
                material.SetColor("_BaseColor", linear.gamma);
                material.SetFloat("_Smoothness", 1f - source.roughness);
                material.SetFloat("_Metallic", source.metallic);
                EditorUtility.SetDirty(material);
                materials[source.name] = material;
            }
            return materials;
        }

        public static void AssignMaterials(GameObject visual, Dictionary<string, Material> materials)
        {
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null || !materials.TryGetValue(slots[i].name, out Material native))
                        throw new InvalidOperationException("Unmapped material on " + renderer.name);
                    slots[i] = native;
                }
                renderer.sharedMaterials = slots;
            }
        }

        public static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
                if (child.name == name) return child;
            return null;
        }

        static void Validate(GameObject root)
        {
            Transform deck = Find(root.transform, "Deck");
            Transform top = Find(root.transform, "DeckTop");
            Transform nose = Find(root.transform, "NoseMarker");
            if (deck == null) throw new InvalidOperationException("Deck mesh missing.");
            var bounds = deck.GetComponent<Renderer>().bounds;
            Vector3 topPoint = root.transform.InverseTransformPoint(top.position);
            Vector3 nosePoint = root.transform.InverseTransformPoint(nose.position);
            if (Mathf.Abs(bounds.size.x - 2.4f) > 0.03f || Mathf.Abs(bounds.size.z - 6f) > 0.03f ||
                Mathf.Abs(topPoint.y - 0.862f) > 0.02f || nosePoint.z < 2.9f)
                throw new InvalidOperationException($"Board axis/scale mismatch: deck {bounds.size}, top {topPoint}, nose {nosePoint}");
            for (int i = 0; i < 4; i++)
                if (Find(root.transform, "WheelPivot" + i) == null)
                    throw new InvalidOperationException("Wheel pivot missing: " + i);
        }
    }
}
