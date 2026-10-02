# Blender models

`build_models.py` builds the game's stylised models with Blender's Python module (no Blender UI needed) and exports
them to `Assets/Resources/Models/<Name>.bytes`, which the game loads through `Game.Art.ArtAsset` (Unity and the
headless preview read the same files; a missing file falls back to the procedural model).

```
python3.11 -m venv .blenv && .blenv/bin/pip install bpy==5.0.1
.blenv/bin/python Tools/Blender/build_models.py --render out/   # exports + Cycles beauty shots
```

- All numbers in the script are game units and axes (X right, Y up, Z forward), on the same rig as `ArtLibrary`:
  riders `Pose > Torso > Head / ArmL / ArmR`, board `Wheel0..Wheel3` (rear-left, rear-right, front-left, front-right).
  Animation code (RiderPose, BoardView) drives these groups unchanged.
- `source/models.blend` is the generated scene (one collection per model, mirrored into Blender's Z-up) for
  looking at or hand-editing; the game uses the `.bytes` files, so re-run the script after changing it.
- Done: `Board`, `Rider0` (P1). Next: `Rider1`..`Rider5` (add to `OUTFITS`).
