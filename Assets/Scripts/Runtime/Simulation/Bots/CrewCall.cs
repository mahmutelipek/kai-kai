using System;

namespace Game.Simulation
{
    /// <summary>
    /// "JUMP!" — the crew's shared call to ollie. A call starts when a human rider presses jump, or when the crew
    /// spots a low hazard (pothole, debris, cone) in the board's path at the right distance. Bots answer calls
    /// after a human-like reaction time (see <see cref="BotBrain"/>), which is what makes crew ollies possible with
    /// one human and five bots (60 % of the riders must jump together). Engine-free; owned by the run.
    /// </summary>
    public sealed class CrewCall
    {
        /// <summary>Hazard calls fire when the board's nose is this many seconds from the hazard (tuned for ~0.15 s crew reaction).</summary>
        public const float HazardCallMin = 0.3f, HazardCallMax = 0.42f;
        const float HazardCooldown = 1.2f;

        /// <summary>Increments with every call; bots answer each id once.</summary>
        public int Id { get; private set; }
        /// <summary>Seconds since the latest call (large when there was none).</summary>
        public float Age { get; private set; } = 99f;
        /// <summary>The latest call came from a spotted hazard (not a human jump): the HUD shows "JUMP!".</summary>
        public bool FromHazard { get; private set; }
        public ObstacleKind HazardKind { get; private set; }

        readonly bool[] _humanJumpHeld = new bool[BoardSimulation.MaxPlayers];
        float _hazardCooldown;

        public void Reset()
        {
            Id = 0; Age = 99f; FromHazard = false; _hazardCooldown = 0f;
            Array.Clear(_humanJumpHeld, 0, _humanJumpHeld.Length);
        }

        /// <param name="inputs">This step's inputs; only the human slots are read (bots decide after this).</param>
        /// <param name="human">Which slots are driven by a person.</param>
        public void Update(RunSimulation run, PlayerInputState[] inputs, bool[] human, float dt)
        {
            Age += dt;
            _hazardCooldown = Math.Max(0f, _hazardCooldown - dt);
            BoardSimulation sim = run.Board;
            for (int i = 0; i < sim.ActivePlayerCount && i < human.Length; i++)
            {
                bool held = human[i] && inputs[i].Jump;
                if (held && !_humanJumpHeld[i] && sim.Players[i].IsOnBoard) Call(false, default);
                _humanJumpHeld[i] = held;
            }
            BoardState b = sim.Board.State;
            if (_hazardCooldown <= 0f && b.Grounded && !b.Crashed && run.State == RunState.Running
                && run.JumpHazardAhead(out float ttc, out ObstacleKind kind) && ttc >= HazardCallMin && ttc <= HazardCallMax)
            {
                Call(true, kind);
                _hazardCooldown = HazardCooldown;
            }
        }

        void Call(bool hazard, ObstacleKind kind)
        {
            if (Age < 0.25f) return; // one call per jump moment (several humans pressing together)
            Id++;
            Age = 0f;
            FromHazard = hazard;
            HazardKind = kind;
        }
    }
}
