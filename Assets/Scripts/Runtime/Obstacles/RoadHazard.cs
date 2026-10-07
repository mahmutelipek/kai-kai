using UnityEngine;

namespace Game
{
    /// <summary>Trigger-based board impacts also work with the kinematic board and fixed scenery.</summary>
    public sealed class RoadHazard : MonoBehaviour
    {
        static Mesh _coneMesh;
        ImpactSeverity _severity;
        float _lastHit = -100f;
        public static RoadHazard Create(Transform parent, Vector3 position, float yaw, bool crate)
        {
            var root = new GameObject(crate ? "Road Crate" : "Road Cone"); root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f));
            var hazard = root.AddComponent<RoadHazard>(); hazard._severity = crate ? ImpactSeverity.Heavy : ImpactSeverity.Light;
            if (crate)
            {
                PrimitiveFactory.Visual(PrimitiveType.Cube, root.transform, new Vector3(0, 0.7f, 0), new Vector3(1.6f, 1.4f, 1.6f), MaterialLibrary.RampWood, "Crate");
                PrimitiveFactory.Visual(PrimitiveType.Cube, root.transform, new Vector3(0, 0.7f, -0.81f), new Vector3(1.65f, 0.22f, 0.04f), MaterialLibrary.LineYellow, "Warning Stripe");
            }
            else
            {
                if (_coneMesh == null) _coneMesh = PrimitiveFactory.CreateCone(0.35f, 1f);
                PrimitiveFactory.MeshObject("Cone", _coneMesh, MaterialLibrary.ConeOrange, root.transform);
                PrimitiveFactory.Visual(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.43f, 0), new Vector3(0.43f, 0.06f, 0.43f), Color.white, "Stripe");
            }
            var trigger = root.AddComponent<BoxCollider>(); trigger.isTrigger = true;
            trigger.center = new Vector3(0, crate ? 0.7f : 0.5f, 0);
            trigger.size = crate ? new Vector3(1.6f, 1.4f, 1.6f) : new Vector3(0.7f, 1f, 0.7f);
            return hazard;
        }
        void OnTriggerEnter(Collider other)
        {
            var board = other.GetComponentInParent<BoardController>();
            if (board == null || board.State.Crashed || Time.time - _lastHit < 0.5f) return;
            var run = board.GetComponentInParent<RunManager>();
            if (run != null && !run.AcceptsGameplay) return;
            run?.HitObstacle(_severity == ImpactSeverity.Heavy);
            _lastHit = Time.time; board.ApplyImpact(_severity, board.State.Speed);
        }
    }
}
