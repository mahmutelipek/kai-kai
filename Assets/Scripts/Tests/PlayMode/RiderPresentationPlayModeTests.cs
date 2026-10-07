using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class RiderPresentationPlayModeTests
    {
        GameManager _game;
        BoardTuning _tuning;
        [TearDown] public void Cleanup()
        {
            if (_game != null && _game.CameraRig != null) Object.Destroy(_game.CameraRig.gameObject);
            if (_game != null) Object.Destroy(_game.gameObject);
            if (_tuning != null) Object.Destroy(_tuning);
            foreach (var view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
        }

        [UnityTest] public IEnumerator WideBoardClearsAdjacentCrateAndRidersKeepThreeRows()
        {
            _tuning = BoardTuning.CreateDefault();
            _tuning.data.boardWidth = 3.4f; _tuning.data.boardLength = 7.2f;
            _game = GameManager.Create(_tuning, 6, bots:false, keyboard:false, endless:true);
            var sim = _game.Board.Simulation;
            var bot = new Game.Simulation.CooperativeBot(3);
            for (int n = 0; n < 180; n++)
            {
                var inputs = new Game.Simulation.PlayerInputState[6];
                for (int i = 0; i < 6; i++) inputs[i] = bot.Decide(new Game.Simulation.BotContext {
                    Self=i, Players=sim.Players, ActivePlayerCount=6, Board=sim.Board.State, Tuning=_tuning.data, KeepFormation=true, Dt=1f/60f });
                sim.Step(1f/60f, inputs);
            }
            Assert.Greater(sim.Players[0].LocalPosition.Y - sim.Players[4].LocalPosition.Y, 3f, "Cooperative riders must retain separate rows.");
            _game.Board.Restart(Vector3.zero, 0f);
            foreach (var player in sim.Players) player.Pin(System.Numerics.Vector2.Zero);
            RoadHazard.Create(_game.Road.transform, new Vector3(3.5f,0,3),0,true);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(0, _game.Run.Crashes);
            Assert.Greater(_game.Board.State.Speed, _tuning.data.startSpeed * .95f, "A crate in the adjacent lane must leave clearance for the wider deck.");
            foreach (var view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
                Assert.AreEqual(view.transform.Find("Pose").localScale.x, view.transform.Find("Pose").localScale.y, .001f, "Speed must not squash a standing rider.");
        }

        [UnityTest] public IEnumerator CloseCameraPullsInForTreesAndRestoresItsFramingWhenClear()
        {
            _game=GameManager.Create(bots:false,keyboard:false,endless:false);
            _game.Board.enabled=false;
            _game.CameraRig.Snap();
            Vector3 normal=_game.CameraRig.transform.position;
            Vector3 origin=_game.Board.transform.position+Vector3.up*1.2f;
            var blocker=new GameObject("Camera obstruction");blocker.transform.SetParent(_game.transform);blocker.layer=2;
            blocker.transform.position=Vector3.Lerp(origin,normal,.55f);blocker.AddComponent<SphereCollider>().radius=.7f;
            Physics.SyncTransforms();_game.CameraRig.Snap();
            Assert.Less(Vector3.Distance(origin,_game.CameraRig.transform.position),Vector3.Distance(origin,normal)-1f);
            blocker.transform.position+=Vector3.right*50;Physics.SyncTransforms();_game.CameraRig.Snap();
            Assert.Less(Vector3.Distance(normal,_game.CameraRig.transform.position),.01f);
            yield return null;
        }

        [UnityTest] public IEnumerator WheelDustEmitsOnlyDuringPlayAndFreezesOnPause()
        {
            _game = GameManager.Create(endless:true, bots:false, keyboard:false);
            _game.Run.ConfigureSession(true);
            var feedback = _game.Board.GetComponent<WheelFeedback>();
            Assert.IsNotNull(feedback);
            yield return new WaitForSeconds(.2f);
            foreach (var dust in feedback.Dust) Assert.AreEqual(0, dust.particleCount);
            _game.Run.RestartRun();
            foreach (var player in _game.Board.Simulation.Players) player.Pin(System.Numerics.Vector2.Zero);
            yield return new WaitForSeconds(.3f);
            foreach (var dust in feedback.Dust) { Assert.Greater(dust.particleCount,0); Assert.LessOrEqual(dust.particleCount,dust.main.maxParticles); }
            _game.Run.TogglePause(); yield return null;
            float time = feedback.Dust[0].time;
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(feedback.Dust[0].isPaused); Assert.AreEqual(time,feedback.Dust[0].time,.001f);
            _game.Run.RestartRun();
            foreach (var dust in feedback.Dust) Assert.AreEqual(0,dust.particleCount,"Restart must clear the old run's wake.");
        }
    }
}
