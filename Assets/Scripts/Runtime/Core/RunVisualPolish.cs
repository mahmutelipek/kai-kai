using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game
{
    /// <summary>Shared restrained color treatment and peripheral speed feedback.</summary>
    public sealed class RunVisualPolish : MonoBehaviour
    {
        VolumeProfile _profile;
        public bool SpeedLinesEnabled { get; set; } = true;
        BoardController _board;
        RunManager _run;
        public static void Apply(Transform owner, Camera camera, BoardController board = null, RunManager run = null)
        {
            var go = new GameObject("Run color and speed"); go.transform.SetParent(owner, false);
            var polish = go.AddComponent<RunVisualPolish>(); polish._board = board; polish._run = run;
            var volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 5;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); polish._profile = profile; volume.sharedProfile = profile;
            var bloom = profile.Add<Bloom>(); bloom.intensity.Override(.38f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.55f);
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(); color.contrast.Override(12); color.saturation.Override(20); color.postExposure.Override(.15f);
            var vignette = profile.Add<Vignette>(); vignette.intensity.Override(.13f); vignette.smoothness.Override(.65f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.allowHDR = true;
        }
        void OnGUI()
        {
            if (!SpeedLinesEnabled || Event.current.type != EventType.Repaint || _board == null || _run == null || !_run.AcceptsGameplay) return;
            float intensity = Mathf.Max(Mathf.InverseLerp(18f, 35f, _board.State.Speed), _run.NitroActive ? 1f : 0f);
            if (intensity <= 0) return;
            Matrix4x4 matrix = GUI.matrix; Color color = GUI.color;
            Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .47f);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16 + .12f;
                Vector2 radial = new Vector2(Mathf.Cos(angle) * Screen.width * .68f, Mathf.Sin(angle) * Screen.height * .7f);
                float phase = Mathf.Repeat(Time.time * (1.5f + i % 3 * .17f) + i * .371f, 1);
                Vector2 start = center + radial * Mathf.Lerp(.76f, 1.15f, phase);
                Vector2 delta = radial.normalized * (30f + 70f * intensity);
                GUI.color = new Color(.78f,.9f,1f, Mathf.Sin(phase * Mathf.PI) * intensity * (_run.NitroActive ? .45f : .2f));
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x) * Mathf.Rad2Deg, start);
                GUI.DrawTexture(new Rect(start.x,start.y,delta.magnitude,1.5f), Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            GUI.color = color;
        }
        void OnDestroy()
        {
            if (_profile == null) return;
            foreach (var component in _profile.components)
                if (Application.isPlaying) Destroy(component); else DestroyImmediate(component);
            if (Application.isPlaying) Destroy(_profile); else DestroyImmediate(_profile);
        }
    }
}
