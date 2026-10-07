# Coastal scenery — first environment integration

Historical art pass. The active scene now uses the simpler endless gameplay in
`ENDLESS_RUN.md`. The bridge/skyline backdrop is no longer spawned, including on
the optional finite track. Old review images record the earlier art composition.

Source: the existing Blender scene, backed up in
`ArtSource/Scene/Downhill_Existing_Baseline.blend`. Original geometry is preserved.
The source's straight 400 m road is not used as the gameplay collider: reusable
scenery is fitted to the existing approximately 2.34 km M1 road and its curves.

## Re-export and rebuild

Run in Blender's Python Console with the source scene open:

```python
p = '/Users/mahmutelipek/Desktop/kai-kai/Tools/Blender/export_environment.py'
exec(compile(open(p).read(), p, 'exec'), {'__file__': p})
```

The exporter builds and removes an isolated temporary scene for each asset.
It exports three house bodies with roofs, one complete palm, one orange/white
barrier unit, and a combined distant bridge/skyline/sailboat/hill backdrop.

| Asset | Source objects | Export vertices |
| --- | ---: | ---: |
| CoastalBackdrop | 317 | 6,390 |
| CoastalBarrier | 5 | 40 |
| CoastalHouse0 | 2 | 16 |
| CoastalHouse1 | 2 | 16 |
| CoastalHouse2 | 2 | 16 |
| CoastalPalm | 17 | 1,158 |

FBX and material data: `Assets/Art/Models/Environment/`.
Generated URP materials: `Assets/Art/Materials/Environment/`.
Runtime prefabs: `Assets/Resources/Art/Environment/`.
Use **Downhill → Art → Rebuild Environment Prefabs** after another export.
Each prefab validates its positive-Z forward and positive-X right markers.
Unity adds colored wall materials and shared window/trim meshes to the otherwise
plain house bodies. Source procedural textures are not baked.

## Placement and gameplay

`TestRoad` builds `CoastalScenery` after the existing road and obstacles.
Imported palms, houses and barriers follow sampled centerline positions and
headings. Runtime static batching combines repeated scenery; imported environment
meshes remain readable for this operation. Generated meshes are cleaned up on
scenery destruction. No frame-rate target has been measured in this pass.

Visual beach, retaining wall, water and hillside strips follow the road.
The water strip follows the road grade at an offset below the road: it is a
stylized visual approximation, not a physical level ocean or a water shader.
The distant backdrop stays fixed in world space for this finite M1 track.
The original right shoulder collider is retained while its wide grass renderer
is hidden under the narrower beach visual. All scenery is visual only and has
no colliders; physics, steering, cones and the ramp retain their M1 behavior.
If any required scenery prefab is missing, the original road visuals remain.
Source cars have not been integrated as traffic. Infinite terrain generation,
textured asphalt, animated water, detailed city assets and final lighting remain
future work.

## Review and verification — 2026-10-07

**Downhill → Art → Capture Coastal Environment Review** rebuilds the scenery
prefabs and renders two static compositions using the actual road mesh:

- `ArtSource/Environment/Unity_Coastal_Review.png` at road distance 40 m.
- `ArtSource/Environment/Unity_Coastal_Curve.png` at road distance 210 m.

Review cameras, character arrangement, fog and lighting belong to the temporary
review scene; they do not replace the gameplay chase camera or final lighting.
The source game scene is restored after capture. Both images were visually
checked for scenery placement, facade details, shoreline and the imported riders.

Unity 6000.6.3f1 imported the six environment FBX assets, generated materials,
facade meshes and prefabs, and compiled the scripts. Five PlayMode tests passed:
all four rider integration tests plus a scenery test checking absence of scenery
colliders, unchanged road ground samples on straights and curves, and palm
position/heading alignment on the first curve. Report:
`Docs/Verification/CoastalScenery_PlayMode.xml`.
The two pre-existing board-test failures in `BOARD_ART_IMPORT.md` remain unresolved.
