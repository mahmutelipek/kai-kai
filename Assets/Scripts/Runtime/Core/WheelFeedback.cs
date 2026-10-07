using UnityEngine;

namespace Game
{
    /// <summary>Four contact emitters and short lived skid ribbons, bounded for endless play.</summary>
    public sealed class WheelFeedback : MonoBehaviour
    {
        readonly ParticleSystem[] _dust = new ParticleSystem[4];
        readonly ParticleSystem[] _grit = new ParticleSystem[4];
        readonly TrailRenderer[] _marks = new TrailRenderer[4];
        Transform[] _wheels;
        BoardController _board;
        RunManager _run;
        Material _material;
        Vector3 _lastPosition;
        public ParticleSystem[] Dust => _dust;

        public void Initialize(Transform board, BoardController controller = null, RunManager run = null)
        {
            _board = controller; _run = run;
            if (_run != null) _run.Restarted += Clear;
            _lastPosition = board.position;
            _material = new Material(Resources.Load<Material>("Art/Feedback/Sparks"));
            _wheels = new Transform[4];
            foreach (Transform child in board.GetComponentsInChildren<Transform>())
                for (int i = 0; i < 4; i++) if (child.name == "WheelPivot" + i) _wheels[i] = child;
            for (int i = 0; i < 4; i++)
            {
                _dust[i] = MakeParticles("Wheel dust " + i, false);
                _grit[i] = MakeParticles("Wheel grit " + i, true);
                var go = new GameObject("Skid ribbon " + i); go.transform.SetParent(transform, false);
                var trail = go.AddComponent<TrailRenderer>(); _marks[i] = trail;
                trail.sharedMaterial = _material; trail.time = 1.6f; trail.minVertexDistance = .2f;
                trail.widthMultiplier = .16f; trail.emitting = false;
                trail.startColor = new Color(.12f, .11f, .1f, .42f); trail.endColor = new Color(.12f, .11f, .1f, 0);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        ParticleSystem MakeParticles(string name, bool grit)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false;
            main.startLifetime = grit ? new ParticleSystem.MinMaxCurve(.18f, .38f) : new ParticleSystem.MinMaxCurve(.35f, .75f);
            main.startSize = grit ? new ParticleSystem.MinMaxCurve(.018f, .045f) : new ParticleSystem.MinMaxCurve(.18f, .38f);
            main.startSpeed = 0; main.maxParticles = grit ? 70 : 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = grit ? new Color(1f,.88f,.64f,.8f) : new Color(.9f,.87f,.81f,.38f);
            main.gravityModifier = grit ? .3f : -.025f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .06f;
            var emission = ps.emission; emission.rateOverTime = 0;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, grit ? 1 : .45f, 1, grit ? .2f : 2f));
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.08f), new GradientAlphaKey(0,1) }); color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (grit) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 2.5f; renderer.velocityScale = .035f; }
            return ps;
        }

        Vector3 Contact(int i) => _wheels[i].position - transform.up * (_board != null ? _board.Tuning.data.wheelRadius - .035f : .315f);

        void LateUpdate()
        {
            if (_board == null) return;
            bool paused = _run != null && _run.Phase == RunPhase.Paused;
            bool active = _run == null || _run.AcceptsGameplay;
            float speed = active && _board.State.Grounded && !_board.State.Crashed ? _board.State.Speed : 0;
            bool reset = Vector3.Distance(_lastPosition, transform.position) > 20f;
            _lastPosition = transform.position;
            for (int i = 0; i < 4; i++)
            {
                if (_wheels[i] == null) continue;
                Vector3 contact = Contact(i);
                _dust[i].transform.position = _grit[i].transform.position = _marks[i].transform.position = contact;
                if (reset) { _dust[i].Clear(); _grit[i].Clear(); _marks[i].Clear(); }
                UpdateEmitter(_dust[i], speed, paused, false);
                UpdateEmitter(_grit[i], speed, paused, true);
                _marks[i].emitting = speed > 10f && (Mathf.Abs(_board.State.Steering) > .22f || _board.State.DriftAmount > .2f);
                if (paused) _marks[i].time = 0; else _marks[i].time = 1.6f;
            }
        }

        void UpdateEmitter(ParticleSystem ps, float speed, bool paused, bool grit)
        {
            if (paused) { ps.Pause(); return; }
            var emission = ps.emission;
            emission.rateOverTime = speed > 5 ? Mathf.Min(grit ? 80 : 45, speed * (grit ? 2.2f : 1.25f)) : 0;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            Vector3 direction = -transform.forward * speed * (grit ? .18f : .08f);
            velocity.x = direction.x; velocity.y = grit ? .55f : .22f; velocity.z = direction.z;
            if (!ps.isPlaying) ps.Play();
        }

        // The review image uses the same particles, placed along an actual wheel wake.
        public void PreviewWake()
        {
            var rng = new System.Random(91);
            for (int i = 0; i < 4; i++)
            {
                if (_wheels[i] == null) continue;
                _dust[i].Simulate(0, false, true); _grit[i].Simulate(0, false, true);
                _dust[i].Play(); _grit[i].Play();
                for (int n = 0; n < 32; n++)
                {
                    float age = (float)rng.NextDouble();
                    Vector3 position = Contact(i) - transform.forward * (age * 2.2f) + transform.right * ((float)rng.NextDouble() - .5f) * .55f + transform.up * age * .25f;
                    var p = new ParticleSystem.EmitParams { position = position, startSize = .16f + age * .5f, startLifetime = 1f,
                        startColor = new Color(.86f,.82f,.73f,.3f * (1-age)), velocity = Vector3.zero };
                    _dust[i].Emit(p, 1);
                    p.startSize = .03f; p.startColor = new Color(1,.9f,.7f,.75f * (1-age)); _grit[i].Emit(p, 1);
                }
                _dust[i].Simulate(.06f, false, false); _grit[i].Simulate(.06f, false, false);
            }
        }

        void Clear()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_dust[i] != null) _dust[i].Clear();
                if (_grit[i] != null) _grit[i].Clear();
                if (_marks[i] != null) _marks[i].Clear();
            }
        }

        void OnDestroy()
        {
            if (_run != null) _run.Restarted -= Clear;
            if (_material != null) Destroy(_material);
        }
    }
}
