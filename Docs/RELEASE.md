# Kai Kai — playable desktop release

The active game is an endless downhill run with 2–6 riders on one giant board.
The board turns only through the riders' weighted deck positions. There is no
bridge or city dependency. Roadside palms, shrubs, rocks, rolling hills and
reflective posts stream and retire with the road. Atmospheric lighting and fog
hide the streaming boundary. The sky has a visible warm sun, drifting cloud
banks, a blue zenith gradient and two seamless distant mountain layers. The
static art review uses the same sky and lighting as the runtime game.

## Play

Open `Builds/Release/kai kai.app` on this Mac. Choose the crew size and click **Yola Çık**
or press Enter. Unoccupied slots use cooperative bots; their route hints make
them walk toward the weight distribution needed to follow the open lane.

- WASD / arrows: walk on the deck.
- Space: jump. E: recenter and balance with the cooperative crew.
- Hold Shift while steering: drift. Release Shift: recover grip.
- Tab: change the keyboard-controlled character.
- Connected gamepads: extra local players; left stick / d-pad move, south button
  jumps, west button balances, left trigger drifts. Start begins a run or pauses/resumes it.
- Escape: pause/resume. R: new run while playing.
- F1/F2: developer overlay/tuning; B/C and 2–6 remain development shortcuts.

The first stretch is gentle. Rows leave one clear route, and the open lane changes
by at most one lane at a time. Obstacle spacing becomes denser with distance.
The board has three hull points: a cone costs one and a crate costs two. A short
hit grace period prevents duplicate damage. Crates also slow the board and stagger
riders. Hull exhaustion, a board tip or leaving the road corridor ends the run and opens the results screen.
Individual fallen riders can still rejoin through the existing respawn system.

Diamonds are shared. Consecutive pickups within three seconds build a combo to
×5. Each diamond awards 25 × combo points; distance in metres adds to the score.
The best score is stored locally. Pause provides resume, restart, sound and menu
controls. Chimes, wheel rumble and pickup/impact particles are generated locally;
no external music or asset-service credentials are required.

## Build and checks

Unity editor: 6000.6.3f1. `Game.EditorTools.ReleaseBuild.BuildMac` creates the Mac
application and its procedural asphalt/particle materials. The source scene is
`Assets/Scenes/M1_TestScene.unity`; its legacy name is retained for compatibility.
Programmatic `GameManager.Create` starts immediately without menus by default so
existing simulation and art tests continue to work. `Run.ConfigureSession(true)`
opts tests into the shipped menu/game-over flow.

The original finite track is retained for ground, crash and ramp tests. The old
board-ground assertion used an unrestricted raycast that could hit the board's
own collider; it now queries GroundSurface colliders. The rider test checks views
that have reattached to DeckTop, avoiding the frame between simulation respawn and
visual reattachment.

All 45 tests passed on the final source and resource materials. Verification
results are recorded in `Docs/Verification/Release_PlayMode.xml`. The Mac build
succeeded (89,672,492 bytes); launch, menu, HUD and pause were checked in the
actual standalone application. Pointer selection/start/mute/menu actions are
read on pointer press through the new Input System; IMGUI renders the interface only. Its player log contained no exceptions or shader
errors during these checks. The final source also has focused session/pointer
regression results in `Docs/Verification/Release_Pointer_PlayMode.xml`.
The streaming checks cover 11.2 km and bounded road/path/collider-cache counts.

## Remaining production work

This is a local playable release, not a store submission. Network multiplayer,
mobile touch controls, mobile/store packaging, a skinned character animation rig,
and a floating origin for extremely long runs are not included. The build is
unsigned and has not been notarized for distribution. Performance should be
profiled on target hardware before claiming a particular frame rate.


## Rider and effects update

The release tuning uses a 3.4 × 7.2 m deck. Standing rider meshes open the baked
crouch, keep shoe soles fixed and remove speed-based vertical squash. Cooperative
bots retain three longitudinal rows while their lateral weight continues to steer
the board. The camera looks closer to the crew from a slight side offset and uses a narrower maximum FOV.

Each wheel emits bounded world-space dust and grit; stronger turns add short
skid ribbons. Menu/pause/restart states control emission and cleanup. Diamonds
use emissive facets and a soft camera-facing halo; pickup sparks fade out.
The original Blender source and FBX exports are preserved.

Validation: all 47 tests passed, including wider-deck lane clearance, retained
cooperative rows, uniform rider scale, bounded dust emission, pause and reset.
Results: `Docs/Verification/Rider_Presentation_PlayMode.xml`.


## Visual polish update

Endless scenery now uses curved palms with separate leaflets and occasional
imported roadside houses. All palms share two cached meshes; their instances
retire with the road chunks. The streaming regression additionally checks palm
counts, mesh reuse and normals after 11.2 km.

Characters keep distinct forward skating angles while resting and add gentle,
individual balance motion at speed. Standing arm meshes remain rigid so opening
the knees does not stretch hands or forearms. Light, color, ACES tonemapping,
restrained bloom and vignette are shared by the game and art reviews. Peripheral
speed lines appear above 18 m/s, with larger wheel dust. The game-camera review
uses the actual chase camera's default position, look target and speed FOV.

All 47 tests passed (`Docs/Verification/Visual_Polish_PlayMode.xml`), and the Mac
build succeeded at 90,427,180 bytes. Native launch, Enter-to-start, visible wheel
dust, emissive diamond bloom and live score/collection were checked in the player.
URP post-processing data is explicitly assigned during release preparation and
the diamond material enables the emission keyword with an emissive GI flag.

Character sizing update: gameplay visuals use a uniform 0.8 scale (20% smaller),
with the same feet-at-zero pivot. Player markers scale and lower to match; pop,
walking, jump and fall poses retain their proportions. On-board previews share
the runtime visual scale. Source mesh size and board tuning remain available.

## Handling update

Release tuning now gives riders support at the rounded deck edge, separates crowded
riders along the deck, reduces involuntary sliding and gives sustained imbalance
more time before tipping. Jumping beyond the deck and serious impacts can still
throw riders off. A fallen rider probes the road surface and uses the lowest body
mesh bounds to land, bounce and slow above the asphalt.

After a gentle opening, the road varies between roughly 4.5–23.5% downhill grades.
Gravity adds speed downhill; the chase camera follows the grade. Curved chunks
turn 24–36 degrees over about 80 metres. Weight steering responds more quickly,
and cooperative bots support a human player's steering intent with their deck
positions. Shift / gamepad LT reduces grip while turning above 8 m/s; heading
slip is bounded to 18 degrees and grip recovers after release. Drift leaves skid
trails, with a HUD indicator. E / gamepad west recenters the human rider and
neutralizes the cooperative crew's steering target.

All 56 PlayMode tests pass: four sustained deck-edge directions, supporting crew
weight, bounded drift and recovery, downhill acceleration, road grade/bend
geometry, actual fallen-mesh clearance, and the existing full regression suite.
Results: `Docs/Verification/Handling_PlayMode.xml`.

The handling Mac build succeeded at 90,429,804 bytes. Native launch, updated
control hints, downhill progress to 235 m, 15 diamond pickups, ×5 combo and pause
were checked in the standalone application; its player log had no exceptions or
shader errors. The updated application is left paused for playtesting.

## Reference arcade update

Coin trails, purple diamonds, electric-blue nitro bottles and chevron-marked wooden
ramps stream in the clear routes and retire with their road chunks. Coins award
10 points and 2 charge, diamonds award their existing combo score and 5 charge,
and a nitro bottle fills the 100-point tank. Sustained drift earns 8 charge/second.
Q / gamepad RB activates a full tank for 3.5 seconds; the boost raises target speed
by 9 m/s with extra acceleration, without changing weight-based steering.
Pause freezes boost duration; crashes/session damage cancel boost; restart
clears currency and charge. Grip drift controls remain Shift / gamepad LT.

The HUD uses brush-shaped compact panels, large outlined italic distance, a
separate best-distance record, coin and diamond counters, combo/nitro ribbons,
colored crew faces and a segmented speed meter. Best score remains a separate
result-screen statistic. Nitro expands FOV gradually, adds bounded cyan exhaust
and stronger peripheral speed lines, blue pickup sparks and wind audio. Yellow
turn signs and striped roadside barriers strengthen route readability. No bridge
or skyline is required. Ramps are actual GroundSurface meshes using the existing
board takeoff/landing physics; their outgoing gem trail is raised for the jump.

Validation: all 60 PlayMode tests pass, including real coin/nitro trigger collection,
single-use pickup counting, boost acceleration/recovery without steering changes,
boost pause/expiry/menu/restart behavior, physical ramp takeoff and landing, and
streamed object retirement. Report: `Docs/Verification/Arcade_Content_PlayMode.xml`.

Final Mac build: 90,441,036 bytes. Native checks confirmed compact HUD placement
at fullscreen resolution, coin and diamond counts, Q activation and visible cyan
exhaust. The application is left paused at 370 m with 27 coins and 15 diamonds.
No player exceptions or shader errors were recorded. The final visual-only pass
corrects local-canvas HUD rotations and stretches the exhaust into speed streaks.

## Reference main menu

The supplied clean downhill image is the fullscreen menu background, cropped to
cover the display. A dark left gradient supports an angled kai kai title and
a yellow brush PLAY selection. Home offers PLAY, SETTINGS, CREDITS and QUIT.
PLAY opens a 2–6 rider selector; Enter then starts the run. Settings toggle sound,
camera shake and speed lines for the current application session. Mouse, keyboard
arrows/Enter/Escape and gamepad d-pad/south/east buttons navigate the menus.
The board remains frozen while viewing all menu pages. Pause/results return to
the home page. Credits identify the supplied project art and menu reference.

Four session/menu tests passed: pointer crew/start/mute/menu flow, settings and
credits/back navigation, frozen menu simulation, pause/restart/fatal damage and
off-road game-over handling. `Docs/Verification/Reference_Menu_PlayMode.xml`.

## Individual podium results

Fatal run endings now freeze a ranked player snapshot and show the top three
on gold/silver/bronze podium columns, followed by ranks 4–6 as compact rows.
All entries show nickname and individual points; 2–5 rider sessions only show
active players. Names are read-only session identities; manual nickname fields have been removed.

Individual points are metres travelled while on the board plus attributed
pickups: coin 10, diamond 25 × shared combo, nitro bottle 30. A pickup goes to
the on-board rider nearest its world position. Shared currencies and team
score retain their existing rules. Score ties keep slot order. Ending the run
freezes scores and names; restart clears scores and preserves nicknames.

Nine targeted ranking and session/menu tests passed
(`Docs/Verification/Podium_Menu_PlayMode.xml`). The Mac build succeeded at
91,847,260 bytes; the reference menu and six nickname fields were verified in
the actual fullscreen application.

The native six-rider run ended at 506 m and displayed the ranked podium plus
three remaining players correctly. All nicknames and distinct contribution
scores were visible; the player log contained no exceptions or shader errors.
The application is left on the results screen for review.


### Character portraits and menu cleanup
Six transparent head portraits are rendered from the actual rider prefabs and cached
as 256px textures. HUD, crew screen, podium and results list all use these portraits.
Menu labels have a 22px inset inside the yellow brush; shortcut footer text is removed.
Read-only player names shrink to fit their allotted row.
`RunLeaderboard.SetSteamIdentity` accepts the Steam ID and persona name from a future
lobby adapter, preserving the full name and frozen result identity. Offline labels
identify the local player and bots. Steamworks, networking and invite handling are
not installed or implemented yet; this build remains local play with bots.


### Closer camera, footwork and roadside gardens
The displayed game name is `kai kai` in lowercase, including the Mac window title.
Chase framing is now 6.3m behind and 3.1m above the board, with 61–70 degree speed FOV
and restrained extra banking during drift. Imported riders have alternating 27 degree
leg swings, a planted stance and 11cm swing-foot lift; mesh bounds keep the shoes
above the deck, and the animation blends back to idle when movement stops.
Each streamed chunk adds five batched meshes for grass tufts, wildflowers, tree
trunks and two canopy tones. Vegetation begins beyond the usable road and retires with its owning chunk.
Trees have camera-only proxies on Ignore Raycast layer; ground probes exclude
them. The camera pulls forward if a tree would block the view. Road shoulders and hills use green
materials; warmer daylight and increased saturation give the scene more color.
Validation: 11 existing movement/streaming/menu tests, 6 foot-grounding cases
and a camera obstruction/restore test passed. These checks establish functional behavior, not a measured frame-rate gain.

The current release application is `Builds/Release/kai kai.app`.


### Matching results screen
The results screen reuses the main menu illustration with a dark overlay, warm
black brush panels, gold headings and italic brush buttons. The first three rider
portraits remain on the podium and the other active riders remain in scored rows.
Existing replay/home pointer hit regions and score ordering are unchanged.
Nine result/menu flow tests passed.
