using Game.Art;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 3D wind: thin glowing streaks rushing past the left and right of the view, spawned ahead of the camera and
    /// flying backwards (stretched particles, one draw call). Driven by <see cref="SpeedFeel"/> so it matches the HUD
    /// streaks: builds with speed and hard acceleration, full and cyan under nitro, orange on a carve boost, a
    /// draft of lines closing in behind a car in the slipstream. The screen centre (board, road ahead) stays clear.
    /// </summary>
    public sealed class SpeedLines : MonoBehaviour
    {
        const float MaxRate = 260f;

        public SpeedFeel Feel;

        BoardController _board;
        ParticleSystem _left, _right, _draft;
        static Material _material;

        static readonly Color Plain = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color NitroTint = new Color(0.55f, 0.95f, 1f, 0.7f);
        static readonly Color BoostTint = new Color(1f, 0.78f, 0.45f, 0.65f);

        public static SpeedLines Create(Camera camera, BoardController board)
        {
            var root = new GameObject("SpeedLines").transform;
            root.SetParent(camera.transform, false);
            var lines = root.gameObject.AddComponent<SpeedLines>();
            lines._board = board;
            // two side sheets (wide, from knee to above head height) and a narrow draft tunnel
            lines._left = Emitter("Wind Left", root, new Vector3(-6.5f, 0.3f, 30f), new Vector3(7f, 6f, 6f));
            lines._right = Emitter("Wind Right", root, new Vector3(6.5f, 0.3f, 30f), new Vector3(7f, 6f, 6f));
            lines._draft = Emitter("Wind Draft", root, new Vector3(0f, 1.2f, 26f), new Vector3(7f, 3.5f, 4f));
            return lines;
        }

        void LateUpdate()
        {
            if (_board == null || _board.Simulation == null) return;
            float wind = Feel != null ? Feel.Wind : 0f;
            float nitro = Feel != null ? Feel.Nitro : 0f, boost = Feel != null ? Feel.Boost : 0f;
            float draft = Feel != null ? Feel.Draft : 0f;
            bool on = !CameraController.ReduceMotion && Time.deltaTime > 0f;

            Color tint = Color.Lerp(Color.Lerp(Plain, BoostTint, boost), NitroTint, nitro);
            // the faster we go, the faster the air rushes past (relative speed: board speed + a floor so it reads)
            float airSpeed = 25f + _board.State.Speed * 1.6f + 25f * nitro;
            float rate = on ? MaxRate * wind * wind : 0f;
            Drive(_left, rate * 0.5f, tint, airSpeed);
            Drive(_right, rate * 0.5f, tint, airSpeed);
            Drive(_draft, on ? 90f * draft : 0f, Plain, airSpeed * 0.8f);
        }

        static void Drive(ParticleSystem ps, float rate, Color tint, float airSpeed)
        {
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTimeMultiplier = rate;
            ParticleSystem.MainModule main = ps.main;
            main.startColor = tint;
            main.startSpeed = new ParticleSystem.MinMaxCurve(airSpeed * 0.85f, airSpeed * 1.15f);
        }

        static ParticleSystem Emitter(string name, Transform parent, Vector3 localPos, Vector3 boxSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // rides with the camera: pure screen-space feel
            main.playOnAwake = true;
            main.maxParticles = 300;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            main.startColor = Plain;
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;
            shape.rotation = new Vector3(0f, 180f, 0f); // emit toward the camera (and past it)
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.09f;   // length grows with the air speed
            r.lengthScale = 2f;
            r.sharedMaterial = Material();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static Material Material()
        {
            if (_material != null) return _material;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            _material = new Material(shader) { name = "Fx Wind Streak", mainTexture = StreakTexture() };
            return _material;
        }

        /// <summary>Soft across, bright core: the stretched billboard maps u across the streak, v along it.</summary>
        static Texture2D StreakTexture()
        {
            const int w = 16, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "WindStreak", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = Mathf.Abs((x + 0.5f) / w * 2f - 1f), along = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                float a = Mathf.Clamp01(1f - across * across) * Mathf.Clamp01(1f - along * along * along);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
