using System.Collections.Generic;
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
        static readonly Color CoinGold = new Color(1f, 0.78f, 0.1f);
        static readonly Color DiamondPurple = new Color(0.72f, 0.25f, 1f);
        static readonly Color NitroBlue = new Color(0.15f, 0.55f, 1f);

        PickupField _field;
        readonly Transform[] _views = new Transform[PickupField.Capacity];
        readonly int[] _generation = new int[PickupField.Capacity];
        readonly PickupKind[] _kind = new PickupKind[PickupField.Capacity];
        readonly Dictionary<PickupKind, Stack<Transform>> _pool = new Dictionary<PickupKind, Stack<Transform>>();
        static Mesh _diamondMesh;

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
            switch (kind)
            {
                case PickupKind.Coin:
                {
                    GameObject coin = PrimitiveFactory.Visual(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.7f, 0.06f, 0.7f), CoinGold, "Coin");
                    coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    coin.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(CoinGold, 0.85f);
                    GameObject rim = PrimitiveFactory.Visual(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.45f, 0.08f, 0.45f), new Color(1f, 0.9f, 0.4f), "Face");
                    rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                }
                case PickupKind.Diamond:
                {
                    if (_diamondMesh == null) _diamondMesh = CreateOctahedron(0.45f, 0.75f);
                    GameObject gem = PrimitiveFactory.MeshObject("Gem", _diamondMesh, DiamondPurple, root);
                    gem.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(DiamondPurple, 0.95f);
                    break;
                }
                default:
                {
                    GameObject bottle = PrimitiveFactory.Visual(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.45f, 0.4f, 0.45f), NitroBlue, "Bottle");
                    bottle.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(NitroBlue, 0.9f);
                    PrimitiveFactory.Visual(PrimitiveType.Cylinder, root, new Vector3(0f, 0.5f, 0f), new Vector3(0.18f, 0.12f, 0.18f), MaterialLibrary.LineWhite, "Neck");
                    GameObject bolt = PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.23f), new Vector3(0.1f, 0.45f, 0.02f), MaterialLibrary.LineYellow, "Bolt");
                    bolt.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                    break;
                }
            }
            return root;
        }

        static Mesh CreateOctahedron(float radius, float height)
        {
            var top = new Vector3(0f, height * 0.5f, 0f);
            var bottom = new Vector3(0f, -height * 0.5f, 0f);
            var ring = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                ring[i] = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
            }
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 4];
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(top);    // outward-facing (Unity clockwise winding)
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                k = verts.Count;
                verts.Add(a); verts.Add(bottom); verts.Add(b);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            var mesh = new Mesh { name = "Diamond" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
