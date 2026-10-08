using System.Collections.Generic;
using Game.Hud;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Draws Game.Hud draw commands with IMGUI (textures, rotated / skewed / scaled rects, outlined text in the
    /// bundled font). Shared by the HUD and the menus; textures are built once and reused.
    /// </summary>
    public static class HudDrawer
    {
        static readonly Dictionary<HudTex, Texture2D> Textures = new Dictionary<HudTex, Texture2D>();
        static GUIStyle _text;

        /// <summary>Builds every HUD texture now so none is generated mid-run (it takes a moment).</summary>
        public static void Prewarm()
        {
            foreach (HudTex t in System.Enum.GetValues(typeof(HudTex))) if (t != HudTex.None) Texture(t);
        }

        /// <summary>Call from OnGUI (Repaint only).</summary>
        public static void Draw(List<HudCmd> cmds)
        {
            EnsureStyle();
            Matrix4x4 baseMatrix = GUI.matrix;
            Color baseColor = GUI.color;
            for (int i = 0; i < cmds.Count; i++) Draw(cmds[i], baseMatrix);
            GUI.matrix = baseMatrix;
            GUI.color = baseColor;
        }

        static void Draw(in HudCmd c, Matrix4x4 baseMatrix)
        {
            float scale = c.Scale == 0f ? 1f : c.Scale;
            var pivot = new Vector3(c.X + c.W * 0.5f, c.Y + c.H * 0.5f, 0f);
            Matrix4x4 local = Matrix4x4.identity;
            if (c.Rotation != 0f || c.Skew != 0f || scale != 1f)
            {
                Matrix4x4 shear = Matrix4x4.identity;
                shear.m01 = c.Skew; // x += skew * y, like the preview's canvas transform
                local = Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, c.Rotation)) * shear
                        * Matrix4x4.Scale(new Vector3(scale, scale, 1f)) * Matrix4x4.Translate(-pivot);
            }
            GUI.matrix = baseMatrix * local;
            var rect = new Rect(c.X, c.Y, c.W, c.H);
            if (c.Text != null)
            {
                int fontSize = Mathf.Max(1, Mathf.RoundToInt(c.FontSize));
                // text never spills out of its box (panels, pills, cards): shrink it to fit instead
                _text.fontSize = FitFontSize(c.Text, fontSize, c.W, c.Outline);
                _text.alignment = c.Align == HudAlign.Left ? TextAnchor.MiddleLeft : c.Align == HudAlign.Center ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
                if (c.Outline > 0f)
                {
                    _text.normal.textColor = ToColor(c.OutlineColor);
                    float d = c.Outline;
                    // 4 diagonal copies make the outline (each label is a draw call, so keep it cheap)
                    for (int k = 0; k < 4; k++)
                    {
                        float ox = (k & 1) == 0 ? -d : d, oy = (k & 2) == 0 ? -d : d;
                        GUI.Label(new Rect(rect.x + ox, rect.y + oy, rect.width, rect.height), c.Text, _text);
                    }
                }
                _text.normal.textColor = ToColor(c.Color);
                GUI.Label(rect, c.Text, _text);
            }
            else
            {
                GUI.color = ToColor(c.Color);
                GUI.DrawTexture(rect, Texture(c.Tex), ScaleMode.StretchToFill, true);
                GUI.color = Color.white;
            }
        }


        static readonly GUIContent Measure = new GUIContent();
        static readonly Dictionary<(string, int), float> Widths = new Dictionary<(string, int), float>(256);

        /// <summary>Largest font size (at most <paramref name="size"/>) at which the text, outline included, stays inside <paramref name="boxWidth"/>.</summary>
        static int FitFontSize(string text, int size, float boxWidth, float outline)
        {
            if (boxWidth <= 1f || string.IsNullOrEmpty(text)) return size;
            float avail = boxWidth - 2f * outline;
            if (!Widths.TryGetValue((text, size), out float w))
            {
                if (Widths.Count > 2000) Widths.Clear(); // numbers change every frame: keep the cache bounded
                _text.fontSize = size;
                Measure.text = text;
                w = _text.CalcSize(Measure).x;
                Widths[(text, size)] = w;
            }
            if (w <= avail || w <= 0f) return size;
            return Mathf.Max(8, Mathf.FloorToInt(size * avail / w));
        }
        static Color ToColor(HudColor c) => new Color(c.R, c.G, c.B, c.A);

        static Texture2D Texture(HudTex t)
        {
            if (Textures.TryGetValue(t, out Texture2D tex) && tex != null) return tex;
            HudImage img = HudTextures.Get(t);
            tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false, false)
            {
                name = "Hud " + t, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            // HudImage rows run top to bottom; Unity textures bottom to top
            var px = new Color32[img.Width * img.Height];
            for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
            {
                int k = ((img.Height - 1 - y) * img.Width + x) * 4;
                px[y * img.Width + x] = new Color32(img.Rgba[k], img.Rgba[k + 1], img.Rgba[k + 2], img.Rgba[k + 3]);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            Textures[t] = tex;
            return tex;
        }

        static void EnsureStyle()
        {
            if (_text != null) return;
            Font font = HudFont.Get();
            _text = new GUIStyle(GUI.skin.label) { fontStyle = HudFont.Style(font), wordWrap = false, clipping = TextClipping.Overflow, richText = false };
            if (font != null) _text.font = font;
        }
    }
}
