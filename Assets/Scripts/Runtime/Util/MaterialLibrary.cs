using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>Creates and caches simple flat-color materials that work in URP (Lit) or the built-in pipeline (Standard).</summary>
    public static class MaterialLibrary
    {
        static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> GlossyCache = new Dictionary<Color, Material>();
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
        public static readonly Color Concrete = new Color(0.72f, 0.72f, 0.7f);
        public static readonly Color ConcreteDark = new Color(0.5f, 0.5f, 0.52f);
        public static readonly Color BridgeSteel = new Color(0.85f, 0.3f, 0.18f);
        public static readonly Color TunnelInside = new Color(0.36f, 0.34f, 0.32f);
        public static readonly Color Pothole = new Color(0.08f, 0.08f, 0.09f);
        public static readonly Color Crate = new Color(0.72f, 0.5f, 0.26f);
        public static readonly Color CrateFrame = new Color(0.45f, 0.3f, 0.15f);
        public static readonly Color Glass = new Color(0.25f, 0.35f, 0.45f);
        public static readonly Color Tire = new Color(0.1f, 0.1f, 0.1f);
        public static readonly Color Headlight = new Color(1f, 0.95f, 0.6f);
        public static readonly Color Taillight = new Color(0.9f, 0.1f, 0.1f);
        public static readonly Color Black = new Color(0.08f, 0.08f, 0.08f);

        /// <summary>Car body colours: bright, well separated from the road and from each other.</summary>
        public static readonly Color[] CarColors =
        {
            new Color(0.95f, 0.8f, 0.15f), new Color(0.2f, 0.45f, 0.9f), new Color(0.9f, 0.9f, 0.92f),
            new Color(0.85f, 0.2f, 0.2f), new Color(0.3f, 0.75f, 0.45f),
        };

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

        /// <summary>Material with a custom smoothness (e.g. glossy car paint, glass).</summary>
        public static Material Get(Color color, float smoothness)
        {
            if (Mathf.Approximately(smoothness, 0.2f)) return Get(color);
            if (GlossyCache.TryGetValue(color, out Material m) && m != null) return m;
            m = new Material(Get(color)) { name = "Glossy " + ColorUtility.ToHtmlStringRGB(color) };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            GlossyCache[color] = m;
            return m;
        }

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
