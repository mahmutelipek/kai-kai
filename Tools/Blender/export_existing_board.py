"""Export the open scene's Board without changing the source objects.

Run from Blender's console with exec(compile(open(path).read(), path, 'exec')).
The separate export scene normalizes the deck to the simulation's 6 x 2.4 m.
Wheel geometry retains its circular 0.35 m radius and four independent pivots.
"""
import bpy
import json
from pathlib import Path
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets' / 'Art' / 'Models' / 'Board'
OUTPUT.mkdir(parents=True, exist_ok=True)
source_scene = bpy.context.scene
source_board = source_scene.objects.get('Board')
if source_board is None:
    raise RuntimeError('Open the existing Downhill scene containing Board first.')

source_objects = [source_board] + list(source_board.children_recursive)
source_inverse = source_board.matrix_world.inverted()
deck = bpy.data.objects['Board.Deck']
deck_points = [source_inverse @ deck.matrix_world @ Vector(p) for p in deck.bound_box]
width = max(p.x for p in deck_points) - min(p.x for p in deck_points)
length = max(p.y for p in deck_points) - min(p.y for p in deck_points)
sx, sy = 2.4 / width, 6.0 / length
export_scene = bpy.data.scenes.new('Board_UnityExport_Temporary')
export_scene.unit_settings.system = 'METRIC'
export_scene.unit_settings.scale_length = 1
depsgraph = bpy.context.evaluated_depsgraph_get()
copies = []
mesh_data = []
materials = {}

try:
    root = bpy.data.objects.new('PartyBoard', None)
    export_scene.collection.objects.link(root)
    copies.append(root)
    wheels = {}
    for index in range(4):
        original = bpy.data.objects['Board.Wheel' + str(index)]
        center = (source_inverse @ original.matrix_world).translation
        pivot = bpy.data.objects.new('WheelPivot' + str(index), None)
        export_scene.collection.objects.link(pivot)
        pivot.parent = root
        pivot.location = (center.x * sx, center.y * sy, center.z)
        wheels[original] = pivot
        copies.append(pivot)

    for original in source_objects:
        if original.type != 'MESH':
            continue
        evaluated = original.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
        mesh_data.append(mesh)
        matrix = source_inverse @ original.matrix_world
        wheel_parent = original.parent if original.parent in wheels else None
        if wheel_parent:
            center = (source_inverse @ wheel_parent.matrix_world).translation
            # Bake the current wheel pose, keep local X as the spin axis.
            for vertex in mesh.vertices:
                vertex.co = matrix @ vertex.co - center
        else:
            for vertex in mesh.vertices:
                p = matrix @ vertex.co
                vertex.co = (p.x * sx, p.y * sy, p.z)
        mesh.update()
        obj = bpy.data.objects.new(original.name.replace('Board.', ''), mesh)
        export_scene.collection.objects.link(obj)
        obj.parent = wheels[wheel_parent] if wheel_parent else root
        copies.append(obj)
        for material in mesh.materials:
            if material is None:
                continue
            bsdf = material.node_tree.nodes.get('Principled BSDF') if material.use_nodes else None
            color = list(bsdf.inputs['Base Color'].default_value) if bsdf else list(material.diffuse_color)
            materials[material.name] = {
                'name': material.name,
                'linearColor': color,
                'roughness': float(bsdf.inputs['Roughness'].default_value) if bsdf else float(material.roughness),
                'metallic': float(bsdf.inputs['Metallic'].default_value) if bsdf else float(material.metallic),
            }

    # Anchor coordinates exported as empties for validation and character placement.
    for name, position in [('DeckTop', (0, 0, 0.862)), ('NoseMarker', (0, 3, 0.85))]:
        marker = bpy.data.objects.new(name, None)
        export_scene.collection.objects.link(marker)
        marker.parent = root
        marker.location = position
        copies.append(marker)

    bpy.context.window.scene = export_scene
    for obj in copies:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(OUTPUT / 'PartyBoard.fbx'),
        use_selection=True,
        object_types={'MESH', 'EMPTY'},
        axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    report = {
        'source': bpy.data.filepath,
        'deckWidth': 2.4, 'deckLength': 6.0, 'deckHeight': 0.862,
        'wheelRadius': 0.35,
        'meshCount': len(mesh_data),
        'vertexCount': sum(len(mesh.vertices) for mesh in mesh_data),
        'materials': list(materials.values()),
    }
    (OUTPUT / 'PartyBoard.materials.json').write_text(json.dumps(report, indent=2))
    print('BOARD_EXPORT_OK', report['meshCount'], 'meshes;', report['vertexCount'], 'vertices')
finally:
    bpy.context.window.scene = source_scene
    for obj in copies:
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in mesh_data:
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    bpy.data.scenes.remove(export_scene)
