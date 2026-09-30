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
        [Tooltip("3-2-1-GO before every run")]
        [SerializeField] bool startCountdown = true;
        [Tooltip("Title screen, lobby, pause and settings (the shipped game). Off = straight into a run (dev, tests)")]
        [SerializeField] bool useFrontend = true;

        public BoardTuning Tuning => tuning;
        public BoardController Board { get; private set; }
        public BoardView BoardView { get; private set; }
        public RoadView RoadView { get; private set; }
        public PlayerInputRouter InputRouter { get; private set; }
        public RunManager Run { get; private set; }
        public CameraController CameraRig { get; private set; }
        public DebugOverlay Overlay { get; private set; }
        public GameFeel Feel { get; private set; }
        public HUDController Hud { get; private set; }
        public AudioDirector Audio { get; private set; }
        public BackdropView Backdrop { get; private set; }
        public FrontendController Frontend { get; private set; }

        /// <summary>Programmatic bootstrap (Play Mode tests, or an empty scene).</summary>
        public static GameManager Create(BoardTuning tuning = null, int players = 1, bool bots = true, bool keyboard = true,
                                         RoadMode mode = RoadMode.Endless, int seed = 0, bool countdown = false, bool frontend = false)
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
            gm.startCountdown = countdown;
            gm.useFrontend = frontend;
            go.SetActive(true);
            return gm;
        }

        void Awake()
        {
            Time.fixedDeltaTime = 1f / BoardController.SimulationRate;
            Application.targetFrameRate = 60;
            if (tuning == null) tuning = BoardTuning.CreateDefault();
            if (!tuning.data.Validate(out string error)) Debug.LogError("BoardTuning invalid: " + error);

            Light sun = SceneAtmosphere.Apply();
            Board = BoardController.Create(tuning, RunManager.NewSeed(seed), startPlayerCount, RunManager.TrackFor(roadMode));
            Board.transform.SetParent(transform, true);
            BoardView = BoardView.Create(Board);
            BoardFx.Create(Board, BoardView);
            Backdrop = BackdropView.Create(transform, Board, sun);
            RoadView = RoadView.Create(transform, Board.Run.Road);
            ObstacleViews.Create(transform, Board.Run.Obstacles);
            PickupViews.Create(transform, Board.Run.Pickups);
            Board.Run.HighScores = new Game.Simulation.HighScoreManager(new Game.Simulation.FileHighScoreStore(
                System.IO.Path.Combine(Application.persistentDataPath, "highscores.txt")));

            InputRouter = Board.gameObject.AddComponent<PlayerInputRouter>();
            InputRouter.Initialize(Board.Run, botsEnabled);
            InputRouter.KeyboardEnabled = keyboardEnabled;
            Board.SetInputProvider(InputRouter);

            for (int i = 0; i < Game.Simulation.BoardSimulation.MaxPlayers; i++) PlayerView.Create(i, Board, BoardView, InputRouter);

            CameraRig = CameraController.Create(Board);
            SpeedLines speedLines = SpeedLines.Create(CameraRig.GetComponent<Camera>(), Board);
            Feel = gameObject.AddComponent<GameFeel>();
            Feel.Initialize(Board);
            Run = gameObject.AddComponent<RunManager>();
            Run.Initialize(Board, CameraRig, roadMode, seed);

            Overlay = gameObject.AddComponent<DebugOverlay>();
            Overlay.Initialize(Board, BoardView, InputRouter, Run);
            var tuningPanel = gameObject.AddComponent<TuningPanel>();
            tuningPanel.Initialize(tuning);
            Hud = gameObject.AddComponent<HUDController>();
            Hud.Initialize(Board, Run, InputRouter, Overlay);
            speedLines.Feel = CameraRig.Feel = Hud.Presenter.Feel;
            Audio = AudioDirector.Create(gameObject, Board, Hud);
            gameObject.AddComponent<GameHotkeys>().Initialize(Board, InputRouter, Run, Overlay, tuningPanel, Hud);
            PlatformServices.Initialize();
            gameObject.AddComponent<AchievementReporter>().Initialize(Board, InputRouter);
            if (startCountdown) Run.EnableCountdown(Hud, startNow: !useFrontend);
            if (useFrontend)
            {
                Frontend = gameObject.AddComponent<FrontendController>();
                Frontend.Initialize(this);
            }
        }
    }
}
