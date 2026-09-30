using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Speed feedback at the wheels (reference image: dust spraying from the big red wheels): dust puffs whose
    /// rate grows with speed and with carving, a puff on landing, a spark burst on hard impacts / wall scrapes, a
    /// blue exhaust while nitro burns, a shockwave ring when nitro / a carve boost fires, light trails from the rear
    /// wheels while boosted (cyan nitro, orange carve boost) and a dust pop on a crew ollie.
    /// Visual only (game-feel skill: "feedback off the critical simulation"); pooled particle systems, no spawning.
    /// </summary>
    public sealed class BoardFx : MonoBehaviour
    {
        BoardController _board;
        ParticleSystem[] _dust;
        ParticleSystem _sparks, _nitro, _ring;
        TrailRenderer[] _trails;
        static Material _material, _trailMaterial;

        static readonly Color NitroRing = new Color(0.45f, 0.95f, 1f, 0.9f);
        static readonly Color BoostRing = new Color(1f, 0.72f, 0.3f, 0.9f);

        public static BoardFx Create(BoardController board, BoardView view)
        {
            var fx = board.gameObject.AddComponent<BoardFx>();
            fx._board = board;
            BoardTuningData t = board.Tuning.data;
            float axleZ = t.boardLength * 0.35f, x = t.boardWidth * 0.5f;
            fx._dust = new ParticleSystem[4];
            int k = 0;
            for (int zs = -1; zs <= 1; zs += 2)
            for (int xs = -1; xs <= 1; xs += 2)
                fx._dust[k++] = Dust(board.transform, new Vector3(xs * x, 0.1f, zs * axleZ));
            fx._sparks = Sparks(board.transform);
            fx._nitro = NitroFlame(board.transform, new Vector3(0f, 0.55f, -t.boardLength * 0.5f - 0.2f));
            fx._ring = Shockwave(board.transform);
            fx._trails = new[]
            {
                Trail(board.transform, new Vector3(-x, 0.12f, -axleZ)),
                Trail(board.transform, new Vector3(x, 0.12f, -axleZ)),
            };
            board.Impact += fx.OnImpact;
            board.Landed += fx.OnLanded;
            board.Stepped += fx.OnStepped;
            return fx;
        }

        void OnDestroy()
        {
            if (_board == null) return;
            _board.Impact -= OnImpact;
            _board.Landed -= OnLanded;
            _board.Stepped -= OnStepped;
        }

        /// <summary>Nitro / carve boost: a flat ring of light bursting outward; ollie: dust pops from every wheel.</summary>
        void OnStepped(RunStepEvents ev)
        {
            if (ev.NitroStarted) Burst(NitroRing, 90, 16f);
            else if (ev.CarveBoost > 0) Burst(BoostRing, ev.CarveBoost > 1 ? 70 : 45, ev.CarveBoost > 1 ? 13f : 10f);
            if (ev.Ollie || ev.PerfectOllie)
                for (int i = 0; i < _dust.Length; i++) _dust[i].Emit(ev.PerfectOllie ? 16 : 10);
        }

        void Burst(Color color, int count, float speed)
        {
            var p = new ParticleSystem.EmitParams { startColor = color, applyShapeToPosition = true };
            // velocity comes from the shape (radial); scale it through startSpeed for this burst
            ParticleSystem.MainModule main = _ring.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed);
            _ring.Emit(p, count);
        }

        /// <summary>Landing puff from every wheel, bigger for harder landings.</summary>
        void OnLanded(float speed)
        {
            if (speed < 2f) return;
            int n = Mathf.RoundToInt(Mathf.Clamp(speed * 3f, 6f, 30f));
            for (int i = 0; i < _dust.Length; i++) _dust[i].Emit(n);
        }

        void OnImpact(float strength)
        {
            if (strength < 0.5f || _sparks == null) return;
            _sparks.Emit(Mathf.RoundToInt(24 * strength));
        }

        void LateUpdate()
        {
            if (_board == null || _board.Simulation == null) return;
            BoardState s = _board.State;
            float speedNorm = Mathf.Clamp01(s.Speed / Mathf.Max(_board.Tuning.data.softCapSpeed, 1f));
            float carve = Mathf.Clamp01(Mathf.Abs(s.YawRate) * 1.5f);
            float rate = s.Grounded && !s.Crashed ? Mathf.Max(0f, speedNorm - 0.25f) * 70f * (0.5f + carve) : 0f;
            ParticleSystem.EmissionModule flame = _nitro.emission;
            flame.rateOverTimeMultiplier = s.NitroTimer > 0f && !s.Crashed ? 140f : 0f;
            // light trails while boosted: cyan under nitro, orange on a carve boost
            // (no trail through the air: it would draw a line from take-off to landing)
            bool trail = !s.Crashed && s.Grounded && s.Speed > 4f && (s.NitroTimer > 0f || s.BoostTimer > 0f);
            Color tc = s.NitroTimer > 0f ? NitroRing : BoostRing;
            for (int i = 0; i < _trails.Length; i++)
            {
                TrailRenderer tr = _trails[i];
                if (tr.emitting != trail) tr.emitting = trail;
                if (!trail) continue;
                tr.startColor = tc;
                tr.endColor = new Color(tc.r, tc.g, tc.b, 0f);
            }
            for (int i = 0; i < _dust.Length; i++)
            {
                ParticleSystem.EmissionModule e = _dust[i].emission;
                // rear wheels spray more; the outside wheels of a carve spray most
                bool rear = i < 2;
                bool outside = (i % 2 == 0) == (s.YawRate > 0f);
                e.rateOverTimeMultiplier = rate * (rear ? 1f : 0.45f) * (outside ? 1.3f : 0.7f);
            }
        }

        static Material ParticleMaterial()
        {
            if (_material != null) return _material;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            _material = new Material(shader) { name = "Fx Soft Particle", mainTexture = SoftDot() };
            return _material;
        }

        static Texture2D SoftDot()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static ParticleSystem Base(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.maxParticles = 200;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static ParticleSystem Dust(Transform parent, Vector3 localPos)
        {
            ParticleSystem ps = Base("Wheel Dust", parent, localPos);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new Color(0.93f, 0.9f, 0.84f, 0.55f);
            main.gravityModifier = -0.05f;
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-160f, 0f, 0f); // backwards and a little up
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ps.Play();
            return ps;
        }

        /// <summary>Blue-white exhaust behind the tail while nitro burns.</summary>
        static ParticleSystem NitroFlame(Transform parent, Vector3 localPos)
        {
            ParticleSystem ps = Base("Nitro Flame", parent, localPos);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.9f, 1f, 0.9f), new Color(0.8f, 0.97f, 1f, 0.9f));
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(0f, 180f, 0f); // backwards
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.2f, 0.6f, 1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ps.Play();
            return ps;
        }

        /// <summary>Flat ring at deck height: particles leave radially in the ground plane and fade while growing.</summary>
        static ParticleSystem Shockwave(Transform parent)
        {
            ParticleSystem ps = Base("Boost Shockwave", parent, new Vector3(0f, 0.3f, 0f));
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.4f);
            main.startSpeed = 14f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.maxParticles = 200;
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.radiusThickness = 0f;           // spawn on the edge
            shape.rotation = new Vector3(90f, 0f, 0f); // lie flat
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 3f;
            ps.Play();
            return ps;
        }

        static TrailRenderer Trail(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Boost Trail");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.35f;
            tr.minVertexDistance = 0.4f;
            tr.widthCurve = AnimationCurve.Linear(0f, 0.16f, 1f, 0f);
            tr.alignment = LineAlignment.View;
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            if (_trailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                _trailMaterial = new Material(shader) { name = "Fx Boost Trail" };
            }
            tr.sharedMaterial = _trailMaterial;
            return tr;
        }

        static ParticleSystem Sparks(Transform parent)
        {
            ParticleSystem ps = Base("Impact Sparks", parent, new Vector3(0f, 0.4f, 0f));
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(1f, 0.8f, 0.3f, 1f);
            main.gravityModifier = 1.5f;
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.8f;
            ps.Play();
            return ps;
        }
    }
}
