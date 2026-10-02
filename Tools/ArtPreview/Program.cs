using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Game.Art;
using Game.Simulation;
using static Game.Art.ArtMath;

namespace ArtPreview
{
    /// <summary>A scene to export: tagged mesh sets in Unity (left-handed) world space plus a camera.</summary>
    sealed class PreviewScene
    {
        public readonly List<(string tag, MeshSet set, bool shadows)> Objects = new List<(string, MeshSet, bool)>();
        public Vector3 CameraPos, CameraTarget;
        public float Fov = 65f, Near = 0.2f, Far = 1500f, Roll;
        public Vector3? ShadowFocus;
        public float ShadowRadius = 40f;
        public bool Fog = true;
        public float FogNear = Atmosphere.FogStart, FogFar = Atmosphere.FogEnd;
        /// <summary>Yaw of the backdrop frame (the sun turns with it), radians.</summary>
        public float SunYaw;

        /// <summary>Optional HUD overlay (draw commands from Game.Hud.HudLayout) and the size it was laid out for.</summary>
        public System.Collections.Generic.List<Game.Hud.HudCmd> Hud;
        public int HudWidth = 1600, HudHeight = 900;

        public MeshSet Set(string tag, bool shadows = true)
        {
            foreach (var o in Objects) if (o.tag == tag) return o.set;
            var s = new MeshSet();
            Objects.Add((tag, s, shadows));
            return s;
        }

        void WriteHud(StringBuilder sb, CultureInfo ci)
        {
            string C(Game.Hud.HudColor c) => string.Format(ci, "[{0:0.###},{1:0.###},{2:0.###},{3:0.###}]", c.R, c.G, c.B, c.A);
            sb.Append(string.Format(ci, ",\"hud\":{{\"w\":{0},\"h\":{1},\"cmds\":[", HudWidth, HudHeight));
            var used = new HashSet<Game.Hud.HudTex>();
            for (int i = 0; i < Hud.Count; i++)
            {
                Game.Hud.HudCmd c = Hud[i];
                if (i > 0) sb.Append(',');
                sb.Append(string.Format(ci, "{{\"x\":{0:0.##},\"y\":{1:0.##},\"w\":{2:0.##},\"h\":{3:0.##},\"rot\":{4:0.##},\"skew\":{5:0.###},\"scale\":{6:0.###},\"color\":{7}",
                    c.X, c.Y, c.W, c.H, c.Rotation, c.Skew, c.Scale == 0f ? 1f : c.Scale, C(c.Color)));
                if (c.Text != null)
                {
                    sb.Append(",\"text\":\"").Append(c.Text.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                    sb.Append(string.Format(ci, ",\"size\":{0:0.##},\"align\":{1},\"outline\":{2:0.##},\"outlineColor\":{3}", c.FontSize, (int)c.Align, c.Outline, C(c.OutlineColor)));
                }
                else
                {
                    sb.Append(",\"tex\":\"").Append(c.Tex).Append('"');
                    used.Add(c.Tex);
                }
                sb.Append('}');
            }
            sb.Append("],\"tex\":{");
            bool first = true;
            foreach (Game.Hud.HudTex t in used)
            {
                Game.Hud.HudImage img = Game.Hud.HudTextures.Get(t);
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(t).Append(string.Format(ci, "\":{{\"w\":{0},\"h\":{1},\"b64\":\"", img.Width, img.Height)).Append(Convert.ToBase64String(img.Rgba)).Append("\"}");
            }
            sb.Append("}}");
        }

        public void Write(string path)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1 << 20);
            string V3(Vector3 v) => string.Format(ci, "[{0:0.###},{1:0.###},{2:0.###}]", v.X, v.Y, -v.Z);
            sb.Append("{\"camera\":{\"pos\":").Append(V3(CameraPos)).Append(",\"target\":").Append(V3(CameraTarget))
              .Append(string.Format(ci, ",\"fov\":{0},\"near\":{1},\"far\":{2},\"roll\":{3}}}", Fov, Near, Far, -Roll * Deg));
            if (Fog) sb.Append(string.Format(ci, ",\"fog\":{{\"color\":\"{0}\",\"near\":{1},\"far\":{2}}}", Atmosphere.FogColor.ToHex(), FogNear, FogFar));
            sb.Append(",\"sky\":\"").Append(Atmosphere.SkyHorizon.ToHex()).Append("\",\"skyTop\":\"").Append(Atmosphere.SkyTop.ToHex()).Append('"');
            sb.Append(",\"ambientSky\":\"").Append(Atmosphere.AmbientSky.ToHex()).Append("\",\"ambientGround\":\"").Append(Atmosphere.AmbientGround.ToHex()).Append('"');
            sb.Append(",\"sunColor\":\"").Append(Atmosphere.SunColor.ToHex()).Append("\",\"sunDir\":").Append(V3(-Vector3.Transform(Atmosphere.SunForward, Quaternion.CreateFromAxisAngle(Vector3.UnitY, SunYaw))));
            if (ShadowFocus.HasValue) sb.Append(",\"shadowFocus\":").Append(V3(ShadowFocus.Value));
            sb.Append(string.Format(ci, ",\"shadowRadius\":{0}", ShadowRadius));
            if (Hud != null) WriteHud(sb, ci);
            sb.Append(",\"objects\":[");
            bool firstObj = true;
            foreach (var (tag, set, shadows) in Objects)
            {
                if (!firstObj) sb.Append(',');
                firstObj = false;
                sb.Append("{\"tag\":\"").Append(tag).Append("\",\"shadows\":").Append(shadows ? "true" : "false").Append(",\"meshes\":[");
                bool firstMesh = true;
                foreach (var kv in set.ByColor)
                {
                    MeshData m = kv.Value;
                    if (m.Triangles.Count == 0) continue;
                    if (!firstMesh) sb.Append(',');
                    firstMesh = false;
                    sb.Append("{\"c\":\"").Append(kv.Key.ToHex()).Append(string.Format(ci, "\",\"s\":{0:0.##},\"v\":[", kv.Key.Smoothness));
                    for (int i = 0; i < m.Vertices.Count; i++)
                    {
                        Vector3 v = m.Vertices[i];
                        if (i > 0) sb.Append(',');
                        sb.Append(string.Format(ci, "{0:0.###},{1:0.###},{2:0.###}", v.X, v.Y, -v.Z)); // to right-handed
                    }
                    sb.Append("],\"i\":[");
                    for (int t = 0; t < m.Triangles.Count; t += 3)
                    {
                        if (t > 0) sb.Append(',');
                        sb.Append(m.Triangles[t]).Append(',').Append(m.Triangles[t + 2]).Append(',').Append(m.Triangles[t + 1]); // flip winding with the mirror
                    }
                    sb.Append("]}");
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            File.WriteAllText(path, sb.ToString());
        }
    }

    static class Program
    {
        static readonly BoardTuningData Tuning = new BoardTuningData();

        static int Main(string[] args)
        {
            // the Blender-made models, read from the same files Unity loads (PREVIEW_PROCEDURAL=1 shows the old primitives)
            if (Environment.GetEnvironmentVariable("PREVIEW_PROCEDURAL") != "1")
            {
                string models = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/Resources/Models"));
                Game.Art.ArtLibrary.ModelSource = name =>
                {
                    string f = Path.Combine(models, name + ".bytes");
                    return File.Exists(f) ? File.ReadAllBytes(f) : null;
                };
            }
            if (args.Length > 1 && args[0] == "audio")
            {
                AudioExport.Run(args[1]);
                return 0;
            }
            if (args.Length > 0 && args[0] == "drawcalls")
            {
                DrawCalls.Report(7, new[] { 300f, 1500f, 3000f, 6000f });
                return 0;
            }
            string outDir = args.Length > 0 ? args[0] : "out";
            Directory.CreateDirectory(outDir);
            var scenes = new Dictionary<string, Func<PreviewScene>>
            {
                ["chars_front"] = () => CharacterLineup(front: true),
                ["chars_back"] = () => CharacterLineup(front: false),
                ["chars_silhouette"] = CharacterSilhouettes,
                ["chars_colors"] = () => CharacterSheet(sameSpot: false),
                ["board_gameplay"] = BoardGameplay,
                ["reaction50"] = () => GameplayScenes.Reaction(50f),
                ["game_start"] = () => GameplayScenes.Build(new GameplayScenes.Options { MinDistance = 250f }),
                ["hud_curve"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.HardCurve, LocalAlong = 15f, Hud = true }),
                ["hud_construction"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Construction, LocalAlong = 30f, Hud = true }),
                ["menu_title"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.GentleCurve, LocalAlong = 20f, Menu = Game.Frontend.MenuScreen.Title }),
                ["menu_lobby"] = () => GameplayScenes.Build(new GameplayScenes.Options { MinDistance = 400f, Menu = Game.Frontend.MenuScreen.Lobby }),
                ["menu_pause_tr"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Bridge, LocalAlong = 40f, Hud = true, Menu = Game.Frontend.MenuScreen.Pause, Language = Game.Frontend.Language.Turkish }),
                ["menu_howto"] = () => GameplayScenes.Build(new GameplayScenes.Options { MinDistance = 300f, Menu = Game.Frontend.MenuScreen.HowToPlay }),
                ["menu_highscores"] = () => GameplayScenes.Build(new GameplayScenes.Options { MinDistance = 500f, Menu = Game.Frontend.MenuScreen.HighScores }),
                ["menu_credits"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.GentleCurve, LocalAlong = 20f, Menu = Game.Frontend.MenuScreen.Credits }),
                ["hud_moves"] = () => GameplayScenes.Build(new GameplayScenes.Options
                {
                    Kind = ChunkKind.HardCurve, LocalAlong = 15f, Hud = true,
                    Hud2 = (p, r) =>
                    {
                        var s = p.State;
                        s.MoveLabel = "STRAIGHTEN!"; s.MoveFill = 0.6f; s.MoveColor = Game.Hud.HudLayout.Yellow; s.MoveReady = true;
                        s.Tip = Game.Hud.TipCoach.Text(Game.Hud.Tip.Carve); s.TipAge = 1f;
                    },
                }),
                ["hud_jump"] = () => GameplayScenes.Build(new GameplayScenes.Options
                {
                    MinDistance = 700f, Hud = true,
                    Hud2 = (p, r) => { p.State.JumpCallAge = 0.25f; p.State.Tip = Game.Hud.TipCoach.Text(Game.Hud.Tip.JumpTogether); p.State.TipAge = 1f; },
                }),
                ["hud_end"] = () => GameplayScenes.Build(new GameplayScenes.Options
                {
                    MinDistance = 900f, Hud = true,
                    Hud2 = (p, r) =>
                    {
                        r.HighScores = GameplayScenes.SampleTable();
                        p.FillEndScreen(r);
                        Game.Hud.ScoreTable.Fill(r.HighScores.Top, 3, p.State.EndTable);
                        p.State.EndBest = "RANK #3   ·   BEST  48,210   ·   5,357 m";
                        p.State.Ended = true; p.State.EndNewBest = false; p.State.EndAge = 4f;
                    },
                }),
                ["menu_pause"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Bridge, LocalAlong = 40f, Hud = true, Menu = Game.Frontend.MenuScreen.Pause }),
                ["menu_settings"] = () => GameplayScenes.Build(new GameplayScenes.Options { MinDistance = 300f, Menu = Game.Frontend.MenuScreen.Settings }),
                ["hud_curve_tr"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.HardCurve, LocalAlong = 15f, Hud = true, Language = Game.Frontend.Language.Turkish }),
                ["hud_nitro"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Straight, LocalAlong = 20f, MinDistance = 500f, Hud = true, NitroSeconds = 0.12f }),
                ["hud_nitro_wind"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Straight, LocalAlong = 20f, MinDistance = 500f, Hud = true, NitroSeconds = 1.2f }),
                ["hud_start"] = () => GameplayScenes.Build(new GameplayScenes.Options { MinDistance = 60f, Hud = true, Countdown = "2" }),
                ["game_ramp"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Ramp, LocalAlong = 20f }),
                ["game_construction"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Construction, LocalAlong = 30f }),
                ["game_bridge"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Bridge, LocalAlong = 60f }),
                ["game_tunnel"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Tunnel, LocalAlong = 10f }),
                ["game_curve"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.HardCurve, LocalAlong = 15f }),
                ["game_traffic"] = () => GameplayScenes.Build(new GameplayScenes.Options { Kind = ChunkKind.Traffic, LocalAlong = 30f }),
            };
            for (int k = 0; k < GameplayScenes.ReactionItems; k++)
            {
                int item = k;
                scenes["react50_" + k] = () => GameplayScenes.Reaction(50f, item);
                scenes["react30_" + k] = () => GameplayScenes.Reaction(30f, item);
            }
            foreach (var kv in scenes)
            {
                if (args.Length > 1 && Array.IndexOf(args, kv.Key) < 0) continue;
                kv.Value().Write(Path.Combine(outDir, kv.Key + ".json"));
                Console.WriteLine("scene " + kv.Key);
            }
            return 0;
        }

        // ------------------------------------------------------------------ helpers

        static Quaternion Yaw(float deg) => Euler(0f, deg, 0f);

        /// <summary>Rest pose the PlayerView uses at cruising speed: arms out for balance, slight forward lean.</summary>
        static void AddRider(PreviewScene s, string tag, int slot, Vector3 pos, float yaw, float speedNorm = 0.5f) =>
            s.Set(tag).Add(ArtLibrary.Character(slot), pos, Yaw(yaw), 1f,
                RiderPose.AsLookup(RiderPose.Compute(new RiderPoseInput { SpeedNorm = speedNorm, Slot = slot, Time = slot * 0.37f })), null);

        static void AddGround(PreviewScene s, float size, ArtColor color)
        {
            MeshData g = s.Set("ground", false)[color];
            g.Quad(V(-size, 0f, -size), V(-size, 0f, size), V(size, 0f, size), V(size, 0f, -size));
        }

        /// <summary>Chase camera exactly as CameraController frames it (board at origin, yaw 0, cruise speed).</summary>
        static void ChaseCamera(PreviewScene s, Vector3 board, float speedNorm = 0.5f)
        {
            s.CameraPos = board + V(CameraRigDefaults.LateralOffset, CameraRigDefaults.Height, -(CameraRigDefaults.Distance + CameraRigDefaults.ExtraDistanceAtSpeed * speedNorm));
            s.CameraTarget = board + V(0f, CameraRigDefaults.LookHeight, CameraRigDefaults.LookAhead);
            s.Fov = CameraRigDefaults.FovMin + (CameraRigDefaults.FovMax - CameraRigDefaults.FovMin) * speedNorm;
        }

        static Vector2[] DeckSpots() => new[]
        {
            new Vector2(-0.55f, 1.9f), new Vector2(0.55f, 1.9f), new Vector2(-0.55f, 0f),
            new Vector2(0.55f, 0f), new Vector2(-0.55f, -1.9f), new Vector2(0.55f, -1.9f),
        };

        // ------------------------------------------------------------------ scenes

        static PreviewScene CharacterLineup(bool front)
        {
            var s = new PreviewScene { Fog = false };
            AddGround(s, 30f, Pal.Sidewalk);
            for (int i = 0; i < 6; i++) AddRider(s, "P" + (i + 1), i, V((i - 2.5f) * 1.25f, 0f, 0f), front ? 180f : 0f, 0.3f);
            s.CameraPos = front ? V(0f, 1.6f, -8f) : V(0f, 3.5f, 7.5f);
            // characters face +Z; the "front" camera sits at -Z after they are turned around
            if (front) s.CameraPos = V(0f, 1.5f, -8.5f);
            s.CameraTarget = V(0f, 0.85f, 0f);
            s.Fov = 40f;
            s.ShadowRadius = 10f;
            return s;
        }

        /// <summary>All six riders at the same deck spot, seen by the gameplay camera: masks for IoU, lit for colours.</summary>
        static PreviewScene CharacterSilhouettes() => CharacterSheet(sameSpot: true);

        /// <summary>
        /// Riders seen by the gameplay camera on the deck. sameSpot: all six at one spot (masks overlap exactly, for
        /// silhouette IoU); otherwise side by side 1.6 m apart (for colours and the visual sheet).
        /// </summary>
        static PreviewScene CharacterSheet(bool sameSpot)
        {
            var s = new PreviewScene { Fog = false };
            AddGround(s, 40f, Pal.Asphalt);
            for (int i = 0; i < 6; i++) AddRider(s, "P" + (i + 1), i, V(sameSpot ? 0f : (i - 2.5f) * 1.6f, Tuning.deckHeight, 0f), 0f);
            ChaseCamera(s, V(0f, 0f, 0f));
            s.ShadowRadius = 15f;
            return s;
        }

        static PreviewScene BoardGameplay()
        {
            var s = new PreviewScene { Fog = false };
            AddGround(s, 60f, Pal.Asphalt);
            var board = ArtLibrary.Board(Tuning.boardWidth, Tuning.boardLength, Tuning.deckHeight, Tuning.wheelRadius);
            s.Set("board").Add(board, Vector3.Zero, Quaternion.Identity);
            Vector2[] spots = DeckSpots();
            for (int i = 0; i < 6; i++) AddRider(s, "P" + (i + 1), i, V(spots[i].X, Tuning.deckHeight + 0.01f, spots[i].Y), 0f);
            ChaseCamera(s, Vector3.Zero);
            s.ShadowRadius = 15f;
            return s;
        }
    }
}
