using UnityEngine;

namespace Game
{
    /// <summary>Original procedural audio and bounded particle feedback; no downloaded assets.</summary>
    public sealed class RunFeedback : MonoBehaviour
    {
        BoardController _board; RunManager _run;
        AudioSource _effects, _rolling, _wind;
        AudioClip _gem, _hit, _loop;
        ParticleSystem _sparks;
        Material _particleMaterial;
        public bool Muted { get; set; }
        public void Initialize(BoardController board, RunManager run)
        {
            _board = board; _run = run;
            board.gameObject.AddComponent<WheelFeedback>().Initialize(board.transform, board, run);
            board.gameObject.AddComponent<NitroWake>().Initialize(board,run);
            if (FindFirstObjectByType<AudioListener>() == null) board.gameObject.AddComponent<AudioListener>();
            _effects = gameObject.AddComponent<AudioSource>(); _effects.playOnAwake = false;
            _rolling = gameObject.AddComponent<AudioSource>(); _rolling.playOnAwake = false; _rolling.loop = true;
            _gem = Tone("Diamond chime", .28f, true); _hit = Tone("Impact", .22f, false); _loop = NoiseLoop();
            _rolling.clip = _loop; _rolling.Play();
            _wind=gameObject.AddComponent<AudioSource>();_wind.playOnAwake=false;_wind.loop=true;_wind.clip=_loop;_wind.pitch=1.9f;_wind.Play(); _run.Feedback += Feedback; _run.Pickup += Pickup; _run.NitroStarted += Boost;
            var go = new GameObject("Pickup sparks"); go.transform.SetParent(transform, false);
            _sparks = go.AddComponent<ParticleSystem>(); _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _sparks.main; main.loop = false; main.duration = .3f; main.startLifetime = .55f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f); main.startSize = new ParticleSystem.MinMaxCurve(.06f, .16f); main.maxParticles = 120; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _sparks.emission; emission.enabled = false;
            var shape = _sparks.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .5f;
            var fade = _sparks.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); fade.color = gradient;
            var source = Resources.Load<Material>("Art/Feedback/Sparks");
            _particleMaterial = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            _sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = _particleMaterial;
        }
        void Update()
        {
            if (_rolling == null) return;
            _rolling.volume = Muted || !_run.AcceptsGameplay ? 0f : Mathf.Clamp01(_board.State.Speed / 35f) * .09f;
            _rolling.pitch = .6f + _board.State.Speed / 35f;
            _effects.mute = Muted;
            _wind.volume=Muted||!_run.AcceptsGameplay?0:Mathf.InverseLerp(10,35,_board.State.Speed)*(_run.NitroActive?.12f:.045f);
        }
        void Feedback(bool gem)
        {
            _effects.pitch = gem ? 1f + (_run.Combo - 1) * .08f : 1f;
            _effects.PlayOneShot(gem ? _gem : _hit, gem ? .3f : .4f);
            _sparks.transform.position = _board.transform.position + Vector3.up * 1.4f;
            var main = _sparks.main; main.startColor = gem ? new Color(.86f,.35f,1f) : new Color(1f,.67f,.2f);
            _sparks.Emit(gem ? 16 : 30);
        }
        void Pickup(ArcadePickupKind kind)
        {
            _effects.pitch=kind==ArcadePickupKind.Coin?1.65f:.7f;
            _effects.PlayOneShot(_gem,.22f);
            _sparks.transform.position=_board.transform.position+Vector3.up*1.4f;
            var main=_sparks.main; main.startColor=kind==ArcadePickupKind.Coin?new Color(1,.75f,.1f):Color.cyan;
            _sparks.Emit(kind==ArcadePickupKind.Coin?8:25);
        }
        void Boost(){Pickup(ArcadePickupKind.Nitro);}
        static AudioClip Tone(string name, float seconds, bool chime)
        {
            const int rate = 22050; var data = new float[(int)(rate * seconds)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate, decay = Mathf.Exp(-t * (chime ? 13f : 22f));
                float frequency = chime ? (t < .09f ? 880f : 1320f) : 95f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * frequency * t) + .3f * Mathf.Sin(4f * Mathf.PI * frequency * t)) * decay * .45f * Mathf.Min(1f, t * 250f);
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        static AudioClip NoiseLoop()
        {
            var rng = new System.Random(42); var data = new float[22050]; float smooth = 0;
            for (int i = 0; i < data.Length; i++) { smooth = Mathf.Lerp(smooth, (float)rng.NextDouble() * 2f - 1f, .13f); data[i] = smooth * .4f; }
            // Fade the join to avoid an audible loop seam.
            for (int i = 0; i < 220; i++) { data[i] *= i / 220f; data[data.Length - 1 - i] *= i / 220f; }
            var clip = AudioClip.Create("Wheel rumble", data.Length, 1, 22050, false); clip.SetData(data, 0); return clip;
        }
        void OnDestroy()
        {
            if (_run != null) { _run.Feedback -= Feedback; _run.Pickup -= Pickup; _run.NitroStarted -= Boost; }
            if (_gem != null) Destroy(_gem); if (_hit != null) Destroy(_hit); if (_loop != null) Destroy(_loop);
            if (_particleMaterial != null) Destroy(_particleMaterial);
        }
    }
}
