using System.Collections;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class CoastalSceneryPlayModeTests
    {
        GameManager _gm;
        [TearDown]
        public void TearDown()
        {
            if (_gm != null && _gm.CameraRig != null) Object.Destroy(_gm.CameraRig.gameObject);
            if (_gm != null) Object.Destroy(_gm.gameObject);
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
        }

        [UnityTest]
        public IEnumerator SceneryFollowsCurvesWithoutChangingGroundOrAddingColliders()
        {
            _gm = GameManager.Create(BoardTuning.CreateDefault(), 6, bots: false, keyboard: false);
            yield return null;
            var scenery = _gm.Road.GetComponentInChildren<CoastalScenery>();
            Assert.IsNotNull(scenery);
            Assert.Greater(scenery.PalmCount, 100);
            Assert.Greater(scenery.HouseCount, 100);
            Assert.AreEqual(0, scenery.GetComponentsInChildren<Collider>().Length, "Visual scenery must not participate in collisions or ground probes.");
            Assert.IsNull(scenery.transform.Find("Distant Bridge and Skyline"));
            Assert.IsNotNull(scenery.transform.Find("CoastalWater"));
            var ground = new UnityGroundProvider();
            foreach (float distance in new[] { 50f, 195f, 350f, 600f, 1000f })
            {
                _gm.Road.Path.Sample(distance, out Vector3 position, out float yaw);
                GroundSample sample = ground.Sample(position.x, position.z, position.y + 5f);
                Assert.IsTrue(sample.Found);
                Assert.AreEqual(SurfaceKind.Road, sample.Surface);
                Assert.That(sample.Height, Is.EqualTo(position.y).Within(0.02f));
            }
            // At least one imported palm on the first curve must match its road-local placement and heading.
            bool curvePalm = false;
            foreach (Transform child in scenery.transform)
            {
                if (!child.name.StartsWith("CoastalPalm")) continue;
                int hint = 0;
                float along = _gm.Road.Path.Project(child.position, ref hint, out float lateral);
                if (along < 175f || along > 250f) continue;
                _gm.Road.Path.Sample(along, out Vector3 position, out float yaw);
                Assert.That(Mathf.Abs(lateral), Is.EqualTo(9.5f).Within(0.08f));
                Assert.That(child.position.y, Is.EqualTo(position.y).Within(0.03f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(child.eulerAngles.y, yaw * Mathf.Rad2Deg)), Is.LessThan(1f));
                curvePalm = true;
            }
            Assert.IsTrue(curvePalm, "Curved road segment must receive aligned imported scenery.");
        }
    }
}
