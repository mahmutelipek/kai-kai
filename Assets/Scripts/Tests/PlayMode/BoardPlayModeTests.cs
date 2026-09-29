using System.Collections;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SVec2 = System.Numerics.Vector2;

namespace Game.Tests
{
    /// <summary>
    /// Milestone 1 checks inside the real engine: raycast ground following on the test road, kinematic board
    /// pose, crash -> respawn flow, ramp launch, player views never below the deck. Run from the Unity Test
    /// Runner (PlayMode tab). The pure-simulation acceptance tests live in BoardAcceptanceTests.
    /// </summary>
    public class BoardPlayModeTests
    {
        GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            _gm = GameManager.Create(BoardTuning.CreateDefault(), 6, bots: false, keyboard: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_gm != null) Object.Destroy(_gm.gameObject);
            if (_gm != null && _gm.CameraRig != null) Object.Destroy(_gm.CameraRig.gameObject);
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
        }

        void Pin(string layout)
        {
            BoardSimulation sim = _gm.Board.Simulation;
            sim.SetActivePlayerCount(layout.Length);
            SVec2[] spots = BoardScenario.Layout(layout, sim.Tuning);
            for (int i = 0; i < spots.Length; i++) sim.Players[i].Pin(spots[i]);
        }

        void UnpinAll()
        {
            foreach (PlayerSim p in _gm.Board.Simulation.Players) p.Unpin();
        }

        float RoadHeightUnderBoard()
        {
            Vector3 p = _gm.Board.transform.position;
            Assert.IsTrue(Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f));
            return hit.point.y;
        }

        [UnityTest]
        public IEnumerator Balanced_FollowsRoadStraightAndStable()
        {
            Pin("CCCCCC");
            yield return new WaitForSeconds(4f);
            BoardState s = _gm.Board.State;
            Assert.IsFalse(s.Crashed);
            Assert.IsTrue(s.Grounded, "board should be on the road");
            Assert.That(Mathf.Abs(s.YawRate * Mathf.Rad2Deg), Is.LessThan(1f));
            Assert.That(Mathf.Abs(_gm.Board.transform.position.y - RoadHeightUnderBoard()), Is.LessThan(0.15f));
            Assert.That(Mathf.Abs(_gm.Run.LateralOffset), Is.LessThan(1f));
            Assert.That(_gm.Run.Distance, Is.GreaterThan(20f), "board should roll downhill");
        }

        [UnityTest]
        public IEnumerator FiveLeftOneRight_TurnsLeft()
        {
            Pin("LLLLLR");
            float startYaw = _gm.Board.State.Yaw;
            yield return new WaitForSeconds(2.5f);
            BoardState s = _gm.Board.State;
            Assert.That(s.YawRate, Is.LessThan(0f));
            Assert.That(Mathf.DeltaAngle(startYaw * Mathf.Rad2Deg, s.Yaw * Mathf.Rad2Deg), Is.LessThan(-5f));
            Assert.That(_gm.Board.transform.eulerAngles.z, Is.GreaterThan(1f).And.LessThan(40f), "board should visibly roll into the left turn");
        }

        [UnityTest]
        public IEnumerator AllLeft_CrashesThenRespawnsOnRoad()
        {
            Pin("LLLLLL");
            float t = 0f;
            while (!_gm.Board.State.Crashed && t < 4f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(_gm.Board.State.Crashed, "should crash within 4 s");
            UnpinAll();
            yield return new WaitForSeconds(_gm.Tuning.data.crashRestartDelay + 0.5f);
            BoardSimulation sim = _gm.Board.Simulation;
            Assert.IsFalse(sim.Board.State.Crashed, "should have respawned");
            for (int i = 0; i < sim.ActivePlayerCount; i++) Assert.IsTrue(sim.Players[i].IsOnBoard, "player back on board");
            Assert.That(Mathf.Abs(_gm.Run.LateralOffset), Is.LessThan(1f), "respawned on the road centre");
        }

        [UnityTest]
        public IEnumerator Ramp_LaunchesAndLandsWithoutCrash()
        {
            Pin("CCCCCC");
            _gm.Road.Path.Sample(TestRoad.RampDistance - 40f, out Vector3 p, out float yaw);
            _gm.Board.Restart(p, yaw);
            _gm.Board.Simulation.Board.State.Speed = 18f;
            bool wasAirborne = false;
            float t = 0f;
            while (t < 5f)
            {
                t += Time.deltaTime;
                if (!_gm.Board.State.Grounded) wasAirborne = true;
                yield return null;
            }
            Assert.IsTrue(wasAirborne, "ramp should launch the board");
            Assert.IsTrue(_gm.Board.State.Grounded, "board should land");
            Assert.IsFalse(_gm.Board.State.Crashed);
        }

        [UnityTest]
        public IEnumerator Bots_PlayersNeverBelowDeckOrInsideEachOther()
        {
            _gm.InputRouter.BotsEnabled = true;
            BoardSimulation sim = _gm.Board.Simulation;
            float deckTop = _gm.Tuning.data.deckHeight;
            float minLocalY = float.PositiveInfinity, minSeparation = float.PositiveInfinity;
            float t = 0f;
            while (t < 20f)
            {
                t += Time.deltaTime;
                foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
                {
                    if (view.Slot >= sim.ActivePlayerCount || !sim.Players[view.Slot].IsOnBoard) continue;
                    Vector3 local = _gm.Board.transform.InverseTransformPoint(view.transform.position);
                    Assert.IsFalse(float.IsNaN(local.x) || float.IsNaN(local.y) || float.IsNaN(local.z));
                    minLocalY = Mathf.Min(minLocalY, local.y);
                }
                minSeparation = Mathf.Min(minSeparation, PlayerCrowdSolver.MinimumSeparation(sim.Players, sim.ActivePlayerCount, sim.Tuning));
                yield return null;
            }
            Debug.Log($"bots 20 s: min player local y {minLocalY:F3} (deck top {deckTop:F2}), min separation {minSeparation:F3}");
            Assert.That(minLocalY, Is.GreaterThanOrEqualTo(deckTop - 0.02f), "player below the deck surface");
            Assert.That(minSeparation, Is.GreaterThan(sim.Tuning.playerRadius * 2f * 0.9f));
        }

        [UnityTest]
        public IEnumerator TwoPlayerMode_OnlyTwoPlayersVisibleAndSteer()
        {
            Pin("RC");
            yield return new WaitForSeconds(3f);
            int visible = 0;
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
                if (view.GetComponentInChildren<Renderer>() != null && view.GetComponentInChildren<Renderer>().enabled &&
                    view.transform.Find("Pose").gameObject.activeSelf) visible++;
            Assert.AreEqual(2, visible);
            Assert.That(_gm.Board.State.YawRate, Is.GreaterThan(0f), "edge + centre turns right");
            Assert.IsFalse(_gm.Board.State.Crashed);
        }
    }
}
