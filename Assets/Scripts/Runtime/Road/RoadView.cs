using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>Keeps one pooled ChunkView per spawned RoadChunk (spawn ahead / recycle behind follows the generator).</summary>
    public sealed class RoadView : MonoBehaviour
    {
        RoadModel _road;
        readonly List<ChunkView> _active = new List<ChunkView>(16);
        readonly Stack<ChunkView> _pool = new Stack<ChunkView>(16);

        public int ActiveViews => _active.Count;

        public static RoadView Create(Transform parent, RoadModel road)
        {
            var go = new GameObject("Road");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<RoadView>();
            view._road = road;
            for (int i = 0; i < RoadGenerator.PrewarmChunks; i++)
            {
                ChunkView c = ChunkView.Create(go.transform);
                c.gameObject.SetActive(false);
                view._pool.Push(c);
            }
            return view;
        }

        void LateUpdate() => Sync();

        /// <summary>Releases views of recycled chunks and builds views for new ones.</summary>
        public void Sync()
        {
            if (_road == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (IsAlive(_active[i].Serial)) continue;
                _active[i].gameObject.SetActive(false);
                _pool.Push(_active[i]);
                _active.RemoveAt(i);
            }
            for (int i = 0; i < _road.Chunks.Count; i++)
            {
                RoadChunk chunk = _road.Chunks[i];
                if (HasView(chunk.Serial)) continue;
                ChunkView view = _pool.Count > 0 ? _pool.Pop() : ChunkView.Create(transform);
                view.Rebuild(chunk);
                view.gameObject.SetActive(true);
                _active.Add(view);
            }
        }

        bool IsAlive(int serial)
        {
            for (int i = 0; i < _road.Chunks.Count; i++) if (_road.Chunks[i].Serial == serial) return true;
            return false;
        }

        bool HasView(int serial)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].Serial == serial) return true;
            return false;
        }
    }
}
