using UnityEngine;

namespace Game
{
    public sealed class DiamondPickup : MonoBehaviour
    {
        static Mesh _mesh;
        Transform _visual, _halo;
        bool _collected;
        public static DiamondPickup Create(Transform parent, Vector3 position)
        {
            if (_mesh == null)
            {
                Vector3 top = Vector3.up * 0.55f, bottom = Vector3.down * 0.55f;
                Vector3[] ring = { Vector3.forward * 0.35f, Vector3.right * 0.35f, Vector3.back * 0.35f, Vector3.left * 0.35f };
                var vertices = new Vector3[24]; var triangles = new int[24];
                for (int i = 0; i < 4; i++)
                {
                    int v = i * 6; vertices[v] = top; vertices[v + 1] = ring[i]; vertices[v + 2] = ring[(i + 1) % 4];
                    vertices[v + 3] = bottom; vertices[v + 4] = ring[(i + 1) % 4]; vertices[v + 5] = ring[i];
                }
                for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
                _mesh = new Mesh { name = "Diamond" }; _mesh.vertices = vertices; _mesh.triangles = triangles; _mesh.RecalculateNormals(); _mesh.RecalculateBounds();
            }
            var root = new GameObject("Diamond"); root.transform.SetParent(parent, false); root.transform.position = position;
            var pickup = root.AddComponent<DiamondPickup>();
            pickup._visual = PrimitiveFactory.MeshObject("Gem", _mesh, new Color(0.75f, 0.12f, 0.95f), root.transform).transform;
            var gemMaterial = Resources.Load<Material>("Art/Feedback/Diamond");
            if (gemMaterial != null) pickup._visual.GetComponent<Renderer>().sharedMaterial = gemMaterial;
            var haloMaterial = Resources.Load<Material>("Art/Feedback/DiamondGlow");
            if (haloMaterial != null)
            {
                var halo = PrimitiveFactory.Visual(PrimitiveType.Quad, root.transform, Vector3.zero, Vector3.one * 1.6f, Color.white, "Gem glow");
                halo.GetComponent<Renderer>().sharedMaterial = haloMaterial; pickup._halo = halo.transform;
            }
            var trigger = root.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 0.65f;
            return pickup;
        }
        void Update()
        {
            _visual.localRotation = Quaternion.Euler(0, Time.time * 110f, 0);
            _visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * 3f + transform.position.z) * 0.1f);
            if (_halo != null && Camera.main != null)
            {
                Vector3 direction = (Camera.main.transform.position - _visual.position).normalized;
                _halo.position = _visual.position - direction * .08f;
                _halo.rotation = Quaternion.LookRotation(-direction);
            }
        }
        void OnTriggerEnter(Collider other)
        {
            var board = other.GetComponentInParent<BoardController>();
            if (board == null || board.State.Crashed || _collected) return;
            var run = board.GetComponentInParent<RunManager>();
            if (run == null || !run.AcceptsGameplay) return;
            _collected = true; run.CollectDiamond(run.NearestRider(transform.position)); gameObject.SetActive(false); Destroy(gameObject);
        }
    }
}
