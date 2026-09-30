using System.Collections.Generic;
using Game.Art;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Mirrors the simulation's pickup pool: spinning gold coins, purple diamonds, glowing blue nitro bottles,
    /// all bobbing; collected pickups pop up and vanish. Purely visual, pooled per kind.
    /// </summary>
    public sealed class PickupViews : MonoBehaviour
    {
        PickupField _field;
        readonly Transform[] _views = new Transform[PickupField.Capacity];
        readonly int[] _generation = new int[PickupField.Capacity];
        readonly PickupKind[] _kind = new PickupKind[PickupField.Capacity];
        readonly Dictionary<PickupKind, Stack<Transform>> _pool = new Dictionary<PickupKind, Stack<Transform>>();

        public static PickupViews Create(Transform parent, PickupField field)
        {
            var go = new GameObject("Pickups");
            go.transform.SetParent(parent, false);
            var views = go.AddComponent<PickupViews>();
            views._field = field;
            return views;
        }

        void LateUpdate()
        {
            if (_field == null) return;
            float time = Time.time;
            for (int i = 0; i < PickupField.Capacity; i++)
            {
                ref Pickup p = ref _field.Items[i];
                if (!p.Active)
                {
                    if (_views[i] != null) Release(i);
                    continue;
                }
                if (_views[i] == null || _generation[i] != p.Generation || _kind[i] != p.Kind)
                {
                    if (_views[i] != null) Release(i);
                    _views[i] = Acquire(p.Kind);
                    _generation[i] = p.Generation;
                    _kind[i] = p.Kind;
                }

                Transform v = _views[i];
                Vector3 pos = p.Position.ToUnity();
                float phase = i * 0.37f;
                if (p.Collected)
                {
                    float t = p.CollectTime;
                    pos += Vector3.up * (t * 4f);
                    float s = Mathf.Max(0f, 1f + t * 2f - t * t * 8f);
                    v.localScale = Vector3.one * s;
                }
                else
                {
                    pos += Vector3.up * (Mathf.Sin(time * 3f + phase) * 0.12f);
                    v.localScale = Vector3.one;
                }
                v.SetPositionAndRotation(pos, Quaternion.Euler(0f, time * 180f + phase * 57f, 0f));
            }
        }

        void Release(int slot)
        {
            Transform view = _views[slot];
            _views[slot] = null;
            view.gameObject.SetActive(false);
            if (!_pool.TryGetValue(_kind[slot], out Stack<Transform> stack)) _pool[_kind[slot]] = stack = new Stack<Transform>();
            stack.Push(view);
        }

        Transform Acquire(PickupKind kind)
        {
            if (_pool.TryGetValue(kind, out Stack<Transform> stack) && stack.Count > 0)
            {
                Transform pooled = stack.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            return Build(kind);
        }

        Transform Build(PickupKind kind)
        {
            var root = new GameObject(kind.ToString()).transform;
            root.SetParent(transform, false);
            // M4: glowing stylised pickups from Game.Art (gems and nitro use the emissive palette material)
            ArtBuilder.Build(ArtLibrary.Pickup(kind), root, castShadows: kind != PickupKind.Coin);
            return root;
        }
    }
}
