using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class ReleaseFlowPlayModeTests
    {
        GameManager _game;
        int _previousBest;
        [SetUp] public void Setup() { _previousBest = PlayerPrefs.GetInt("KaiKai.BestScore", 0); }
        [TearDown] public void Cleanup()
        {
            PlayerPrefs.SetInt("KaiKai.BestScore", _previousBest);
            if (_game != null && _game.CameraRig != null) Object.Destroy(_game.CameraRig.gameObject);
            if (_game != null) Object.Destroy(_game.gameObject);
            foreach (var view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
        }
        [UnityTest] public IEnumerator PointerButtonsSelectCrewStartMuteAndReturnToMenu()
        {
            _game = GameManager.Create(endless:true, bots:false, keyboard:false);
            _game.Run.ConfigureSession(true);
            var hud = _game.GetComponent<RunHud>();
            hud.ClickAt(new Vector2(200,475));
            Assert.AreEqual(RunHud.MenuPage.Crew,hud.CurrentMenuPage);
            hud.ClickAt(new Vector2(95,475)); hud.ClickAt(new Vector2(300,585));
            Assert.AreEqual(2,_game.Board.Simulation.ActivePlayerCount);
            Assert.AreEqual(RunPhase.Playing,_game.Run.Phase);
            _game.Run.TogglePause(); hud.ClickAt(new Vector2(800,545));
            Assert.IsTrue(_game.GetComponent<RunFeedback>().Muted);
            hud.ClickAt(new Vector2(800,635));
            Assert.AreEqual(RunPhase.Ready,_game.Run.Phase);
            Assert.IsFalse(_game.Board.enabled);
            yield return null;
        }
        [UnityTest] public IEnumerator MenuSettingsCreditsAndCrewBackKeepSimulationFrozen()
        {
            _game=GameManager.Create(endless:true,bots:false,keyboard:false);_game.Run.ConfigureSession(true);
            var hud=_game.GetComponent<RunHud>();Vector3 start=_game.Board.transform.position;
            Assert.IsNotNull(Resources.Load<Texture2D>("Art/Menu/MenuBackground"));
            hud.ClickAt(new Vector2(200,550));Assert.AreEqual(RunHud.MenuPage.Settings,hud.CurrentMenuPage);
            hud.ClickAt(new Vector2(200,430));Assert.IsTrue(_game.GetComponent<RunFeedback>().Muted);
            hud.ClickAt(new Vector2(200,510));Assert.AreEqual(0,_game.CameraRig.impactShake);
            hud.ClickAt(new Vector2(200,590));Assert.IsFalse(_game.GetComponentInChildren<RunVisualPolish>().SpeedLinesEnabled);
            hud.ClickAt(new Vector2(200,780));hud.ClickAt(new Vector2(200,610));Assert.AreEqual(RunHud.MenuPage.Credits,hud.CurrentMenuPage);
            hud.ClickAt(new Vector2(200,780));hud.ClickAt(new Vector2(200,475));Assert.AreEqual(RunHud.MenuPage.Crew,hud.CurrentMenuPage);
            hud.ClickAt(new Vector2(200,780));Assert.AreEqual(RunHud.MenuPage.Home,hud.CurrentMenuPage);
            yield return new WaitForSeconds(.1f);Assert.AreEqual(start,_game.Board.transform.position);Assert.AreEqual(RunPhase.Ready,_game.Run.Phase);
        }
        [UnityTest] public IEnumerator LeavingTheRoadEndsTheSessionInsteadOfFallingForever()
        {
            _game = GameManager.Create(endless:true, bots:false, keyboard:false);
            _game.Run.ConfigureSession(true); _game.Run.RestartRun();
            foreach (var p in _game.Board.Simulation.Players) p.Pin(System.Numerics.Vector2.Zero);
            _game.Road.Path.Sample(50, out Vector3 position, out float yaw);
            _game.Board.Restart(position + Vector3.right * 20, yaw);
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(RunPhase.Finishing, _game.Run.Phase);
            Assert.IsTrue(_game.Board.State.Crashed);
        }
        [UnityTest] public IEnumerator MenuPauseRestartAndFatalDamageRespectSessionState()
        {
            _game = GameManager.Create(endless:true, bots:false, keyboard:false);
            _game.Run.ConfigureSession(true);
            Vector3 start = _game.Board.transform.position;
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(RunPhase.Ready, _game.Run.Phase);
            Assert.AreEqual(start, _game.Board.transform.position);
            _game.Run.CollectDiamond(); Assert.AreEqual(0,_game.Run.Diamonds);
            _game.Run.RestartRun();
            foreach (var p in _game.Board.Simulation.Players) p.Pin(System.Numerics.Vector2.Zero);
            yield return new WaitForSeconds(.2f);
            Assert.Greater(_game.Run.Distance,0);
            _game.Run.CollectDiamond(); _game.Run.CollectDiamond(); _game.Run.CollectDiamond();
            Assert.AreEqual(3,_game.Run.Combo); Assert.AreEqual(150,_game.Run.GemScore);
            _game.Run.TogglePause();
            var frozen = _game.Board.State.Position;
            yield return new WaitForSeconds(3.2f);
            Assert.AreEqual(frozen,_game.Board.State.Position,"Pause must freeze the authoritative simulation.");
            _game.Run.CollectDiamond(); Assert.AreEqual(3,_game.Run.Diamonds);
            _game.Run.TogglePause();
            _game.Run.CollectDiamond(); Assert.AreEqual(4,_game.Run.Combo,"Pause must preserve the combo timer.");
            _game.Run.HitObstacle(true);
            Assert.AreEqual(1,_game.Run.Hull);
            _game.Run.HitObstacle(true); Assert.AreEqual(1,_game.Run.Hull,"Hit grace prevents duplicate damage.");
            yield return new WaitForSeconds(1.1f);
            _game.Run.HitObstacle(false);
            Assert.AreEqual(RunPhase.Finishing,_game.Run.Phase);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(RunPhase.Results,_game.Run.Phase);
            Assert.IsFalse(_game.Board.enabled);
            _game.Run.RestartRun();
            Assert.AreEqual(RunPhase.Playing,_game.Run.Phase);
            Assert.AreEqual(3,_game.Run.Hull); Assert.AreEqual(0,_game.Run.Score);
            Assert.IsFalse(_game.Board.State.Crashed);
        }
    }
}
