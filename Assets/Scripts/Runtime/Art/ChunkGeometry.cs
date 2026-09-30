using System;
using System.Numerics;
using Game.Simulation;
using static Game.Art.ArtMath;

namespace Game.Art
{
    /// <summary>
    /// Builds the visual geometry of one road chunk: asphalt and markings, curbs and sidewalks, walls, bridge
    /// deck and railings, tunnel roof, ramps, and the modular backdrop along it (hillside terrain rising on the
    /// left and falling toward the bay on the right, houses, palms, street lights, signs, construction props).
    /// Deterministic per chunk serial; allocation-free once the MeshSet has warmed up.
    /// Road readability first: props stay outside the curb, backgrounds use soft pastel colours.
    /// </summary>
    public static class ChunkGeometry
    {
        public const float SidewalkWidth = 3f;
        public const float CurbWidth = 0.3f;
        public const float CurbHeight = 0.12f;
        /// <summary>Scenery starts rising this far outside the road edge (the physics offroad strip is flat).</summary>
        public const float FlatZone = 9f;

        // terrain profile per side: offsets from the road edge and heights (left rises, right falls to the bay)
        static readonly float[] ProfileOffsets = { SidewalkWidth + CurbWidth, FlatZone, 16f, 28f, 48f, 90f };
        static readonly float[] LeftHeights = { CurbHeight, 0.3f, 2.6f, 7.5f, 16f, 30f };
        static readonly float[] RightHeights = { CurbHeight, 0.1f, -2.5f, -9f, -22f, -33f };

        public static void Build(RoadChunk chunk, MeshSet set)
        {
            int n = chunk.SampleCount;
            if (n < 2) return;
            bool retaining = chunk.Serial % 3 == 1 && chunk.Kind != ChunkKind.Bridge && chunk.Kind != ChunkKind.Tunnel;

            for (int i = 1; i < n; i++)
            {
                Vector3 c0 = chunk.Points[i - 1], c1 = chunk.Points[i];
                Vector3 r0 = SimMath.Right3(chunk.Yaws[i - 1]), r1 = SimMath.Right3(chunk.Yaws[i]);
                float h0 = chunk.HalfWidths[i - 1], h1 = chunk.HalfWidths[i];
                float a0 = chunk.LocalAlongAt(i - 1), a1 = chunk.LocalAlongAt(i);
                float along0 = chunk.StartAlong + a0;
                bool crossing = InCrossing(chunk, a0) || InCrossing(chunk, a1);

                // asphalt, with an occasional darker patch
                set[Pal.Asphalt].Quad(c0 - r0 * h0, c1 - r1 * h1, c1 + r1 * h1, c0 + r0 * h0);
                if (Hash(chunk.Serial, i) % 23 == 0)
                {
                    float x = (Hash(chunk.Serial, i + 1000) % 100 / 100f - 0.5f) * (h0 - 1.5f);
                    Line(set[Pal.AsphaltPatch], c0, c1, r0, r1, x, x, 1.6f, 0.012f);
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    EdgeKind e0 = side < 0 ? chunk.LeftEdges[i - 1] : chunk.RightEdges[i - 1];
                    EdgeKind e1 = side < 0 ? chunk.LeftEdges[i] : chunk.RightEdges[i];
                    EdgeKind edge = e0 == e1 ? e0 : EdgeKind.Grass;
                    Vector3 in0 = c0 + r0 * (side * h0), in1 = c1 + r1 * (side * h1);
                    bool roofed = IsRoofed(chunk, i) || IsRoofed(chunk, i - 1);
                    switch (edge)
                    {
                        case EdgeKind.Grass:
                            if (!crossing)
                            {
                                Segment(set[Pal.Curb], in0 + r0 * (side * CurbWidth * 0.5f), in1 + r1 * (side * CurbWidth * 0.5f), CurbWidth, CurbHeight);
                                Strip(set[Pal.Sidewalk], in0, in1, r0, r1, side, CurbWidth, CurbWidth + SidewalkWidth, CurbHeight, CurbHeight);
                                // metal guardrail on the bay side, where the hill falls away
                                if (side > 0)
                                {
                                    float go = CurbWidth + SidewalkWidth - 0.15f;
                                    Segment(set[Pal.Metal], in0 + r0 * go + Up(CurbHeight + 0.55f), in1 + r1 * go + Up(CurbHeight + 0.55f), 0.08f, 0.32f);
                                    if (i % 2 == 0) set[Pal.MetalDark].Box(in1 + r1 * (go + 0.08f) + Up(CurbHeight + 0.42f), Euler(0f, chunk.Yaws[i] * SimMath.Rad2Deg, 0f), V(0.1f, 0.84f, 0.1f));
                                }
                                // red / white plastic barriers along the outside of real bends and construction zones
                                bool bendOutside = MathF.Abs(chunk.Shape.Turn) > 0.5f && (chunk.Shape.Turn > 0f ? side < 0 : side > 0) && a0 > chunk.Length * 0.12f && a1 < chunk.Length * 0.9f;
                                bool zone = chunk.Kind == ChunkKind.Construction && a0 > 10f && a1 < chunk.Length - 10f;
                                if (bendOutside || zone)
                                {
                                    ArtColor bc = (i / 1) % 2 == 0 ? Pal.Red : Pal.White;
                                    Segment(set[bc], in0 + r0 * (side * (CurbWidth + 0.45f)) + Up(CurbHeight), in1 + r1 * (side * (CurbWidth + 0.45f)) + Up(CurbHeight), 0.5f, 0.8f);
                                }
                            }
                            else
                            {
                                Strip(set[Pal.Asphalt], in0, in1, r0, r1, side, 0f, CurbWidth + SidewalkWidth, -0.01f, -0.01f);
                            }
                            break;
                        case EdgeKind.Wall:
                            Segment(set[Pal.Concrete], in0 + r0 * (side * 0.45f), in1 + r1 * (side * 0.45f), 0.3f, 1.2f);
                            Segment(set[Pal.Yellow], in0 + r0 * (side * 0.29f), in1 + r1 * (side * 0.29f), 0.02f, 0.3f);
                            if (roofed) Segment(set[Pal.TunnelInside], in0 + r0 * (side * 0.45f) + Up(1.2f), in1 + r1 * (side * 0.45f) + Up(1.2f), 0.3f, 4.8f);
                            break;
                        case EdgeKind.Void:
                            // bridge: deck edge beam, red railing with posts
                            Segment(set[Pal.Concrete], in0 + r0 * (side * 0.2f) + Up(-0.9f), in1 + r1 * (side * 0.2f) + Up(-0.9f), 0.4f, 0.9f);
                            Segment(set[Pal.BridgeRed], in0 + r0 * (side * 0.15f) + Up(0.95f), in1 + r1 * (side * 0.15f) + Up(0.95f), 0.12f, 0.12f);
                            Segment(set[Pal.BridgeRed], in0 + r0 * (side * 0.15f) + Up(0.45f), in1 + r1 * (side * 0.15f) + Up(0.45f), 0.08f, 0.08f);
                            if (i % 2 == 0) set[Pal.BridgeRed].Box(in1 + r1 * (side * 0.15f) + Up(0.5f), Euler(0f, chunk.Yaws[i] * SimMath.Rad2Deg, 0f), V(0.12f, 1f, 0.12f));
                            break;
                    }
                    if (edge != EdgeKind.Void) Terrain(chunk, set, i, side, edge, roofed, retaining && side < 0);
                }

                // bridge underside: deck slab + piers into the bay
                if (chunk.LeftEdges[i] == EdgeKind.Void && chunk.LeftEdges[i - 1] == EdgeKind.Void)
                {
                    set[Pal.ConcreteDark].Quad(c0 + r0 * h0 + Up(-0.9f), c1 + r1 * h1 + Up(-0.9f), c1 - r1 * h1 + Up(-0.9f), c0 - r0 * h0 + Up(-0.9f));
                    if (i % 15 == 0)
                    {
                        Quaternion rot = Euler(0f, chunk.Yaws[i] * SimMath.Rad2Deg, 0f);
                        set[Pal.BridgeRed].Box(c1 - r1 * (h1 - 1f) + Up(-20.9f), rot, V(1.6f, 40f, 1.6f));
                        set[Pal.BridgeRed].Box(c1 + r1 * (h1 - 1f) + Up(-20.9f), rot, V(1.6f, 40f, 1.6f));
                        set[Pal.BridgeRed].Box(c1 + Up(-3f), rot, V(h1 * 2f, 1.2f, 1.2f));
                    }
                }

                // tunnel roof: dark underside, grass on top
                if (IsRoofed(chunk, i))
                {
                    Vector3 up = Up(6f);
                    float rw0 = h0 + 0.6f, rw1 = h1 + 0.6f;
                    set[Pal.TunnelInside].Quad(c0 + r0 * rw0 + up, c1 + r1 * rw1 + up, c1 - r1 * rw1 + up, c0 - r0 * rw0 + up);
                    Vector3 top = Up(6.3f);
                    set[Pal.Hill].Quad(c0 - r0 * rw0 + top, c1 - r1 * rw1 + top, c1 + r1 * rw1 + top, c0 + r0 * rw0 + top);
                    if (i % 6 == 0) set[Pal.Lamp].Box((c0 + c1) * 0.5f + Up(5.9f), Euler(0f, chunk.Yaws[i] * SimMath.Rad2Deg, 0f), V(0.5f, 0.1f, 1.8f));
                }

                // markings: white edge lines, dashed double yellow centre line
                if (!crossing)
                {
                    Line(set[Pal.LineWhite], c0, c1, r0, r1, -h0 + 0.35f, -h1 + 0.35f, 0.15f);
                    Line(set[Pal.LineWhite], c0, c1, r0, r1, h0 - 0.35f, h1 - 0.35f, 0.15f);
                    if (MathF.Floor(along0 / 4f) % 2 == 0 && chunk.Kind != ChunkKind.Fork)
                    {
                        Line(set[Pal.LineYellow], c0, c1, r0, r1, -0.12f, -0.12f, 0.1f);
                        Line(set[Pal.LineYellow], c0, c1, r0, r1, 0.12f, 0.12f, 0.1f);
                    }
                }
            }

            if (chunk.Kind == ChunkKind.Intersection) CrossRoad(chunk, set);
            if (chunk.Kind == ChunkKind.Tunnel) Portals(chunk, set);
            for (int k = 0; k < chunk.Ramps.Count; k++) Ramp(chunk, chunk.Ramps[k], set);
            Props(chunk, set, retaining);
        }

        // ------------------------------------------------------------------ road pieces

        static Vector3 Up(float y) => new Vector3(0f, y, 0f);

        static int Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
            }
        }

        static bool IsRoofed(RoadChunk c, int i)
        {
            float a = c.LocalAlongAt(i);
            return c.RoofEnd > c.RoofStart && a >= c.RoofStart && a <= c.RoofEnd;
        }

        static bool InCrossing(RoadChunk c, float a) => c.Kind == ChunkKind.Intersection && MathF.Abs(a - c.Length * 0.5f) < 6f;

        /// <summary>How much of the side terrain profile applies (flattened around an intersection).</summary>
        static float TerrainScale(RoadChunk c, float a) =>
            c.Kind == ChunkKind.Intersection ? SimMath.SmoothStep(8f, 18f, MathF.Abs(a - c.Length * 0.5f)) : 1f;

        /// <summary>A box running from p0 to p1 (bottom centre points), with the given thickness and height.</summary>
        static void Segment(MeshData m, Vector3 p0, Vector3 p1, float thickness, float height)
        {
            Vector3 d = p1 - p0;
            float len = d.Length();
            if (len < 1e-3f) return;
            float yaw = MathF.Atan2(d.X, d.Z);
            float pitch = -MathF.Atan2(d.Y, MathF.Sqrt(d.X * d.X + d.Z * d.Z));
            Quaternion rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch);
            m.Box((p0 + p1) * 0.5f + Up(height * 0.5f), rot, new Vector3(thickness, height, len + 0.02f));
        }

        static void Line(MeshData m, Vector3 c0, Vector3 c1, Vector3 r0, Vector3 r1, float off0, float off1, float width, float lift = 0.02f)
        {
            Vector3 up = Up(lift);
            m.Quad(c0 + r0 * (off0 - width * 0.5f) + up, c1 + r1 * (off1 - width * 0.5f) + up,
                   c1 + r1 * (off1 + width * 0.5f) + up, c0 + r0 * (off0 + width * 0.5f) + up);
        }

        /// <summary>A flat strip beside the road edge from offset o0 to o1 (outward), heights y0 / y1, facing up.</summary>
        static void Strip(MeshData m, Vector3 in0, Vector3 in1, Vector3 r0, Vector3 r1, int side, float o0, float o1, float y0, float y1)
        {
            Vector3 a0 = in0 + r0 * (side * o0) + Up(y0), a1 = in1 + r1 * (side * o0) + Up(y0);
            Vector3 b0 = in0 + r0 * (side * o1) + Up(y1), b1 = in1 + r1 * (side * o1) + Up(y1);
            if (side > 0) m.Quad(a0, a1, b1, b0);
            else m.Quad(b0, b1, a1, a0);
        }

        static void Terrain(RoadChunk chunk, MeshSet set, int i, int side, EdgeKind edge, bool roofed, bool retaining)
        {
            Vector3 c0 = chunk.Points[i - 1], c1 = chunk.Points[i];
            Vector3 r0 = SimMath.Right3(chunk.Yaws[i - 1]), r1 = SimMath.Right3(chunk.Yaws[i]);
            Vector3 in0 = c0 + r0 * (side * chunk.HalfWidths[i - 1]), in1 = c1 + r1 * (side * chunk.HalfWidths[i]);
            float s0 = TerrainScale(chunk, chunk.LocalAlongAt(i - 1)), s1 = TerrainScale(chunk, chunk.LocalAlongAt(i));
            float[] heights = side < 0 ? LeftHeights : RightHeights;
            float start = edge == EdgeKind.Wall ? 0.6f : ProfileOffsets[0];
            float prevO = start;
            float prevH = edge == EdgeKind.Wall ? 1.2f : CurbHeight;
            if (roofed) prevH = 6.3f;
            for (int k = 1; k < ProfileOffsets.Length; k++)
            {
                float o = ProfileOffsets[k];
                float h = heights[k];
                if (roofed) h = Math.Max(h, 6.3f + (k - 1) * 1.5f);
                else if (edge == EdgeKind.Wall && k == 1) h = Math.Max(h, 1.2f);
                if (retaining && k == 1) h = 0.1f;
                if (retaining && k == 2) h = Math.Max(h, 4.5f);
                float p0 = prevH, p1 = h;
                float ph0 = p0 * (roofed ? 1f : s0), ph1 = p1 * (roofed ? 1f : s0);
                float qh0 = p0 * (roofed ? 1f : s1), qh1 = p1 * (roofed ? 1f : s1);
                ArtColor color = k == 1 ? Pal.Grass : (k % 2 == 0 ? Pal.Hill : Pal.GrassDark);
                MeshData m = set[color];
                Vector3 a0 = in0 + r0 * (side * prevO) + Up(ph0), a1 = in1 + r1 * (side * prevO) + Up(qh0);
                Vector3 b0 = in0 + r0 * (side * o) + Up(ph1), b1 = in1 + r1 * (side * o) + Up(qh1);
                if (side > 0) m.Quad(a0, a1, b1, b0);
                else m.Quad(b0, b1, a1, a0);
                if (retaining && k == 2)
                {
                    // concrete retaining wall face where the hillside steps up
                    Segment(set[Pal.Concrete], in0 + r0 * (side * (FlatZone + 0.2f)) + Up(0.1f * s0 - 0.3f), in1 + r1 * (side * (FlatZone + 0.2f)) + Up(0.1f * s1 - 0.3f), 0.5f, 4.7f * Math.Max(s0, 0.05f));
                }
                prevO = o;
                prevH = h;
            }
        }

        static void CrossRoad(RoadChunk chunk, MeshSet set)
        {
            chunk.SampleLocal(chunk.Length * 0.5f, out Vector3 cs, out float yaw, out float hw);
            Vector3 c = cs + Up(-0.01f);
            Vector3 f = SimMath.Forward3(yaw), r = SimMath.Right3(yaw);
            const float halfLength = 45f, halfWidth = 5f;
            set[Pal.Asphalt].Quad(c - r * halfLength - f * halfWidth, c - r * halfLength + f * halfWidth,
                                  c + r * halfLength + f * halfWidth, c + r * halfLength - f * halfWidth);
            Quaternion rot = Euler(0f, yaw * SimMath.Rad2Deg, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                set[Pal.LineWhite].Box(c + r * (s * (hw + 1.5f)) + Up(0.03f), rot, V(0.4f, 0.02f, halfWidth * 2f));
                // sidewalk corners along the crossing road
                for (int e = -1; e <= 1; e += 2)
                    set[Pal.Sidewalk].Box(c + r * (s * (hw + 26f)) + f * (e * (halfWidth + 1.6f)) + Up(0.06f), rot, V(40f, 0.12f, 3f));
            }
            for (float x = -hw + 0.6f; x < hw - 0.4f; x += 1.2f)
                set[Pal.LineWhite].Box(c + r * x - f * (halfWidth + 2f) + Up(0.03f), rot, V(0.6f, 0.02f, 3f));
            // traffic lights at the corners, facing the rider
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = c + r * (s * (hw + 2f)) - f * (halfWidth + 3.5f);
                set[Pal.MetalDark].Box(p + Up(2.2f), rot, V(0.18f, 4.4f, 0.18f));
                set[Pal.Black].Box(p + Up(4.2f) - f * 0.1f, rot, V(0.45f, 1.2f, 0.35f));
                set[Pal.Red.Glossy(0.8f)].Box(p + Up(4.55f) - f * 0.3f, rot, V(0.26f, 0.26f, 0.05f));
                set[Pal.Yellow].Box(p + Up(4.2f) - f * 0.3f, rot, V(0.26f, 0.26f, 0.05f));
                set[Pal.GrassDark].Box(p + Up(3.85f) - f * 0.3f, rot, V(0.26f, 0.26f, 0.05f));
            }
        }

        static void Portals(RoadChunk chunk, MeshSet set)
        {
            // the hill the tunnel runs through: an ellipsoid whose cross-section at road level stays within the roof
            float mid = (chunk.RoofStart + chunk.RoofEnd) * 0.5f, roofLen = chunk.RoofEnd - chunk.RoofStart;
            chunk.SampleLocal(mid, out Vector3 hc, out float hy, out float hhw);
            set[Pal.Hill].Sphere(hc + Up(6.3f), Euler(0f, hy * SimMath.Rad2Deg, 0f), V(hhw * 2f + 90f, 44f, roofLen + 2f), 8, 16);

            for (int end = 0; end < 2; end++)
            {
                float a = end == 0 ? chunk.RoofStart : chunk.RoofEnd;
                chunk.SampleLocal(a, out Vector3 c, out float yaw, out float hw);
                Quaternion rot = Euler(0f, yaw * SimMath.Rad2Deg, 0f);
                Vector3 r = SimMath.Right3(yaw);
                set[Pal.Concrete].Box(c + Up(7f), rot, V(hw * 2f + 4f, 2f, 1.5f));
                set[Pal.Concrete].Box(c - r * (hw + 1.4f) + Up(3.5f), rot, V(1.6f, 7f, 1.5f));
                set[Pal.Concrete].Box(c + r * (hw + 1.4f) + Up(3.5f), rot, V(1.6f, 7f, 1.5f));
                // hazard stripes on the portal edges
                for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 4; k++)
                    set[k % 2 == 0 ? Pal.Yellow : Pal.Black].Box(c + r * (s * (hw + 0.62f)) + Up(0.4f + k * 0.5f), rot, V(0.06f, 0.5f, 1.52f));
            }
        }

        static void Ramp(RoadChunk chunk, RampFeature ramp, MeshSet set)
        {
            chunk.SampleLocal(ramp.Along - chunk.StartAlong, out Vector3 cs, out float yaw, out _);
            Vector3 origin = cs + SimMath.Right3(yaw) * ramp.Lateral + Up(0.01f);
            float grade = MathF.Atan(chunk.Shape.Grade) * SimMath.Rad2Deg;
            Quaternion rot = Euler(grade, yaw * SimMath.Rad2Deg, 0f);
            set[Pal.RampWood].Wedge(origin, rot, ramp.Width, ramp.Length, ramp.Height);
            // side rails in darker wood
            for (int s = -1; s <= 1; s += 2)
                set[Pal.RampWoodDark].Wedge(origin + Vector3.Transform(V(s * (ramp.Width * 0.5f + 0.06f), 0f, 0f), rot), rot, 0.12f, ramp.Length, ramp.Height + 0.05f);
            // yellow chevrons on the slope, pointing forward
            float slopeDeg = MathF.Atan2(ramp.Height, ramp.Length) * SimMath.Rad2Deg;
            Quaternion slope = rot * Euler(-slopeDeg, 0f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float z = ramp.Length * (0.2f + i * 0.27f);
                Vector3 p = origin + Vector3.Transform(V(0f, ramp.Height * z / ramp.Length + 0.03f, z), rot);
                float arm = ramp.Width * 0.3f;
                for (int s = -1; s <= 1; s += 2)
                    set[Pal.Yellow].Box(p + Vector3.Transform(V(s * arm * 0.45f, 0f, -arm * 0.3f), slope), slope * Euler(0f, -s * 35f, 0f), V(0.35f, 0.04f, arm * 1.05f));
            }
        }

        // ------------------------------------------------------------------ scenery props

        static void Props(RoadChunk chunk, MeshSet set, bool retaining)
        {
            var rng = new ArtRandom(chunk.Serial * 92821 + 17);
            float len = chunk.Length;
            bool bridge = chunk.Kind == ChunkKind.Bridge;
            bool tunnel = chunk.Kind == ChunkKind.Tunnel;

            // street lights, staggered left / right every 32 m (on the bridge: on the deck edge)
            for (float a = 8f; a < len - 4f; a += 16f)
            {
                int side = ((int)(a / 16f) % 2 == 0) ? -1 : 1;
                if (IsRoofedAt(chunk, a) || InCrossing(chunk, a) || InCrossing(chunk, a + 6f)) continue;
                EdgeKind e = EdgeAt(chunk, a, side);
                if (e == EdgeKind.Wall) continue;
                float offset = e == EdgeKind.Void ? 0.25f : CurbWidth + 0.5f;
                float y = e == EdgeKind.Void ? 0f : CurbHeight;
                Place(set, chunk, ArtLibrary.StreetLight(), a, side, offset, y, side < 0 ? 0f : 180f);
            }
            if (bridge || tunnel) return;

            // chevron signs on the outside of real bends
            float turn = chunk.Shape.Turn;
            if (MathF.Abs(turn) > 0.3f)
            {
                int outside = turn > 0f ? -1 : 1;
                float end = chunk.Shape.SCurve ? len * 0.5f : len * 0.88f;
                for (float a = len * 0.15f; a < end; a += 14f)
                    Place(set, chunk, ArtLibrary.ChevronSign(turn > 0f), a, outside, CurbWidth + 0.4f, CurbHeight, 180f);
                if (chunk.Shape.SCurve)
                    for (float a = len * 0.55f; a < len * 0.88f; a += 14f)
                        Place(set, chunk, ArtLibrary.ChevronSign(turn < 0f), a, -outside, CurbWidth + 0.4f, CurbHeight, 180f);
            }
            // a warning sign ahead of every chunk start
            Place(set, chunk, ArtLibrary.RoadSign(chunk.Serial), 4f, 1, CurbWidth + 1.2f, CurbHeight, 180f);

            // construction zone dressing on the sidewalks
            if (chunk.Kind == ChunkKind.Construction || chunk.Kind == ChunkKind.BrokenRoad)
            {
                for (float a = 10f; a < len - 10f; a += 9f + rng.Range(0f, 6f))
                {
                    int side = rng.Chance(0.5f) ? -1 : 1;
                    Place(set, chunk, rng.Chance(0.5f) ? ArtLibrary.Barrel() : ArtLibrary.ConeStack(), a, side, CurbWidth + rng.Range(0.6f, 2.4f), CurbHeight, rng.Range(0f, 360f));
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                // palms along the sidewalk edge / lawn
                for (float a = 6f + rng.Range(0f, 8f); a < len - 4f; a += (side > 0 ? 13f : 22f) + rng.Range(0f, 9f))
                {
                    if (InCrossing(chunk, a) || InCrossing(chunk, a + 8f) || InCrossing(chunk, a - 8f) || EdgeAt(chunk, a, side) != EdgeKind.Grass) continue;
                    float o = CurbWidth + SidewalkWidth + rng.Range(0.8f, 3.5f);
                    Place(set, chunk, ArtLibrary.Palm(rng.Range(0, 6)), a, side, o, TerrainHeightAt(side, o, retaining && side < 0) * TerrainScale(chunk, a), rng.Range(0f, 360f));
                }

                // houses, apartments, trees and bushes in the terrain band
                for (float a = 5f + rng.Range(0f, 6f); a < len - 5f;)
                {
                    bool apartment = rng.Chance(side < 0 ? 0.12f : 0.18f);
                    float width = apartment ? 14f : 10f;
                    float centre = a + width * 0.5f;
                    if (InCrossing(chunk, centre) || TerrainScale(chunk, centre) < 0.95f || EdgeAt(chunk, centre, side) == EdgeKind.Void)
                    {
                        a += width;
                        continue;
                    }
                    float o = FlatZone + (side < 0 ? 5.5f : 6.5f) + (apartment ? 2f : 0f) + rng.Range(0f, 3f);
                    if (side > 0 && rng.Chance(0.35f))
                    {
                        // gaps on the bay side keep the view open: a tree or bushes instead
                        Place(set, chunk, rng.Chance(0.5f) ? ArtLibrary.Tree(rng.Range(0, 4)) : ArtLibrary.Bush(rng.Range(0, 4)), centre, side, o - 2f, TerrainHeightAt(side, o - 2f, false), rng.Range(0f, 360f));
                    }
                    else
                    {
                        ArtModel b = apartment ? ArtLibrary.Apartment(rng.Range(0, 6)) : ArtLibrary.House(rng.Range(0, 14));
                        float lo = MathF.Min(TerrainHeightAt(side, o - 4f, retaining && side < 0), TerrainHeightAt(side, o + 4f, retaining && side < 0));
                        Place(set, chunk, b, centre, side, o, lo, side < 0 ? 90f : -90f);
                    }
                    // a bush between buildings
                    if (rng.Chance(0.5f))
                        Place(set, chunk, ArtLibrary.Bush(rng.Range(0, 4)), a + width + 1.5f, side, FlatZone - 1.2f, TerrainHeightAt(side, FlatZone - 1.2f, retaining && side < 0) * TerrainScale(chunk, a + width + 1.5f), 0f);
                    a += width + rng.Range(2f, 6f);
                }
            }
        }

        static bool IsRoofedAt(RoadChunk c, float a) => c.RoofEnd > c.RoofStart && a >= c.RoofStart - 4f && a <= c.RoofEnd + 4f;

        static EdgeKind EdgeAt(RoadChunk c, float a, int side)
        {
            int i = c.SampleIndexAt(a);
            return side < 0 ? c.LeftEdges[i] : c.RightEdges[i];
        }

        /// <summary>Terrain height (relative to the road) at an offset outside the edge, from the side profile.</summary>
        public static float TerrainHeightAt(int side, float offset, bool retaining)
        {
            float[] heights = side < 0 ? LeftHeights : RightHeights;
            if (offset <= ProfileOffsets[0]) return CurbHeight;
            for (int k = 1; k < ProfileOffsets.Length; k++)
            {
                if (offset > ProfileOffsets[k]) continue;
                float h0 = heights[k - 1], h1 = heights[k];
                if (retaining && k == 1) h1 = 0.1f;
                if (retaining && k == 2) { h0 = 0.1f; h1 = Math.Max(h1, 4.5f); if (offset > FlatZone) return h1 * 0.9f; }
                float t = (offset - ProfileOffsets[k - 1]) / (ProfileOffsets[k] - ProfileOffsets[k - 1]);
                return h0 + (h1 - h0) * t;
            }
            return heights[heights.Length - 1];
        }

        /// <summary>Places a model beside the road: local along, side (-1 left / +1 right), offset outside the edge.</summary>
        static void Place(MeshSet set, RoadChunk chunk, ArtModel model, float a, int side, float offset, float height, float yawOffsetDeg)
        {
            if (a < 0f || a > chunk.Length) return;
            chunk.SampleLocal(a, out Vector3 c, out float yaw, out float hw);
            Vector3 pos = c + SimMath.Right3(yaw) * (side * (hw + offset)) + Up(height);
            set.Add(model, pos, Euler(0f, yaw * SimMath.Rad2Deg + yawOffsetDeg, 0f));
        }
    }
}
