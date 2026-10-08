using System.Collections;
using Game.Frontend;
using Game.Hud;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Drives the title / lobby with a virtual mouse: hover focuses, the controls text is not a button, clicks navigate and start a solo run.</summary>
    public class MenuMouseTests
    {
        GameManager _gm;
        Mouse _mouse;

        [TearDown]
        public void TearDown()
        {
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            Time.timeScale = 1f;
            if (_gm == null) return;
            if (_gm.CameraRig != null) Object.Destroy(_gm.CameraRig.gameObject);
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>()) Object.Destroy(view.gameObject);
            Object.Destroy(_gm.gameObject);
        }

        IEnumerator MoveTo(float x, float guiY)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(x, Screen.height - guiY) });
            yield return null;
            yield return null;
        }

        IEnumerator Click()
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _mouse.position.ReadValue(), buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _mouse.position.ReadValue(), buttons = 0 });
            yield return null;
            yield return null;
        }

        /// <summary>Centre y of menu item <paramref name="index"/>, found by scanning the last drawn layout.</summary>
        static float RowY(FrontendModel m, int index)
        {
            float x = Screen.width * 0.5f;
            for (float y = 0f; y < Screen.height; y += 2f)
                if (FrontendLayout.HitItem(m, x, y, out _) == index) return y + 4f;
            return -1f;
        }

        [UnityTest]
        public IEnumerator Mouse_HoverClickLobbyStart_AndControlsTextIsNotAButton()
        {
            _mouse = InputSystem.AddDevice<Mouse>();
            _gm = GameManager.Create(BoardTuning.CreateDefault(), 1, bots: true, keyboard: true, mode: RoadMode.Endless, seed: 3, countdown: true, frontend: true);
            yield return new WaitForSeconds(1.5f);
            FrontendController fe = _gm.Frontend;
            FrontendModel m = fe.Model;
            Assert.AreEqual(MenuScreen.Title, m.Screen);

            float play = RowY(m, 0), settings = RowY(m, 3);
            Assert.Greater(play, 0f, "PLAY row drawn");
            Assert.Greater(settings, play);

            yield return MoveTo(Screen.width * 0.5f, settings);
            Assert.AreEqual(3, m.Focus, "hover focuses SETTINGS");

            // the controls hint along the bottom is not interactive
            m.SetFocus(0);
            float hintY = Screen.height - 50f;
            Assert.AreEqual(-1, FrontendLayout.HitItem(m, Screen.width * 0.5f, hintY, out _), "no button under the controls text");
            yield return MoveTo(Screen.width * 0.5f, hintY);
            Assert.AreEqual(0, m.Focus, "hovering the controls text does not move the focus");

            yield return MoveTo(Screen.width * 0.5f, play);
            yield return Click();
            Assert.AreEqual(MenuScreen.Lobby, m.Screen, "click PLAY opens the lobby");
            Assert.AreEqual(1, m.Lobby.CrewSize, "default crew is 1");

            int start = System.Array.IndexOf(m.Items, MenuItem.Start);
            yield return MoveTo(Screen.width * 0.5f, RowY(m, start));
            yield return Click();
            Assert.IsFalse(fe.InMenu, "click START begins the run (keyboard rider auto-joins)");
            Assert.AreEqual(1, _gm.Board.Simulation.ActivePlayerCount, "solo run");
            Assert.IsTrue(_gm.InputRouter.IsHumanControlled(0));
        }
    }
}
