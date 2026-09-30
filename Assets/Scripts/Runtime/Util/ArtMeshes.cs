using System.Collections.Generic;
using Game.Art;
using UnityEngine;
using UnityEngine.Rendering;
using SVec3 = System.Numerics.Vector3;

namespace Game
{
    /// <summary>
    /// Turns engine-free Game.Art geometry into Unity meshes. Every colour is a texel of one palette texture, so
    /// all art shares three materials (matte, glossy, glowing) and each mesh has at most three submeshes.
    /// Buffers are reused: rebuilding a chunk mesh does not allocate once warmed up.
    /// </summary>
    public static class ArtMeshes
    {
        public const int Matte = 0, Glossy = 1, Glow = 2;

        static Texture2D _palette;
        static int _paletteVersion = -1;
        static Material[] _materials;
        static readonly List<Vector3> Verts = new List<Vector3>(65536);
        static readonly List<Vector2> Uvs = new List<Vector2>(65536);
        static readonly List<int>[] Tris = { new List<int>(98304), new List<int>(16384), new List<int>(4096) };
        static readonly Dictionary<ArtModel, Dictionary<string, Mesh>> GroupMeshes = new Dictionary<ArtModel, Dictionary<string, Mesh>>();
        static readonly MeshSet Scratch = new MeshSet();

        /// <summary>Shared materials: [matte, glossy, glow], all sampling the palette texture.</summary>
        public static Material[] Materials
        {
            get
            {
                if (_materials == null || _materials[0] == null) CreateMaterials();
                RefreshPalette();
                return _materials;
            }
        }

        /// <summary>Fills <paramref name="mesh"/> with a MeshSet (world or local space, as built).</summary>
        public static void Upload(MeshSet set, Mesh mesh)
        {
            Verts.Clear();
            Uvs.Clear();
            for (int k = 0; k < Tris.Length; k++) Tris[k].Clear();
            foreach (KeyValuePair<ArtColor, MeshData> kv in set.ByColor)
            {
                MeshData d = kv.Value;
                if (d.Triangles.Count == 0) continue;
                System.Numerics.Vector2 uv = Palette.Uv(Palette.IndexOf(kv.Key));
                var unityUv = new Vector2(uv.X, uv.Y);
                int baseIndex = Verts.Count;
                List<SVec3> src = d.Vertices;
                for (int i = 0; i < src.Count; i++)
                {
                    SVec3 v = src[i];
                    Verts.Add(new Vector3(v.X, v.Y, v.Z));
                    Uvs.Add(unityUv);
                }
                List<int> dst = Tris[Palette.Bucket(kv.Key)];
                List<int> tris = d.Triangles;
                for (int i = 0; i < tris.Count; i++) dst.Add(baseIndex + tris[i]);
            }
            mesh.Clear();
            mesh.indexFormat = Verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Verts);
            mesh.SetUVs(0, Uvs);
            mesh.subMeshCount = 3;
            for (int k = 0; k < Tris.Length; k++) mesh.SetTriangles(Tris[k], k, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            RefreshPalette();
        }

        /// <summary>Mesh of one group of a model in the group's local space (cached per model and group).</summary>
        public static Mesh GroupMesh(ArtModel model, string group)
        {
            if (!GroupMeshes.TryGetValue(model, out Dictionary<string, Mesh> byGroup)) GroupMeshes[model] = byGroup = new Dictionary<string, Mesh>();
            if (byGroup.TryGetValue(group, out Mesh mesh) && mesh != null) return mesh;
            Scratch.Clear();
            Scratch.AddGroupLocal(model, group);
            mesh = new Mesh { name = model.Name + "/" + (group.Length == 0 ? "Root" : group) };
            Upload(Scratch, mesh);
            mesh.UploadMeshData(false);
            byGroup[group] = mesh;
            return mesh;
        }

        static void CreateMaterials()
        {
            _palette = new Texture2D(Palette.Size, Palette.Size, TextureFormat.RGBA32, false, false)
            {
                name = "ArtPalette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
            };
            _paletteVersion = -1;
            _materials = new Material[3];
            _materials[Matte] = Make("Art Matte", 0.12f, false);
            _materials[Glossy] = Make("Art Glossy", 0.72f, false);
            _materials[Glow] = Make("Art Glow", 0.5f, true);
        }

        static Material Make(string name, float smoothness, bool emissive)
        {
            Material m = MaterialLibrary.Get(Color.white);
            m = new Material(m) { name = name, enableInstancing = true };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", _palette);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", _palette);
            m.color = Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", _palette);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white * 1.6f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        static void RefreshPalette()
        {
            if (_palette == null || _paletteVersion == Palette.Version) return;
            _paletteVersion = Palette.Version;
            var pixels = new Color32[Palette.Size * Palette.Size];
            for (int i = 0; i < Palette.Count; i++)
            {
                ArtColor c = Palette.Colors[i];
                pixels[i] = new Color32((byte)Palette.Byte(c.R), (byte)Palette.Byte(c.G), (byte)Palette.Byte(c.B), 255);
            }
            _palette.SetPixels32(pixels);
            _palette.Apply(false);
        }
    }
}
