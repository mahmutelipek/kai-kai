using System.Collections.Generic;
using System.Numerics;

namespace Game.Art
{
    /// <summary>
    /// Every colour the art uses gets one texel in a small palette texture, so all art shares a handful of
    /// materials (matte / glossy / glowing) and meshes pick their colour by UV. Keeps draw calls and material
    /// count low (see .claude/skills/performance-optimization: "atlas textures and share materials").
    /// </summary>
    public static class Palette
    {
        public const int Size = 32;
        public const int Capacity = Size * Size;

        static readonly Dictionary<int, int> IndexByRgb = new Dictionary<int, int>(256);
        static readonly List<ArtColor> ColorsList = new List<ArtColor>(256);

        /// <summary>Increments whenever a colour is added (the Unity texture re-uploads then).</summary>
        public static int Version { get; private set; }
        public static int Count => ColorsList.Count;
        public static IReadOnlyList<ArtColor> Colors => ColorsList;

        public static int IndexOf(ArtColor c)
        {
            int key = Byte(c.R) << 16 | Byte(c.G) << 8 | Byte(c.B);
            if (IndexByRgb.TryGetValue(key, out int index)) return index;
            if (ColorsList.Count >= Capacity) return Capacity - 1;
            index = ColorsList.Count;
            ColorsList.Add(new ArtColor(c.R, c.G, c.B));
            IndexByRgb[key] = index;
            Version++;
            return index;
        }

        /// <summary>UV of the texel centre (row 0 at v = 0).</summary>
        public static Vector2 Uv(int index) => new Vector2((index % Size + 0.5f) / Size, (index / Size + 0.5f) / Size);

        public static int Byte(float v) => v <= 0f ? 0 : v >= 1f ? 255 : (int)(v * 255f + 0.5f);

        /// <summary>Material bucket: 0 matte, 1 glossy, 2 emissive.</summary>
        public static int Bucket(ArtColor c) => c.Emissive ? 2 : c.Smoothness >= 0.5f ? 1 : 0;
    }
}
