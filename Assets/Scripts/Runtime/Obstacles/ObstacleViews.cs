using System.Collections.Generic;
using Game.Art;
using Game.Simulation;
using UnityEngine;
using SVec2 = System.Numerics.Vector2;

namespace Game
{
    /// <summary>
    /// Mirrors the simulation's obstacle pool: one pooled model per active slot, rebuilt only when the
    /// slot's generation changes. Knocked obstacles fly off along their knock velocity. Purely visual.
    /// </summary>
    public sealed class ObstacleViews : MonoBehaviour
    {
        ObstacleField _field;
        readonly Transform[] _views = new Transform[ObstacleField.Capacity];
        readonly int[] _generation = new int[ObstacleField.Capacity];
        readonly ObstacleKind[] _kind = new ObstacleKind[ObstacleField.Capacity];
        readonly Dictionary<ObstacleKind, Stack<Transform>> _pool = new Dictionary<ObstacleKind, Stack<Transform>>();
        int _carColor;

        public static ObstacleViews Create(Transform parent, ObstacleField field)
        {
            var go = new GameObject("Obstacles");
            go.transform.SetParent(parent, false);
            var views = go.AddComponent<ObstacleViews>();
            views._field = field;
            return views;
        }

        void LateUpdate()
        {
            if (_field == null) return;
            for (int i = 0; i < ObstacleField.Capacity; i++)
            {
                ref Obstacle o = ref _field.Items[i];
                if (!o.Active)
                {
                    if (_views[i] != null) Release(i);
                    continue;
                }
                if (_views[i] == null || _generation[i] != o.Generation || _kind[i] != o.Kind)
                {
                    if (_views[i] != null) Release(i);
                    _views[i] = Acquire(o);
                    _generation[i] = o.Generation;
                    _kind[i] = o.Kind;
                }
                Place(_views[i], ref o);
            }
        }

        static void Place(Transform view, ref Obstacle o)
        {
            Vector3 pos = o.Position.ToUnity();
            Quaternion rot = Quaternion.Euler(0f, o.Yaw * Mathf.Rad2Deg, 0f);
            if (o.Knocked)
            {
                float t = o.KnockTime;
                Vector3 v = o.KnockVelocity.ToUnity();
                pos += new Vector3(v.x * t, v.y * t - 0.5f * 20f * t * t, v.z * t);
                rot *= Quaternion.Euler(t * 540f, t * 200f, t * 320f);
            }
            view.SetPositionAndRotation(pos, rot);
        }

        void Release(int slot)
        {
            Transform view = _views[slot];
            _views[slot] = null;
            view.gameObject.SetActive(false);
            if (!_pool.TryGetValue(_kind[slot], out Stack<Transform> stack)) _pool[_kind[slot]] = stack = new Stack<Transform>();
            stack.Push(view);
        }

        Transform Acquire(in Obstacle o)
        {
            Transform view = null;
            if (_pool.TryGetValue(o.Kind, out Stack<Transform> stack) && stack.Count > 0) view = stack.Pop();
            if (view == null) view = Build(o.Kind);
            // size-dependent kinds stretch their "Scaled" child to the obstacle's footprint
            Transform scaled = view.Find("Scaled");
            if (scaled != null)
            {
                SVec2 unit = ObstacleCatalog.DefaultHalfExtents(o.Kind);
                scaled.localScale = new Vector3(o.HalfExtents.X / unit.X, 1f, o.HalfExtents.Y / unit.Y);
            }
            view.gameObject.SetActive(true);
            return view;
        }

        Transform Build(ObstacleKind kind)
        {
            var root = new GameObject(kind.ToString()).transform;
            root.SetParent(transform, false);
            // M4: stylised models from Game.Art; size-dependent kinds keep their geometry in group "Scaled"
            int variants = ArtLibrary.ObstacleVariants(kind);
            ArtBuilder.Build(ArtLibrary.Obstacle(kind, variants > 1 ? _carColor++ % variants : 0), root);
            return root;
        }
    }
}
