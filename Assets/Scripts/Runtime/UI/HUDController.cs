using System.Collections.Generic;
using Game.Hud;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The HUD of the reference image, drawn with IMGUI from engine-free draw commands (Game.Hud.HudLayout):
    /// distance + best on torn brush panels, coin / gem pills, slanted COMBO and NITRO READY! banners, big speed
    /// with a wedge of segments, P1..P6 avatars, countdown, popups, danger / nitro vignettes, end screen.
    /// The same commands render in the headless preview, so screenshots show this layout.
    /// Anchored to Screen.safeArea, scaled from 1080p; strings are only rebuilt when values change
    /// (skills: game-ui-ux, performance-optimization).
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        BoardController _board;
        RunManager _run;
        PlayerInputRouter _router;
        DebugOverlay _overlay;
        readonly HudPresenter _presenter = new HudPresenter();
        readonly List<HudCmd> _cmds = new List<HudCmd>(128);
        readonly Dictionary<HudTex, Texture2D> _textures = new Dictionary<HudTex, Texture2D>();
        GUIStyle _text;
        Font _font;
        bool _endFilled;

        public bool Visible { get; set; } = true;
        public HudPresenter Presenter => _presenter;

        public void Initialize(BoardController board, RunManager run, PlayerInputRouter router, DebugOverlay overlay)
        {
            _board = board;
            _run = run;
            _router = router;
            _overlay = overlay;
            _board.Stepped += OnStepped;
            // pre-build every HUD texture now: generating one mid-run (first danger vignette) would hitch
            foreach (HudTex t in System.Enum.GetValues(typeof(HudTex))) if (t != HudTex.None) Texture(t);
        }

        void OnDestroy()
        {
            if (_board != null) _board.Stepped -= OnStepped;
            foreach (Texture2D t in _textures.Values) if (t != null) Destroy(t);
        }

        void OnStepped(RunStepEvents ev) => _presenter.OnStep(ev, _board.Run);

        /// <summary>Short message in the popup column (e.g. "RIDERS: 1" from the count hotkeys).</summary>
        public void Toast(string text) => _presenter.Push(text, HudColor.White);

        public void SetCountdown(string text) => _presenter.SetCountdown(text);

        void Update()
        {
            if (_board == null || _board.Run == null) return;
            RunSimulation r = _board.Run;
            _presenter.Update(r, _run != null ? _run.BestDistance : 0f, Time.unscaledTime, Time.unscaledDeltaTime);
            HudState s = _presenter.State;
            for (int i = 0; i < s.Human.Length; i++) s.Human[i] = _router != null && _router.IsHumanControlled(i);
            s.DebugPanelOpen = _overlay != null && _overlay.Visible;
            if (s.Ended && !_endFilled) { _presenter.FillEndScreen(r); _endFilled = true; }
            if (!s.Ended) _endFilled = false;
        }

        void OnGUI()
        {
            if (!Visible || _board == null || _board.Run == null || Event.current.type != EventType.Repaint) return;
            Rect safe = Screen.safeArea;
            HudLayout.Build(_presenter.State, Screen.width, Screen.height, safe.x, Screen.height - safe.yMax, safe.width, safe.height, _cmds);
            EnsureStyle();
            Matrix4x4 baseMatrix = GUI.matrix;
            Color baseColor = GUI.color;
            for (int i = 0; i < _cmds.Count; i++) Draw(_cmds[i], baseMatrix);
            GUI.matrix = baseMatrix;
            GUI.color = baseColor;
        }

        void Draw(in HudCmd c, Matrix4x4 baseMatrix)
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
                _text.fontSize = Mathf.Max(1, Mathf.RoundToInt(c.FontSize));
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

        static Color ToColor(HudColor c) => new Color(c.R, c.G, c.B, c.A);

        Texture2D Texture(HudTex t)
        {
            if (_textures.TryGetValue(t, out Texture2D tex) && tex != null) return tex;
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
            _textures[t] = tex;
            return tex;
        }

        void EnsureStyle()
        {
            if (_text != null) return;
            // chunky display font like the reference; falls back to the default GUI font
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Arial Black", "Impact", "Segoe UI Black", "Arial" }, 48);
            _text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.BoldAndItalic, wordWrap = false, clipping = TextClipping.Overflow, richText = false };
            if (_font != null) _text.font = _font;
        }
    }
}
