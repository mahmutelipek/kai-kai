using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Simulation;
namespace Game.Tests
{
    public sealed class ArcadeContentTests
    {
        GameManager _gm;
        bool _hadDistance,_hadScore;float _distance;int _score;
        [SetUp] public void PreserveRecords()
        {
            _hadDistance=PlayerPrefs.HasKey("KaiKai.BestDistance");_hadScore=PlayerPrefs.HasKey("KaiKai.BestScore");
            _distance=PlayerPrefs.GetFloat("KaiKai.BestDistance");_score=PlayerPrefs.GetInt("KaiKai.BestScore");
        }
        [TearDown] public void Cleanup()
        {
            if(_gm!=null){if(_gm.CameraRig!=null)Object.Destroy(_gm.CameraRig.gameObject);Object.Destroy(_gm.gameObject);}
            if(_hadDistance)PlayerPrefs.SetFloat("KaiKai.BestDistance",_distance);else PlayerPrefs.DeleteKey("KaiKai.BestDistance");
            if(_hadScore)PlayerPrefs.SetInt("KaiKai.BestScore",_score);else PlayerPrefs.DeleteKey("KaiKai.BestScore");
            PlayerPrefs.Save();
            foreach(var view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None))Object.Destroy(view.gameObject);
        }
        void Create(){_gm=GameManager.Create(BoardTuning.CreateDefault(),6,bots:false,keyboard:false,endless:true);foreach(var p in _gm.Board.Simulation.Players)p.Pin(System.Numerics.Vector2.Zero);}
        [UnityTest] public IEnumerator CoinAndNitroTriggersCountOnceAndRestartClearsResources()
        {
            Create();yield return new WaitForFixedUpdate();
            ArcadePickup.Create(_gm.transform,_gm.Board.transform.position+Vector3.up*.8f,ArcadePickupKind.Coin);
            ArcadePickup.Create(_gm.transform,_gm.Board.transform.position+Vector3.up*.8f,ArcadePickupKind.Nitro);
            Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.AreEqual(1,_gm.Run.Coins);Assert.IsTrue(_gm.Run.NitroReady);Assert.AreEqual(0,_gm.Run.Diamonds);
            yield return new WaitForFixedUpdate();Assert.AreEqual(1,_gm.Run.Coins);
            Assert.IsTrue(_gm.Run.TryActivateNitro());Assert.IsFalse(_gm.Run.TryActivateNitro());
            _gm.Run.RestartRun();Assert.AreEqual(0,_gm.Run.Coins);Assert.AreEqual(0,_gm.Run.NitroCharge);Assert.IsFalse(_gm.Run.NitroActive);Assert.IsFalse(_gm.Board.Simulation.Board.NitroActive);
        }
        [UnityTest] public IEnumerator NitroPausesExpiresAndCannotStartInMenus()
        {
            Create();_gm.Run.ConfigureSession(true);Assert.IsFalse(_gm.Run.TryActivateNitro());
            _gm.Run.CollectArcade(ArcadePickupKind.Nitro);Assert.AreEqual(0,_gm.Run.NitroCharge);
            _gm.Run.RestartRun();_gm.Run.CollectArcade(ArcadePickupKind.Nitro);Assert.IsTrue(_gm.Run.TryActivateNitro());
            _gm.Run.TogglePause();float remaining=_gm.Run.NitroRemaining;
            yield return new WaitForSeconds(.15f);Assert.AreEqual(remaining,_gm.Run.NitroRemaining);Assert.IsFalse(_gm.Run.TryActivateNitro());
            _gm.Run.TogglePause();_gm.Run.RestartRun();_gm.Run.CollectArcade(ArcadePickupKind.Nitro);_gm.Run.TryActivateNitro();
            yield return new WaitForSeconds(3.7f);Assert.IsFalse(_gm.Run.NitroActive);Assert.IsFalse(_gm.Board.Simulation.Board.NitroActive);
        }
        [Test] public void NitroAddsSpeedWithoutSettingSteeringAndRecoversAfterRelease()
        {
            var t=ArcadeHandlingTests.PlayableTuning();var plain=new BoardPhysicsSim();var boost=new BoardPhysicsSim();
            plain.Reset(System.Numerics.Vector3.Zero,0,20);boost.Reset(System.Numerics.Vector3.Zero,0,20);boost.NitroActive=true;
            for(int i=0;i<180;i++){plain.Step(1f/60,new WeightResult(),t,new SlopedPlaneGround(0));boost.Step(1f/60,new WeightResult(),t,new SlopedPlaneGround(0));}
            Assert.Greater(boost.State.Speed,plain.State.Speed+7);Assert.AreEqual(0,boost.State.Yaw);Assert.IsFalse(boost.State.Crashed);
            float peak=boost.State.Speed;boost.NitroActive=false;for(int i=0;i<180;i++)boost.Step(1f/60,new WeightResult(),t,new SlopedPlaneGround(0));Assert.Less(boost.State.Speed,peak-3);
        }
        [UnityTest] public IEnumerator StreamedRampLaunchesAndLandsWithoutCrashing()
        {
            Create();var ramp=Object.FindFirstObjectByType<ArcadeRamp>();Assert.IsNotNull(ramp);
            var road=new UnityGroundProvider();Physics.SyncTransforms();
            var t=ArcadeHandlingTests.PlayableTuning();t.speedRampPerSecond=0;
            var board=new BoardPhysicsSim();Vector3 start=ramp.transform.TransformPoint(new Vector3(0,0,-10));
            var ground=road.Sample(start.x,start.z,start.y+4);start.y=ground.Height;
            board.Reset(start.ToSim(),ramp.transform.eulerAngles.y*Mathf.Deg2Rad,17);
            bool air=false,land=false;float maximum=0;
            for(int i=0;i<180;i++)
            {
                var ev=board.Step(1f/60,new WeightResult(),t,road);air|=ev.LeftGround;land|=air&&ev.Landed;maximum=Mathf.Max(maximum,board.State.Position.Y-ground.Height);
            }
            Assert.IsTrue(air,"The physical ramp must launch the board.");Assert.IsTrue(land,"The jump must land on the road.");Assert.IsFalse(board.State.Crashed);
            Assert.LessOrEqual(_gm.Road.Streamer.ActiveChunkCount,9);
            _gm.Road.Streamer.Maintain(1600);yield return null;
            Assert.LessOrEqual(Object.FindObjectsByType<ArcadeRamp>(FindObjectsSortMode.None).Length,4);
            Assert.LessOrEqual(Object.FindObjectsByType<ArcadePickup>(FindObjectsSortMode.None).Length,110);
        }
    }
}
