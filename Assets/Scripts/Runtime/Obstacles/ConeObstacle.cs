using UnityEngine;

namespace Game
{
    /// <summary>Traffic cone: light hit (small slowdown), gets knocked away by the physics engine.</summary>
    public sealed class ConeObstacle : ObstacleBase
    {
        public override ImpactSeverity Severity => ImpactSeverity.Light;

        public static ConeObstacle Create(Transform parent, Vector3 position, float yawDeg)
        {
            var root = new GameObject("Cone");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDeg, 0f));

            const float radius = 0.28f, height = 0.75f;
            GameObject body = PrimitiveFactory.MeshObject("Body", PrimitiveFactory.CreateCone(radius, height), MaterialLibrary.ConeOrange, root.transform);
            body.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            PrimitiveFactory.Visual(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.36f, 0.05f, 0.36f), MaterialLibrary.LineWhite, "Stripe");
            PrimitiveFactory.Visual(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.04f, 0.6f), MaterialLibrary.ConeOrange, "Base");

            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.25f;
            collider.height = height;
            collider.center = new Vector3(0f, height * 0.5f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            return root.AddComponent<ConeObstacle>();
        }
    }
}
