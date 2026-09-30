using Game.Art;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Visual for one RoadChunk: road, markings, walls, bridge, tunnel, ramps and the roadside scenery, all from
    /// Game.Art.ChunkGeometry (the same triangles the headless preview renders). One mesh, one renderer, the
    /// shared palette materials. Pooled; Rebuild() reuses its buffers.
    /// </summary>
    public sealed class ChunkView : MonoBehaviour
    {
        static readonly Unity.Profiling.ProfilerMarker RebuildMarker = new Unity.Profiling.ProfilerMarker("Downhill.ChunkView.Rebuild");
        readonly MeshSet _set = new MeshSet();
        Mesh _mesh;

        public int Serial { get; private set; } = -1;

        public static ChunkView Create(Transform parent)
        {
            var go = new GameObject("Chunk");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ChunkView>();
            view._mesh = new Mesh { name = "Chunk" };
            view._mesh.MarkDynamic();
            ArtBuilder.MeshObject("Geometry", go.transform, view._mesh, castShadows: true);
            return view;
        }

        public void Rebuild(RoadChunk chunk)
        {
            Serial = chunk.Serial;
            gameObject.name = $"Chunk {chunk.Serial} {chunk.Kind}";
            using (RebuildMarker.Auto())
            {
                _set.Clear();
                ChunkGeometry.Build(chunk, _set);
                ArtMeshes.Upload(_set, _mesh);
            }
        }
    }
}
