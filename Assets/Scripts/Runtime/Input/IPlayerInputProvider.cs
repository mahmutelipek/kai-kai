using Game.Simulation;

namespace Game
{
    /// <summary>Fills one PlayerInputState per active player slot. Local input today, network input in Milestone 5.</summary>
    public interface IPlayerInputProvider
    {
        void CollectInputs(BoardSimulation simulation, PlayerInputState[] into, float dt);
    }
}
