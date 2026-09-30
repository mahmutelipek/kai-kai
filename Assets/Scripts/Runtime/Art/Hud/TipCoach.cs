using Game.Frontend;
using Game.Simulation;

namespace Game.Hud
{
    public enum Tip : byte { Steer, JumpTogether, Carve, Draft, Nitro }

    /// <summary>
    /// First-run coaching (skill: game-ui-ux, "teach in context"): each mechanic gets one short tip the first time it
    /// matters — steering right after GO, jumping when the crew first calls a hazard, carving once a carve starts
    /// charging, drafting and nitro when they come up — at most one every few seconds, each only once per profile
    /// (<see cref="SeenMask"/> is saved in the settings). Engine-free; only while a human rides.
    /// </summary>
    public sealed class TipCoach
    {
        public const float ShowTime = 5f, MinGap = 7f;

        public bool Enabled = true;
        /// <summary>Bit per <see cref="Tip"/> already shown (persisted by the front end).</summary>
        public int SeenMask;
        public string Current { get; private set; }
        public float Age { get; private set; } = 99f;

        float _rideTime, _sinceLast = 99f, _lastDistance;

        public static string Text(Tip t)
        {
            switch (t)
            {
                case Tip.Steer: return "LEAN LEFT OR RIGHT: WHERE YOU STAND STEERS THE BOARD!";
                case Tip.JumpTogether: return "EVERYONE JUMP TOGETHER TO OLLIE OVER IT!";
                case Tip.Carve: return "HOLD THE CARVE... THEN STRAIGHTEN OUT FOR A BOOST!";
                case Tip.Draft: return "RIDE RIGHT BEHIND CARS TO DRAFT FOR SPEED!";
                default: return "NITRO READY! PRESS ACTION: (X) / E";
            }
        }

        public bool Seen(Tip t) => (SeenMask & (1 << (int)t)) != 0;

        public void Update(RunSimulation r, bool humanRiding, float dt)
        {
            Age += dt;
            _sinceLast += dt;
            if (Age > ShowTime) Current = null;
            if (r.MaxDistance < _lastDistance - 1f) _rideTime = 0f; // a new run
            _lastDistance = r.MaxDistance;
            BoardState b = r.Board.Board.State;
            if (!Enabled || !humanRiding || r.State != RunState.Running || b.Crashed || r.Distance < 1f) return;
            _rideTime += dt;
            if (Current != null || _sinceLast < MinGap) return;

            if (!Seen(Tip.Steer) && _rideTime > 1.5f) Show(Tip.Steer);
            else if (!Seen(Tip.JumpTogether) && ((r.Crew.FromHazard && r.Crew.Age < 0.2f) || _rideTime > 40f)) Show(Tip.JumpTogether);
            else if (!Seen(Tip.Nitro) && r.NitroCharges > 0) Show(Tip.Nitro);
            else if (!Seen(Tip.Carve) && (b.CarveCharge > 0.35f || _rideTime > 70f)) Show(Tip.Carve);
            else if (!Seen(Tip.Draft) && _rideTime > 100f) Show(Tip.Draft);
        }

        void Show(Tip t)
        {
            SeenMask |= 1 << (int)t;
            Current = Loc.T(Text(t));
            Age = 0f;
            _sinceLast = 0f;
        }
    }
}
