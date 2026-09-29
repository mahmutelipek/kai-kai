using UnityEngine;

namespace Game
{
    /// <summary>
    /// Scene bootstrap. Builds everything from code (simulation host, road and obstacle views, board, players,
    /// camera, run flow, debug tools) so the scene only needs this one component.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] BoardTuning tuning;
        [SerializeField] RoadMode roadMode = RoadMode.Endless;
        [Tooltip("0 = new random road every run")]
        [SerializeField] int seed = 0;
        // Renamed from playerCount so scenes saved with the old default (6) start solo too.
        [Range(1, 6)] [SerializeField] int startPlayerCount = 1;
        [SerializeField] bool botsEnabled = true;
        [SerializeField] bool keyboardEnabled = true;

        public BoardTuning Tuning => tuning;
        public BoardController Board { get; private set; }
        public BoardView BoardView { get; private set; }
        public RoadView RoadView { get; private set; }
        public PlayerInputRouter InputRouter { get; private set; }
        public RunManager Run { get; private set; }
        public CameraController CameraRig { get; private set; }
        public DebugOverlay Overlay { get; private set; }
        public GameFeel Feel { get; private set; }

        /// <summary>Programmatic bootstrap (Play Mode tests, or an empty scene).</summary>
        public static GameManager Create(BoardTuning tuning = null, int players = 1, bool bots = true, bool keyboard = true,
                                         RoadMode mode = RoadMode.Endless, int seed = 0)
        {
            var go = new GameObject("GameManager");
            go.SetActive(false); // configure before Awake runs
            var gm = go.AddComponent<GameManager>();
            gm.tuning = tuning;
            gm.startPlayerCount = players;
            gm.botsEnabled = bots;
            gm.keyboardEnabled = keyboard;
            gm.roadMode = mode;
            gm.seed = seed;
            go.SetActive(true);
            return gm;
        }

        void Awake()
        {
            Time.fixedDeltaTime = 1f / BoardController.SimulationRate;
            Application.targetFrameRate = 60;
            if (tuning == null) tuning = BoardTuning.CreateDefault();
            if (!tuning.data.Validate(out string error)) Debug.LogError("BoardTuning invalid: " + error);

            EnsureLight();
            Board = BoardController.Create(tuning, RunManager.NewSeed(seed), startPlayerCount, RunManager.TrackFor(roadMode));
            Board.transform.SetParent(transform, true);
            BoardView = BoardView.Create(Board);
            RoadView = RoadView.Create(transform, Board.Run.Road);
            ObstacleViews.Create(transform, Board.Run.Obstacles);

            InputRouter = Board.gameObject.AddComponent<PlayerInputRouter>();
            InputRouter.Initialize(Board.Run, botsEnabled);
            InputRouter.KeyboardEnabled = keyboardEnabled;
            Board.SetInputProvider(InputRouter);

            for (int i = 0; i < Game.Simulation.BoardSimulation.MaxPlayers; i++) PlayerView.Create(i, Board, BoardView, InputRouter);

            CameraRig = CameraController.Create(Board);
            SpeedLines.Create(CameraRig.GetComponent<Camera>(), Board);
            Feel = gameObject.AddComponent<GameFeel>();
            Feel.Initialize(Board);
            Run = gameObject.AddComponent<RunManager>();
            Run.Initialize(Board, CameraRig, roadMode, seed);

            Overlay = gameObject.AddComponent<DebugOverlay>();
            Overlay.Initialize(Board, BoardView, InputRouter, Run);
            var tuningPanel = gameObject.AddComponent<TuningPanel>();
            tuningPanel.Initialize(tuning);
            gameObject.AddComponent<GameHotkeys>().Initialize(Board, InputRouter, Run, Overlay, tuningPanel);
        }

        static void EnsureLight()
        {
            if (FindFirstObjectByType<Light>() != null) return;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.75f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.75f, 0.78f, 0.8f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.42f, 0.38f);
        }
    }
}
