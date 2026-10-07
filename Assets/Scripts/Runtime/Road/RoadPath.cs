using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Road centerline as a polyline with cumulative distance. Used for respawn, distance and the bots' steer hint.</summary>
    public sealed class RoadPath
    {
        readonly List<Vector3> _points = new List<Vector3>();
        readonly List<float> _distance = new List<float>();
        readonly List<float> _yaw = new List<float>();

        public float HalfWidth { get; }
        public float StartDistance => _distance.Count == 0 ? 0f : _distance[0];
        public float Length => _distance.Count == 0 ? 0f : _distance[_distance.Count - 1];
        public int Count => _points.Count;

        public RoadPath(float width)
        {
            HalfWidth = width * 0.5f;
        }

        public void Add(Vector3 point, float yawRad)
        {
            float d = _points.Count == 0 ? 0f : _distance[_distance.Count - 1] + Vector3.Distance(_points[_points.Count - 1], point);
            _points.Add(point);
            _distance.Add(d);
            _yaw.Add(yawRad);
        }

        public void Clear()
        {
            _points.Clear(); _distance.Clear(); _yaw.Clear();
        }

        /// <summary>Keep absolute distances while discarding old samples; retain a boundary point.</summary>
        public void DiscardBefore(float distance)
        {
            int count = 0;
            while (count + 1 < _distance.Count && _distance[count + 1] <= distance) count++;
            if (count == 0) return;
            _points.RemoveRange(0, count); _distance.RemoveRange(0, count); _yaw.RemoveRange(0, count);
        }

        public Vector3 PointAt(int index) => _points[index];
        public float YawAt(int index) => _yaw[index];
        public float DistanceAt(int index) => _distance[index];

        /// <summary>Position and heading at a distance along the road (clamped to the ends).</summary>
        public void Sample(float distance, out Vector3 position, out float yawRad)
        {
            int n = _points.Count;
            if (n == 0) { position = Vector3.zero; yawRad = 0f; return; }
            if (distance <= StartDistance || n == 1) { position = _points[0]; yawRad = _yaw[0]; return; }
            if (distance >= Length) { position = _points[n - 1]; yawRad = _yaw[n - 1]; return; }

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_distance[mid] <= distance) lo = mid; else hi = mid;
            }
            float t = Mathf.InverseLerp(_distance[lo], _distance[hi], distance);
            position = Vector3.Lerp(_points[lo], _points[hi], t);
            yawRad = Mathf.LerpAngle(_yaw[lo] * Mathf.Rad2Deg, _yaw[hi] * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Projects a world point onto the centerline. Returns distance along the road; lateral is signed (+ = right of centre).
        /// <paramref name="hint"/> is a segment index cache that keeps the search local.
        /// </summary>
        public float Project(Vector3 world, ref int hint, out float lateral)
        {
            int n = _points.Count;
            lateral = 0f;
            if (n < 2) return 0f;

            int from = Mathf.Clamp(hint - 30, 0, n - 2);
            int to = Mathf.Clamp(hint + 60, 0, n - 2);
            float best = ProjectRange(world, from, to, out int bestSeg, out float bestT);
            if (best > HalfWidth * HalfWidth * 16f) // lost: full search
                best = ProjectRange(world, 0, n - 2, out bestSeg, out bestT);

            hint = bestSeg;
            Vector3 a = _points[bestSeg], b = _points[bestSeg + 1];
            Vector3 closest = Vector3.Lerp(a, b, bestT);
            Vector3 right = SimConvert.YawRight(Mathf.LerpAngle(_yaw[bestSeg] * Mathf.Rad2Deg, _yaw[bestSeg + 1] * Mathf.Rad2Deg, bestT) * Mathf.Deg2Rad);
            Vector3 delta = world - closest;
            lateral = delta.x * right.x + delta.z * right.z;
            return Mathf.Lerp(_distance[bestSeg], _distance[bestSeg + 1], bestT);
        }

        float ProjectRange(Vector3 world, int from, int to, out int bestSeg, out float bestT)
        {
            float best = float.PositiveInfinity;
            bestSeg = from;
            bestT = 0f;
            var p = new Vector2(world.x, world.z);
            for (int i = from; i <= to; i++)
            {
                var a = new Vector2(_points[i].x, _points[i].z);
                var b = new Vector2(_points[i + 1].x, _points[i + 1].z);
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                float d2 = (a + ab * t - p).sqrMagnitude;
                if (d2 < best) { best = d2; bestSeg = i; bestT = t; }
            }
            return best;
        }

        /// <summary>
        /// Pure-pursuit steering request in steering units (-1..1) for a board at this pose: what the road "asks for".
        /// Only cooperative bots use it; the board itself never auto-steers.
        /// </summary>
        public float SteerHint(Vector3 position, float yawRad, float speed, float maxYawRateRad, ref int hint)
        {
            float along = Project(position, ref hint, out _);
            float lookAhead = 10f + 0.6f * speed;
            Sample(along + lookAhead, out Vector3 target, out _);
            Vector3 d = target - position;
            float alpha = Mathf.DeltaAngle(yawRad * Mathf.Rad2Deg, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float desiredYawRate = 2f * Mathf.Max(speed, 1f) * Mathf.Sin(alpha) / lookAhead;
            return Mathf.Clamp(desiredYawRate / Mathf.Max(maxYawRateRad, 1e-3f), -1f, 1f);
        }
    }
}
