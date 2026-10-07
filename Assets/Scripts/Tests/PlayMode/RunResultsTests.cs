using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class RunResultsTests
    {
        GameManager _gm;
        [TearDown]public void Cleanup(){if(_gm!=null){Object.Destroy(_gm.CameraRig.gameObject);Object.Destroy(_gm.gameObject);}foreach(var v in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None))Object.Destroy(v.gameObject);}
        [Test]public void RankedPodiumUsesRealPointsAndStableTiesAndFreezesTheSnapshot()
        {
            var scores=new RunLeaderboard();for(int i=0;i<6;i++)scores.Travel(i,100);
            scores.SetSteamIdentity(4,76561198000000001UL,"Violet");scores.Award(4,100);scores.Award(2,50);scores.Award(1,50);scores.Finish(6);
            Assert.AreEqual(4,scores.Results[0].Slot);Assert.AreEqual("Violet",scores.Results[0].Nick);Assert.AreEqual(76561198000000001UL,scores.Results[0].SteamId);Assert.AreEqual(200,scores.Results[0].Points);
            Assert.AreEqual(1,scores.Results[1].Slot);Assert.AreEqual(2,scores.Results[2].Slot);
            scores.Award(0,999);Assert.AreEqual(4,scores.Results[0].Slot);Assert.AreEqual(100,scores.Results[3].Points);
            scores.Reset();Assert.AreEqual("Violet",scores.Nick(4));Assert.AreEqual(0,scores.Score(4));Assert.AreEqual(0,scores.Results.Length);
        }
        [TestCase(2)][TestCase(3)][TestCase(6)]public void ResultsOnlyIncludeActiveCrewAndEmptyNicksHaveFallback(int count)
        {
            var scores=new RunLeaderboard();scores.SetSteamIdentity(0,0," ");scores.Finish(count);Assert.AreEqual(count,scores.Results.Length);Assert.AreEqual("Yerel oyuncu",scores.Results[0].Nick);
        }
        [UnityTest]public IEnumerator ActualPickupAttributionAndFatalCrashFreezeIndividualRanking()
        {
            _gm=GameManager.Create(endless:true,bots:false,keyboard:false);_gm.Run.ConfigureSession(true);_gm.Run.RestartRun();
            var p=_gm.Board.Simulation.Players[3];var position=_gm.Board.transform.TransformPoint(new Vector3(p.LocalPosition.X,_gm.Tuning.data.deckHeight,p.LocalPosition.Y));
            Assert.AreEqual(3,_gm.Run.NearestRider(position));
            _gm.Run.CollectDiamond(3);_gm.Run.CollectArcade(ArcadePickupKind.Coin,3);_gm.Run.CollectArcade(ArcadePickupKind.Nitro,4);
            Assert.AreEqual(35,_gm.Run.Leaderboard.Score(3));Assert.AreEqual(30,_gm.Run.Leaderboard.Score(4));
            _gm.Board.ApplyImpact(ImpactSeverity.Crash,10);Assert.AreEqual(6,_gm.Run.Leaderboard.Results.Length);Assert.AreEqual(3,_gm.Run.Leaderboard.Results[0].Slot);
            _gm.Run.CollectDiamond(0);Assert.AreEqual(35,_gm.Run.Leaderboard.Results[0].Points);
            _gm.Run.ShowMenu();Assert.AreEqual(0,_gm.Run.Leaderboard.Results.Length);yield return null;
        }
    }
}
