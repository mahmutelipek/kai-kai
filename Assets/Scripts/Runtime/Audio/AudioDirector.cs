using System.Collections.Generic;
using Game.Audio;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Plays the game's sounds: recorded / generated files from <c>Resources/Audio/&lt;name&gt;</c> (ElevenLabs, see
    /// Tools/SoundGen) where they exist, otherwise the procedural bank (<see cref="Synth"/>). Wheel roll, wind and nitro-jet loops mixed from the
    /// ride every frame (<see cref="AudioMix"/>), the music loop, and pooled one-shots for simulation events, the
    /// countdown and the menus. Clips are synthesised once at start-up (~0.5 s of CPU, no asset files).
    /// Loops fade out under the pause menu, everything slows down with the crash slow-motion, and the attract ride
    /// behind the title plays quietly. Volumes come from the settings (master = AudioListener.volume).
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public static float MusicVolume = 0.7f, EffectsVolume = 0.9f;
        const float MusicLevel = 0.45f, LoopLevel = 0.8f, AttractLevel = 0.35f;
        const int PoolSize = 12;

        /// <summary>True while the bot ride plays behind the title menu (quieter ride sounds).</summary>
        public bool Attract;

        BoardController _board;
        HUDController _hud;
        AudioClip[] _clips;
        AudioSource _wind, _roll, _burn, _music;
        readonly AudioSource[] _pool = new AudioSource[PoolSize];
        int _next;
        readonly AudioMix _mix = new AudioMix();
        readonly List<SfxCue> _cues = new List<SfxCue>(16);
        string _lastCountdown;
        float _pauseFade = 1f;

        public static AudioDirector Create(GameObject host, BoardController board, HUDController hud)
        {
            var d = host.AddComponent<AudioDirector>();
            d._board = board;
            d._hud = hud;
            d.BuildBank();
            board.Stepped += d.OnStepped;
            return d;
        }

        void OnDestroy()
        {
            if (_board != null) _board.Stepped -= OnStepped;
        }

        void BuildBank()
        {
            var values = (Sfx[])System.Enum.GetValues(typeof(Sfx));
            _clips = new AudioClip[values.Length];
            int files = 0;
            foreach (Sfx s in values)
            {
                if (s == Sfx.None) continue;
                AudioClip file = Resources.Load<AudioClip>("Audio/" + s);   // null (not fake-null) when missing
                if (file != null) files++;
                _clips[(int)s] = file != null ? file : Clip(s.ToString(), Synth.Build(s));
            }
            _wind = LoopSource(Loop.Wind);
            _roll = LoopSource(Loop.Roll);
            _burn = LoopSource(Loop.NitroBurn);
            _music = LoopSource(Loop.Music);
            Debug.Log($"Downhill: audio {files}/{values.Length - 1} effects from Resources/Audio, the rest procedural");
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _pool[i] = src;
            }
        }

        static AudioClip Clip(string name, float[] pcm)
        {
            AudioClip clip = AudioClip.Create(name, pcm.Length, 1, Synth.SampleRate, false);
            clip.SetData(pcm, 0);
            return clip;
        }

        AudioSource LoopSource(Loop loop)
        {
            string name = loop.ToString();
            var go = new GameObject("Audio " + name);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            AudioClip file = Resources.Load<AudioClip>("Audio/" + name);
            src.clip = file != null ? file : Clip(name, Synth.BuildLoop(loop));
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.Play();
            return src;
        }

        /// <summary>A ride one-shot (quieter behind the title).</summary>
        public void Play(Sfx s, float volume = 1f, float pitch = 1f) =>
            PlayRaw(s, volume * EffectsVolume * (Attract ? AttractLevel : 1f), pitch * TimePitch());

        /// <summary>A menu sound: full effects volume, unaffected by pause or slow motion.</summary>
        public void Ui(Sfx s) => PlayRaw(s, 0.8f * EffectsVolume, 1f);

        void PlayRaw(Sfx s, float volume, float pitch)
        {
            AudioClip clip = _clips != null && (int)s < _clips.Length ? _clips[(int)s] : null;
            if (clip == null || volume <= 0.001f) return;
            AudioSource src = _pool[_next];
            _next = (_next + 1) % PoolSize;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.Play();
        }

        /// <summary>Crash slow-motion and hit-stop slow the sound down with the picture.</summary>
        static float TimePitch() => Time.timeScale <= 0f ? 1f : Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(Time.timeScale));

        void OnStepped(RunStepEvents ev)
        {
            RunSimulation r = _board.Run;
            _mix.Cues(ev, r.Score.Multiplier, Time.time, _cues);
            for (int i = 0; i < _cues.Count; i++) Play(_cues[i].Sound, _cues[i].Volume, _cues[i].Pitch);
            _cues.Clear();
        }

        void Update()
        {
            if (_board == null || _board.Run == null || _wind == null) return;
            RunSimulation r = _board.Run;
            bool paused = Time.timeScale <= 0f;
            _mix.Update(_hud.Presenter.Feel, r.Board.Board.State, r.Tuning, r.State == RunState.Running, Time.deltaTime);

            // loops fade out quickly under the pause menu (real time: game time is stopped)
            _pauseFade = Mathf.MoveTowards(_pauseFade, paused ? 0f : 1f, Time.unscaledDeltaTime * 4f);
            float ride = LoopLevel * EffectsVolume * _pauseFade * (Attract ? AttractLevel : 1f);
            float timePitch = TimePitch();
            Set(_roll, _mix.RollVolume * ride, _mix.RollPitch * timePitch);
            Set(_wind, _mix.WindVolume * ride, _mix.WindPitch * timePitch);
            Set(_burn, _mix.BurnVolume * ride, _mix.BurnPitch * timePitch);
            // music keeps playing under menus, a little quieter while paused
            Set(_music, MusicLevel * MusicVolume * _mix.MusicDuck * (paused ? 0.6f : 1f), 1f);

            // countdown beeps: "3", "2", "1" tick, "GO!" chimes (the presenter keeps the string instance per value)
            string c = _hud.Presenter.State.Countdown;
            if (!ReferenceEquals(c, _lastCountdown))
            {
                if (!string.IsNullOrEmpty(c)) Ui(c.Length == 1 && char.IsDigit(c[0]) ? Sfx.CountdownTick : Sfx.CountdownGo);
                _lastCountdown = c;
            }
        }

        static void Set(AudioSource src, float volume, float pitch)
        {
            src.volume = volume;
            src.pitch = pitch;
        }
    }
}
