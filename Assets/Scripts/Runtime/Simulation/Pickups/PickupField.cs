using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    public enum PickupKind : byte
    {
        Coin,
        Diamond,
        Nitro,
    }

    public struct Pickup
    {
        public bool Active;
        public bool Collected;
        public PickupKind Kind;
        public int Generation;
        public int ChunkSerial;
        public float Along;
        public float Lateral;
        /// <summary>Height above the road surface.</summary>
        public float Hover;
        public Vector3 Position;
        /// <summary>Seconds since collection (views animate the pop).</summary>
        public float CollectTime;
    }

    public struct PickupEvent
    {
        public int Slot;
        public PickupKind Kind;
    }

    /// <summary>Fixed pool of pickups placed with the road; collection against the board footprint (+ riders' height).</summary>
    public sealed class PickupField
    {
        public const int Capacity = 768;
        public const float CollectRadius = 0.9f;
        /// <summary>The board plus its riders reach this high above the deck contact point.</summary>
        public const float CrewHeight = 2.2f;

        public readonly Pickup[] Items = new Pickup[Capacity];
        int _cursor;

        public int ActiveCount { get; private set; }

        public int Spawn(PickupKind kind, int chunkSerial, float along, float lateral, float hover, RoadModel road)
        {
            for (int n = 0; n < Capacity; n++)
            {
                int i = (_cursor + n) % Capacity;
                if (Items[i].Active) continue;
                int generation = Items[i].Generation + 1;
                Items[i] = new Pickup
                {
                    Active = true,
                    Kind = kind,
                    Generation = generation,
                    ChunkSerial = chunkSerial,
                    Along = along,
                    Lateral = lateral,
                    Hover = hover,
                    Position = road.WorldPoint(along, lateral, out _) + new Vector3(0f, hover, 0f),
                };
                _cursor = (i + 1) % Capacity;
                ActiveCount++;
                return i;
            }
            return -1;
        }

        public void Despawn(int slot)
        {
            if (slot < 0 || !Items[slot].Active) return;
            Items[slot].Active = false;
            ActiveCount--;
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) Items[i].Active = false;
            ActiveCount = 0;
        }

        public void Step(float dt)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!Items[i].Active || !Items[i].Collected) continue;
                Items[i].CollectTime += dt;
                if (Items[i].CollectTime > 0.6f) Despawn(i);
            }
        }

        /// <summary>Collects every pickup the board footprint (plus crew height) touches this step.</summary>
        public int Collect(in BoardState board, BoardTuningData t, float boardAlong, List<PickupEvent> collected)
        {
            collected.Clear();
            if (board.Crashed) return 0;
            var center = new Vector2(board.Position.X, board.Position.Z);
            Vector2 fwd = SimMath.HeadingToDirection(board.Yaw);
            var right = new Vector2(fwd.Y, -fwd.X);
            for (int i = 0; i < Capacity; i++)
            {
                ref Pickup p = ref Items[i];
                if (!p.Active || p.Collected || Math.Abs(p.Along - boardAlong) > 6f) continue;
                float dy = p.Position.Y - board.Position.Y;
                if (dy < -CollectRadius || dy > CrewHeight + CollectRadius) continue;
                Vector2 d = new Vector2(p.Position.X, p.Position.Z) - center;
                float localX = Math.Abs(Vector2.Dot(d, right)) - t.HalfWidth;
                float localZ = Math.Abs(Vector2.Dot(d, fwd)) - t.HalfLength;
                float outside = new Vector2(Math.Max(localX, 0f), Math.Max(localZ, 0f)).Length();
                if (outside > CollectRadius) continue;
                p.Collected = true;
                p.CollectTime = 0f;
                collected.Add(new PickupEvent { Slot = i, Kind = p.Kind });
            }
            return collected.Count;
        }
    }
}
