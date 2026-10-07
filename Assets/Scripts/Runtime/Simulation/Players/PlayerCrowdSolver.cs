using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// Keeps players from overlapping on the deck (circle vs circle in board-local space) and turns
    /// fast collisions into bumps and staggers. Pinned players act as immovable.
    /// </summary>
    public static class PlayerCrowdSolver
    {
        public const int Iterations = 4;

        public static int Resolve(IReadOnlyList<PlayerSim> players, int count, BoardTuningData t)
        {
            int staggers = 0;
            float minDist = t.playerRadius * 2f;
            float verticalClearance = t.playerHeight * 0.8f;

            for (int iter = 0; iter < (t.edgeAssist > .5f ? 10 : Iterations); iter++)
            {
                for (int i = 0; i < count; i++)
                {
                    PlayerSim a = players[i];
                    if (!a.IsOnBoard) continue;
                    for (int j = i + 1; j < count; j++)
                    {
                        PlayerSim b = players[j];
                        if (!b.IsOnBoard) continue;
                        if (Math.Abs(a.Height - b.Height) >= verticalClearance) continue; // jumped over

                        float wa = a.Pinned ? 0f : 1f;
                        float wb = b.Pinned ? 0f : 1f;
                        float wSum = wa + wb;
                        if (wSum <= 0f) continue;

                        Vector2 d = b.LocalPosition - a.LocalPosition;
                        float dist = d.Length();
                        if (dist >= minDist) continue;

                        Vector2 n;
                        if (dist > 1e-5f) n = d / dist;
                        else
                        {
                            // exactly coincident: deterministic separation direction
                            float angle = (a.Id * 7 + b.Id * 13) * 0.61803f * SimMath.TwoPi;
                            n = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        }

                        // At a supported side edge, separate along the deck rather than pushing someone off.
                        if (t.edgeAssist > .5f && !a.IsAirborne && !b.IsAirborne &&
                            Math.Abs(a.LocalPosition.X) > t.HalfWidth - t.playerRadius * .7f &&
                            Math.Abs(b.LocalPosition.X) > t.HalfWidth - t.playerRadius * .7f &&
                            MathF.Sign(a.LocalPosition.X) == MathF.Sign(b.LocalPosition.X))
                            n = new Vector2(0, d.Y < 0 ? -1 : 1);
                        float penetration = n.X == 0 && t.edgeAssist > .5f
                            ? Math.Max(0, MathF.Sqrt(Math.Max(0,minDist * minDist - d.X * d.X)) - Math.Abs(d.Y))
                            : minDist - dist;
                        a.LocalPosition -= n * (penetration * wa / wSum);
                        b.LocalPosition += n * (penetration * wb / wSum);

                        if (t.edgeAssist > .5f)
                        {
                            a.ConstrainToDeck(t); b.ConstrainToDeck(t);
                            float remaining = minDist - Vector2.Distance(a.LocalPosition,b.LocalPosition);
                            if (remaining > 0)
                            {
                                a.LocalPosition -= n * remaining * wa / wSum;
                                b.LocalPosition += n * remaining * wb / wSum;
                                a.ConstrainToDeck(t); b.ConstrainToDeck(t);
                            }
                        }
                        float approach = Vector2.Dot(b.Velocity - a.Velocity, n);
                        if (approach < 0f)
                        {
                            float impulse = -(1f + t.bumpRestitution) * approach / wSum;
                            a.Velocity -= n * (impulse * wa);
                            b.Velocity += n * (impulse * wb);
                            if (iter == 0 && -approach > t.staggerRelativeSpeed)
                            {
                                a.Stagger(t.staggerTime);
                                b.Stagger(t.staggerTime);
                                staggers++;
                            }
                        }
                    }
                }
            }
            return staggers;
        }

        /// <summary>Smallest centre distance between two on-board players whose heights overlap.</summary>
        public static float MinimumSeparation(IReadOnlyList<PlayerSim> players, int count, BoardTuningData t)
        {
            float min = float.PositiveInfinity;
            float verticalClearance = t.playerHeight * 0.8f;
            for (int i = 0; i < count; i++)
            {
                if (!players[i].IsOnBoard) continue;
                for (int j = i + 1; j < count; j++)
                {
                    if (!players[j].IsOnBoard) continue;
                    if (Math.Abs(players[i].Height - players[j].Height) >= verticalClearance) continue;
                    min = Math.Min(min, Vector2.Distance(players[i].LocalPosition, players[j].LocalPosition));
                }
            }
            return min;
        }
    }
}
