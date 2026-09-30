using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Speed feedback at the wheels (reference image: dust spraying from the big red wheels): dust puffs whose
    /// rate grows with speed and with carving, and a spark burst on hard impacts / wall scrapes.
    /// Visual only (game-feel skill: "feedback off the critical simulation"); pooled particle systems, no spawning.
    /// </summary>
    public sealed class BoardFx : MonoBehaviour
    {
        BoardController _board;
        ParticleSystem[] _dust;
        ParticleSystem _sparks;
        static Material _material;

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
            board.Impact += fx.OnImpact;
            return fx;
        }

        void OnDestroy()
        {
            if (_board != null) _board.Impact -= OnImpact;
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
