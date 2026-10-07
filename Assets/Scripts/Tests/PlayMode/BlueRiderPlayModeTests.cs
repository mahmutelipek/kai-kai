using System.Collections;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SVector2 = System.Numerics.Vector2;

namespace Game.Tests
{
    public sealed class BlueRiderPlayModeTests
    {
        GameManager _gm;

        sealed class Inputs : IPlayerInputProvider
        {
            public SVector2 Move;
            public bool Jump;
            public void CollectInputs(BoardSimulation simulation, PlayerInputState[] into, float dt)
            {
                into[0] = new PlayerInputState(Move, Jump);
                Jump = false;
            }
        }

        [SetUp]
        public void SetUp() => _gm = GameManager.Create(BoardTuning.CreateDefault(), 2, bots: false, keyboard: false);

        [TearDown]
        public void TearDown()
        {
            if (_gm != null && _gm.CameraRig != null) Object.Destroy(_gm.CameraRig.gameObject);
            if (_gm != null) Object.Destroy(_gm.gameObject);
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
        }

        PlayerView BlueView()
        {
            foreach (PlayerView view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
                if (view.Slot == 0) return view;
            Assert.Fail("P1 view missing.");
            return null;
        }

        [UnityTest]
        public IEnumerator ImportedRider_WalksAndJumpsWithSimulation()
        {
            yield return null;
            PlayerView view = BlueView();
            RiderArtRig rig = view.GetComponentInChildren<RiderArtRig>();
            Assert.IsNotNull(rig, "P1 must use the imported rider, not the fallback.");
            Assert.AreEqual(6, rig.GetComponentsInChildren<MeshRenderer>().Length);
            foreach (Renderer renderer in rig.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.sharedMaterials)
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            Quaternion legStart = rig.legLeft.localRotation;
            var inputs = new Inputs { Move = new SVector2(0f, 0.45f) };
            _gm.Board.SetInputProvider(inputs);
            SVector2 positionStart = _gm.Board.Simulation.Players[0].LocalPosition;
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(_gm.Board.Simulation.Players[0].LocalPosition.Y, positionStart.Y + 0.1f);
            Assert.Greater(Quaternion.Angle(legStart, rig.legLeft.localRotation), 0.5f, "Walking must animate the imported leg.");
            inputs.Move = SVector2.Zero;
            inputs.Jump = true;
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(_gm.Board.Simulation.Players[0].IsAirborne);
            Assert.Greater(view.transform.localPosition.y, 0.05f, "View must follow the simulation jump height.");
            Assert.AreSame(_gm.BoardView.DeckTop, view.transform.parent);
        }

        [UnityTest]
        public IEnumerator ImportedRider_FallsAndRespawnsWithRigIntact()
        {
            yield return null;
            PlayerView view = BlueView();
            RiderArtRig rig = view.GetComponentInChildren<RiderArtRig>();
            Assert.IsNotNull(rig);
            _gm.Board.Simulation.Players[0].FallOff(_gm.Tuning.data.respawnDelay);
            yield return new WaitForSeconds(0.1f);
            Assert.IsNull(view.transform.parent, "Fallen rider must detach from the deck.");
            yield return new WaitForSeconds(_gm.Tuning.data.respawnDelay + 0.15f);
            Assert.IsTrue(_gm.Board.Simulation.Players[0].IsOnBoard);
            Assert.AreSame(_gm.BoardView.DeckTop, view.transform.parent);
            Assert.IsTrue(view.transform.Find("Pose").gameObject.activeSelf);
            Assert.AreSame(rig, view.GetComponentInChildren<RiderArtRig>());
            Assert.Less(Vector2.Distance(new Vector2(view.transform.localPosition.x, view.transform.localPosition.z),
                _gm.Board.Simulation.Players[0].LocalPosition.ToUnity()), 0.08f);
        }
    }
}
