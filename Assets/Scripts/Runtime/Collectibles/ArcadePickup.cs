using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    public enum ArcadePickupKind { Coin, Nitro }
    public sealed class ArcadePickup : MonoBehaviour
    {
        public ArcadePickupKind Kind { get; private set; }
        Transform _visual;
        bool _collected;
        static Material _gold, _blue;
        static Material Glow(Color color, string name)
        {
            var m = new Material(MaterialLibrary.Get(color)) { name = name };
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            m.SetFloat("_Smoothness", .65f); return m;
        }
        public static ArcadePickup Create(Transform parent, Vector3 position, ArcadePickupKind kind)
        {
            var root = new GameObject(kind.ToString()); root.transform.SetParent(parent,false); root.transform.position=position;
            var pickup=root.AddComponent<ArcadePickup>(); pickup.Kind=kind;
            pickup._visual=new GameObject("Pickup visual").transform; pickup._visual.SetParent(root.transform,false);
            if (_gold==null) _gold=Glow(new Color(1,.62f,.04f),"Coin gold");
            if (_blue==null) _blue=Glow(new Color(.03f,.65f,1),"Nitro electric blue");
            if (kind==ArcadePickupKind.Coin)
            {
                var rim=PrimitiveFactory.Visual(PrimitiveType.Cylinder,pickup._visual,Vector3.zero,new Vector3(.85f,.075f,.85f),Color.yellow,"Gold coin");
                rim.transform.localRotation=Quaternion.Euler(90,0,0); rim.GetComponent<Renderer>().sharedMaterial=_gold;
                for(int side=-1;side<=1;side+=2)
                {
                    var face=PrimitiveFactory.Visual(PrimitiveType.Cylinder,pickup._visual,new Vector3(0,0,side*.08f),new Vector3(.65f,.018f,.65f),new Color(1,.85f,.18f),"Embossed face");
                    face.transform.localRotation=Quaternion.Euler(90,0,0);
                    PrimitiveFactory.Visual(PrimitiveType.Cube,pickup._visual,new Vector3(0,0,side*.11f),new Vector3(.09f,.35f,.03f),new Color(1,.52f,.04f),"Coin stamp");
                }
            }
            else
            {
                var bottle=PrimitiveFactory.Visual(PrimitiveType.Cube,pickup._visual,Vector3.zero,new Vector3(.57f,.85f,.35f),Color.cyan,"Nitro canister");
                bottle.GetComponent<Renderer>().sharedMaterial=_blue;
                PrimitiveFactory.Visual(PrimitiveType.Cube,pickup._visual,new Vector3(0,.51f,0),new Vector3(.3f,.18f,.32f),new Color(.04f,.16f,.25f),"Cap");
                for(int side=-1;side<=1;side+=2)
                    for(int i=0;i<2;i++)
                    {
                        var bolt=PrimitiveFactory.Visual(PrimitiveType.Cube,pickup._visual,new Vector3((i==0?.04f:-.04f),(i==0?.12f:-.12f),side*.19f),new Vector3(.11f,.34f,.035f),Color.white,"Lightning");
                        bolt.transform.localRotation=Quaternion.Euler(0,0,-35);
                    }
                pickup._visual.localRotation=Quaternion.Euler(0,0,-12);
            }
            var trigger=root.AddComponent<SphereCollider>(); trigger.isTrigger=true; trigger.radius=.7f;
            return pickup;
        }
        void Update()
        {
            _visual.Rotate(0,Time.deltaTime*90,0,Space.World);
            _visual.localPosition=Vector3.up*Mathf.Sin(Time.time*3+transform.position.z)*.12f;
        }
        void OnTriggerEnter(Collider other)
        {
            var board=other.GetComponentInParent<BoardController>();
            if(board==null || board.State.Crashed || _collected) return;
            var run=board.GetComponentInParent<RunManager>(); if(run==null || !run.AcceptsGameplay) return;
            _collected=true; run.CollectArcade(Kind,run.NearestRider(transform.position)); gameObject.SetActive(false); Destroy(gameObject);
        }
    }
}
