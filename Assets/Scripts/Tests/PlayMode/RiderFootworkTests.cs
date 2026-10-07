using NUnit.Framework;
using UnityEngine;
namespace Game.Tests
{
    public sealed class RiderFootworkTests
    {
        static float Bottom(Transform leg)
        {
            float y=float.PositiveInfinity;
            foreach(var renderer in leg.GetComponentsInChildren<Renderer>())y=Mathf.Min(y,renderer.bounds.min.y);
            return y;
        }
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void WalkingAlternatesRaisedFeetWithoutSinkingAndSettlesWhenStopped(int slot)
        {
            var rider=Object.Instantiate(Resources.Load<GameObject>("Art/Riders/"+RiderArtRig.AssetNames[slot]));
            try
            {
                var rig=rider.GetComponent<RiderArtRig>();
                float leftRest=Bottom(rig.legLeft),rightRest=Bottom(rig.legRight),leftLift=0,rightLift=0;
                Quaternion rest=rig.legLeft.localRotation;
                for(int frame=0;frame<180;frame++)
                {
                    rig.PoseMovement(1.8f,false,0,1f/60f);
                    float left=Bottom(rig.legLeft)-leftRest,right=Bottom(rig.legRight)-rightRest;
                    Assert.GreaterOrEqual(left,-.002f);Assert.GreaterOrEqual(right,-.002f);
                    Assert.Less(Mathf.Min(left,right),.015f,"One shoe must remain planted during each step.");
                    leftLift=Mathf.Max(leftLift,left);rightLift=Mathf.Max(rightLift,right);
                }
                Assert.Greater(leftLift,.08f);Assert.Greater(rightLift,.08f);
                for(int frame=0;frame<60;frame++)rig.PoseMovement(0,false,0,1f/60f);
                Assert.Less(Quaternion.Angle(rest,rig.legLeft.localRotation),.1f);
                Assert.That(Bottom(rig.legLeft),Is.EqualTo(leftRest).Within(.002f));
            }
            finally{Object.DestroyImmediate(rider);}
        }
    }
}
