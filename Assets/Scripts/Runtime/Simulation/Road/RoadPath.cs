using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// Road centerline as a polyline with cumulative distance (engine-independent so the whole track can be
    /// driven in headless tests). Yaw convention as everywhere: 0 = +Z, positive = right.
    /// </summary>
    public sealed class RoadPath
    {
        readonly List<Vector3> _points = new List<Vector3>();
        readonly List<float> _distance = new List<float>();
        readonly List<float> _yaw = new List<float>();

        public float HalfWidth { get; }
        public float Length => _distance.Count == 0 ? 0f : _distance[_distance.Count - 1];
        public int Count => _points.Count;

        public RoadPath(float width)
        {
            HalfWidth = width * 0.5f;
        }

        public static Vector3 RightOf(float yaw) => new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));

        public static Vector3 ForwardOf(float yaw) => new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));

        public void Add(Vector3 point, float yaw)
        {
            float d = _points.Count == 0 ? 0f : _distance[_distance.Count - 1] + Vector3.Distance(_points[_points.Count - 1], point);
            _points.Add(point);
            _distance.Add(d);
            _yaw.Add(yaw);
        }

        public Vector3 PointAt(int index) => _points[index];
        public float YawAt(int index) => _yaw[index];
        public float DistanceAt(int index) => _distance[index];

        /// <summary>Position and heading at a distance along the road (clamped to the ends).</summary>
        public void Sample(float distance, out Vector3 position, out float yaw)
        {
            int n = _points.Count;
            if (n == 0) { position = Vector3.Zero; yaw = 0f; return; }
            if (distance <= 0f || n == 1) { position = _points[0]; yaw = _yaw[0]; return; }
            if (distance >= Length) { position = _points[n - 1]; yaw = _yaw[n - 1]; return; }

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_distance[mid] <= distance) lo = mid; else hi = mid;
            }
            float t = SimMath.InverseLerp(_distance[lo], _distance[hi], distance);
            position = Vector3.Lerp(_points[lo], _points[hi], t);
            yaw = _yaw[lo] + SimMath.WrapAngle(_yaw[hi] - _yaw[lo]) * t;
        }

        /// <summary>
        /// Projects a world point onto the centerline. Returns distance along the road; lateral is signed
        /// (+ = right of centre). <paramref name="hint"/> is a segment index cache that keeps the search local.
        /// </summary>
        public float Project(Vector3 world, ref int hint, out float lateral, out float centerHeight)
        {
            int n = _points.Count;
            lateral = 0f;
            centerHeight = n > 0 ? _points[0].Y : 0f;
            if (n < 2) return 0f;

            int from = Math.Max(0, Math.Min(hint - 30, n - 2));
            int to = Math.Max(0, Math.Min(hint + 60, n - 2));
            float best = ProjectRange(world, from, to, out int seg, out float t);
            if (best > HalfWidth * HalfWidth * 16f) // lost (teleport, far off-road): full search
                ProjectRange(world, 0, n - 2, out seg, out t);

            hint = seg;
            Vector3 closest = Vector3.Lerp(_points[seg], _points[seg + 1], t);
            float yaw = _yaw[seg] + SimMath.WrapAngle(_yaw[seg + 1] - _yaw[seg]) * t;
            Vector3 right = RightOf(yaw);
            lateral = (world.X - closest.X) * right.X + (world.Z - closest.Z) * right.Z;
            centerHeight = closest.Y;
            return _distance[seg] + (_distance[seg + 1] - _distance[seg]) * t;
        }

        public float Project(Vector3 world, ref int hint, out float lateral) => Project(world, ref hint, out lateral, out _);

        float ProjectRange(Vector3 world, int from, int to, out int bestSeg, out float bestT)
        {
            float best = float.PositiveInfinity;
            bestSeg = from;
            bestT = 0f;
            var p = new Vector2(world.X, world.Z);
            for (int i = from; i <= to; i++)
            {
                var a = new Vector2(_points[i].X, _points[i].Z);
                var b = new Vector2(_points[i + 1].X, _points[i + 1].Z);
                Vector2 ab = b - a;
                float len2 = ab.LengthSquared();
                float t = len2 > 1e-6f ? SimMath.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                float d2 = (a + ab * t - p).LengthSquared();
                if (d2 < best) { best = d2; bestSeg = i; bestT = t; }
            }
            return best;
        }

        /// <summary>
        /// Pure-pursuit steering request (-1..1, steering units) for a board at this pose: what the road "asks for".
        /// Only cooperative-minded bots use it; the board itself never auto-steers.
        /// </summary>
        public float SteerHint(Vector3 position, float yaw, float speed, float maxYawRate, ref int hint)
        {
            float along = Project(position, ref hint, out _);
            float lookAhead = 10f + 0.6f * speed;
            Sample(along + lookAhead, out Vector3 target, out _);
            float alpha = SimMath.WrapAngle(MathF.Atan2(target.X - position.X, target.Z - position.Z) - yaw);
            float desiredYawRate = 2f * Math.Max(speed, 1f) * MathF.Sin(alpha) / lookAhead;
            return SimMath.Clamp(desiredYawRate / Math.Max(maxYawRate, 1e-3f), -1f, 1f);
        }
    }
}
