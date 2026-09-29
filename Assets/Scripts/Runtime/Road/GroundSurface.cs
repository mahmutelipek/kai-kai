using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>Marks a collider as drivable ground for the board's ground sampling.</summary>
    public sealed class GroundSurface : MonoBehaviour
    {
        public SurfaceKind kind = SurfaceKind.Road;
    }
}
