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
        readonly int[] _gamepadForSlot = new int[BoardSimulation.MaxPlayers];
        RoadPath _road;
        int _roadHint;

        public int KeyboardSlot { get; private set; }
        public bool BotsEnabled { get; set; } = true;
        /// <summary>When false no slot is keyboard-driven (bot-only demo, automated tests).</summary>
        public bool KeyboardEnabled { get; set; } = true;
        public float LastSteerHint { get; private set; }
        public BotPreset Preset { get; private set; }

        public void Initialize(RoadPath road, bool botsEnabled)
        {
            _road = road;
            BotsEnabled = botsEnabled;
            ApplyPreset(BotPreset.Mixed);
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
                Time = sim.Time,
                Dt = dt,
            };

            for (int i = 0; i < active; i++)
            {
                switch (_sources[i])
                {
                    case InputSourceKind.Keyboard:
                        into[i] = LocalDeviceInput.ReadKeyboard();
                        break;
                    case InputSourceKind.Gamepad:
                        into[i] = LocalDeviceInput.ReadGamepad(LocalDeviceInput.GetGamepad(_gamepadForSlot[i]));
                        break;
                    case InputSourceKind.Bot:
                        ctx.Self = i;
                        into[i] = _bots[i].Decide(ctx);
                        break;
                    default:
                        into[i] = PlayerInputState.None;
                        break;
                }
            }
        }

        void AssignSources(int active)
        {
            for (int i = 0; i < _sources.Length; i++) _sources[i] = InputSourceKind.None;
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
            if (_road == null) return 0f;
            BoardState s = sim.Board.State;
            BoardTuningData t = sim.Tuning;
            float maxYawRate = (t.yawRateBaseDeg + t.yawRatePerSpeedDeg * s.Speed) * Mathf.Deg2Rad;
            return _road.SteerHint(s.Position, s.Yaw, s.Speed, maxYawRate, ref _roadHint);
        }
    }
}
