using System;
using System.Numerics;
using static Game.Art.ArtMath;

namespace Game.Art
{
    /// <summary>
    /// The far scenery of the coastal hillside: the bay, a red suspension bridge, a pastel skyline, an island,
    /// sailboats, far hills and clouds. Built once in a local frame that follows the board (origin at the board,
    /// +Z = downhill travel direction, turned slowly with the road) so it always stays in the distance.
    /// </summary>
    public static class Backdrop
    {
        /// <summary>Sea level below the board.</summary>
        public const float SeaLevel = -45f;

        public static MeshSet Build()
        {
            var set = new MeshSet();
            var rng = new ArtRandom(20260930);
            Quaternion id = Quaternion.Identity;

            // sea: a large disc, with a lighter shallow ring near the shore
            set[Pal.SeaDeep].Cylinder(V(0f, SeaLevel - 0.5f, 400f), id, 2600f, 1f, 40);
            set[Pal.Sea].Cylinder(V(0f, SeaLevel - 0.3f, 400f), id, 900f, 1f, 40);

            // the hillside keeps falling toward the water on the right (bay side), far beyond the chunk terrain
            MeshData slope = set[Pal.FarHill2];
            for (int i = 0; i < 12; i++)
            {
                float z0 = -400f + i * 110f, z1 = z0 + 110f;
                slope.Quad(V(95f, -32f, z0), V(95f, -32f, z1), V(420f, SeaLevel + 0.5f, z1), V(420f, SeaLevel + 0.5f, z0));
            }
            set[Pal.Sand].Box(V(430f, SeaLevel + 0.2f, 400f), Euler(0f, 0f, -2f), V(30f, 1f, 1330f));

            // hills: big uphill mass on the left, far shore across the bay
            for (int i = 0; i < 9; i++)
            {
                float z = -300f + i * 160f + rng.Range(-30f, 30f);
                float h = rng.Range(90f, 170f);
                set[i % 2 == 0 ? Pal.Hill : Pal.FarHill].Sphere(V(-260f - rng.Range(0f, 120f), -30f, z), id, V(rng.Range(260f, 360f), h * 2f, rng.Range(220f, 300f)));
            }
            for (int i = 0; i < 12; i++)
            {
                float x = -900f + i * 230f + rng.Range(-60f, 60f);
                float h = rng.Range(50f, 120f);
                set[i % 2 == 0 ? Pal.FarHill : Pal.FarHill2].Sphere(V(x, SeaLevel - 5f, 1500f + rng.Range(0f, 250f)), id, V(rng.Range(300f, 480f), h * 2f, 260f));
            }

            City(set, ref rng);
            Skyline(set, ref rng, V(330f, SeaLevel, 820f));
            Bridge(set, V(-420f, SeaLevel, 700f), 520f);

            // island with a white building
            set[Pal.FarHill].Sphere(V(90f, SeaLevel - 4f, 520f), id, V(120f, 34f, 70f));
            set[Pal.Towers[0]].Box(V(90f, SeaLevel + 14f, 520f), id, V(40f, 8f, 14f));
            set[Pal.Towers[1]].Box(V(105f, SeaLevel + 22f, 520f), id, V(5f, 14f, 5f));

            // sailboats
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = V(rng.Range(-150f, 420f), SeaLevel, rng.Range(250f, 1100f));
                Quaternion r = Euler(0f, rng.Range(0f, 360f), 0f);
                set[Pal.White].Box(p + V(0f, 0.8f, 0f), r, V(3f, 1.6f, 9f));
                set[Pal.White].Wedge(p + V(0f, 1.6f, -3f), r, 0.3f, 6f, 10f);
            }

            // clouds
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = V(rng.Range(-1400f, 1400f), rng.Range(230f, 380f), rng.Range(300f, 1600f));
                for (int k = 0; k < 4; k++)
                {
                    float s = rng.Range(60f, 130f);
                    set[Pal.White].Sphere(p + V(k * s * 0.6f - s, rng.Range(-10f, 15f), rng.Range(-20f, 20f)), id, V(s * 1.6f, s * 0.55f, s));
                }
            }
            return set;
        }

        /// <summary>
        /// The town below the road on the bay side (reference: rooftops falling toward the water): pastel blocks
        /// with terracotta or white roofs and a few trees on the slope between the chunk terrain and the shore.
        /// </summary>
        static void City(MeshSet set, ref ArtRandom rng)
        {
            Quaternion id = Quaternion.Identity;
            for (float z = -260f; z < 900f; z += 17f)
            for (float x = 112f; x < 405f; x += 17f)
            {
                if (rng.Chance(0.12f)) continue;
                float px = x + rng.Range(-4f, 4f), pz = z + rng.Range(-4f, 4f);
                float t = (px - 95f) / (420f - 95f);
                float ground = -32f + (SeaLevel + 0.5f - -32f) * t;
                if (rng.Chance(0.15f))
                {
                    set[Pal.TreeLeaf].Sphere(V(px, ground + 3f, pz), id, V(7f, 6f, 7f), 3, 6);
                    continue;
                }
                float w = rng.Range(8f, 13f), d = rng.Range(8f, 12f), h = rng.Range(5f, 11f) + (rng.Chance(0.1f) ? 10f : 0f);
                Quaternion r = Euler(0f, rng.Range(-6f, 6f), 0f);
                set[Pal.Walls[rng.Range(0, Pal.Walls.Length)]].Box(V(px, ground + h * 0.5f - 1.5f, pz), r, V(w, h + 3f, d));
                ArtColor roof = rng.Chance(0.65f) ? (rng.Chance(0.5f) ? Pal.Roof : Pal.RoofDark) : Pal.Trim;
                set[roof].Box(V(px, ground + h - 1.5f + 0.9f, pz), r, V(w + 0.8f, 1.8f, d + 0.8f));
            }
        }

        static void Skyline(MeshSet set, ref ArtRandom rng, Vector3 centre)
        {
            Quaternion id = Quaternion.Identity;
            set[Pal.FarHill2].Sphere(centre + V(0f, -8f, 20f), id, V(420f, 30f, 200f));
            for (int i = 0; i < 34; i++)
            {
                float dx = rng.Range(-190f, 190f), dz = rng.Range(-60f, 90f);
                float fall = 1f - MathF.Abs(dx) / 230f;
                float h = 18f + rng.Range(10f, 120f) * fall * fall;
                float w = rng.Range(14f, 30f), d = rng.Range(14f, 30f);
                ArtColor c = Pal.Towers[rng.Range(0, Pal.Towers.Length)];
                Vector3 b = centre + V(dx, h * 0.5f, dz);
                set[c].Box(b, Euler(0f, rng.Range(-8f, 8f), 0f), V(w, h, d));
                if (h > 70f) set[c.Shade(0.9f)].Box(b + V(0f, h * 0.5f + 4f, 0f), id, V(w * 0.6f, 8f, d * 0.6f));
            }
            // the tall pyramid tower
            set[Pal.Towers[0]].Cylinder(centre + V(-40f, 95f, 10f), Euler(0f, 45f, 0f), 18f, 190f, 4, 1.5f);
        }

        /// <summary>Red suspension bridge: two towers, deck, sagging main cables, hangers.</summary>
        static void Bridge(MeshSet set, Vector3 start, float span)
        {
            Quaternion id = Quaternion.Identity;
            ArtColor red = Pal.BridgeRed;
            float deckY = 62f, towerH = 150f;
            float t0 = start.X + span * 0.22f, t1 = start.X + span * 0.78f;
            // deck from shore to shore
            set[red].Box(V(start.X + span * 0.5f, start.Y + deckY, start.Z), id, V(span * 1.25f, 4f, 14f));
            foreach (float tx in new[] { t0, t1 })
            for (int s = -1; s <= 1; s += 2)
            {
                set[red].Box(V(tx, start.Y + towerH * 0.5f, start.Z + s * 7f), id, V(7f, towerH, 5f));
                for (int k = 1; k <= 3; k++) set[red].Box(V(tx, start.Y + deckY + k * 25f, start.Z), id, V(6f, 5f, 14f));
            }
            // main cables: approximate catenary with straight pieces
            for (int s = -1; s <= 1; s += 2)
            {
                float z = start.Z + s * 7f;
                Vector3 prev = default;
                int pieces = 24;
                float x0 = start.X - span * 0.1f, x1 = start.X + span * 1.1f;
                for (int i = 0; i <= pieces; i++)
                {
                    float x = x0 + (x1 - x0) * i / pieces;
                    float y;
                    if (x < t0) y = deckY + (towerH - deckY) * (x - x0) / (t0 - x0);
                    else if (x > t1) y = deckY + (towerH - deckY) * (x1 - x) / (x1 - t1);
                    else { float u = (x - t0) / (t1 - t0) * 2f - 1f; y = deckY + 6f + (towerH - deckY - 6f) * u * u; }
                    var p = V(x, start.Y + y, z);
                    if (i > 0)
                    {
                        Vector3 d = p - prev;
                        set[red].Box((p + prev) * 0.5f, FromTo(Vector3.UnitX, d), V(d.Length() + 0.5f, 1.2f, 1.2f));
                        if (i % 2 == 0 && x > x0 + 20f && x < x1 - 20f)
                            set[red].Box(V(x, start.Y + (deckY + y) * 0.5f, z), id, V(0.4f, y - deckY, 0.4f));
                    }
                    prev = p;
                }
            }
        }
    }
}
