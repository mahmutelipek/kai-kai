# ArtPreview

Headless preview of the Game.Art geometry — the exact triangles Unity uploads — rendered with three.js in
Chromium (SwiftShader WebGL) via Playwright. Lighting approximates `SceneAtmosphere`; it is **not** Unity's
renderer, so use it for shapes, colours, framing and readability, not for final lighting.

```bash
npm install                      # three + playwright-core (Chromium: set CHROMIUM=/path/to/chrome if needed)
./render_all.sh                  # gameplay scenes, rider sheets, silhouette IoU / colour distances -> out/
./reaction.sh                    # every obstacle / pickup at 50 m and 30 m: on-screen size and contrast at 1080p
dotnet bin/Debug/net8.0/ArtPreview.dll drawcalls   # draw-call estimate from real game states
```

Gameplay scenes come from a real `RunSimulation` driven by the test `IdealDriver`, frozen at a chunk kind.
