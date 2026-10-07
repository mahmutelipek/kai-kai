# Endless road gameplay

Updated scope: a shared longboard runs down an endless road, steered by where
players stand. Players avoid obstacles and collect diamonds. Environment detail
is secondary; the reference's bridge and distant city are not gameplay content.

## Active scene

`Assets/Scenes/M1_TestScene.unity` now enables `GameManager.endlessRoad`.
Press Play: streamed road, six imported riders, sparse palms, cones/crates,
purple diamond trails and a shared HUD are built at runtime. The scene filename
is retained so existing project launch/build settings keep working.
WASD/arrows move the selected player on the board, Space jumps, Tab switches
player, Shift holds a drift, E balances, B toggles bots, C changes the bot preset and R restarts.
The board receives steering only from the players' weighted positions.

## Streaming

`EndlessRoad` generates approximately 80 m chunks with continuous slope and
varying downhill grades and sampled moderate left/right bends. It generates 400 m ahead and retains about
160 m behind. Old chunk objects and meshes are destroyed; old centerline samples
are discarded while their absolute distance values are preserved. The raycast
surface cache periodically evicts destroyed colliders. The road does not restart
at the former finite test-track endpoint. A new run regenerates from the same seed.
There is no floating-origin system yet; very long sessions remain subject to
Unity's floating-point world-coordinate limits. No FPS target is claimed.

## Obstacles and diamonds

Each obstacle row leaves one of three board-width routes clear. Cones cause a
light slowdown; crates cause a heavier slowdown and player stagger. Trigger
contacts support the existing kinematic board. Diamonds mark clear routes and
are counted once when the board touches their trigger. The score is shared by
the team, not assigned to individual players. Collected gems disappear.
The desktop release now includes distance/score/diamonds/speed and hull in the HUD,
with menu, pause and results flow. Crashes end a configured session; programmatic
tests without session mode retain the legacy respawn flow. Obstacle density
increases with distance, and cooperative bots use the clear route to inform their
deck positions. See `RELEASE.md` for scoring, controls and build details.

## Compatibility

The optional finite M1 track is retained for original physics/art checks.
`GameManager.Create` keeps its old finite default; pass `endless: true` for the
new mode. The serialized scene's default is endless. The bridge/skyline prefab
is no longer spawned in either road mode. Source Blender assets are preserved.

## Verification

Unity 6000.6.3f1: all seven selected PlayMode tests passed. Checks cover:

- Streaming and continuous ground queries through 11.2 km, with bounded active
  chunk/sample counts, both bend directions, absolute-distance projection and
  eviction of old collider cache entries.
- Actual physics-trigger diamond collection counted once and crate slowdown.
- Restart resetting the collected total, road origin and active chunk window.
- Progress beyond 2.7 km without returning to the start.
- All four existing imported-rider walking/jumping/fall/respawn checks.

Report: `Docs/Verification/EndlessRun_PlayMode.xml`.
The earlier finite-board assertions were corrected to query road surfaces and
ignore the simulation/view reattachment transition; the release report records
the complete suite.

**Downhill → Art → Capture Endless Run Review** creates a static Unity view at
`ArtSource/Endless/Unity_Endless_Review.png`. Its review camera and lighting are
separate from the runtime chase camera and HUD; it is not a gameplay recording.
