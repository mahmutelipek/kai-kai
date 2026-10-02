using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace Game.Art
{
    /// <summary>
    /// Reads a model exported by Tools/Blender/build_models.py ("DPBM" v1, little endian) into an ArtModel whose
    /// parts are baked meshes (<see cref="ArtShape.Mesh"/>) on the same group rig as the procedural models, so every
    /// view (Unity, the headless preview) and the animation code work unchanged. Engine-free.
    /// Layout: "DPBM", int version, string name, int groups {string name, string parent, float3 pivot},
    /// int parts {string group, string name, uint rgb, float smoothness, int n, float3[n] vertices (group space),
    /// int m, int[m] indices}. Strings: int byte length + UTF-8.
    /// </summary>
    public static class ArtAsset
    {
        public static ArtModel Read(byte[] data)
        {
            using var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
            if (r.ReadByte() != 'D' || r.ReadByte() != 'P' || r.ReadByte() != 'B' || r.ReadByte() != 'M')
                throw new InvalidDataException("not a DPBM model");
            int version = r.ReadInt32();
            if (version != 1) throw new InvalidDataException("unsupported DPBM version " + version);
            var model = new ArtModel(Str(r));
            int groups = r.ReadInt32();
            for (int i = 0; i < groups; i++)
            {
                string name = Str(r), parent = Str(r);
                model.Group(name, V3(r), parent);
            }
            int parts = r.ReadInt32();
            for (int i = 0; i < parts; i++)
            {
                string group = Str(r), name = Str(r);
                uint rgb = r.ReadUInt32();
                float smooth = r.ReadSingle();
                var mesh = new MeshData();
                int n = r.ReadInt32();
                for (int v = 0; v < n; v++) mesh.Vertices.Add(V3(r));
                int m = r.ReadInt32();
                for (int t = 0; t < m; t++)
                {
                    int index = r.ReadInt32();
                    if ((uint)index >= (uint)n) throw new InvalidDataException($"{name}: index {index} out of range");
                    mesh.Triangles.Add(index);
                }
                model.Parts.Add(new ArtPart
                {
                    Group = group, Name = name, Shape = ArtShape.Mesh, Mesh = mesh, Position = Vector3.Zero,
                    Size = Vector3.One, Rotation = Quaternion.Identity, Color = ArtColor.Hex(rgb, smooth),
                });
            }
            return model;
        }

        static string Str(BinaryReader r)
        {
            int len = r.ReadInt32();
            if (len < 0 || len > 1024) throw new InvalidDataException("bad string length");
            return Encoding.UTF8.GetString(r.ReadBytes(len));
        }

        static Vector3 V3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
