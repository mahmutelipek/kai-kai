using System;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ArcadeHandlingTests
    {
        public static BoardTuningData PlayableTuning()
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<BoardTuning>("Assets/Settings/BoardTuning.asset");
            Assert.IsNotNull(asset);
            return asset.data.Clone();
#else
            throw new InvalidOperationException("This regression uses the release tuning asset in the editor.");
#endif
        }

        [TestCase(1f,0f)] [TestCase(-1f,0f)] [TestCase(0f,1f)] [TestCase(1f,1f)]
        public void SupportedCrewCanHoldTheEdgeWithoutImmediateFallsOrOverlap(float x,float z)
        {
            var t = PlayableTuning(); var sim = new BoardSimulation(t,new SlopedPlaneGround(0),6);
            var inputs = new PlayerInputState[6];
            for (int i = 0; i < 6; i++) inputs[i] = new PlayerInputState(new Vector2(x,z));
            for (int n = 0; n < 240; n++) sim.Step(1f/60f,inputs);
            foreach (var player in sim.Players)
            {
                Assert.IsTrue(player.IsOnBoard); Assert.AreEqual(0,player.FallCount);
                Assert.LessOrEqual(Math.Abs(player.LocalPosition.X),t.HalfWidth);
                Assert.LessOrEqual(Math.Abs(player.LocalPosition.Y),t.HalfLength);
            }
            Assert.Greater(PlayerCrowdSolver.MinimumSeparation(sim.Players,6,t),t.playerRadius*2*.95f);
            Assert.IsFalse(sim.Board.State.Crashed);
        }

        [Test] public void DriftHasBoundedSlipAndRecoversAfterRelease()
        {
            var t = PlayableTuning(); t.speedRampPerSecond = 0; t.startSpeed = 22;
            var board = new BoardPhysicsSim(); board.Reset(Vector3.Zero,0,22);
            var ground = new SlopedPlaneGround(0); var weight = new WeightResult {RawSteering=.55f};
            for (int n = 0; n < 90; n++) board.Step(1f/60f,weight,t,ground,true);
            float slip = Math.Abs(SimMath.WrapAngle(board.State.Yaw-board.State.TravelYaw))*SimMath.Rad2Deg;
            Assert.Greater(board.State.DriftAmount,.8f); Assert.Greater(slip,5f); Assert.LessOrEqual(slip,18.01f);
            Assert.IsFalse(board.State.Crashed);
            for (int n = 0; n < 90; n++) board.Step(1f/60f,new WeightResult(),t,ground,false);
            Assert.Less(board.State.DriftAmount,.01f);
            Assert.Less(Math.Abs(SimMath.WrapAngle(board.State.Yaw-board.State.TravelYaw))*SimMath.Rad2Deg,2f);
        }

        [Test] public void CooperativeCrewSupportsAnEdgeLeaningPlayerWithWeightAndDrift()
        {
            var t=PlayableTuning(); t.startSpeed=20;
            var sim=new BoardSimulation(t,new SlopedPlaneGround(0),6);
            var bots=new BotBrain[6]; for (int i=1;i<6;i++) bots[i]=new CooperativeBot(i);
            for (int n=0;n<120;n++)
            {
                var input=new PlayerInputState[6]; input[0]=new PlayerInputState(Vector2.UnitX,drift:true);
                for (int i=1;i<6;i++) input[i]=bots[i].Decide(new BotContext {
                    Self=i,Players=sim.Players,ActivePlayerCount=6,Board=sim.Board.State,Tuning=t,KeepFormation=true,SteerHint=.55f,Dt=1f/60f});
                sim.Step(1f/60f,input);
            }
            Assert.IsTrue(sim.Players[0].IsOnBoard);
            Assert.Greater(sim.Board.State.Steering,.35f);
            Assert.Greater(sim.Board.State.DriftAmount,.6f);
            Assert.IsFalse(sim.Board.State.Crashed);
        }

        [Test] public void DownhillGradeAddsSpeedComparedWithFlatRoad()
        {
            var t = PlayableTuning(); var flat = new BoardPhysicsSim(); var slope = new BoardPhysicsSim();
            flat.Reset(Vector3.Zero,0,8); slope.Reset(Vector3.Zero,0,8);
            for (int n = 0; n < 240; n++)
            {
                flat.Step(1f/60f,new WeightResult(),t,new SlopedPlaneGround(0));
                slope.Step(1f/60f,new WeightResult(),t,new SlopedPlaneGround(.2f));
            }
            Assert.Greater(slope.State.Speed,flat.State.Speed+3f);
            Assert.IsTrue(slope.State.Grounded); Assert.IsFalse(slope.State.Crashed);
        }
    }
}
