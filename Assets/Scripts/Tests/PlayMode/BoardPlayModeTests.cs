using System.Collections;
using System.Collections.Generic;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SVec2 = System.Numerics.Vector2;

namespace Game.Tests
{
    /// <summary>
    /// Checks inside the real engine: the hosted RunSimulation drives the kinematic board, road and obstacle
    /// views follow the simulation, crash -> respawn, ramp launch, player views never below the deck, and a
    /// frame-time / GC report for the endless road. Run from the Unity Test Runner (PlayMode tab).
    /// The pure-simulation acceptance tests live in BoardAcceptanceTests / RoadAcceptanceTests.
    /// </summary>
    public class BoardPlayModeTests
    {
        GameManager _gm;

        void Start(RoadMode mode, int players = 6, bool bots = false, int seed = 42)
        {
            _gm = GameManager.Create(BoardTuning.CreateDefault(), players, bots, keyboard: false, mode: mode, seed: seed);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_gm == null) return;
            if (_gm.CameraRig != null) Object.Destroy(_gm.CameraRig.gameObject);
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
            Object.Destroy(_gm.gameObject);
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

        [UnityTest]
        public IEnumerator Balanced_FollowsRoadStraightAndStable()
        {
            Start(RoadMode.TestTrack);
            Pin("CCCCCC");
            yield return new WaitForSeconds(4f);
            BoardState s = _gm.Board.State;
            Assert.IsFalse(s.Crashed);
            Assert.IsTrue(s.Grounded, "board should be on the road");
            Assert.That(Mathf.Abs(s.YawRate * Mathf.Rad2Deg), Is.LessThan(1f));
            GroundSample g = _gm.Board.Run.Road.Sample(s.Position.X, s.Position.Z, s.Position.Y + 3f);
            Assert.That(Mathf.Abs(_gm.Board.transform.position.y - g.Height), Is.LessThan(0.15f), "transform follows the simulated ground");
            Assert.That(Mathf.Abs(_gm.Run.LateralOffset), Is.LessThan(1f));
            Assert.That(_gm.Run.Distance, Is.GreaterThan(20f), "board should roll downhill");
        }

        [UnityTest]
        public IEnumerator FiveLeftOneRight_TurnsLeft()
        {
            Start(RoadMode.TestTrack);
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
            Start(RoadMode.TestTrack);
            Pin("LLLLLL");
            float t = 0f;
            while (!_gm.Board.State.Crashed && t < 4f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(_gm.Board.State.Crashed, "should crash within 4 s");
            UnpinAll();
            yield return new WaitForSeconds(_gm.Tuning.data.crashRestartDelay + 0.5f);
            BoardSimulation sim = _gm.Board.Simulation;
            Assert.IsFalse(sim.Board.State.Crashed, "should have respawned");
            for (int i = 0; i < sim.ActivePlayerCount; i++) Assert.IsTrue(sim.Players[i].IsOnBoard, "player back on board");
            Assert.That(Mathf.Abs(_gm.Run.LateralOffset), Is.LessThan(1f), "respawned on the road");
        }

        [UnityTest]
        public IEnumerator Ramp_LaunchesAndLandsWithoutCrash()
        {
            Start(RoadMode.TestTrack);
            Pin("CCCCCC");
            _gm.Board.PlaceOnRoad(TestRoadLayout.RampDistance - 40f, 18f);
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
            Start(RoadMode.TestTrack, bots: true);
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
            Start(RoadMode.TestTrack);
            Pin("RC");
            yield return new WaitForSeconds(3f);
            int visible = 0;
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
                if (view.transform.Find("Pose").gameObject.activeSelf) visible++;
            Assert.AreEqual(2, visible);
            Assert.That(_gm.Board.State.YawRate, Is.GreaterThan(0f), "edge + centre turns right");
            Assert.IsFalse(_gm.Board.State.Crashed);
        }

        [UnityTest]
        public IEnumerator LastLife_EndsRun_ShowsEndScreen_RestartStartsFresh()
        {
            var tuning = BoardTuning.CreateDefault();
            tuning.data.livesPerRun = 1;
            _gm = GameManager.Create(tuning, 6, bots: false, keyboard: false, mode: RoadMode.TestTrack);
            Pin("CCCCCC");
            yield return new WaitForSeconds(3f);
            Assert.That(_gm.Board.Run.Score.Score, Is.GreaterThan(0f), "distance scores");
            _gm.Board.Simulation.CrashNow();
            float t = 0f;
            while (_gm.Board.Run.State != RunState.Ended && t < 6f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(RunState.Ended, _gm.Board.Run.State, "no lives left: run over");
            Assert.IsTrue(_gm.Hud.Visible);
            _gm.Run.RestartRun();
            yield return null;
            Assert.AreEqual(RunState.Running, _gm.Board.Run.State);
            Assert.AreEqual(0f, _gm.Board.Run.Score.Score, 1e-3);
        }

        /// <summary>
        /// M4 acceptance 2: six riders on the endless road with the full art, 30 s. Logs frame time and (in the
        /// editor) batches / SetPass calls / triangles from UnityStats. Editor numbers include the editor; profile a
        /// player build for the real 60 fps check (skill: performance-optimization, "profile a release build").
        /// </summary>
        [UnityTest]
        public IEnumerator M4_SixRiders_RenderStatsReport()
        {
            Start(RoadMode.Endless, players: 6, bots: true);
            _gm.InputRouter.CyclePreset(); // all cooperative
            yield return new WaitForSeconds(2f); // let the road and pools warm up
            var frameMs = new List<float>(4000);
            long batches = 0, setPass = 0, tris = 0;
            int maxBatches = 0, samples = 0;
            float t = 0f;
            while (t < 30f)
            {
                t += Time.deltaTime;
                frameMs.Add(Time.unscaledDeltaTime * 1000f);
#if UNITY_EDITOR
                batches += UnityEditor.UnityStats.batches;
                setPass += UnityEditor.UnityStats.setPassCalls;
                tris += UnityEditor.UnityStats.triangles;
                maxBatches = Mathf.Max(maxBatches, UnityEditor.UnityStats.batches);
                samples++;
#endif
                yield return null;
            }
            frameMs.Sort();
            float mean = 0f; foreach (float f in frameMs) mean += f; mean /= frameMs.Count;
            int n = Mathf.Max(samples, 1);
            Debug.Log($"M4 render stats, 6 riders, 30 s: frame mean {mean:0.0} ms, p95 {frameMs[(int)(frameMs.Count * 0.95f)]:0.0} ms, " +
                      $"max {frameMs[frameMs.Count - 1]:0.0} ms | batches avg {batches / n} max {maxBatches}, SetPass avg {setPass / n}, " +
                      $"triangles avg {tris / n:N0} | active chunks {_gm.RoadView.ActiveViews}, obstacles {_gm.Board.Run.Obstacles.ActiveCount}");
            Assert.That(_gm.Run.Distance, Is.GreaterThan(300f));
#if UNITY_EDITOR
            Assert.That(maxBatches, Is.LessThan(1500), "draw-call ceiling (generous; see the logged average)");
#endif
        }

        /// <summary>Shipped flow: title over an attract ride, lobby join, countdown, pause freezes, resume continues.</summary>
        [UnityTest]
        public IEnumerator Frontend_TitleLobbyCountdownPauseResume()
        {
            _gm = GameManager.Create(BoardTuning.CreateDefault(), 1, bots: true, keyboard: true, mode: RoadMode.Endless, seed: 3, countdown: true, frontend: true);
            yield return new WaitForSeconds(2f);
            FrontendController fe = _gm.Frontend;
            Assert.IsTrue(fe.InMenu, "starts on the title screen");
            Assert.AreEqual(Game.Frontend.MenuScreen.Title, fe.Model.Screen);
            Assert.IsFalse(_gm.Hud.Visible, "no HUD behind the title");
            Assert.That(_gm.Run.Distance, Is.GreaterThan(5f), "the attract ride rolls behind the title");

            fe.Model.Lobby.Join(Game.Frontend.JoinedDevice.Keyboard(false));
            fe.Model.Lobby.CrewSize = 4;
            fe.StartRun();
            Assert.IsFalse(fe.InMenu);
            Assert.AreEqual(4, _gm.Board.Simulation.ActivePlayerCount, "1 human + 3 bots");
            Assert.IsTrue(_gm.InputRouter.IsHumanControlled(0));
            Assert.IsTrue(_gm.Board.Frozen, "3-2-1 countdown holds the board");
            yield return new WaitForSeconds(RunManager.CountdownSeconds + 0.5f);
            Assert.IsFalse(_gm.Board.Frozen, "GO!");

            fe.Pause();
            float d = _gm.Run.Distance;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(d, _gm.Run.Distance, 1e-3, "paused: nothing moves");
            fe.Resume();
            yield return new WaitForSeconds(1f);
            Assert.That(_gm.Run.Distance, Is.GreaterThan(d), "resumed");
        }

        [UnityTest]
        public IEnumerator Endless_ViewsFollowSimulation_FrameTimeAndGcReport()
        {
            Start(RoadMode.Endless, bots: true);
            _gm.InputRouter.CyclePreset(); // all cooperative: they follow the planned line
            var frameMs = new List<float>(4000);
            int gcBefore = System.GC.CollectionCount(0);
            float t = 0f;
            while (t < 40f)
            {
                t += Time.deltaTime;
                frameMs.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
                Assert.AreEqual(_gm.Board.Run.Road.Chunks.Count, _gm.RoadView.ActiveViews, "one chunk view per spawned chunk");
            }
            int gcCollections = System.GC.CollectionCount(0) - gcBefore;
            frameMs.Sort();
            float mean = 0f; foreach (float f in frameMs) mean += f; mean /= frameMs.Count;
            Debug.Log($"endless 40 s: {_gm.Run.Distance:0} m, frames {frameMs.Count}, frame time mean {mean:0.0} ms, " +
                      $"p99 {frameMs[(int)(frameMs.Count * 0.99f)]:0.0} ms, max {frameMs[frameMs.Count - 1]:0.0} ms, gen0 GCs {gcCollections} " +
                      $"(editor numbers include the editor itself)");
            Assert.That(_gm.Run.Distance, Is.GreaterThan(300f), "the run should progress");
            Assert.IsFalse(_gm.Board.State.Crashed && _gm.Board.Run.Crashes > 3, "cooperative bots should not crash repeatedly");
        }
    }
}
