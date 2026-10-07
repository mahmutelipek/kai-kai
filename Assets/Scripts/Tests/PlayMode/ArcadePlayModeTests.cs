using System.Collections;
using Game.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class ArcadePlayModeTests
    {
        GameManager _game; BoardTuning _tuning;
        [TearDown] public void Cleanup()
        {
            if (_game != null && _game.CameraRig != null) Object.Destroy(_game.CameraRig.gameObject);
            if (_game != null) Object.Destroy(_game.gameObject);
            if (_tuning != null) Object.Destroy(_tuning);
            foreach (var view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) Object.Destroy(view.gameObject);
        }
        [UnityTest] public IEnumerator FallenRiderLandsAboveTheRoadInsteadOfPassingThroughIt()
        {
            _tuning = BoardTuning.CreateDefault(); _tuning.data = ArcadeHandlingTests.PlayableTuning();
            _tuning.data.respawnDelay = 3;
            _game = GameManager.Create(_tuning,6,bots:false,keyboard:false,endless:true);
            foreach (var player in _game.Board.Simulation.Players) player.Pin(System.Numerics.Vector2.Zero);
            yield return null;
            PlayerView rider = null;
            foreach (var view in Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)) if (view.Slot==0) rider=view;
            _game.Board.Simulation.Players[0].FallOff(3);
            var ground = new UnityGroundProvider(); bool touched = false;
            float duration = 0;
            while (duration < 1.7f)
            {
                yield return null; duration += Time.deltaTime;
                if (rider.transform.Find("Pose").gameObject.activeSelf)
                {
                    float minY = float.PositiveInfinity;
                    foreach (var body in rider.GetComponentsInChildren<Renderer>())
                        if (body.name != "YouMarker") minY=Mathf.Min(minY,body.bounds.min.y);
                    var floor=ground.Sample(rider.transform.position.x,rider.transform.position.z,rider.transform.position.y+4);
                    if (!floor.Found) continue;
                    Assert.GreaterOrEqual(minY,floor.Height+.015f,"A rotated body part must never clip through asphalt.");
                    touched |= minY-floor.Height < .1f;
                }
            }
            Assert.IsTrue(touched,"The falling rider must reach and settle onto the road.");
        }

        [UnityTest] public IEnumerator EndlessRoadContainsVisibleChangingGradesAndModerateBends()
        {
            _game = GameManager.Create(endless:true,bots:false,keyboard:false);
            var path=_game.Road.Path; float min=1,max=0; bool curved=false;
            for (int i=1;i<path.Count;i++)
            {
                Vector3 delta=path.PointAt(i)-path.PointAt(i-1);
                float grade=-delta.y/new Vector2(delta.x,delta.z).magnitude;
                min=Mathf.Min(min,grade); max=Mathf.Max(max,grade);
                curved |= Mathf.Abs(path.YawAt(i)-path.YawAt(i-1)) > .009f;
            }
            Assert.Greater(max,.18f); Assert.Less(min,.1f); Assert.IsTrue(curved);
            Assert.LessOrEqual(max,.24f);
            yield return null;
        }
    }
}
