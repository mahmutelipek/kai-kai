using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Visual for one RoadChunk: asphalt, grass shoulders, markings, walls (tunnel / narrow), bridge deck and
    /// railings, tunnel roof, crossing road at intersections, ramps. Pooled; Rebuild() reuses its meshes.
    /// </summary>
    public sealed class ChunkView : MonoBehaviour
    {
        enum Part { Asphalt, Grass, LineWhite, LineYellow, Concrete, Steel, Roof, Wood, Count }

        static readonly Color[] PartColors =
        {
            MaterialLibrary.Road, MaterialLibrary.Grass, MaterialLibrary.LineWhite, MaterialLibrary.LineYellow,
            MaterialLibrary.Concrete, MaterialLibrary.BridgeSteel, MaterialLibrary.TunnelInside, MaterialLibrary.RampWood,
        };

        readonly MeshWriter[] _writers = new MeshWriter[(int)Part.Count];
        readonly Mesh[] _meshes = new Mesh[(int)Part.Count];

        public int Serial { get; private set; } = -1;

        public static ChunkView Create(Transform parent)
        {
            var go = new GameObject("Chunk");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ChunkView>();
            for (int i = 0; i < (int)Part.Count; i++)
            {
                view._writers[i] = new MeshWriter();
                view._meshes[i] = new Mesh { name = ((Part)i).ToString() };
                var child = new GameObject(((Part)i).ToString());
                child.transform.SetParent(go.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = view._meshes[i];
                child.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get(PartColors[i]);
            }
            return view;
        }

        MeshWriter W(Part p) => _writers[(int)p];

        public void Rebuild(RoadChunk chunk)
        {
            Serial = chunk.Serial;
            gameObject.name = $"Chunk {chunk.Serial} {chunk.Kind}";
            for (int i = 0; i < _writers.Length; i++) _writers[i].Clear();

            int n = chunk.SampleCount;
            for (int i = 1; i < n; i++)
            {
                Vector3 c0 = chunk.Points[i - 1].ToUnity(), c1 = chunk.Points[i].ToUnity();
                Vector3 r0 = SimConvert.YawRight(chunk.Yaws[i - 1]), r1 = SimConvert.YawRight(chunk.Yaws[i]);
                float h0 = chunk.HalfWidths[i - 1], h1 = chunk.HalfWidths[i];
                float along0 = chunk.StartAlong + chunk.LocalAlongAt(i - 1);

                // asphalt
                W(Part.Asphalt).Quad(c0 - r0 * h0, c1 - r1 * h1, c1 + r1 * h1, c0 + r0 * h0);

                // edges, per side
                for (int side = -1; side <= 1; side += 2)
                {
                    EdgeKind e0 = side < 0 ? chunk.LeftEdges[i - 1] : chunk.RightEdges[i - 1];
                    EdgeKind e1 = side < 0 ? chunk.LeftEdges[i] : chunk.RightEdges[i];
                    EdgeKind edge = e0 == e1 ? e0 : EdgeKind.Grass;
                    Vector3 in0 = c0 + r0 * (side * h0), in1 = c1 + r1 * (side * h1);
                    switch (edge)
                    {
                        case EdgeKind.Grass:
                        {
                            float w = RoadModel.ShoulderWidth;
                            Vector3 out0 = in0 + r0 * (side * w) + Vector3.down * 0.02f, out1 = in1 + r1 * (side * w) + Vector3.down * 0.02f;
                            if (side > 0) W(Part.Grass).Quad(in0 + Vector3.down * 0.02f, in1 + Vector3.down * 0.02f, out1, out0);
                            else W(Part.Grass).Quad(out0, out1, in1 + Vector3.down * 0.02f, in0 + Vector3.down * 0.02f);
                            break;
                        }
                        case EdgeKind.Wall:
                            Segment(W(Part.Concrete), in0 + r0 * (side * 0.45f), in1 + r1 * (side * 0.45f), 0.3f, 1.2f);
                            if (IsRoofed(chunk, i)) Segment(W(Part.Concrete), in0 + r0 * (side * 0.45f) + Vector3.up * 1.2f, in1 + r1 * (side * 0.45f) + Vector3.up * 1.2f, 0.3f, 4.8f);
                            break;
                        case EdgeKind.Void:
                            // bridge: deck edge beam + railing
                            Segment(W(Part.Concrete), in0 + r0 * (side * 0.2f) + Vector3.down * 0.9f, in1 + r1 * (side * 0.2f) + Vector3.down * 0.9f, 0.4f, 0.9f);
                            Segment(W(Part.Steel), in0 + r0 * (side * 0.15f) + Vector3.up * 0.95f, in1 + r1 * (side * 0.15f) + Vector3.up * 0.95f, 0.12f, 0.12f);
                            if (i % 3 == 0) W(Part.Steel).Box(in1 + r1 * (side * 0.15f) + Vector3.up * 0.5f, Quaternion.Euler(0f, chunk.Yaws[i] * Mathf.Rad2Deg, 0f), new Vector3(0.1f, 1f, 0.1f));
                            break;
                    }
                }

                // bridge underside: deck slab + pillars
                if (chunk.LeftEdges[i] == EdgeKind.Void && chunk.LeftEdges[i - 1] == EdgeKind.Void)
                {
                    W(Part.Concrete).Quad(c0 + r0 * h0 + Vector3.down * 0.9f, c1 + r1 * h1 + Vector3.down * 0.9f, c1 - r1 * h1 + Vector3.down * 0.9f, c0 - r0 * h0 + Vector3.down * 0.9f);
                    if (i % 15 == 0)
                    {
                        Quaternion rot = Quaternion.Euler(0f, chunk.Yaws[i] * Mathf.Rad2Deg, 0f);
                        W(Part.Steel).Box(c1 + Vector3.down * 20.9f, rot, new Vector3(3f, 40f, 2f));
                    }
                }

                // tunnel roof
                if (IsRoofed(chunk, i))
                {
                    Vector3 up = Vector3.up * 6f;
                    float rw0 = h0 + 0.6f, rw1 = h1 + 0.6f;
                    W(Part.Roof).Quad(c0 + r0 * rw0 + up, c1 + r1 * rw1 + up, c1 - r1 * rw1 + up, c0 - r0 * rw0 + up); // underside (seen from inside)
                    W(Part.Concrete).Quad(c0 - r0 * rw0 + up + Vector3.up * 0.3f, c1 - r1 * rw1 + up + Vector3.up * 0.3f, c1 + r1 * rw1 + up + Vector3.up * 0.3f, c0 + r0 * rw0 + up + Vector3.up * 0.3f);
                }

                // markings: white edge lines, dashed yellow centre line
                Line(W(Part.LineWhite), c0, c1, r0, r1, -h0 + 0.35f, -h1 + 0.35f, 0.15f);
                Line(W(Part.LineWhite), c0, c1, r0, r1, h0 - 0.35f, h1 - 0.35f, 0.15f);
                if (Mathf.FloorToInt(along0 / 4f) % 2 == 0 && chunk.Kind != ChunkKind.Fork)
                {
                    Line(W(Part.LineYellow), c0, c1, r0, r1, -0.12f, -0.12f, 0.1f);
                    Line(W(Part.LineYellow), c0, c1, r0, r1, 0.12f, 0.12f, 0.1f);
                }
            }

            if (chunk.Kind == ChunkKind.Intersection) BuildCrossRoad(chunk);
            if (chunk.Kind == ChunkKind.Tunnel) BuildPortals(chunk);
            for (int k = 0; k < chunk.Ramps.Count; k++) BuildRamp(chunk, chunk.Ramps[k]);

            for (int i = 0; i < _writers.Length; i++) _writers[i].Upload(_meshes[i]);
        }

        static bool IsRoofed(RoadChunk c, int i)
        {
            float a = c.LocalAlongAt(i);
            return c.RoofEnd > c.RoofStart && a >= c.RoofStart && a <= c.RoofEnd;
        }

        /// <summary>A box running from p0 to p1 (bottom centre points), with the given thickness and height.</summary>
        static void Segment(MeshWriter w, Vector3 p0, Vector3 p1, float thickness, float height)
        {
            Vector3 d = p1 - p0;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            Quaternion rot = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z).sqrMagnitude > 1e-6f ? d : Vector3.forward, Vector3.up);
            w.Box((p0 + p1) * 0.5f + Vector3.up * (height * 0.5f), rot, new Vector3(thickness, height, len + 0.02f));
        }

        static void Line(MeshWriter w, Vector3 c0, Vector3 c1, Vector3 r0, Vector3 r1, float off0, float off1, float width)
        {
            Vector3 up = Vector3.up * 0.02f;
            w.Quad(c0 + r0 * (off0 - width * 0.5f) + up, c1 + r1 * (off1 - width * 0.5f) + up,
                   c1 + r1 * (off1 + width * 0.5f) + up, c0 + r0 * (off0 + width * 0.5f) + up);
        }

        void BuildCrossRoad(RoadChunk chunk)
        {
            chunk.SampleLocal(chunk.Length * 0.5f, out System.Numerics.Vector3 cs, out float yaw, out float hw);
            Vector3 c = cs.ToUnity() + Vector3.down * 0.01f;
            Vector3 f = SimConvert.YawForward(yaw), r = SimConvert.YawRight(yaw);
            const float halfLength = 45f, halfWidth = 5f;
            W(Part.Asphalt).Quad(c - r * halfLength - f * halfWidth, c - r * halfLength + f * halfWidth,
                                 c + r * halfLength + f * halfWidth, c + r * halfLength - f * halfWidth);
            // stop lines on the crossing road, zebra across the main road
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 stop = c + r * (s * (hw + 1.5f)) + Vector3.up * 0.03f;
                W(Part.LineWhite).Box(stop, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f), new Vector3(0.4f, 0.02f, halfWidth * 2f));
            }
            for (float x = -hw + 0.6f; x < hw - 0.4f; x += 1.2f)
                W(Part.LineWhite).Box(c + r * x - f * (halfWidth + 2f) + Vector3.up * 0.03f, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f), new Vector3(0.6f, 0.02f, 3f));
        }

        void BuildPortals(RoadChunk chunk)
        {
            for (int end = 0; end < 2; end++)
            {
                float a = end == 0 ? chunk.RoofStart : chunk.RoofEnd;
                chunk.SampleLocal(a, out System.Numerics.Vector3 cs, out float yaw, out float hw);
                Quaternion rot = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
                Vector3 c = cs.ToUnity(), r = SimConvert.YawRight(yaw);
                W(Part.Concrete).Box(c + Vector3.up * 7f, rot, new Vector3(hw * 2f + 4f, 2f, 1.5f));        // lintel
                W(Part.Concrete).Box(c - r * (hw + 1.4f) + Vector3.up * 3.5f, rot, new Vector3(1.6f, 7f, 1.5f));
                W(Part.Concrete).Box(c + r * (hw + 1.4f) + Vector3.up * 3.5f, rot, new Vector3(1.6f, 7f, 1.5f));
            }
        }

        void BuildRamp(RoadChunk chunk, RampFeature ramp)
        {
            chunk.SampleLocal(ramp.Along - chunk.StartAlong, out System.Numerics.Vector3 cs, out float yaw, out _);
            Vector3 origin = cs.ToUnity() + SimConvert.YawRight(yaw) * ramp.Lateral + Vector3.up * 0.01f;
            Quaternion rot = Quaternion.Euler(Mathf.Atan(chunk.Shape.Grade) * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0f);
            W(Part.Wood).Wedge(origin, rot, ramp.Width, ramp.Length, ramp.Height);
            // yellow chevrons on the slope
            Quaternion slope = rot * Quaternion.Euler(-Mathf.Atan2(ramp.Height, ramp.Length) * Mathf.Rad2Deg, 0f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float z = ramp.Length * (0.22f + i * 0.26f);
                Vector3 p = origin + rot * Vector3.forward * z + Vector3.up * (ramp.Height * z / ramp.Length + 0.03f);
                W(Part.LineYellow).Box(p, slope, new Vector3(ramp.Width * 0.65f, 0.04f, 0.5f));
            }
        }
    }
}
