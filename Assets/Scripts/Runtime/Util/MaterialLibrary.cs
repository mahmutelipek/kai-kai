using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>Creates and caches simple flat-color materials that work in URP (Lit) or the built-in pipeline (Standard).</summary>
    public static class MaterialLibrary
    {
        static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        static Shader _shader;

        public static readonly Color Road = new Color(0.27f, 0.28f, 0.31f);
        public static readonly Color Grass = new Color(0.45f, 0.72f, 0.33f);
        public static readonly Color LineYellow = new Color(1f, 0.82f, 0.15f);
        public static readonly Color LineWhite = new Color(0.95f, 0.95f, 0.95f);
        public static readonly Color ConeOrange = new Color(1f, 0.45f, 0.1f);
        public static readonly Color RampWood = new Color(0.78f, 0.58f, 0.35f);
        public static readonly Color DeckGrip = new Color(0.12f, 0.12f, 0.14f);
        public static readonly Color DeckWood = new Color(0.86f, 0.7f, 0.45f);
        public static readonly Color DeckStripe = new Color(0.9f, 0.2f, 0.18f);
        public static readonly Color Wheel = new Color(0.92f, 0.2f, 0.2f);
        public static readonly Color Metal = new Color(0.6f, 0.62f, 0.66f);
        public static readonly Color Skin = new Color(0.96f, 0.78f, 0.62f);
        public static readonly Color Pants = new Color(0.16f, 0.17f, 0.22f);

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
