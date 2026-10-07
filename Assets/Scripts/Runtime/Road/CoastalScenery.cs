using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Visual scenery fitted to the test centerline; does not contribute colliders or steering.</summary>
    public sealed class CoastalScenery : MonoBehaviour
    {
        public static readonly string[] AssetNames =
            { "CoastalHouse0", "CoastalHouse1", "CoastalHouse2", "CoastalPalm", "CoastalBarrier" };
        readonly List<Mesh> _meshes = new List<Mesh>();
        public int PalmCount { get; private set; }
        public int HouseCount { get; private set; }

        public static CoastalScenery Build(TestRoad road)
        {
            var assets = new GameObject[AssetNames.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                assets[i] = Resources.Load<GameObject>("Art/Environment/" + AssetNames[i]);
                if (assets[i] == null) return null;
            }
            var root = new GameObject("Coastal Scenery"); root.transform.SetParent(road.transform, false);
            var scenery = root.AddComponent<CoastalScenery>();
            scenery.BuildInternal(road.Path, assets);
            // The original shoulder collider remains, but the narrower beach has its own visual.
            var shoulder = road.transform.Find("ShoulderRight");
            if (shoulder != null) shoulder.GetComponent<Renderer>().enabled = false;
            road.transform.Find("ShoulderLeft").GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(new Color(0.28f, 0.37f, 0.2f));
            if (Application.isPlaying)
            {
                var originals = new HashSet<Mesh>();
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>()) originals.Add(filter.sharedMesh);
                StaticBatchingUtility.Combine(root);
                var generated = new HashSet<Mesh>();
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                    if (!originals.Contains(filter.sharedMesh)) generated.Add(filter.sharedMesh);
                scenery._meshes.AddRange(generated);
            }
            return scenery;
        }

        void Place(GameObject prefab, RoadPath path, float distance, float lateral, float yawOffset = 0f)
        {
            path.Sample(distance, out Vector3 position, out float yaw);
            var obj = Instantiate(prefab, transform);
            obj.transform.SetPositionAndRotation(position + SimConvert.YawRight(yaw) * lateral,
                Quaternion.Euler(0f, yaw * Mathf.Rad2Deg + yawOffset, 0f));
        }

        void BuildInternal(RoadPath path, GameObject[] assets)
        {
            for (float d = 14f; d < path.Length - 10f; d += 30f)
            {
                Place(assets[3], path, d, -9.5f); Place(assets[3], path, d + 8f, 9.5f);
                PalmCount += 2;
            }
            for (float d = 26f; d < path.Length - 20f; d += 28f)
            {
                Place(assets[HouseCount % 3], path, d, -20f, 90f); HouseCount++;
                Place(assets[HouseCount % 3], path, d + 12f, -33f, 90f); HouseCount++;
            }
            for (float d = 6f; d < path.Length - 5f; d += 2.2f)
                Place(assets[4], path, d, 6.65f);
            Strip(path, "Beach", 6f, 12f, 0.025f, 0.025f, new Color(0.65f, 0.58f, 0.43f));
            Strip(path, "Seawall", 12f, 12f, 0.025f, -12f, new Color(0.48f, 0.39f, 0.29f));
            Strip(path, "CoastalWater", 12f, 750f, -12f, -12f, new Color(0.05f, 0.43f, 0.62f));
            Strip(path, "Hillside", -140f, -31f, 34f, -0.02f, new Color(0.39f, 0.48f, 0.29f));
        }

        void Strip(RoadPath path, string name, float a, float b, float heightA, float heightB, Color color)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 center = path.PointAt(i), right = SimConvert.YawRight(path.YawAt(i));
                vertices.Add(center + right * a + Vector3.up * heightA);
                vertices.Add(center + right * b + Vector3.up * heightB);
                if (i == 0) continue;
                int v = vertices.Count - 4;
                triangles.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _meshes.Add(mesh);
            PrimitiveFactory.MeshObject(name, mesh, color, transform);
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in _meshes)
                if (mesh != null)
                {
                    if (Application.isPlaying) Destroy(mesh);
                    else DestroyImmediate(mesh);
                }
        }
    }
}
