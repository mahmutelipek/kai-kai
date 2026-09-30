using System;
using System.Collections.Generic;
using Game.Art;
using Game.Simulation;
using Game.Tests;

namespace ArtPreview
{
    /// <summary>
    /// Estimates draw calls from real game states: every Unity renderer the views create, counted per non-empty
    /// submesh (matte / glossy / glow), with no frustum culling (worst case). Not a measurement: Unity's own
    /// numbers come from the PlayMode test M4_SixRiders_RenderStatsReport.
    /// </summary>
    static class DrawCalls
    {
        static int Buckets(IEnumerable<ArtColor> colors)
        {
            var used = new HashSet<int>();
            foreach (ArtColor c in colors) used.Add(Palette.Bucket(c));
            return used.Count;
        }

        /// <summary>Draw calls of one model built by ArtBuilder: one renderer per group with parts.</summary>
        static int Model(ArtModel m)
        {
            var byGroup = new Dictionary<string, List<ArtColor>>();
            foreach (ArtPart p in m.Parts)
            {
                if (!byGroup.TryGetValue(p.Group, out var list)) byGroup[p.Group] = list = new List<ArtColor>();
                list.Add(p.Color);
            }
            int n = 0;
            foreach (var kv in byGroup) n += Buckets(kv.Value);
            return n;
        }

        public static void Report(int seed, float[] distances)
        {
            var t = new BoardTuningData { livesPerRun = 0 };
            Console.WriteLine("| distance | chunks | chunk DC | backdrop | board | 6 riders | obstacles (n) | pickups (n) | FX+HUD | total (main pass) |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (float d in distances)
            {
                RunSimulation run = GameplayScenes.Drive(new GameplayScenes.Options { Seed = seed, MinDistance = d });
                int chunkDc = 0;
                foreach (RoadChunk c in run.Road.Chunks)
                {
                    var set = new MeshSet();
                    ChunkGeometry.Build(c, set);
                    chunkDc += Buckets(set.ByColor.Keys);
                }
                int backdrop = Buckets(Backdrop.Build().ByColor.Keys);
                int board = Model(ArtLibrary.Board(t.boardWidth, t.boardLength, t.deckHeight, t.wheelRadius));
                int riders = 0;
                for (int i = 0; i < 6; i++) riders += Model(ArtLibrary.Character(i));
                int obstacles = 0, obstacleCount = 0;
                for (int i = 0; i < ObstacleField.Capacity; i++)
                {
                    ref Obstacle o = ref run.Obstacles.Items[i];
                    if (!o.Active) continue;
                    obstacleCount++;
                    obstacles += Model(ArtLibrary.Obstacle(o.Kind, 0));
                }
                int pickups = 0, pickupCount = 0;
                for (int i = 0; i < PickupField.Capacity; i++)
                {
                    ref Pickup p = ref run.Pickups.Items[i];
                    if (!p.Active || p.Collected) continue;
                    pickupCount++;
                    pickups += Model(ArtLibrary.Pickup(p.Kind));
                }
                const int fxAndHud = 5 /* particle systems */ + 24 /* speed lines at full speed */ + 45 /* IMGUI HUD quads and labels */;
                int total = chunkDc + backdrop + board + riders + obstacles + pickups + fxAndHud;
                Console.WriteLine($"| {run.Distance:0} m | {run.Road.Chunks.Count} | {chunkDc} | {backdrop} | {board} | {riders} | {obstacles} ({obstacleCount}) | {pickups} ({pickupCount}) | {fxAndHud} | {total} |");
            }
        }
    }
}
