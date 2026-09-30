# Downhill Party Board

Co-op endless downhill party game prototype (Unity 6, URP). 1–6 players stand on one giant longboard (2–6 is the real game; 1 is a solo test mode).
There is no steering input: **the board is steered only by where the players stand.**

Current state: **Milestone 4 – stylised visual pass** (6 distinct riders, giant longboard, coastal hillside with bay, bridge and
skyline, glowing pickups, wheel dust, 3/4 chase camera, post-processing) plus a more dynamic tuning. Milestones 1–3 are done.
M4.1 follow-up: reference-style HUD (brush panels, coin / gem pills, slanted COMBO and NITRO banners, speed wedge,
rider avatars), 3-2-1-GO start, nitro flame and camera kick, landing dust, danger warning, a town below the road.
Screenshots (headless preview of the same geometry and HUD draw commands): `Docs/M4/`.

---

## Opening the project

1. Unity Hub → *Add project from disk* → this folder. Use a **Unity 6** editor (tested target: 6000.6.3f1;
   6000.0.58f2 has a known package-signature bug, do not use it). Accept the project upgrade prompt.
2. Packages resolve from `Packages/manifest.json` (Input System, URP, Test Framework).
   If Unity asks to **enable the new Input System backends / restart**, answer **Yes**.
3. On first import `Game.Editor/ProjectSetup` creates `Assets/Settings/BoardTuning.asset` and
   `Assets/Scenes/M1_TestScene.unity` and opens the scene. (Menu **Downhill → Rebuild M1 Test Scene** redoes it.)
4. Press **Play**. The scene only contains a `GameManager`; road, board, players, camera and debug tools are built from code.
   `GameManager` fields: road mode (Endless / TestTrack), seed (0 = random each run), start player count, bots, keyboard.

**Render pipeline:** on first open `UrpSetup` creates `Assets/Settings/URP_Pipeline.asset` (+ `URP_Renderer.asset`)
and assigns it as the default and per-quality-level pipeline (menu **Downhill → Setup URP Pipeline** redoes it).
If that fails, the Console shows a warning with the manual steps. Materials use `Universal Render Pipeline/Lit`
when URP is active and fall back to `Standard` otherwise.

## Controls (local testing)

| Key | Action |
|---|---|
| WASD / arrows | Move your player on the deck (W = toward the nose) |
| Space | Jump (airborne players weigh 25 %) |
| E | Fire a stored nitro (any player's action button can do it) |
| Tab | Switch which player the keyboard controls |
| Gamepad | Each connected gamepad takes over one more player (left stick / d-pad, A/Cross = jump) |
| B | Bots on / off (bots drive every slot not taken by keyboard / gamepad) |
| C | Bot mix: *Mixed* (cooperative, stubborn-left, stubborn-right, wanderer, greedy-front, scared-rear) ↔ *All cooperative*. Every bot keeps its own lane along the deck; all but the stubborn ones partly follow the road |
| 1 – 6 (or numpad) | Exactly that many riders on the board (1 = solo, shown as "RIDERS: n"). The game starts **solo**: you alone steer the board, the clearest way to feel the mechanic. Add players / bots with 2–6 |
| R | Restart the run (endless: new random road). On the end screen also Space / gamepad A. Every run starts with 3-2-1-GO (GameManager → Start Countdown) |
| M | Switch between the endless road and the M1 test track |
| F1 | Debug overlay: centre-of-mass dot (magenta), smoothed steering (cyan), lateral/longitudinal/steering/roll/speed/danger |
| F2 | Live tuning panel (every `BoardTuning` value) |
| F3 | Reduce motion: no camera shake, hit-stop / slow motion or speed lines (accessibility) |

Crash → players are thrown off → the board respawns on the free line of the road ~2.5 s later (10 m back).
The M1 test track (M key) is ~2.5 km: straight, left curve, straight, right curve, long straight with cones and a ramp.

## Pickups, score and combo (Milestone 3)

- **Coins** in lines along the planned safe line; **diamonds** on risky lines (grazing a barrier, the outside of a
  hard curve, next to a wall or debris, or high above a ramp landing – only reachable in the air);
  **nitro** about once per kilometre: stored (max 2), fired by any player's action button. Nitro = +12 m/s target
  speed and +35 % instability for 3 s (`nitro*` tuning).
- **Score** = (distance + coins + diamonds + near misses + airtime) × combo multiplier at the moment each point is earned.
  Multiplier = 1 + combo / `comboStep` (10), capped at `comboMaxMultiplier` (×6).
- **Combo** +1 coin, +3 diamond, +2 near miss, +2 clean landing, +3 hard section survived, +1 nitro pickup.
  It drains after `comboIdleTime` (4 s) without events; a light hit (cone, crate, pothole) halves it;
  a heavy hit, a player falling off or a crash resets it.
- **Near miss**: passing a solid obstacle or car within `nearMissDistance` (1.2 m) at ≥ 10 m/s without touching it.
- **Clean landing**: board comes down balanced (below the wobble zone), nobody thrown off, no crash.
- **Run end**: `livesPerRun` (3) crashes end the run → end screen with the breakdown and best score. 0 = endless practice.
- **High score** (best score, best distance) is stored in `Application.persistentDataPath/highscores.txt`.

## Endless road (Milestone 2)

`RoadGenerator` assembles the road from `RoadChunkLibrary` chunks, 450 m ahead of the board, recycling chunks
150 m behind it (pre-filled pool, no allocation while playing). Chunk types: straight, gentle L/R curve,
hard L/R curve, S-curve, narrow section (walls), construction area, bridge (no shoulders – falling off = crash),
tunnel (walls + roof), ramp, broken road (potholes + debris), traffic (same-direction and, later, oncoming cars),
downhill intersection (cross traffic that waits for a gap), partial barriers (weave), fork (central divider).
Every chunk has entry/exit sockets, a width profile, edge types and metadata (rating 1–10, earliest distance).

**Always traversable:** after placing a chunk's obstacles, `RoadPlanner` proves a line exists that the board
centre can follow (asphalt minus inflated obstacles and traffic lanes, lateral change limited to 0.06 m per m).
If a random layout ever blocks the way, the blocking obstacle is removed. Cooperative bots follow that line.

**Difficulty** (`DifficultyManager`, 0 → 1 over `difficultyFullDistance` = 8 km) raises speed *and* decisions:
target chunk rating 1.5 → 8, road width 14 → 10 m, obstacle density, traffic speed, oncoming traffic (> 0.55),
chained hazards such as ramp → hard curve (> 0.65), speed cap +25 %.

**Collision rules** (all inside the simulation, not Unity physics):

| Obstacle | Severity |
|---|---|
| cone, construction barrier, crate | light: knocked away, −8 % speed (M3: combo hit) |
| pothole | bump: −6 % speed, everybody staggers |
| concrete barrier, parked / moving car, divider, debris | heavy: deflect, −30 % speed, stagger, players on the impact side fall off |
| heavy obstacle hit with closing speed > 16 m/s | crash |
| tunnel / narrow walls | scrape: pushed back, speed loss; hitting a wall at > 11 m/s sideways = crash |

## How steering works

```
board-local player positions
  → nx = x / halfWidth, nz = z / halfLength           (per player, clamped to ±1)
  → lateral = weighted mean(nx), longitudinal = weighted mean(nz)   (weight 0 if fallen, 0.25 if airborne)
  → steering = sign(lateral) · |lateral|^steeringExponent
  → smoothed with a first-order lag (steeringSmoothingTime)
  → target yaw rate = steering · (yawRateBaseDeg + yawRatePerSpeedDeg · speed)
  → yaw rate follows the target with yawResponseTime (torque-like), heading integrates the yaw rate
  → roll = lean into the turn + wobble + tipping
```

Board rotation is never set from input; it is integrated from the yaw rate. The weighted **mean** normalizes
by player count: 2 players at the edge steer exactly as hard as 6 players at the edge.

**Danger** = |smoothed steering| × stability(speed, front weight) / crashThreshold.
From `wobbleStartFraction` (0.7) the board wobbles (roll + yaw oscillation), grip drops and players slide toward
the low side. At ≥ 1.0 a tip accumulator fills in `crashTipTime`; when full the board crashes.

## Tuning parameters (`Assets/Settings/BoardTuning.asset`, F2 in play mode)

| Group | Field | Default | Meaning |
|---|---|---|---|
| Board | boardLength / boardWidth | 6 / 2.4 m | Deck size (visuals update after restart) |
| | wheelRadius / deckHeight | 0.35 / 0.85 m | |
| Steering | steeringExponent | 1.6 | Response curve; > 1 = centre players matter less, edge players more |
| | steeringSmoothingTime | 0.28 s (M3: 0.4) | Heaviness / delay of the response |
| | airborneWeightFactor | 0.25 | Weight of a jumping player |
| | yawRateBaseDeg / yawRatePerSpeedDeg | 24 (M3: 20) / 1.2 | Max yaw rate = base + perSpeed·speed (deg/s): steering gets stronger with speed |
| | yawResponseTime | 0.18 s (M3: 0.25) | How fast the yaw rate follows its target (the "torque") |
| | tractionAlignRate | 8 /s | How quickly the travel direction follows the heading (× grip) |
| Roll | maxRollDeg / rollResponseTime | 24° / 0.15 s (M3: 18° / 0.2) | Lean into turns |
| Stability | stabilityFactorLowSpeed / HighSpeed | 1.25 / 1.8 | Danger multiplier, interpolated by speed / stabilityReferenceSpeed (35) |
| | frontWeightInstability | 0.15 | Extra danger from front weight at speed |
| | crashThreshold | 1.0 | Danger = 1 means crash zone |
| | wobbleStartFraction | 0.7 | Wobble / grip loss / sliding begin here |
| | wobbleMaxRollDeg / wobbleFrequencyHz | 6° / 4.5 Hz | |
| | gripLossMax | 0.5 | Grip at danger 1 = 50 % |
| | crashTipTime / tipRecoveryTime | 0.5 / 1.0 s | Time in the crash zone before flipping / recovering |
| Speed | startSpeed / speedRampPerSecond / softCapSpeed | 12 m/s / 0.4 / 38 m/s (M3: 8 / 0.25 / 35) | Cruise speed rises over time to the soft cap |
| | cruiseGain | 0.35 /s | Pull toward cruise speed |
| | frontAcceleration / rearBraking | 6 (M3: 4) / 6 m/s² | Scaled by longitudinal weight |
| | frontAccelFadeAboveCap | 0.15 | Front push fades between 0.85× and 1.15× the cap |
| | minSpeed / offroadDrag | 3 m/s / 5 m/s² | |
| | lightImpactSpeedLoss | 0.08 | Cone hit |
| Vertical | gravity / hardLandingSpeed | 20 m/s² / 6 m/s | Ramps, hard landings stagger players |
| Crash | crashRestartDelay / crashDeceleration | 2.5 s / 14 m/s² | |
| Impacts | heavyImpactSpeedLoss / crashImpactSpeed | 0.3 / 16 m/s | Heavy hit speed loss; closing speed that turns a heavy hit into a crash |
| | heavyStaggerTime / heavyFallThreshold | 0.7 s / 0.55 | Players beyond 55 % of the deck toward the impact fall off |
| | potholeSpeedLoss / wallScrapeSpeedLoss / wallCrashLateralSpeed | 0.06 / 0.12 / 11 m/s | |
| Difficulty | difficultyFullDistance / difficultySpeedCapBonus | 8000 m / 0.25 | |
| Crash | respawnSpeedFraction | 0.7 (M3: 0.6) | After a crash the board resumes at 70 % of its previous cruise speed |
| Nitro | nitroDuration / nitroSpeedBonus / nitroAcceleration / nitroInstability | 3 s / 12 m/s / 10 m/s² / 1.35 | |
| Score | pointsPerMeter / coinPoints / diamondPoints / nearMissPoints / airtimePointsPerSecond | 1 / 10 / 100 / 50 / 100 | Base points |
| | comboStep / comboMaxMultiplier / comboIdleTime / comboDrainPerSecond | 10 / 6 / 4 s / 2 per s | |
| | nearMissDistance / nearMissMinSpeed / minScoredAirtime / livesPerRun | 1.2 m / 10 m/s / 0.35 s / 3 | |
| Players | playerRadius / playerHeight | 0.33 / 1.1 m | |
| | playerMoveSpeed / GroundAcceleration / AirAcceleration | 4.2 m/s (M3: 3.6) / 20 / 6 m/s² | |
| | playerJumpVelocity / playerGravity / playerJumpCooldown | 4.5 m/s / 14 m/s² / 0.25 s | |
| | respawnDelay | 1.5 s | After falling off the deck |
| | bumpRestitution / staggerRelativeSpeed / staggerTime / staggerControlFactor | 0.5 / 2.8 m/s / 0.35 s / 0.2 | Player-player bumps |
| | dangerSlideAcceleration | 5 m/s² | Slide toward the low side while wobbling |
| | boardInertiaFactor | 0.25 | Players lurch forward when the board brakes |
| | landingStaggerFactor | 1 | |

## Running the tests

**In Unity:** *Window → General → Test Runner → PlayMode → Run All*. `Game.Tests` contains
- `BoardAcceptanceTests` / `BoardFlowTests` — the Milestone 1 acceptance tests on the engine-independent simulation
- `TrackRunTests` — bot crews drive the whole M1 test track headless (same layout, ramp and respawn rules as the scene)
- `ScoringAcceptanceTests` — Milestone 3: score and combo rules, near miss and airtime detection, nitro, high score persistence
- `RoadAcceptanceTests` — Milestone 2: 10 km soak on 5 seeds with a validity check at every chunk join, scripted
  ideal driver over 10 km, difficulty curve (set `DOWNHILL_REPORT_DIR` to also write `M2_difficulty_curve.csv`),
  allocation and step-time measurement
- `ArtAcceptanceTests` — Milestone 4: riders distinct by construction and on the shared rig, every obstacle / pickup builds,
  chunk geometry deterministic, allocation-free and within a triangle budget, palette fits one texture, tuning pinned
- `BoardPlayModeTests` — the same mechanics inside the engine (raycast ground on the test road, crash → respawn, ramp, player views vs deck);
  `M4_SixRiders_RenderStatsReport` logs frame time, batches, SetPass calls and triangles with six riders

**Without Unity (headless, .NET 8 SDK):**
```bash
cd Tools/Headless/Tests && dotnet test --logger "console;verbosity=normal"
```
Runs the same `BoardAcceptanceTests` / `BoardFlowTests` source files against the simulation compiled as
netstandard2.1 / C# 9 (what Unity uses) and prints the measured numbers.

**Compile check of the Unity scripts without Unity:** `Tools/UnityCompileCheck/build.sh` downloads Unity
reference assemblies from NuGet (2021.3 engine, 2021.1 editor – *not* Unity 6) and compiles
`Game.Runtime`, `Game.Editor`, `Game.Tests` with warnings as errors. Input System and Test Runner are small
hand-written stubs. A pass is a smoke test, not a guarantee for Unity 6.

## Code layout

```
Assets/Scripts/
  Runtime/                      Game.Runtime.asmdef
    Simulation/                 engine-independent (System.Numerics only) – the one authoritative sim
      BoardSimulation.cs        step order: players → crowd solver → edge falls → respawn → weight → board
      Board/                    BoardWeightSystem, BoardPhysicsSim, IGroundProvider
      Players/                  PlayerSim (kinematic, board-local), PlayerCrowdSolver
      Bots/                     BotBrain + 6 behaviours
      Road/                     RoadGenerator, RoadChunkLibrary, RoadChunk, RoadModel (ground/projection),
                                RoadPlanner (traversability), DifficultyManager, TestRoadLayout (M1 track)
      Obstacles/                ObstacleField (pool, traffic, collision, near misses), ObstacleTypes (kinds, severity)
      Pickups/PickupField.cs    coin / diamond / nitro pool and collection
      Scoring/                  ScoreManager (score + combo rules), HighScoreManager + FileHighScoreStore
      RunSimulation.cs          one authoritative run: road + obstacles + difficulty + board
      Input/PlayerInputState.cs the only input the sim reads
      Tuning/BoardTuningData.cs every tuning value
    Art/                        engine-free art (System.Numerics): ArtModel / MeshData primitives, ArtLibrary (riders,
                                board, obstacles, pickups, props), ChunkGeometry (road + scenery), Backdrop (bay, bridge,
                                skyline), RiderPose, Palette, Atmosphere (+ camera framing constants)
    Board/                      BoardController (hosts RunSimulation in FixedUpdate, kinematic Rigidbody), BoardView, BoardFx (wheel dust, sparks)
    Players/PlayerView.cs       rider on the art rig, procedural skate pose (RiderPose)
    Input/                      PlayerInputRouter (keyboard / gamepads / bots), LocalDeviceInput
    Road/                       RoadView + ChunkView (pooled chunk meshes)
    Obstacles/ObstacleViews.cs  pooled art models mirroring the obstacle pool
    Pickups/PickupViews.cs      spinning coins, diamonds, nitro bottles
    Art/Hud/                    engine-free HUD: HudPresenter (run -> HudState), HudLayout (draw commands), HudTextures
    UI/HUDController.cs         draws the HUD commands with IMGUI (the preview draws the same commands on a canvas)
    CameraRig/                  CameraController (3/4 chase), GameFeel (hit-stop, crash slow-mo), SpeedLines, BackdropView
    CameraRig/CameraController.cs
    Core/                       GameManager (bootstrap), RunManager, GameHotkeys, SceneAtmosphere (sun, sky, fog, post)
    Util/                       ArtMeshes (palette texture + 3 shared materials), ArtBuilder (model → GameObjects), MaterialLibrary
    DebugTools/                 DebugOverlay (F1), TuningPanel (F2)
    Tuning/BoardTuning.cs       ScriptableObject wrapping BoardTuningData
  Editor/                       Game.Editor.asmdef – ProjectSetup
  Tests/                        Game.Tests.asmdef – Simulation/ (NUnit), PlayMode/ (UnityTest)
Tools/Headless/                 dotnet projects for headless tests
Tools/UnityCompileCheck/        dotnet compile check against Unity reference assemblies
Tools/ArtPreview/               exports Game.Art geometry and renders it headless (three.js + Chromium): screenshots,
                                silhouette / contrast measurements, draw-call estimate (see its README)
.claude/skills/                 vendored agent skills used as review checklists (licenses inside)
Docs/                           milestone reports
```
