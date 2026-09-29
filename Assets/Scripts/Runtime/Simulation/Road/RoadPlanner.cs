using System;

namespace Game.Simulation
{
    /// <summary>
    /// Traversability proof for one chunk. Per 2 m sample it computes where the board centre may be
    /// (asphalt minus blocking obstacles and traffic lanes, inflated by the board size), propagates what is
    /// reachable with a limited lateral change per metre, and extracts one smooth reachable line.
    /// No allocation: fixed scratch buffers.
    /// </summary>
    public sealed class RoadPlanner
    {
        public const int MaxSamples = 256;
        public const int MaxIntervals = 8;
        /// <summary>Allowed lateral change of the board centre per metre travelled (~3.4 deg path angle).</summary>
        public const float LateralSlope = 0.06f;
        public const float Margin = 0.75f;

        readonly float[] _free = new float[MaxSamples * MaxIntervals * 2];
        readonly int[] _freeCount = new int[MaxSamples];
        readonly float[] _reach = new float[MaxSamples * MaxIntervals * 2];
        readonly int[] _reachCount = new int[MaxSamples];
        readonly float[] _tmp = new float[MaxIntervals * 2 * 2];

        /// <summary>Along (global) of the first unreachable sample from the last failed Plan call.</summary>
        public float FailedAlong { get; private set; }

        /// <summary>Fills chunk.PlannedLateral. Returns false if some sample cannot be reached.</summary>
        public bool Plan(RoadChunk chunk, ObstacleField obstacles, BoardTuningData t, float entryLateral)
        {
            int n = chunk.SampleCount;
            if (n > MaxSamples) throw new InvalidOperationException("chunk too long for the planner");
            float boardHalfW = t.HalfWidth + Margin;
            float boardHalfL = t.HalfLength + 1f;

            // 1) free centre intervals per sample
            for (int i = 0; i < n; i++)
            {
                float along = chunk.StartAlong + chunk.LocalAlongAt(i);
                float limit = chunk.HalfWidths[i] - boardHalfW;
                int count = 0;
                if (limit > 0f) { Set(_free, i, 0, -limit, limit); count = 1; }
                for (int k = 0; k < chunk.ObstacleSlots.Count && count > 0; k++)
                {
                    if (!chunk.Owns(obstacles, k)) continue;
                    ref Obstacle o = ref obstacles.Items[chunk.ObstacleSlots[k]];
                    if (!ObstacleCatalog.BlocksCorridor(o.Kind) || o.Motion == ObstacleMotion.Cross) continue;
                    float cos = MathF.Abs(MathF.Cos(o.YawOffset)), sin = MathF.Abs(MathF.Sin(o.YawOffset));
                    float extLat = o.HalfExtents.X * cos + o.HalfExtents.Y * sin;
                    float extAlong = o.HalfExtents.X * sin + o.HalfExtents.Y * cos;
                    bool covers = o.Motion == ObstacleMotion.Static
                        ? Math.Abs(along - o.Along) <= extAlong + boardHalfL
                        : along >= o.MotionMin - boardHalfL && along <= o.MotionMax + boardHalfL; // traffic lane
                    if (!covers) continue;
                    count = Subtract(_free, i, count, o.Lateral - extLat - boardHalfW, o.Lateral + extLat + boardHalfW);
                }
                _freeCount[i] = count;
            }

            // 2) forward reachability
            float step = RoadChunk.SampleSpacing * LateralSlope;
            float e = SimMath.Clamp(entryLateral, -1000f, 1000f);
            _reachCount[0] = IntersectPoint(_free, _freeCount[0], 0, e, _reach, 0);
            if (_reachCount[0] == 0)
            {
                // entry outside the free space (e.g. width change): accept the nearest free interval
                _reachCount[0] = CopyNearest(_free, _freeCount[0], 0, e, _reach, 0);
            }
            for (int i = 1; i < n; i++)
            {
                _reachCount[i] = ExpandIntersect(_reach, i - 1, _reachCount[i - 1], step, _free, i, _freeCount[i], _reach, i);
                if (_reachCount[i] == 0)
                {
                    FailedAlong = chunk.StartAlong + chunk.LocalAlongAt(i);
                    return false;
                }
            }

            // 3) backward pass: keep only states from which the end is still reachable
            for (int i = n - 2; i >= 0; i--)
            {
                int c = ExpandIntersect(_reach, i + 1, _reachCount[i + 1], step, _reach, i, _reachCount[i], _free, i);
                // reuse _free as scratch for the refined set, then copy back
                for (int k = 0; k < c * 2; k++) _reach[(i * MaxIntervals) * 2 + k] = _free[(i * MaxIntervals) * 2 + k];
                _reachCount[i] = c;
                if (c == 0) { FailedAlong = chunk.StartAlong + chunk.LocalAlongAt(i); return false; }
            }

            // 4) extract a line: stay put, drift gently toward the centre (firmly near the chunk end so the
            //    next chunk starts near the middle), never exceed the lateral slope
            chunk.PlannedLateral.Clear();
            float x = Nearest(_reach, 0, _reachCount[0], e);
            chunk.PlannedLateral.Add(x);
            for (int i = 1; i < n; i++)
            {
                float preferred = chunk.LocalAlongAt(i) > chunk.Length - 40f ? x * 0.9f : x * 0.98f;
                float lo = x - step, hi = x + step;
                x = NearestWithin(_reach, i, _reachCount[i], preferred, lo, hi);
                chunk.PlannedLateral.Add(x);
            }
            return true;
        }

        static void Set(float[] buf, int sample, int k, float lo, float hi)
        {
            int b = (sample * MaxIntervals + k) * 2;
            buf[b] = lo;
            buf[b + 1] = hi;
        }

        static float Lo(float[] buf, int sample, int k) => buf[(sample * MaxIntervals + k) * 2];
        static float Hi(float[] buf, int sample, int k) => buf[(sample * MaxIntervals + k) * 2 + 1];

        /// <summary>Removes [cutLo, cutHi] from the sorted, disjoint interval list of a sample.</summary>
        int Subtract(float[] buf, int sample, int count, float cutLo, float cutHi)
        {
            int m = 0;
            for (int k = 0; k < count; k++)
            {
                float lo = Lo(buf, sample, k), hi = Hi(buf, sample, k);
                if (cutHi <= lo || cutLo >= hi) { _tmp[m * 2] = lo; _tmp[m * 2 + 1] = hi; m++; continue; }
                if (cutLo > lo && m < MaxIntervals * 2) { _tmp[m * 2] = lo; _tmp[m * 2 + 1] = cutLo; m++; }
                if (cutHi < hi && m < MaxIntervals * 2) { _tmp[m * 2] = cutHi; _tmp[m * 2 + 1] = hi; m++; }
            }
            m = Math.Min(m, MaxIntervals);
            for (int k = 0; k < m; k++) Set(buf, sample, k, _tmp[k * 2], _tmp[k * 2 + 1]);
            return m;
        }

        static int IntersectPoint(float[] src, int count, int sample, float x, float[] dst, int dstSample)
        {
            for (int k = 0; k < count; k++)
            {
                if (x >= Lo(src, sample, k) - 1e-4f && x <= Hi(src, sample, k) + 1e-4f)
                {
                    Set(dst, dstSample, 0, x, x);
                    return 1;
                }
            }
            return 0;
        }

        static int CopyNearest(float[] src, int count, int sample, float x, float[] dst, int dstSample)
        {
            if (count == 0) return 0;
            int best = 0; float bestD = float.PositiveInfinity;
            for (int k = 0; k < count; k++)
            {
                float d = Math.Abs(SimMath.Clamp(x, Lo(src, sample, k), Hi(src, sample, k)) - x);
                if (d < bestD) { bestD = d; best = k; }
            }
            float p = SimMath.Clamp(x, Lo(src, sample, best), Hi(src, sample, best));
            Set(dst, dstSample, 0, p, p);
            return 1;
        }

        /// <summary>dst[dstSample] = expand(a[aSample], by) ∩ b[bSample]. Returns interval count (merged, sorted).</summary>
        int ExpandIntersect(float[] a, int aSample, int aCount, float by, float[] b, int bSample, int bCount, float[] dst, int dstSample)
        {
            int m = 0;
            for (int i = 0; i < aCount; i++)
            {
                float alo = Lo(a, aSample, i) - by, ahi = Hi(a, aSample, i) + by;
                for (int j = 0; j < bCount; j++)
                {
                    float lo = Math.Max(alo, Lo(b, bSample, j)), hi = Math.Min(ahi, Hi(b, bSample, j));
                    if (hi < lo || m >= MaxIntervals * 2) continue;
                    _tmp[m * 2] = lo; _tmp[m * 2 + 1] = hi; m++;
                }
            }
            // sort (tiny n) and merge
            for (int i = 1; i < m; i++)
                for (int j = i; j > 0 && _tmp[j * 2] < _tmp[(j - 1) * 2]; j--)
                {
                    float l = _tmp[j * 2], h = _tmp[j * 2 + 1];
                    _tmp[j * 2] = _tmp[(j - 1) * 2]; _tmp[j * 2 + 1] = _tmp[(j - 1) * 2 + 1];
                    _tmp[(j - 1) * 2] = l; _tmp[(j - 1) * 2 + 1] = h;
                }
            int outCount = 0;
            for (int i = 0; i < m && outCount < MaxIntervals; i++)
            {
                float lo = _tmp[i * 2], hi = _tmp[i * 2 + 1];
                if (outCount > 0 && lo <= Hi(dst, dstSample, outCount - 1) + 1e-5f)
                {
                    int b2 = (dstSample * MaxIntervals + outCount - 1) * 2 + 1;
                    dst[b2] = Math.Max(dst[b2], hi);
                    continue;
                }
                Set(dst, dstSample, outCount++, lo, hi);
            }
            return outCount;
        }

        static float Nearest(float[] buf, int sample, int count, float x)
        {
            float best = x, bestD = float.PositiveInfinity;
            for (int k = 0; k < count; k++)
            {
                float p = SimMath.Clamp(x, Lo(buf, sample, k), Hi(buf, sample, k));
                float d = Math.Abs(p - x);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        static float NearestWithin(float[] buf, int sample, int count, float x, float lo, float hi)
        {
            float best = float.NaN, bestD = float.PositiveInfinity;
            for (int k = 0; k < count; k++)
            {
                float ilo = Math.Max(lo, Lo(buf, sample, k)), ihi = Math.Min(hi, Hi(buf, sample, k));
                if (ihi < ilo) continue;
                float p = SimMath.Clamp(x, ilo, ihi);
                float d = Math.Abs(p - x);
                if (d < bestD) { bestD = d; best = p; }
            }
            // by construction (backward pass) a point within [lo, hi] exists; fall back defensively
            return float.IsNaN(best) ? Nearest(buf, sample, count, x) : best;
        }
    }
}
