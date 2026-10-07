# Six playable riders — Blender to Unity

Source: `Rider0` through `Rider5` from the existing scene, backed up in
`ArtSource/Scene/Downhill_Existing_Baseline.blend`. Source geometry is preserved.

## Re-export and rebuild

In Blender's Python Console, with the source scene open:

```python
p = '/Users/mahmutelipek/Desktop/kai-kai/Tools/Blender/export_riders.py'
exec(compile(open(p).read(), p, 'exec'), {'__file__': p})
```

The exporter creates and removes an isolated temporary scene for each rider.
`export_blue_rider.py` remains a compatibility entry point for P1 only.
In Unity use **Downhill → Art → Rebuild All Rider Prefabs** after re-exporting.
Missing prefabs are generated on first import.

## Exported geometry

| Player | Asset | Source vertices | Export vertices |
| --- | --- | ---: | ---: |
| 1 | BlueRider | 253,162 | 33,538 |
| 2 | RedRider | 259,003 | 37,636 |
| 3 | GreenRider | 279,430 | 33,955 |
| 4 | YellowRider | 426,606 | 50,969 |
| 5 | PurpleRider | 268,800 | 33,349 |
| 6 | OrangeRider | 285,496 | 38,091 |

Total: 1,772,497 → 227,538 mesh vertices
(87.2% reduction). This is mesh-data reduction, not a measured FPS gain.
Each source character has six mesh groups, multiple material slots, height 1.35 m,
feet at local height zero, and a validated positive-Z forward anchor.

The meshes are grouped into body, head, two arms and two legs. Source shoulder
spread is removed so runtime balance poses can control it. Jeans are separated
by side for leg articulation. Face, headwear, hairstyle, outfit and shoe details
are retained with reduced tessellation on larger surfaces.

## Standing stance

`RiderArtSetup` derives standing meshes under `Assets/Art/Models/Riders/Standing`.
The shoe region below 0.13 m remains fixed; the knee/hip region opens smoothly
by 0.22 m, aligning the leg, shorts, body and shoulder seams. Prefab height is
1.57 m. Every rebuild starts from the unchanged source FBX, so deformation never
accumulates. Runtime speed no longer compresses the character vertically.
The exported source and original Blender scene remain available for later rigging.

## Runtime integration

P1–P6 load Blue, Red, Green, Yellow, Purple and Orange respectively from
`Assets/Resources/Art/Riders/`. FBX files and material source data are under
`Assets/Art/Models/Riders/`; generated URP Lit materials are under
`Assets/Art/Materials/Riders/`.

`PlayerView` follows the existing simulation and `RiderArtRig` controls arm
balance/panic, head movement, leg stepping and jump tuck. Detachment, tumble,
and reattachment use the existing fall/respawn behavior. A missing rider prefab
falls back to the primitive visual for that slot.

This remains a rigid-part transform rig: no bones, skin weights or Humanoid
Avatar. Smooth knee/elbow deformation, authored animation clips, texture baking,
and draw-call optimization remain future work. Blender procedural textures are
not transferred. Physics and player input are unchanged.

## Visual review and verification — 2026-10-07

**Downhill → Art → Capture All Riders Review** generates:

- `ArtSource/Riders/Unity_AllRiders_OnBoard.png`
- `ArtSource/Riders/Unity_AllRiders_Lineup.png`

These static review arrangements are separate from simulation spawn positions.
Unity 6000.6.3f1 compiled the scripts, imported all FBX files, generated prefabs
and materials, and validated feet height, scale, forward direction and pivots.
Both captures were visually checked for all six silhouettes, colors and board scale.

Four PlayMode tests passed: the two existing blue-rider tests and two six-player
integration tests. Checks cover correct per-slot imported art and URP binding,
walking with moving legs, jumping, falling off, and respawning with the same rig.
Report: `Docs/Verification/AllRiders_PlayMode.xml`.
The two pre-existing board-test failures recorded in `BOARD_ART_IMPORT.md` remain
unresolved. This pass does not establish a rendering performance budget.
