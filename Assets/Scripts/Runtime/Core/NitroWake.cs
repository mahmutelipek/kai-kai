using UnityEngine;
namespace Game
{
    /// <summary>Two bounded blue exhaust trails; emission follows the session and pause state.</summary>
    public sealed class NitroWake : MonoBehaviour
    {
        BoardController _board; RunManager _run; ParticleSystem _jets; Material _material;
        public void Initialize(BoardController board,RunManager run)
        {
            _board=board;_run=run;
            var go=new GameObject("Nitro exhaust"); go.transform.SetParent(board.transform,false);go.transform.localPosition=new Vector3(0,.6f,-board.Tuning.data.HalfLength);
            go.transform.localRotation=Quaternion.Euler(0,180,0);
            _jets=go.AddComponent<ParticleSystem>();_jets.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=_jets.main;main.loop=true;main.startLifetime=.35f;main.startSpeed=8;main.startSize=new ParticleSystem.MinMaxCurve(.15f,.45f);main.startColor=new Color(.05f,.8f,1,.8f);main.maxParticles=90;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=_jets.emission;emission.enabled=false;
            var shape=_jets.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(2,.1f,.1f);
            var fade=_jets.colorOverLifetime;fade.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.cyan,0),new GradientColorKey(new Color(.05f,.2f,1),1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(0,1)});fade.color=g;
            _material=new Material(Resources.Load<Material>("Art/Feedback/Sparks"));var renderer=_jets.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_material;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.08f;
            _jets.Play();_run.Restarted+=Clear;
        }
        void Update()
        {
            if(!_run.AcceptsGameplay){if(_jets.isPlaying)_jets.Pause();return;}
            if(!_jets.isPlaying)_jets.Play();
            if(_run.NitroActive&&!_board.State.Crashed)_jets.Emit(Mathf.CeilToInt(100*Time.deltaTime));
        }
        void Clear(){_jets.Clear();}
        void OnDestroy(){if(_run!=null)_run.Restarted-=Clear;if(_material!=null)Destroy(_material);}
    }
}
