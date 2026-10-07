using UnityEngine;

namespace Game
{
    /// <summary>
    /// Scene bootstrap. Builds the endless game or finite test setup from code (road, board, players, camera, run flow,
    /// debug tools) so the scene only needs this one component.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] BoardTuning tuning;
        [Range(2, 6)] [SerializeField] int playerCount = 6;
        [SerializeField] bool botsEnabled = true;
        [SerializeField] bool keyboardEnabled = true;
        [SerializeField] bool endlessRoad = true;
        [SerializeField] bool showMenu = true;

        public BoardTuning Tuning => tuning;
        public TestRoad Road { get; private set; }
        public BoardController Board { get; private set; }
        public BoardView BoardView { get; private set; }
        public PlayerInputRouter InputRouter { get; private set; }
        public RunManager Run { get; private set; }
        public CameraController CameraRig { get; private set; }
        public DebugOverlay Overlay { get; private set; }

        /// <summary>Programmatic bootstrap (Play Mode tests, or an empty scene).</summary>
        public static GameManager Create(BoardTuning tuning = null, int players = 6, bool bots = true, bool keyboard = true, bool endless = false)
        {
            var go = new GameObject("GameManager");
            go.SetActive(false); // configure before Awake runs
            var gm = go.AddComponent<GameManager>();
            gm.tuning = tuning;
            gm.playerCount = players;
            gm.botsEnabled = bots;
            gm.keyboardEnabled = keyboard;
            gm.endlessRoad = endless;
            gm.showMenu = false;
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
            Road = TestRoad.Build(transform, endlessRoad);
            Road.Path.Sample(0f, out Vector3 start, out float startYaw);

            Board = BoardController.Create(tuning, new UnityGroundProvider(), start, startYaw, playerCount);
            Board.transform.SetParent(transform, true);
            BoardView = BoardView.Create(Board);

            InputRouter = Board.gameObject.AddComponent<PlayerInputRouter>();
            InputRouter.Initialize(Road.Path, botsEnabled);
            InputRouter.KeyboardEnabled = keyboardEnabled;
            InputRouter.Streamer = Road.Streamer;
            Board.SetInputProvider(InputRouter);

            for (int i = 0; i < Game.Simulation.BoardSimulation.MaxPlayers; i++) PlayerView.Create(i, Board, BoardView, InputRouter);

            CameraRig = CameraController.Create(Board);
            Run = gameObject.AddComponent<RunManager>();
            Run.Initialize(Board, Road.Path, CameraRig, Road.Streamer);
            if (Road.Streamer != null)
            {
                Road.Streamer.Attach(Board);
                gameObject.AddComponent<RunFeedback>().Initialize(Board, Run);
                Run.ConfigureSession(showMenu);
                if (showMenu) InputRouter.CyclePreset();
                RoadAtmosphere.Apply();
                RunVisualPolish.Apply(transform, CameraRig.GetComponent<Camera>(), Board, Run);
                gameObject.AddComponent<RunHud>().Initialize(Board, Run);
            }

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
