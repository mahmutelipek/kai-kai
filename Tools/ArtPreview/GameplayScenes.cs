using System;
using System.Numerics;
using Game.Art;
using Game.Simulation;
using Game.Tests;
using static Game.Art.ArtMath;

namespace ArtPreview
{
    /// <summary>
    /// Gameplay frames: a real RunSimulation driven by the ideal driver (the M2 acceptance driver), frozen at a
    /// chosen chunk kind, then every visible thing baked exactly as the Unity views place it.
    /// </summary>
    static class GameplayScenes
    {
        public sealed class Options
        {
            public int Seed = 7;
            public ChunkKind? Kind;
            public float MinDistance = 300f;
            public float LocalAlong = 40f;
            public float CameraHeight = CameraRigDefaults.Height;
            public float CameraDistance = CameraRigDefaults.Distance;
            public float LookAhead = CameraRigDefaults.LookAhead;
            public float LookHeight = CameraRigDefaults.LookHeight;
            public float Lateral = CameraRigDefaults.LateralOffset;
            public float FovMin = CameraRigDefaults.FovMin, FovMax = CameraRigDefaults.FovMax;
            public bool Backdrop = true;
            public bool Scenery = true;
            public bool Hud;
            /// <summary>Front-end screen drawn over the scene (the in-game HUD is hidden behind title / lobby).</summary>
            public Game.Frontend.MenuScreen Menu;
            public Game.Frontend.Language Language;
            public string Countdown;
        }

        public static RunSimulation Drive(Options o) => Drive(o, null);

        public static RunSimulation Drive(Options o, Action<RunStepEvents, RunSimulation> perStep)
        {
            var t = new BoardTuningData { livesPerRun = 0 };
            var run = new RunSimulation(t, o.Seed, 6);
            var driver = new IdealDriver(run);
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            float dt = BoardScenario.Dt;
            for (int step = 0; step < 200000; step++)
            {
                driver.Drive(dt);
                RunStepEvents ev = run.Step(dt, none);
                perStep?.Invoke(ev, run);
                RoadChunk c = run.Road.ChunkAt(run.Distance);
                if (run.Distance < o.MinDistance || run.Board.Board.State.Crashed || !run.Board.Board.State.Grounded) continue;
                if (o.Kind == null || (c.Kind == o.Kind && run.Distance - c.StartAlong > o.LocalAlong)) break;
            }
            return run;
        }

        public static PreviewScene Build(Options o)
        {
            Game.Frontend.Loc.Current = o.Language;
            var hud = new Game.Hud.HudPresenter();
            float simTime = 0f;
            RunSimulation run = Drive(o, (ev, r) =>
            {
                simTime += BoardScenario.Dt;
                if (!o.Hud) return;
                hud.OnStep(ev, r);
                hud.Update(r, 0f, simTime, BoardScenario.Dt);
            });
            var s = new PreviewScene();
            Game.Frontend.Loc.Current = o.Language;
            if (o.Hud || o.Menu != Game.Frontend.MenuScreen.None)
            {
                hud.State.Human[0] = true;
                if (o.Countdown != null) { hud.SetCountdown(o.Countdown); hud.State.CountdownAge = 0.4f; }
                s.Hud = new System.Collections.Generic.List<Game.Hud.HudCmd>();
                float k = s.HudWidth, h = s.HudHeight;
                if (o.Menu == Game.Frontend.MenuScreen.None || o.Menu == Game.Frontend.MenuScreen.Pause)
                    Game.Hud.HudLayout.Build(hud.State, k, h, 0f, 0f, k, h, s.Hud);
                if (o.Menu != Game.Frontend.MenuScreen.None)
                {
                    var model = new Game.Frontend.FrontendModel(new Game.Frontend.GameSettings { Language = o.Language });
                    model.Resolutions = new[] { "1280 x 720", "1920 x 1080", "2560 x 1440" };
                    model.Lobby.Join(Game.Frontend.JoinedDevice.Keyboard(false));
                    model.Lobby.Join(Game.Frontend.JoinedDevice.Pad(1));
                    model.Lobby.Join(Game.Frontend.JoinedDevice.Pad(2));
                    model.Lobby.CrewSize = 5;
                    if (o.Menu == Game.Frontend.MenuScreen.Settings) { model.Open(Game.Frontend.MenuScreen.Pause, false); model.Open(Game.Frontend.MenuScreen.Settings); }
                    else model.Open(o.Menu, false);
                    if (o.Menu == Game.Frontend.MenuScreen.Settings) model.Handle(new Game.Frontend.MenuInput { Down = true });
                    var menu = new System.Collections.Generic.List<Game.Hud.HudCmd>();
                    Game.Hud.FrontendLayout.Build(model, 1f, k, h, 0f, 0f, k, h, menu);
                    s.Hud.AddRange(menu);
                }
            }
            BoardSimulation sim = run.Board;
            BoardState b = sim.Board.State;
            BoardTuningData t = sim.Tuning;

            // road chunks (+ scenery)
            MeshSet road = s.Set("road");
            foreach (RoadChunk chunk in run.Road.Chunks)
            {
                if (o.Scenery) ChunkGeometry.Build(chunk, road);
            }

            // backdrop follows the board, turned to the travel direction
            float frameYaw = b.TravelYaw;
            if (o.Backdrop)
                AddSet(s.Set("backdrop", false), Backdrop.Build(), V(b.Position.X, b.Position.Y, b.Position.Z), Quaternion.CreateFromAxisAngle(Vector3.UnitY, frameYaw));
            s.SunYaw = frameYaw;

            // obstacles
            for (int i = 0; i < ObstacleField.Capacity; i++)
            {
                ref Obstacle ob = ref run.Obstacles.Items[i];
                if (!ob.Active || ob.Knocked) continue;
                int variant = i % ArtLibrary.ObstacleVariants(ob.Kind);
                Vector2 unit = ObstacleCatalog.DefaultHalfExtents(ob.Kind);
                var scaled = new Vector3(ob.HalfExtents.X / unit.X, 1f, ob.HalfExtents.Y / unit.Y);
                AddScaled(s.Set("obstacle " + ob.Kind), ArtLibrary.Obstacle(ob.Kind, variant), ob.Position, Euler(0f, ob.Yaw * SimMath.Rad2Deg, 0f), scaled);
            }

            // pickups
            for (int i = 0; i < PickupField.Capacity; i++)
            {
                ref Pickup p = ref run.Pickups.Items[i];
                if (!p.Active || p.Collected) continue;
                s.Set("pickup " + p.Kind).Add(ArtLibrary.Pickup(p.Kind), p.Position, Euler(0f, i * 37f, 0f));
            }

            // board and riders
            Quaternion boardRot = PoseRotation(b.Yaw, b.Pitch, b.Roll);
            Vector3 boardPos = b.Position;
            s.Set("board").Add(ArtLibrary.Board(t.boardWidth, t.boardLength, t.deckHeight, t.wheelRadius), boardPos, boardRot);
            for (int i = 0; i < sim.ActivePlayerCount; i++)
            {
                PlayerSim p = sim.Players[i];
                if (!p.IsOnBoard) continue;
                Vector3 local = V(p.LocalPosition.X, t.deckHeight + 0.01f + p.Height, p.LocalPosition.Y);
                Vector3 world = boardPos + Vector3.Transform(local, boardRot);
                var pose = RiderPose.Compute(new RiderPoseInput
                {
                    SpeedNorm = SimMath.Clamp01(b.Speed / Math.Max(t.softCapSpeed, 1f)), Wobble = b.Wobble, BoardRoll = b.Roll,
                    YawRate = b.YawRate, Time = i * 0.37f, Slot = i,
                });
                s.Set("P" + (i + 1)).Add(ArtLibrary.Character(i), world, boardRot, 1f, RiderPose.AsLookup(pose), null);
            }

            // chase camera as CameraController frames it
            float sn = SimMath.Clamp01(b.Speed / Math.Max(t.softCapSpeed, 1f));
            Vector3 fwd = SimMath.Forward3(b.TravelYaw);
            Vector3 right = SimMath.Right3(b.TravelYaw);
            s.CameraPos = boardPos - fwd * (o.CameraDistance + CameraRigDefaults.ExtraDistanceAtSpeed * sn) + V(0f, o.CameraHeight, 0f) + right * o.Lateral;
            s.CameraTarget = boardPos + fwd * o.LookAhead + V(0f, o.LookHeight, 0f);
            s.Fov = o.FovMin + (o.FovMax - o.FovMin) * sn;
            s.Roll = -b.Roll * SimMath.Rad2Deg * 0.25f * Deg;
            s.ShadowFocus = boardPos + fwd * 20f;
            s.ShadowRadius = 45f;
            Console.WriteLine($"  seed {o.Seed}: {run.Distance:0} m, chunk {run.Road.ChunkAt(run.Distance).Kind}, speed {b.Speed:0.0} m/s, " +
                              $"{run.Road.Chunks.Count} chunks, road verts {road.VertexCount}");
            return s;
        }

        /// <summary>
        /// Every obstacle and pickup side by side at <paramref name="distance"/> metres ahead of the board on the
        /// straight M1 test road, seen by the gameplay camera: used to measure on-screen size at reaction distance.
        /// </summary>
        public const int ReactionItems = 12;

        public static PreviewScene Reaction(float distance, int only = -1, bool withRiders = true)
        {
            var t = new BoardTuningData { livesPerRun = 0 };
            var run = new RunSimulation(t, 1, 6, TestRoadLayout.Build());
            var s = new PreviewScene();
            MeshSet road = s.Set("road");
            foreach (RoadChunk chunk in run.Road.Chunks) ChunkGeometry.Build(chunk, road);
            float boardAlong = 60f;
            run.Road.Sample(boardAlong, out Vector3 bp, out float yaw, out _);
            AddSet(s.Set("backdrop", false), Backdrop.Build(), bp, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw));
            s.SunYaw = yaw;
            Quaternion rot = Euler(0f, yaw * SimMath.Rad2Deg, 0f);
            s.Set("board").Add(ArtLibrary.Board(t.boardWidth, t.boardLength, t.deckHeight, t.wheelRadius), bp, rot);
            if (withRiders)
            {
                Vector2[] spots = { new Vector2(-0.6f, 1.6f), new Vector2(0.6f, 1.6f), new Vector2(-0.6f, 0f), new Vector2(0.6f, 0f), new Vector2(-0.6f, -1.6f), new Vector2(0.6f, -1.6f) };
                for (int i = 0; i < 6; i++)
                    s.Set("P" + (i + 1)).Add(ArtLibrary.Character(i), bp + Vector3.Transform(V(spots[i].X, t.deckHeight + 0.01f, spots[i].Y), rot), rot, 1f,
                        RiderPose.AsLookup(RiderPose.Compute(new RiderPoseInput { SpeedNorm = 0.6f, Slot = i, Time = i * 0.37f })), null);
            }

            // items across the road at the reaction distance; lateral spacing keeps them apart on screen
            var items = new (string tag, Action<MeshSet, Vector3, Quaternion> add)[]
            {
                ("obstacle Cone", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.Cone), p, r)),
                ("obstacle ConcreteBarrier", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.ConcreteBarrier), p, r)),
                ("obstacle ParkedCar", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.ParkedCar, 1), p, r)),
                ("obstacle ConstructionBarrier", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.ConstructionBarrier), p, r)),
                ("obstacle Pothole", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.Pothole), p, r)),
                ("obstacle Crate", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.Crate), p, r)),
                ("obstacle BrokenPiece", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.BrokenPiece), p, r)),
                ("obstacle Divider", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.Divider), p + Vector3.Transform(V(0f, 0f, 10f), r), r)),
                ("pickup Coin", (m, p, r) => m.Add(ArtLibrary.Pickup(PickupKind.Coin), p + V(0f, 1.1f, 0f), r)),
                ("pickup Diamond", (m, p, r) => m.Add(ArtLibrary.Pickup(PickupKind.Diamond), p + V(0f, 1.2f, 0f), r)),
                ("pickup Nitro", (m, p, r) => m.Add(ArtLibrary.Pickup(PickupKind.Nitro), p + V(0f, 1.1f, 0f), r)),
                ("obstacle MovingCar", (m, p, r) => m.Add(ArtLibrary.Obstacle(ObstacleKind.MovingCar, 0), p, r)),
            };
            float spacing = 2.2f;
            for (int k = 0; k < items.Length; k++)
            {
                if (only >= 0 && k != only) continue;
                // lineup: two staggered rows across the road; measurement: alone, 2.5 m right of the centre line
                float lateral = only >= 0 ? 2.5f : ((k / 2) - 2.5f) * spacing + (k % 2) * 1.1f;
                float depth = only >= 0 ? 0f : (k % 2) * 6f;
                run.Road.Sample(boardAlong + distance + depth, out Vector3 p, out float y2, out _);
                Quaternion r2 = Euler(0f, y2 * SimMath.Rad2Deg, 0f);
                items[k].add(s.Set(items[k].tag), p + SimMath.Right3(y2) * lateral, r2);
            }

            Vector3 fwd = SimMath.Forward3(yaw);
            float sn = 0.6f;
            s.CameraPos = bp - fwd * (CameraRigDefaults.Distance + CameraRigDefaults.ExtraDistanceAtSpeed * sn) + V(0f, CameraRigDefaults.Height, 0f) + SimMath.Right3(yaw) * CameraRigDefaults.LateralOffset;
            s.CameraTarget = bp + fwd * CameraRigDefaults.LookAhead + V(0f, CameraRigDefaults.LookHeight, 0f);
            s.Fov = CameraRigDefaults.FovMin + (CameraRigDefaults.FovMax - CameraRigDefaults.FovMin) * sn;
            s.ShadowFocus = bp + fwd * (distance * 0.5f);
            s.ShadowRadius = distance * 0.7f + 20f;
            return s;
        }

        /// <summary>Same as SimConvert.PoseRotation in Unity: Euler(-pitch, yaw, -roll).</summary>
        static Quaternion PoseRotation(float yaw, float pitch, float roll) =>
            Euler(-pitch * SimMath.Rad2Deg, yaw * SimMath.Rad2Deg, -roll * SimMath.Rad2Deg);

        static void AddSet(MeshSet dst, MeshSet src, Vector3 pos, Quaternion rot)
        {
            foreach (var kv in src.ByColor)
            {
                MeshData to = dst[kv.Key];
                int baseIndex = to.Vertices.Count;
                foreach (Vector3 v in kv.Value.Vertices) to.Vertices.Add(pos + Vector3.Transform(v, rot));
                foreach (int i in kv.Value.Triangles) to.Triangles.Add(baseIndex + i);
            }
        }

        /// <summary>Bakes a model whose "Scaled" group is stretched like ObstacleViews stretches it.</summary>
        static void AddScaled(MeshSet set, ArtModel model, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            foreach (ArtPart p in model.Parts)
            {
                Vector3 c = p.Position, size = p.Size;
                if (p.Group == "Scaled")
                {
                    c *= scale;
                    size *= scale; // parts in "Scaled" are axis aligned (or only yawed a little)
                }
                MeshSet.AddShape(set[p.Color], p.Shape, pos + Vector3.Transform(c, rot), rot * p.Rotation, size);
            }
        }
    }
}
