using System.Collections;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class EndlessRunPlayModeTests
    {
        GameManager _gm;
        GameObject _standalone;
        [TearDown]
        public void TearDown()
        {
            if (_gm != null && _gm.CameraRig != null) Object.Destroy(_gm.CameraRig.gameObject);
            if (_gm != null) Object.Destroy(_gm.gameObject);
            if (_standalone != null) Object.Destroy(_standalone);
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
        }

        [UnityTest]
        public IEnumerator StreamingRetiresOldRoadAndKeepsContinuousGroundBeyondTenKilometers()
        {
            _standalone = new GameObject("Streaming test");
            TestRoad road = TestRoad.Build(_standalone.transform, endless: true);
            var ground = new UnityGroundProvider();
            Physics.SyncTransforms();
            Assert.IsTrue(ground.Sample(0, -12, 8).Found, "The chase-camera start area must be covered by road.");
            bool left = false, right = false;
            for (int step = 0; step <= 140; step++)
            {
                float distance = step * 80f;
                road.Streamer.Maintain(distance);
                Physics.SyncTransforms();
                road.Path.Sample(distance, out Vector3 point, out float yaw);
                Assert.That(road.Path.Length, Is.GreaterThanOrEqualTo(distance + EndlessRoad.AheadDistance));
                Assert.That(road.Streamer.ActiveChunkCount, Is.LessThanOrEqualTo(9));
                Assert.That(road.Path.Count, Is.LessThanOrEqualTo(370));
                GroundSample sample = ground.Sample(point.x, point.z, point.y + 5f);
                Assert.IsTrue(sample.Found, "Ground missing at distance " + distance);
                // A streamed ramp may legitimately be the highest drivable surface above the centerline.
                Assert.That(sample.Height, Is.InRange(point.y-.02f,point.y+1.2f));
                if(sample.Height>point.y+.02f)
                {
                    bool rampHit=false;
                    foreach(var hit in Physics.RaycastAll(point+Vector3.up*5,Vector3.down,8))
                        if(hit.collider.GetComponent<ArcadeRamp>()!=null && Mathf.Abs(hit.point.y-sample.Height)<.02f)rampHit=true;
                    Assert.IsTrue(rampHit,"Only a physical jump ramp may raise the ground above the road centerline.");
                }
                for (int i = 1; i < road.Path.Count; i++)
                {
                    float delta = road.Path.YawAt(i) - road.Path.YawAt(i - 1);
                    left |= delta < -0.0001f; right |= delta > 0.0001f;
                }
                if (step % 10 == 0) yield return null;
            }
            Assert.IsTrue(left && right, "Road must contain bends in both directions.");
            Assert.Greater(road.Path.StartDistance, 10000f, "Old centerline data must also be discarded.");
            int palms = 0;
            var palmMeshes = new System.Collections.Generic.HashSet<Mesh>();
            foreach (Transform child in road.transform.GetComponentsInChildren<Transform>())
                if (child.name == "Frond Palm")
                {
                    palms++;
                    foreach (var filter in child.GetComponentsInChildren<MeshFilter>()) palmMeshes.Add(filter.sharedMesh);
                }
            Assert.LessOrEqual(palms,36,"Palm instances must retire with road chunks.");
            Assert.AreEqual(2,palmMeshes.Count,"All palms must reuse the two cached meshes.");
            foreach (Mesh mesh in palmMeshes)
                foreach (var normal in mesh.normals) Assert.Greater(normal.sqrMagnitude,.9f,"Palm faces need valid normals on both sides.");
            Assert.Less(ground.CachedColliderCount, 32, "Destroyed chunk colliders must be evicted from the probe cache.");
            road.Path.Sample(11200f, out Vector3 final, out _);
            int hint = 0;
            Assert.That(road.Path.Project(final, ref hint, out float lateral), Is.EqualTo(11200f).Within(0.1f));
            Assert.That(lateral, Is.EqualTo(0f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator BoardCollectsEachDiamondOnceAndHazardsReduceSpeed()
        {
            _gm = GameManager.Create(BoardTuning.CreateDefault(), 6, bots: false, keyboard: false, endless: true);
            foreach (var p in _gm.Board.Simulation.Players) p.Pin(System.Numerics.Vector2.Zero);
            yield return new WaitForFixedUpdate();
            DiamondPickup.Create(_gm.Road.transform, _gm.Board.transform.position + Vector3.up * 0.8f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, _gm.Run.Diamonds);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, _gm.Run.Diamonds, "Multiple physics contacts must not double-count a gem.");
            float before = _gm.Board.State.Speed;
            RoadHazard.Create(_gm.Road.transform, _gm.Board.transform.position, 0f, crate: true);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Less(_gm.Board.State.Speed, before - 0.5f, "Crate trigger must apply a real board impact.");
            _gm.Road.Streamer.Maintain(700f);
            _gm.Run.RestartRun();
            yield return null;
            Assert.AreEqual(0, _gm.Run.Diamonds);
            Assert.AreEqual(0f, _gm.Road.Path.StartDistance);
            Assert.Less(_gm.Board.transform.position.magnitude, 1f);
            Assert.LessOrEqual(_gm.Road.Streamer.ActiveChunkCount, 9);
        }

        [UnityTest]
        public IEnumerator EndlessModeDoesNotRestartAtTheFormerTestTrackEnd()
        {
            _gm = GameManager.Create(BoardTuning.CreateDefault(), 6, bots: false, keyboard: false, endless: true);
            for (float d = 0; d <= 2800; d += 80) _gm.Road.Streamer.Maintain(d);
            _gm.Road.Path.Sample(2700f, out Vector3 position, out float yaw);
            _gm.Board.Restart(position, yaw);
            Physics.SyncTransforms();
            yield return null;
            Assert.Greater(_gm.Run.Distance, 2600f);
            Assert.Greater(_gm.Road.Path.Length, 3000f);
            Assert.AreEqual(0, _gm.Run.RunDistanceStart, "Passing the old endpoint must not restart the run.");
        }
    }
}
