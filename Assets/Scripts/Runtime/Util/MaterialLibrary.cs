using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>Flat-colour materials (URP Lit or Standard) for debug markers and effects, plus the P1..P6 HUD colours.</summary>
    public static class MaterialLibrary
    {
        static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        static Shader _shader;

        /// <summary>P1..P6 colors, matching the reference HUD (blue, red, green, yellow, purple, orange).</summary>
        public static readonly Color[] PlayerColors =
        {
            new Color(0.2f, 0.55f, 1f),
            new Color(0.95f, 0.22f, 0.22f),
            new Color(0.2f, 0.78f, 0.3f),
            new Color(1f, 0.85f, 0.15f),
            new Color(0.62f, 0.3f, 0.95f),
            new Color(1f, 0.55f, 0.12f),
        };

        public static Material Get(Color color)
        {
            if (Cache.TryGetValue(color, out Material m) && m != null) return m;
            m = new Material(GetShader()) { name = "Flat " + ColorUtility.ToHtmlStringRGB(color) };
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.2f);
            Cache[color] = m;
            return m;
        }

        static Shader GetShader()
        {
            if (_shader != null) return _shader;
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            _shader = srp ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (_shader == null) _shader = Shader.Find("Standard");
            if (_shader == null) _shader = Shader.Find("Universal Render Pipeline/Lit");
            return _shader;
        }
    }
}
