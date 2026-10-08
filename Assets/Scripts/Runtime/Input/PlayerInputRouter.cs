using System.Collections.Generic;
using Game.Frontend;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    public enum InputSourceKind
    {
        None = 0,
        Keyboard = 1,
        Gamepad = 2,
        Bot = 3,
    }

    public enum BotPreset
    {
        /// <summary>Every slot has a different personality (cooperative, stubborn L/R, wanderer, greedy, scared).</summary>
        Mixed = 0,
        /// <summary>All bots cooperate with the road: isolates the human player's influence.</summary>
        AllCooperative = 1,
    }

    /// <summary>
    /// Decides who drives each player slot: the keyboard drives one slot (Tab switches), each connected
    /// gamepad drives one other slot, bots (toggleable) drive the rest.
    /// </summary>
    public sealed class PlayerInputRouter : MonoBehaviour, IPlayerInputProvider
    {
        readonly BotBrain[] _bots = new BotBrain[BoardSimulation.MaxPlayers];
        readonly InputSourceKind[] _sources = new InputSourceKind[BoardSimulation.MaxPlayers];
        readonly bool[] _human = new bool[BoardSimulation.MaxPlayers];
        readonly int[] _gamepadForSlot = new int[BoardSimulation.MaxPlayers];
        readonly LocalDeviceInput.KeyboardScheme[] _schemeForSlot = new LocalDeviceInput.KeyboardScheme[BoardSimulation.MaxPlayers];
        readonly List<JoinedDevice> _joined = new List<JoinedDevice>(BoardSimulation.MaxPlayers);
        bool _useJoined;
        RunSimulation _run;
        int _roadHint;

        public int KeyboardSlot { get; private set; }
        public bool BotsEnabled { get; set; } = true;
        /// <summary>When false no slot is keyboard-driven (bot-only demo, automated tests).</summary>
        public bool KeyboardEnabled { get; set; } = true;
        public float LastSteerHint { get; private set; }
        public BotPreset Preset { get; private set; }

        public void Initialize(RunSimulation run, bool botsEnabled)
        {
            _run = run;
            BotsEnabled = botsEnabled;
            ApplyPreset(BotPreset.Mixed);
        }

        /// <summary>
        /// Lobby mode: slot i is driven by joined device i (keyboard halves or a gamepad by device id); bots fill
        /// the remaining slots when enabled. Pass null to go back to automatic assignment (dev / tests).
        /// </summary>
        public void SetJoinedDevices(IReadOnlyList<JoinedDevice> joined)
        {
            _joined.Clear();
            _useJoined = joined != null;
            if (joined != null) for (int i = 0; i < joined.Count; i++) _joined.Add(joined[i]);
        }

        /// <summary>Attract mode behind the title screen: every slot is a cooperative bot.</summary>
        public void SetAttract(bool on)
        {
            if (on) { SetJoinedDevices(new List<JoinedDevice>()); BotsEnabled = true; ApplyPreset(BotPreset.AllCooperative); }
            else ApplyPreset(BotPreset.Mixed);
        }

        public void CyclePreset() => ApplyPreset(Preset == BotPreset.Mixed ? BotPreset.AllCooperative : BotPreset.Mixed);

        void ApplyPreset(BotPreset preset)
        {
            Preset = preset;
            for (int i = 0; i < _bots.Length; i++)
            {
                BotBehavior behavior = preset == BotPreset.Mixed ? BotBrain.DefaultBehaviorForSlot(i) : BotBehavior.Cooperative;
                _bots[i] = BotBrain.Create(behavior, 1000 + i * 17);
            }
        }

        public InputSourceKind SourceOf(int slot) => slot >= 0 && slot < _sources.Length ? _sources[slot] : InputSourceKind.None;

        public bool IsHumanControlled(int slot) => SourceOf(slot) == InputSourceKind.Keyboard || SourceOf(slot) == InputSourceKind.Gamepad;

        public BotBehavior BotBehaviorOf(int slot) => _bots[slot].Behavior;

        public void CycleKeyboardSlot(int activePlayers)
        {
            KeyboardSlot = (KeyboardSlot + 1) % Mathf.Max(1, activePlayers);
        }

        public void CollectInputs(BoardSimulation sim, PlayerInputState[] into, float dt)
        {
            int active = sim.ActivePlayerCount;
            if (KeyboardSlot >= active) KeyboardSlot = 0;
            AssignSources(active);

            BoardState board = sim.Board.State;
            LastSteerHint = ComputeSteerHint(sim);
            var ctx = new BotContext
            {
                Players = sim.Players,
                ActivePlayerCount = active,
                Board = board,
                Tuning = sim.Tuning,
                SteerHint = LastSteerHint,
                BrakeHint = _run != null ? _run.Road.BrakeHint(_run.Distance, board.Speed, sim.Tuning) : 0f,
                NitroAvailable = _run != null && _run.NitroCharges > 0,
                Time = sim.Time,
                Dt = dt,
            };

            // humans first: their jump presses become the crew's "JUMP!" call that the bots answer this same step
            for (int i = 0; i < active; i++)
            {
                _human[i] = false;
                switch (_sources[i])
                {
                    case InputSourceKind.Keyboard:
                        into[i] = LocalDeviceInput.ReadKeyboard(_useJoined ? _schemeForSlot[i] : LocalDeviceInput.KeyboardScheme.Full);
                        _human[i] = true;
                        break;
                    case InputSourceKind.Gamepad:
                        into[i] = LocalDeviceInput.ReadGamepad(_useJoined ? LocalDeviceInput.GamepadById(_gamepadForSlot[i]) : LocalDeviceInput.GetGamepad(_gamepadForSlot[i]));
                        _human[i] = true;
                        break;
                    case InputSourceKind.Bot:
                        break;
                    default:
                        into[i] = PlayerInputState.None;
                        break;
                }
            }
            if (_run != null)
            {
                _run.Crew.Update(_run, into, _human, dt);
                ctx.CrewCallId = _run.Crew.Id;
                ctx.CrewCallAge = _run.Crew.Age;
            }
            for (int i = 0; i < active; i++)
            {
                if (_sources[i] != InputSourceKind.Bot) continue;
                ctx.Self = i;
                into[i] = _bots[i] is GreedyFrontBot greedy ? greedy.DecideWithNitro(ctx) : _bots[i].Decide(ctx);
            }
        }

        /// <summary>Re-reads who drives which slot without stepping the sim (the board is frozen during the 3-2-1 countdown).</summary>
        public void RefreshSources(int active) => AssignSources(active);

        void AssignSources(int active)
        {
            for (int i = 0; i < _sources.Length; i++) _sources[i] = InputSourceKind.None;
            if (_useJoined)
            {
                bool twoKeyboards = false;
                int kbCount = 0;
                foreach (JoinedDevice d in _joined) if (d.Kind != DeviceKind.Gamepad) kbCount++;
                twoKeyboards = kbCount > 1;
                for (int i = 0; i < active && i < _joined.Count; i++)
                {
                    JoinedDevice d = _joined[i];
                    if (d.Kind == DeviceKind.Gamepad) { _sources[i] = InputSourceKind.Gamepad; _gamepadForSlot[i] = d.GamepadId; }
                    else
                    {
                        _sources[i] = InputSourceKind.Keyboard;
                        _schemeForSlot[i] = !twoKeyboards ? LocalDeviceInput.KeyboardScheme.Full
                            : d.Kind == DeviceKind.KeyboardRight ? LocalDeviceInput.KeyboardScheme.Right : LocalDeviceInput.KeyboardScheme.Left;
                    }
                }
                for (int i = 0; i < active; i++)
                    if (_sources[i] == InputSourceKind.None && BotsEnabled) _sources[i] = InputSourceKind.Bot;
                return;
            }
            if (KeyboardEnabled) _sources[KeyboardSlot] = InputSourceKind.Keyboard;

            int pads = LocalDeviceInput.GamepadCount;
            int slot = 0;
            for (int p = 0; p < pads; p++)
            {
                while (slot < active && _sources[slot] != InputSourceKind.None) slot++;
                if (slot >= active) break;
                _sources[slot] = InputSourceKind.Gamepad;
                _gamepadForSlot[slot] = p;
            }

            for (int i = 0; i < active; i++)
                if (_sources[i] == InputSourceKind.None && BotsEnabled) _sources[i] = InputSourceKind.Bot;
        }

        float ComputeSteerHint(BoardSimulation sim)
        {
            if (_run == null) return 0f;
            BoardState s = sim.Board.State;
            BoardTuningData t = sim.Tuning;
            float maxYawRate = (t.yawRateBaseDeg + t.yawRatePerSpeedDeg * s.Speed) * Mathf.Deg2Rad;
            return _run.Road.PredictiveSteerHint(s, maxYawRate, _run.Tuning.CrewResponseDelay + 0.05f, ref _roadHint);
        }
    }
}
