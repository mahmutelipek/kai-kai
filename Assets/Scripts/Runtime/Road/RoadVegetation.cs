using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Game
{
    /// <summary>Five batched meshes per streamed chunk; camera-only tree blockers, no individual grass objects.</summary>
    public sealed class RoadVegetation : MonoBehaviour
    {
        readonly List<Mesh> _meshes=new List<Mesh>();
        sealed class Geometry
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<int> Triangles=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,bool doubleSided=false)
            {
                int v=Vertices.Count;Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);Triangles.Add(v);Triangles.Add(v+1);Triangles.Add(v+2);
                if(doubleSided){Vertices.Add(c);Vertices.Add(b);Vertices.Add(a);Triangles.Add(v+3);Triangles.Add(v+4);Triangles.Add(v+5);}
            }
            public void Crown(Vector3 center,Vector3 scale)
            {
                const int rings=6,sides=9;int start=Vertices.Count;
                for(int r=0;r<=rings;r++)for(int s=0;s<=sides;s++)
                {
                    float phi=r*Mathf.PI/rings,theta=s*Mathf.PI*2/sides;
                    float wobble=1+.08f*Mathf.Sin(s*2.7f+r*1.9f);
                    Vertices.Add(center+Vector3.Scale(new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta))*wobble,scale));
                }
                for(int r=0;r<rings;r++)for(int s=0;s<sides;s++)
                {
                    int a=start+r*(sides+1)+s,b=a+sides+1;
                    Triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});
                }
            }
            public void Trunk(Vector3 basePoint,float height,float radius)
            {
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI*2/7,b=(i+1)*Mathf.PI*2/7;
                    Vector3 p=basePoint+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),q=basePoint+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
                    Vector3 top=basePoint+Vector3.up*height+new Vector3(.18f,0,.1f);
                    Vector3 u=top+(p-basePoint)*.65f,v=top+(q-basePoint)*.65f;
                    Triangle(p,u,q);Triangle(q,u,v);
                }
            }
        }
        static float Next(System.Random random,float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
        public static void Build(RoadPath path,Transform parent,float start,float end,int index)
        {
            var owner=new GameObject("Batched roadside garden");owner.transform.SetParent(parent,false);
            var component=owner.AddComponent<RoadVegetation>();
            var grass=new Geometry();var flowers=new Geometry();var wood=new Geometry();var leaves=new Geometry();var highlights=new Geometry();
            var random=new System.Random(6907+index*971);
            for(int side=-1;side<=1;side+=2)
            {
                // Keep the entire asphalt and shoulder recovery lane readable and clear.
                for(float d=start+1;d<end-1;d+=1.4f)
                {
                    for(int patch=0;patch<5;patch++)
                    {
                        path.Sample(d+Next(random,-.5f,.5f),out Vector3 center,out float yaw);
                        Vector3 right=SimConvert.YawRight(yaw),forward=SimConvert.YawForward(yaw);
                        Vector3 p=center+right*side*Next(random,7.6f,23.5f)-Vector3.up*.015f;
                        float height=Next(random,.22f,.65f),width=Next(random,.12f,.24f);
                        for(int blade=0;blade<3;blade++)
                        {
                            float angle=blade*Mathf.PI/3;Vector3 across=right*Mathf.Cos(angle)+forward*Mathf.Sin(angle);
                            grass.Triangle(p-across*width,p+Vector3.up*height+forward*.08f,p+across*width,true);
                        }
                        if(patch==0 && random.Next(3)==0)
                        {
                            Vector3 bloom=p+Vector3.up*(height+.06f);
                            flowers.Triangle(bloom-right*.14f,bloom+Vector3.up*.14f,bloom+right*.14f,true);
                            flowers.Triangle(bloom-forward*.14f,bloom+Vector3.up*.14f,bloom+forward*.14f,true);
                        }
                    }
                }
                for(float d=start+9;d<end-5;d+=13)
                {
                    path.Sample(d+Next(random,-2,2),out Vector3 p,out float yaw);
                    p+=SimConvert.YawRight(yaw)*side*Next(random,9.5f,21.5f);
                    float height=Next(random,3.6f,6.3f),radius=Next(random,1.5f,2.3f);
                    // Ignore Raycast keeps this proxy out of gameplay ground probes; only the camera queries layer 2.
                    var blocker=new GameObject("Tree camera blocker");blocker.layer=2;blocker.transform.SetParent(owner.transform,false);
                    blocker.transform.position=p+Vector3.up*(height*.55f);
                    var capsule=blocker.AddComponent<CapsuleCollider>();capsule.radius=radius*.85f;capsule.height=height+radius;
                    wood.Trunk(p,height*.8f,.22f);
                    leaves.Crown(p+Vector3.up*height,new Vector3(radius,height*.37f,radius));
                    leaves.Crown(p+new Vector3(radius*.65f,height*.77f,.15f),new Vector3(radius*.8f,radius*.85f,radius*.8f));
                    highlights.Crown(p+new Vector3(-radius*.58f,height*.9f,-.25f),new Vector3(radius*.7f,radius*.78f,radius*.72f));
                }
            }
            component.Create(grass,"Grass tufts",new Color(.25f,.52f,.10f));
            component.Create(flowers,"Wildflowers",new Color(1,.35f,.57f));
            component.Create(wood,"Tree trunks",new Color(.39f,.2f,.075f));
            component.Create(leaves,"Tree canopies",new Color(.12f,.36f,.045f));
            component.Create(highlights,"Sunlit leaves",new Color(.28f,.50f,.08f));
        }
        void Create(Geometry geometry,string name,Color color)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(geometry.Vertices);mesh.SetTriangles(geometry.Triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();_meshes.Add(mesh);
            var go=PrimitiveFactory.MeshObject(name,mesh,color,transform);
            var renderer=go.GetComponent<Renderer>();
            if(name=="Grass tufts"||name=="Wildflowers")renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        void OnDestroy(){foreach(var mesh in _meshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
