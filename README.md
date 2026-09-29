# Downhill Party Board

Co-op endless downhill party game prototype (Unity 6, URP). 2–6 players stand on one giant longboard.
There is no steering input: **the board is steered only by where the players stand.**

Current state: **Milestone 1 – core board control** (primitive placeholder art, test road).

---

## Opening the project

1. Unity Hub → *Add project from disk* → this folder. Use **Unity 6 LTS (6000.0.x)**; `ProjectVersion.txt`
   says 6000.0.58f2, and any 6000.0.x should work (Hub will offer to switch).
2. Packages resolve from `Packages/manifest.json` (Input System, URP, Test Framework).
   If Unity asks to **enable the new Input System backends / restart**, answer **Yes**.
3. On first import `Game.Editor/ProjectSetup` creates `Assets/Settings/BoardTuning.asset` and
   `Assets/Scenes/M1_TestScene.unity` and opens the scene. (Menu **Downhill → Rebuild M1 Test Scene** redoes it.)
4. Press **Play**. The scene only contains a `GameManager`; road, board, players, camera and debug tools are built from code.

**Render pipeline:** materials use `Universal Render Pipeline/Lit` when a URP asset is active and fall back
to `Standard` otherwise, so the scene renders either way. To get URP: *Assets → Create → Rendering → URP Asset
(with Universal Renderer)* and assign it in *Project Settings → Graphics* and *Quality*.

## Controls (local testing)

| Key | Action |
|---|---|
| WASD / arrows | Move your player on the deck (W = toward the nose) |
| Space | Jump (airborne players weigh 25 %) |
| Tab | Switch which player the keyboard controls |
| Gamepad | Each connected gamepad takes over one more player (left stick / d-pad, A/Cross = jump) |
| B | Bots on / off (bots drive every slot not taken by keyboard / gamepad) |
| C | Bot mix: *Mixed* (cooperative, stubborn-left, stubborn-right, wanderer, greedy-front, scared-rear) ↔ *All cooperative* |
| 2 – 6 | Number of players on the board |
| R | Restart the run at the top |
| F1 | Debug overlay: centre-of-mass dot (magenta), smoothed steering (cyan), lateral/longitudinal/steering/roll/speed/danger |
| F2 | Live tuning panel (every `BoardTuning` value) |

Crash → players are thrown off → the board respawns on the road ~2.5 s later.
The test road is ~2.3 km: straight, left curve, straight, right curve, then a long straight with cones and one ramp.
At the end the run restarts from the top.

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
| | steeringSmoothingTime | 0.4 s | Heaviness / delay of the response |
| | airborneWeightFactor | 0.25 | Weight of a jumping player |
| | yawRateBaseDeg / yawRatePerSpeedDeg | 20 / 1.2 | Max yaw rate = base + perSpeed·speed (deg/s): steering gets stronger with speed |
| | yawResponseTime | 0.25 s | How fast the yaw rate follows its target (the "torque") |
| | tractionAlignRate | 8 /s | How quickly the travel direction follows the heading (× grip) |
| Roll | maxRollDeg / rollResponseTime | 18° / 0.2 s | Lean into turns |
| Stability | stabilityFactorLowSpeed / HighSpeed | 1.25 / 1.8 | Danger multiplier, interpolated by speed / stabilityReferenceSpeed (35) |
| | frontWeightInstability | 0.15 | Extra danger from front weight at speed |
| | crashThreshold | 1.0 | Danger = 1 means crash zone |
| | wobbleStartFraction | 0.7 | Wobble / grip loss / sliding begin here |
| | wobbleMaxRollDeg / wobbleFrequencyHz | 6° / 4.5 Hz | |
| | gripLossMax | 0.5 | Grip at danger 1 = 50 % |
| | crashTipTime / tipRecoveryTime | 0.5 / 1.0 s | Time in the crash zone before flipping / recovering |
| Speed | startSpeed / speedRampPerSecond / softCapSpeed | 8 m/s / 0.25 / 35 m/s | Cruise speed rises over time to the soft cap |
| | cruiseGain | 0.35 /s | Pull toward cruise speed |
| | frontAcceleration / rearBraking | 4 / 6 m/s² | Scaled by longitudinal weight |
| | frontAccelFadeAboveCap | 0.15 | Front push fades between 0.85× and 1.15× the cap |
| | minSpeed / offroadDrag | 3 m/s / 5 m/s² | |
| | lightImpactSpeedLoss | 0.08 | Cone hit |
| Vertical | gravity / hardLandingSpeed | 20 m/s² / 6 m/s | Ramps, hard landings stagger players |
| Crash | crashRestartDelay / crashDeceleration | 2.5 s / 14 m/s² | |
| Players | playerRadius / playerHeight | 0.33 / 1.1 m | |
| | playerMoveSpeed / GroundAcceleration / AirAcceleration | 3.6 m/s / 20 / 6 m/s² | |
| | playerJumpVelocity / playerGravity / playerJumpCooldown | 4.5 m/s / 14 m/s² / 0.25 s | |
| | respawnDelay | 1.5 s | After falling off the deck |
| | bumpRestitution / staggerRelativeSpeed / staggerTime / staggerControlFactor | 0.5 / 2.8 m/s / 0.35 s / 0.2 | Player-player bumps |
| | dangerSlideAcceleration | 5 m/s² | Slide toward the low side while wobbling |
| | boardInertiaFactor | 0.25 | Players lurch forward when the board brakes |
| | landingStaggerFactor | 1 | |

## Running the tests

**In Unity:** *Window → General → Test Runner → PlayMode → Run All*. `Game.Tests` contains
- `BoardAcceptanceTests` / `BoardFlowTests` — the Milestone 1 acceptance tests on the engine-independent simulation
- `BoardPlayModeTests` — the same mechanics inside the engine (raycast ground on the test road, crash → respawn, ramp, player views vs deck)

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
      Input/PlayerInputState.cs the only input the sim reads
      Tuning/BoardTuningData.cs every tuning value
    Board/                      BoardController (FixedUpdate host, kinematic Rigidbody), BoardView
    Players/PlayerView.cs       primitive character visuals
    Input/                      PlayerInputRouter (keyboard / gamepads / bots), LocalDeviceInput
    Road/                       TestRoad (M1 track), RoadPath, UnityGroundProvider, GroundSurface
    Obstacles/                  ObstacleBase, ConeObstacle
    CameraRig/CameraController.cs
    Core/                       GameManager (bootstrap), RunManager, GameHotkeys
    DebugTools/                 DebugOverlay (F1), TuningPanel (F2)
    Tuning/BoardTuning.cs       ScriptableObject wrapping BoardTuningData
  Editor/                       Game.Editor.asmdef – ProjectSetup
  Tests/                        Game.Tests.asmdef – Simulation/ (NUnit), PlayMode/ (UnityTest)
Tools/Headless/                 dotnet projects for headless tests
Tools/UnityCompileCheck/        dotnet compile check against Unity reference assemblies
Docs/                           milestone reports
```
