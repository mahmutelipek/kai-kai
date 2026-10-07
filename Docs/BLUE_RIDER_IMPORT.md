# Blue rider — first playable art integration

This records the first P1 integration. All six slots are now imported; see
`RIDERS_ART_IMPORT.md` for the current pipeline and verification.

Source: `Rider0` in the existing Blender scene, backed up in
`ArtSource/Scene/Downhill_Existing_Baseline.blend`.

The source character is preserved. `Tools/Blender/export_blue_rider.py` builds
an isolated, reduced copy in a temporary scene, then exports only that copy.
Run it from Blender's Python Console:

```python
p = '/Users/mahmutelipek/Desktop/kai-kai/Tools/Blender/export_blue_rider.py'
exec(compile(open(p).read(), p, 'exec'), {'__file__': p})
```

## Asset preparation

- Source: 253,162 mesh vertices across 77 mesh objects.
- Export: 33,538 mesh vertices across six mesh groups (about 87% fewer vertices).
- Height: 1.35 m; feet aligned to local ground height zero.
- Independent head, left/right shoulder, and left/right leg pivots.
- Source arm spread neutralized in the export so Unity can animate arm balance.
- Smaller face, hat, and sneaker details retained; larger rounded surfaces reduced.
- Source meshes merged by articulation group; this does not guarantee six draw
  calls, since mesh groups still have multiple material slots.

This is a rigid-part transform rig. It has no bones, skin weights, or Humanoid
Avatar, and cannot directly use Humanoid animation clips. Procedural torso lean,
head movement, arm balance/panic, leg stepping/jump tuck, and fall/respawn visuals
are driven by `PlayerView` and `RiderArtRig`. Smooth knee/elbow deformation and
authored locomotion clips remain future work.

## Unity

- FBX: `Assets/Art/Models/Riders/BlueRider.fbx`
- Material data: `Assets/Art/Models/Riders/BlueRider.materials.json`
- Native URP materials: `Assets/Art/Materials/Riders/Blue/`
- Runtime prefab: `Assets/Resources/Art/Riders/BlueRider.prefab`

The prefab is generated on first import. After another export use
**Downhill → Art → Rebuild Blue Rider Prefab**. P1 loads this prefab; P2–P6 now load their corresponding rider prefabs. Input and authoritative simulation
are unchanged. If a slot's prefab is missing, that slot uses the primitive fallback.
Blender procedural shader textures are not baked in this pass.

**Downhill → Art → Capture Blue Rider Review** creates a close-up and a board
scale review under `ArtSource/Riders/`. The static review pose is separate from
the runtime movement animation.

## Verification — 2026-10-07

Unity 6000.6.3f1 compiled the changes, imported the FBX, validated scale/forward
direction/articulation anchors, and generated the native materials and prefab.
Unity renders were visually checked for face, outfit, shoes, and board scale.

Both `BlueRiderPlayModeTests` passed in Unity: imported P1 mesh/material binding,
walking and leg articulation, jumping, detachment on falling, and respawn with
the same rig intact. Report: `Docs/Verification/BlueRider_PlayMode.xml`.

The two pre-existing failures in `BoardPlayModeTests` documented in
`BOARD_ART_IMPORT.md` remain unresolved. This pass does not establish a frame-rate
or performance budget.
