using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>IGroundProvider backed by downward raycasts against colliders that carry a GroundSurface.</summary>
    public sealed class UnityGroundProvider : IGroundProvider
    {
        const float MaxProbeDistance = 200f;
        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly Dictionary<Collider, GroundSurface> _surfaceCache = new Dictionary<Collider, GroundSurface>();
        readonly List<Collider> _expired = new List<Collider>();
        int _samples;
        public int CachedColliderCount => _surfaceCache.Count;

        public GroundSample Sample(float x, float z, float searchFromHeight)
        {
            if (++_samples % 128 == 0)
            {
                _expired.Clear();
                foreach (var entry in _surfaceCache) if (entry.Key == null) _expired.Add(entry.Key);
                foreach (Collider collider in _expired) _surfaceCache.Remove(collider);
            }
            var origin = new Vector3(x, searchFromHeight, z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, MaxProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            GroundSample best = GroundSample.Missing;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance >= bestDistance) continue;
                GroundSurface surface = GetSurface(hit.collider);
                if (surface == null) continue;
                bestDistance = hit.distance;
                best = GroundSample.At(hit.point.y, surface.kind);
            }
            return best;
        }

        GroundSurface GetSurface(Collider collider)
        {
            if (!_surfaceCache.TryGetValue(collider, out GroundSurface surface))
            {
                surface = collider.GetComponent<GroundSurface>();
                _surfaceCache[collider] = surface;
            }
            return surface;
        }
    }
}
