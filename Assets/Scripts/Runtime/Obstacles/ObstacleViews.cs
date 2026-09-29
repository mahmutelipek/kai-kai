using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;
using SVec2 = System.Numerics.Vector2;

namespace Game
{
    /// <summary>
    /// Mirrors the simulation's obstacle pool: one pooled primitive model per active slot, rebuilt only when the
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
            SVec2 half = ObstacleCatalog.DefaultHalfExtents(kind);
            switch (kind)
            {
                case ObstacleKind.Cone:
                    PrimitiveFactory.MeshObject("Body", PrimitiveFactory.CreateCone(0.28f, 0.75f), MaterialLibrary.ConeOrange, root).transform.localPosition = new Vector3(0f, 0.04f, 0f);
                    PrimitiveFactory.Visual(PrimitiveType.Cylinder, root, new Vector3(0f, 0.36f, 0f), new Vector3(0.36f, 0.05f, 0.36f), MaterialLibrary.LineWhite, "Stripe");
                    PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.04f, 0.6f), MaterialLibrary.ConeOrange, "Base");
                    break;

                case ObstacleKind.ConcreteBarrier:
                {
                    Transform s = Scaled(root);
                    PrimitiveFactory.Visual(PrimitiveType.Cube, s, new Vector3(0f, 0.2f, 0f), new Vector3(half.X * 2f, 0.4f, 0.7f), MaterialLibrary.Concrete, "Base");
                    PrimitiveFactory.Visual(PrimitiveType.Cube, s, new Vector3(0f, 0.65f, 0f), new Vector3(half.X * 2f, 0.5f, 0.3f), MaterialLibrary.Concrete, "Top");
                    for (int i = -1; i <= 1; i += 2) // red/white hazard stripes on the ends
                        PrimitiveFactory.Visual(PrimitiveType.Cube, s, new Vector3(i * (half.X - 0.15f), 0.65f, 0f), new Vector3(0.3f, 0.52f, 0.32f), MaterialLibrary.DeckStripe, "Stripe");
                    break;
                }

                case ObstacleKind.ParkedCar:
                case ObstacleKind.MovingCar:
                {
                    Color paint = MaterialLibrary.CarColors[_carColor++ % MaterialLibrary.CarColors.Length];
                    GameObject body = PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(0f, 0.65f, 0f), new Vector3(2f, 0.8f, 4.4f), paint, "Body");
                    body.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(paint, 0.7f);
                    PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(0f, 1.3f, -0.3f), new Vector3(1.8f, 0.6f, 2.2f), MaterialLibrary.Glass, "Cabin");
                    for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        GameObject wheel = PrimitiveFactory.Visual(PrimitiveType.Cylinder, root, new Vector3(x * 0.95f, 0.35f, z * 1.4f), new Vector3(0.7f, 0.12f, 0.7f), MaterialLibrary.Tire, "Wheel");
                        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(x * 0.7f, 0.75f, z * 2.2f), new Vector3(0.4f, 0.18f, 0.05f), z > 0 ? MaterialLibrary.Headlight : MaterialLibrary.Taillight, "Light");
                    }
                    break;
                }

                case ObstacleKind.ConstructionBarrier:
                {
                    Transform s = Scaled(root);
                    int stripes = 6;
                    for (int i = 0; i < stripes; i++)
                    {
                        float x = -half.X + (i + 0.5f) * (half.X * 2f / stripes);
                        PrimitiveFactory.Visual(PrimitiveType.Cube, s, new Vector3(x, 0.8f, 0f), new Vector3(half.X * 2f / stripes, 0.3f, 0.08f),
                                                i % 2 == 0 ? MaterialLibrary.ConeOrange : MaterialLibrary.LineWhite, "Board");
                    }
                    for (int i = -1; i <= 1; i += 2)
                        PrimitiveFactory.Visual(PrimitiveType.Cube, s, new Vector3(i * (half.X - 0.1f), 0.4f, 0f), new Vector3(0.08f, 0.8f, 0.4f), MaterialLibrary.Metal, "Leg");
                    break;
                }

                case ObstacleKind.Pothole:
                {
                    Transform s = Scaled(root);
                    PrimitiveFactory.Visual(PrimitiveType.Cylinder, s, new Vector3(0f, 0.015f, 0f), new Vector3(half.X * 2f, 0.01f, half.Y * 2f), MaterialLibrary.Pothole, "Hole");
                    PrimitiveFactory.Visual(PrimitiveType.Cylinder, s, new Vector3(0f, 0.01f, 0f), new Vector3(half.X * 2.4f, 0.01f, half.Y * 2.4f), MaterialLibrary.ConcreteDark, "Rim");
                    break;
                }

                case ObstacleKind.Divider:
                {
                    Transform s = Scaled(root);
                    PrimitiveFactory.Visual(PrimitiveType.Cube, s, new Vector3(0f, 0.45f, 0f), new Vector3(half.X * 2f, 0.9f, half.Y * 2f), MaterialLibrary.Concrete, "Wall");
                    PrimitiveFactory.Visual(PrimitiveType.Cube, s, new Vector3(0f, 0.92f, 0f), new Vector3(half.X * 2f + 0.02f, 0.06f, half.Y * 2f), MaterialLibrary.LineYellow, "Cap");
                    break;
                }

                case ObstacleKind.Crate:
                    PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f), MaterialLibrary.Crate, "Crate");
                    for (int i = -1; i <= 1; i += 2)
                    {
                        PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(0f, 0.5f, i * 0.5f), new Vector3(1.04f, 0.12f, 0.04f), MaterialLibrary.CrateFrame, "Plank");
                        PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(i * 0.5f, 0.5f, 0f), new Vector3(0.04f, 0.12f, 1.04f), MaterialLibrary.CrateFrame, "Plank");
                    }
                    break;

                default: // BrokenPiece: tilted slab of old asphalt
                {
                    GameObject slab = PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(0f, 0.18f, 0f), new Vector3(1.6f, 0.3f, 1.2f), MaterialLibrary.ConcreteDark, "Slab");
                    slab.transform.localRotation = Quaternion.Euler(12f, 0f, -8f);
                    PrimitiveFactory.Visual(PrimitiveType.Cube, root, new Vector3(0.1f, 0.33f, 0.1f), new Vector3(1.5f, 0.02f, 1.1f), MaterialLibrary.Road, "Top").transform.localRotation = Quaternion.Euler(12f, 0f, -8f);
                    break;
                }
            }
            return root;
        }

        static Transform Scaled(Transform root)
        {
            var s = new GameObject("Scaled").transform;
            s.SetParent(root, false);
            return s;
        }
    }
}
