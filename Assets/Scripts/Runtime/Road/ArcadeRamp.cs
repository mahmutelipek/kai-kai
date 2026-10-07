using UnityEngine;
using Game.Simulation;
namespace Game
{
    public sealed class ArcadeRamp : MonoBehaviour
    {
        Mesh _mesh;
        public static ArcadeRamp Create(Transform parent, RoadPath path, float distance, float lateral)
        {
            path.Sample(distance,out Vector3 p,out float yaw); path.Sample(distance+8,out Vector3 end,out _);
            float grade=(p.y-end.y)/8f;
            var mesh=PrimitiveFactory.CreateWedge(4.6f,8f,1.15f);
            var go=PrimitiveFactory.MeshObject("Jump Ramp",mesh,MaterialLibrary.RampWood,parent);
            go.transform.SetPositionAndRotation(p+SimConvert.YawRight(yaw)*lateral+Vector3.up*.015f,Quaternion.Euler(Mathf.Atan(grade)*Mathf.Rad2Deg,yaw*Mathf.Rad2Deg,0));
            var ramp=go.AddComponent<ArcadeRamp>(); ramp._mesh=mesh;
            go.AddComponent<MeshCollider>().sharedMesh=mesh; go.AddComponent<GroundSurface>().kind=SurfaceKind.Road;
            for(int i=0;i<3;i++) for(int side=-1;side<=1;side+=2)
            {
                float z=1.7f+i*2f; float x=side*.62f;
                var arrow=PrimitiveFactory.Visual(PrimitiveType.Cube,go.transform,new Vector3(x,1.15f*z/8+.04f,z),new Vector3(1.7f,.045f,.22f),MaterialLibrary.LineYellow,"Ramp arrow");
                arrow.transform.localRotation=Quaternion.Euler(-Mathf.Atan2(1.15f,8)*Mathf.Rad2Deg,side*32,0);
            }
            return ramp;
        }
        void OnDestroy(){if(_mesh!=null){if(Application.isPlaying)Destroy(_mesh);else DestroyImmediate(_mesh);}}
    }
}
