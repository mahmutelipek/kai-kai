# Existing Blender board → Unity

The source art is the user's existing `Untitled.blend` scene, backed up in
`ArtSource/Scene/Downhill_Existing_Baseline.blend`. No new board was substituted.

## Re-export

Open the existing scene in Blender, switch a panel to the Python Console, and run:

```python
p = '/Users/mahmutelipek/Desktop/kai-kai/Tools/Blender/export_existing_board.py'
exec(compile(open(p).read(), p, 'exec'), {'__file__': p})
```

The script creates an isolated temporary scene, exports only the board, and removes
the temporary copies. The source board, riders, environment, and their transforms
are preserved. The original deck is 2.7 × 6.966 m; the exported deck is normalized
to the existing simulation's 2.4 × 6 m. Wheels remain circular with 0.35 m radius.
`DeckTop` is at 0.862 m, accounting for the top of the grip. Four `WheelPivot` empties
allow the runtime to rotate wheels independently.

Outputs:

- `Assets/Art/Models/Board/PartyBoard.fbx`
- `Assets/Art/Models/Board/PartyBoard.materials.json`

The first export contains 48 meshes and 15,164 vertices. It is a visual integration
pass; mesh batching and draw-call optimization have not been performed.

## Unity

`BoardArtImporter` sets axis conversion, disables animation/camera/light/collider
imports, and preserves the pivot hierarchy. `BoardArtSetup` checks deck bounds,
deck height, nose direction, and four wheel pivots before saving the prefab.

On first import, the prefab is generated automatically. After another export use
**Downhill → Art → Rebuild Board Prefab**.

- Native URP materials: `Assets/Art/Materials/Board/`
- Runtime prefab: `Assets/Resources/Art/PartyBoard.prefab`
- Review render: **Downhill → Art → Capture Board Review**

`BoardView` loads the prefab and creates an identity-oriented player anchor in
board-local coordinates. Player positioning and the authoritative simulation do
not depend on the FBX hierarchy. If the prefab is absent or its anchors are missing,
the previous primitive board is used.

Materials transfer base color, roughness, and metallic values into URP Lit.
Blender procedural shader textures are not baked or transferred in this pass.
All six characters are now imported separately; see `RIDERS_ART_IMPORT.md`.
Reusable source scenery is now imported; see `ENVIRONMENT_ART_IMPORT.md`.

## Verification — 2026-10-07

Unity 6000.6.3f1 imported the FBX, compiled the scripts, generated the URP materials
and prefab, and passed the scale/axis/anchor validation in `BoardArtSetup`.

The existing six `BoardPlayModeTests` were run with the imported board and again
with the primitive board (temporarily disabling the imported-art branch, then
restoring it). Both runs passed 4/6: turning, crash/respawn, ramp, and two-player
mode. Both failed the same ground-height and player-view-height assertions with
nearly identical measured values. These failures predate the imported board;
they remain unresolved. Test reports are in `Docs/Verification/`.

The imported board was also rendered and visually checked in Unity using a URP
single-camera render request after editor initialization. The capture is at
`ArtSource/Board/Unity_Board_Review.png`. The review helper references the installed
URP assemblies directly; the old Unity 2021 stub-based compile-check harness does
not cover this helper. Actual Unity 6 compilation was used for this change.
