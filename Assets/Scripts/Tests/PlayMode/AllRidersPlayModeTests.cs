using System.Collections;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SVector2 = System.Numerics.Vector2;

namespace Game.Tests
{
    public sealed class AllRidersPlayModeTests
    {
        GameManager _gm;

        sealed class Inputs : IPlayerInputProvider
        {
            public SVector2 Move;
            public bool Jump;
            public void CollectInputs(BoardSimulation simulation, PlayerInputState[] into, float dt)
            {
                for (int i = 0; i < simulation.ActivePlayerCount; i++)
                    into[i] = new PlayerInputState(Move, Jump);
                Jump = false;
            }
        }

        [SetUp]
        public void SetUp() => _gm = GameManager.Create(BoardTuning.CreateDefault(), 6, bots: false, keyboard: false);

        [TearDown]
        public void TearDown()
        {
            if (_gm != null && _gm.CameraRig != null) Object.Destroy(_gm.CameraRig.gameObject);
            if (_gm != null) Object.Destroy(_gm.gameObject);
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
        }

        PlayerView[] Views()
        {
            var views = new PlayerView[6];
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) views[view.Slot] = view;
            for (int i = 0; i < views.Length; i++) Assert.IsNotNull(views[i], "Missing player " + (i + 1));
            return views;
        }

        [UnityTest]
        public IEnumerator SixImportedRiders_HaveDistinctArtAndFollowWalkingAndJumping()
        {
            yield return null;
            var views = Views();
            var starts = new SVector2[6];
            var rotations = new Quaternion[6];
            for (int i = 0; i < views.Length; i++)
            {
                var rig = views[i].GetComponentInChildren<RiderArtRig>();
                Assert.IsNotNull(rig, "Imported art missing for P" + (i + 1));
                Assert.AreEqual(RiderArtRig.AssetNames[i] + "(Clone)", rig.name);
                Assert.AreEqual(6, rig.GetComponentsInChildren<MeshRenderer>().Length);
                foreach (Renderer renderer in rig.GetComponentsInChildren<Renderer>())
                    foreach (Material material in renderer.sharedMaterials)
                        Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                starts[i] = _gm.Board.Simulation.Players[i].LocalPosition;
                rotations[i] = rig.legLeft.localRotation;
            }
            var inputs = new Inputs { Move = new SVector2(0f, 0.45f) };
            _gm.Board.SetInputProvider(inputs);
            yield return new WaitForSeconds(0.35f);
            for (int i = 0; i < views.Length; i++)
            {
                Assert.Greater(_gm.Board.Simulation.Players[i].LocalPosition.Y, starts[i].Y + 0.1f);
                Assert.Greater(Quaternion.Angle(rotations[i], views[i].GetComponentInChildren<RiderArtRig>().legLeft.localRotation), 0.5f);
            }
            inputs.Move = SVector2.Zero;
            inputs.Jump = true;
            yield return new WaitForSeconds(0.15f);
            for (int i = 0; i < views.Length; i++)
            {
                Assert.IsTrue(_gm.Board.Simulation.Players[i].IsAirborne, "P" + (i + 1));
                Assert.Greater(views[i].transform.localPosition.y, 0.05f);
                Assert.AreSame(_gm.BoardView.DeckTop, views[i].transform.parent);
            }
        }

        [UnityTest]
        public IEnumerator SixImportedRiders_FallAndRespawnWithoutLosingTheirRig()
        {
            yield return null;
            var views = Views();
            var rigs = new RiderArtRig[6];
            for (int i = 0; i < views.Length; i++)
            {
                rigs[i] = views[i].GetComponentInChildren<RiderArtRig>();
                Assert.IsNotNull(rigs[i]);
                _gm.Board.Simulation.Players[i].FallOff(_gm.Tuning.data.respawnDelay);
            }
            yield return new WaitForSeconds(0.1f);
            foreach (var view in views) Assert.IsNull(view.transform.parent);
            yield return new WaitForSeconds(_gm.Tuning.data.respawnDelay + 0.15f);
            for (int i = 0; i < views.Length; i++)
            {
                Assert.IsTrue(_gm.Board.Simulation.Players[i].IsOnBoard);
                Assert.AreSame(_gm.BoardView.DeckTop, views[i].transform.parent);
                Assert.AreSame(rigs[i], views[i].GetComponentInChildren<RiderArtRig>());
                Assert.IsTrue(views[i].transform.Find("Pose").gameObject.activeSelf);
                Assert.Less(Vector2.Distance(new Vector2(views[i].transform.localPosition.x, views[i].transform.localPosition.z),
                    _gm.Board.Simulation.Players[i].LocalPosition.ToUnity()), 0.08f);
            }
        }
    }
}
