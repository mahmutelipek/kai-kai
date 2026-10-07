using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Bounded road streaming: generate ahead, retire behind, keep absolute run distances.</summary>
    public sealed class EndlessRoad : MonoBehaviour
    {
        public const float ChunkLength = 80f;
        public const float AheadDistance = 400f;
        public const float BehindDistance = 160f;
        const int Seed = 271828;
        readonly Queue<RoadChunk> _chunks = new Queue<RoadChunk>();
        RoadPath _path;
        BoardController _board;
        System.Random _random;
        Vector3 _cursor;
        float _yaw, _horizontalDistance;
        int _chunkIndex, _hint;
        RoadChunk _startApron;
        struct RouteRow { public float Distance; public int Lane; }
        readonly List<RouteRow> _route = new List<RouteRow>();
        int _lastLane = 1;
        public float RouteSteerHint(Vector3 position, float yaw, float speed, float maxYawRate, ref int hint)
        {
            float along = _path.Project(position, ref hint, out _);
            float look = 16f + .65f * speed;
            int lane = 1;
            foreach (var row in _route)
                if (row.Distance >= along - 4f) { lane = row.Lane; break; }
            _path.Sample(along + look, out Vector3 target, out float targetYaw);
            target += SimConvert.YawRight(targetYaw) * (lane - 1) * 3.5f;
            Vector3 delta = target - position;
            float alpha = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return Mathf.Clamp(2f * Mathf.Max(speed, 1f) * Mathf.Sin(alpha) / look / Mathf.Max(maxYawRate, .001f), -1f, 1f);
        }
        public int ActiveChunkCount => _chunks.Count;
        public RoadPath Path => _path;

        public void Initialize(RoadPath path)
        {
            _path = path;
            ResetRoad();
        }

        public void Attach(BoardController board) => _board = board;

        public void ResetRoad()
        {
            if (_startApron != null) { Retire(_startApron); _startApron = null; }
            foreach (RoadChunk chunk in _chunks) Retire(chunk);
            _chunks.Clear(); _route.Clear(); _lastLane = 1; _path.Clear(); _random = new System.Random(Seed);
            _cursor = Vector3.zero; _yaw = 0f; _horizontalDistance = 0; _hint = 0; _chunkIndex = 0;
            _path.Add(_cursor, _yaw);
            // Extend behind the start so the chase camera never looks into empty space.
            var apronPath = new RoadPath(12f);
            for (int i = 0; i <= 40; i++)
            {
                float z = -80f + i * 2f;
                apronPath.Add(new Vector3(0, -TestRoad.Grade * z, z), 0f);
            }
            var apron = new GameObject("Start Apron"); apron.transform.SetParent(transform, false);
            _startApron = apron.AddComponent<RoadChunk>();
            _startApron.Initialize(apronPath, 0, 40, -80f, 0f);
            Maintain(0f);
        }

        void Update()
        {
            if (_board == null || _board.Simulation == null) return;
            float distance = _path.Project(_board.State.Position.ToUnity(), ref _hint, out _);
            Maintain(distance);
        }

        public void Maintain(float distance)
        {
            if (_startApron != null && distance > BehindDistance) { Retire(_startApron); _startApron = null; }
            while (_path.Length < distance + AheadDistance) GenerateChunk();
            bool removed = false;
            while (_chunks.Count > 1 && _chunks.Peek().EndDistance < distance - BehindDistance)
            {
                Retire(_chunks.Dequeue()); removed = true;
            }
            if (removed)
            {
                _path.DiscardBefore(_chunks.Peek().StartDistance);
                _hint = 0;
                _route.RemoveAll(row => row.Distance < _path.StartDistance);
            }
        }

        static void Retire(RoadChunk chunk)
        {
            chunk.gameObject.SetActive(false); // Remove old colliders immediately, before deferred destruction.
            if (Application.isPlaying) Destroy(chunk.gameObject);
            else DestroyImmediate(chunk.gameObject);
        }

        void GenerateChunk()
        {
            float start = _path.Length;
            int firstPoint = _path.Count - 1;
            // Alternating gentle bends and straights; keep forward progress to avoid road intersections.
            float turn = _chunkIndex < 1 || _chunkIndex % 4 == 0 ? 0f
                : (_chunkIndex % 2 == 0 ? -1f : 1f) * (24f + (float)_random.NextDouble() * 12f);
            float nextYaw = Mathf.Clamp(_yaw + turn * Mathf.Deg2Rad, -55f * Mathf.Deg2Rad, 55f * Mathf.Deg2Rad);
            float stepTurn = (nextYaw - _yaw) / 40f;
            for (int i = 0; i < 40; i++)
            {
                _cursor += SimConvert.YawForward(_yaw + stepTurn * 0.5f) * 2f;
                _cursor.y -= GradeAt(_horizontalDistance + 1f) * 2f;
                _horizontalDistance += 2f;
                _yaw += stepTurn; _path.Add(_cursor, _yaw);
            }
            var go = new GameObject("Road Chunk " + _chunkIndex); go.transform.SetParent(transform, false);
            var chunk = go.AddComponent<RoadChunk>();
            chunk.Initialize(_path, firstPoint, _path.Count - 1, start, _path.Length);
            _chunks.Enqueue(chunk);
            for (float d = start + 24f; d < chunk.EndDistance - 10f; d += Mathf.Lerp(38f, 27f, Mathf.Clamp01(start / 2000f)))
            {
                int safeLane = _chunkIndex == 0 ? 1 : Mathf.Clamp(_lastLane + _random.Next(-1, 2), 0, 2);
                _lastLane = safeLane;
                _route.Add(new RouteRow { Distance = d, Lane = safeLane });
                if (d > 70f)
                    for (int lane = 0; lane < 3; lane++)
                        if (lane != safeLane)
                        {
                            Sample(d, (lane - 1) * 3.5f, out Vector3 p, out float yaw);
                            RoadHazard.Create(chunk.transform, p, yaw, _random.Next(3) == 0);
                        }
                bool rampRow = _chunkIndex % 3 == 1 && d < start + 30f;
                if (rampRow) ArcadeRamp.Create(chunk.transform,_path,d-8f,(safeLane-1)*3.5f);
                for (int c = 0; c < 5; c++)
                {
                    Sample(d-14f+c*2.4f,(safeLane-1)*3.5f,out Vector3 coin,out _);
                    ArcadePickup.Create(chunk.transform,coin+Vector3.up*.95f,ArcadePickupKind.Coin);
                }
                if (_chunkIndex % 2 == 0 && d < start+30f)
                {
                    Sample(d+16f,(safeLane-1)*3.5f,out Vector3 nitro,out _);
                    ArcadePickup.Create(chunk.transform,nitro+Vector3.up*1.1f,ArcadePickupKind.Nitro);
                }
                for (int i = 0; i < 3; i++)
                {
                    Sample(d + 5f + i * 2.6f, (safeLane - 1) * 3.5f, out Vector3 p, out _);
                    DiamondPickup.Create(chunk.transform, p + Vector3.up * (rampRow ? 2.4f : .95f));
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Sample(start + 36f, side * 10f, out Vector3 p, out float yaw);
                StylizedPalm.Create(chunk.transform, p, yaw * Mathf.Rad2Deg + _chunkIndex * 37f, .9f + (_chunkIndex % 3) * .08f);
                Sample(start + 66f, side * 16f, out p, out yaw);
                StylizedPalm.Create(chunk.transform, p, yaw * Mathf.Rad2Deg + side * 45f, .72f);
            }
            RoadAtmosphere.Dress(_path, chunk.transform, start, chunk.EndDistance, _chunkIndex);
            _chunkIndex++;
        }

        public static float GradeAt(float horizontalDistance)
        {
            float rolling = .14f + .07f * Mathf.Sin(horizontalDistance * .017f) + .025f * Mathf.Sin(horizontalDistance * .006f + 1.2f);
            return Mathf.Lerp(TestRoad.Grade, rolling, Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,100,horizontalDistance)));
        }

        void Sample(float distance, float offset, out Vector3 position, out float yaw)
        {
            _path.Sample(distance, out position, out yaw);
            position += SimConvert.YawRight(yaw) * offset;
        }
    }

    public sealed class RoadChunk : MonoBehaviour
    {
        readonly List<Mesh> _meshes = new List<Mesh>();
        public float StartDistance { get; private set; }
        public float EndDistance { get; private set; }

        public void Initialize(RoadPath path, int first, int last, float start, float end)
        {
            StartDistance = start; EndDistance = end;
            Strip(path, first, last, "Road", -6, 6, 0, new Color(0.18f, 0.2f, 0.23f), true, false);
            Strip(path, first, last, "Shoulder Left", -24, -6, -0.02f, new Color(.20f,.43f,.075f), true, false);
            Strip(path, first, last, "Shoulder Right", 6, 24, -0.02f, new Color(.20f,.43f,.075f), true, false);
            Strip(path, first, last, "Center Dashes", -0.1f, 0.1f, 0.015f, MaterialLibrary.LineYellow, false, true);
            Strip(path, first, last, "Left Edge", -5.75f, -5.57f, 0.015f, MaterialLibrary.LineWhite, false, false);
            Strip(path, first, last, "Right Edge", 5.57f, 5.75f, 0.015f, MaterialLibrary.LineWhite, false, false);
        }

        void Strip(RoadPath path, int first, int last, string name, float left, float rightOffset, float height, Color color, bool ground, bool dashed)
        {
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            for (int i = first + 1; i <= last; i++)
            {
                if (dashed && Mathf.FloorToInt(path.DistanceAt(i) / 4f) % 2 == 1) continue;
                Vector3 a = path.PointAt(i - 1) + Vector3.up * height, b = path.PointAt(i) + Vector3.up * height;
                Vector3 ra = SimConvert.YawRight(path.YawAt(i - 1)), rb = SimConvert.YawRight(path.YawAt(i));
                int v = vertices.Count;
                vertices.Add(a + ra * left); vertices.Add(a + ra * rightOffset);
                vertices.Add(b + rb * left); vertices.Add(b + rb * rightOffset);
                uvs.Add(new Vector2(left / 4f, path.DistanceAt(i - 1) / 4f));
                uvs.Add(new Vector2(rightOffset / 4f, path.DistanceAt(i - 1) / 4f));
                uvs.Add(new Vector2(left / 4f, path.DistanceAt(i) / 4f));
                uvs.Add(new Vector2(rightOffset / 4f, path.DistanceAt(i) / 4f));
                triangles.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _meshes.Add(mesh);
            var go = PrimitiveFactory.MeshObject(name, mesh, color, transform);
            if (name == "Road")
            {
                Material asphalt = Resources.Load<Material>("Art/Feedback/Asphalt");
                if (asphalt != null) go.GetComponent<Renderer>().sharedMaterial = asphalt;
            }
            if (ground)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<GroundSurface>().kind = name == "Road" ? Game.Simulation.SurfaceKind.Road : Game.Simulation.SurfaceKind.Offroad;
            }
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in _meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
