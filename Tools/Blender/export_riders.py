"""Export optimized articulated copies of the six riders; preserve the source scene."""
import bpy
import json
from pathlib import Path
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets' / 'Art' / 'Models' / 'Riders'
OUTPUT.mkdir(parents=True, exist_ok=True)
RIDER_ASSETS = ['BlueRider', 'RedRider', 'GreenRider', 'YellowRider', 'PurpleRider', 'OrangeRider']

def export_rider(index, asset_name):
    source_name = 'Rider' + str(index)
    original_scene = bpy.context.scene
    rider = original_scene.objects[source_name]
    objects = [o for o in rider.children_recursive if o.type == 'MESH']
    inverse = rider.matrix_world.inverted()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    scene = bpy.data.scenes.new(asset_name + '_Export_Temporary')
    scene.unit_settings.system = 'METRIC'
    copies, meshes = [], []
    materials = {}
    groups = {name: {'vertices': [], 'faces': [], 'material_indices': [], 'smooth': [], 'materials': []}
              for name in ['Body', 'Head', 'ArmL', 'ArmR', 'LegL', 'LegR']}
    head = original_scene.objects[source_name + '.Head']
    torso = original_scene.objects[source_name + '.Torso']
    arms = {side: original_scene.objects[source_name + '.' + side] for side in ['ArmL', 'ArmR']}
    pivots = {'Body': Vector((0, 0, 0)), 'Head': (inverse @ head.matrix_world).translation}
    for name, arm in arms.items():
        pivots[name] = (inverse @ torso.matrix_world @ Matrix.Translation(arm.location)).translation

    def add_faces(group_name, mesh, coords, face_indices):
        group = groups[group_name]
        remap = {}
        for index in face_indices:
            face = mesh.polygons[index]
            indices = []
            for vertex in face.vertices:
                if vertex not in remap:
                    remap[vertex] = len(group['vertices'])
                    group['vertices'].append(coords[vertex])
                indices.append(remap[vertex])
            group['faces'].append(indices)
            material = mesh.materials[face.material_index] if len(mesh.materials) else None
            if material not in group['materials']:
                group['materials'].append(material)
            group['material_indices'].append(group['materials'].index(material))
            group['smooth'].append(face.use_smooth)

    try:
        bpy.context.window.scene = scene
        for original in objects:
            mesh = bpy.data.meshes.new_from_object(original.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
            meshes.append(mesh)
            copy = bpy.data.objects.new('Temporary_' + original.name, mesh)
            scene.collection.objects.link(copy)
            copies.append(copy)
            # Reduce over-tessellated rounded surfaces, retain small face/shoe details.
            count = len(mesh.vertices)
            budget = 2500 if 'Head.001' in original.name else 1800 if any(s in original.name for s in ['Hair', 'Shorts', 'Shirt']) else 700
            if count > 1000:
                modifier = copy.modifiers.new('Game mesh reduction', 'DECIMATE')
                modifier.ratio = min(1.0, budget / count)
                bpy.context.view_layer.objects.active = copy
                copy.select_set(True)
                bpy.ops.object.modifier_apply(modifier=modifier.name)
                copy.select_set(False)
            mesh = copy.data
            if mesh not in meshes:
                meshes.append(mesh)
            matrix = inverse @ original.matrix_world
            group_name = 'Body'
            if original.parent == head:
                group_name = 'Head'
            for side, arm in arms.items():
                if original.parent == arm:
                    group_name = side
                    # Remove the source's arm spread while retaining geometry and shoulder positions.
                    matrix = inverse @ torso.matrix_world @ Matrix.Translation(arm.location) @ arm.matrix_world.inverted() @ original.matrix_world
            coords = [matrix @ v.co for v in mesh.vertices]
            if original.parent.name == source_name + '.Pose' and not original.name.startswith(source_name + '.Shorts'):
                left, right = [], []
                for face in mesh.polygons:
                    mean_x = sum(coords[v].x for v in face.vertices) / len(face.vertices)
                    (left if mean_x < 0 else right).append(face.index)
                add_faces('LegL', mesh, coords, left)
                add_faces('LegR', mesh, coords, right)
            else:
                add_faces(group_name, mesh, coords, range(len(mesh.polygons)))
            for material in mesh.materials:
                if material is None:
                    continue
                bsdf = material.node_tree.nodes.get('Principled BSDF') if material.use_nodes else None
                materials[material.name] = {
                    'name': material.name,
                    'linearColor': list(bsdf.inputs['Base Color'].default_value) if bsdf else list(material.diffuse_color),
                    'roughness': float(bsdf.inputs['Roughness'].default_value) if bsdf else float(material.roughness),
                    'metallic': float(bsdf.inputs['Metallic'].default_value) if bsdf else float(material.metallic),
                }

        points = [point for group in groups.values() for point in group['vertices']]
        lowest = min(p.z for p in points)
        highest = max(p.z for p in points)
        scale = 1.35 / (highest - lowest)
        for side in ['LegL', 'LegR']:
            points = groups[side]['vertices']
            pivots[side] = Vector((sum(p.x for p in points) / len(points), 0, min(0.46, max(p.z for p in points))))

        root = bpy.data.objects.new(asset_name, None)
        scene.collection.objects.link(root)
        copies.append(root)
        for name, group in groups.items():
            pivot = bpy.data.objects.new(name + 'Pivot', None)
            scene.collection.objects.link(pivot)
            pivot.parent = root
            p = pivots[name]
            pivot.location = (p.x * scale, p.y * scale, (p.z - lowest) * scale)
            copies.append(pivot)
            mesh = bpy.data.meshes.new(name + 'Mesh')
            mesh.from_pydata([(point - p) * scale for point in group['vertices']], [], group['faces'])
            for material in group['materials']:
                mesh.materials.append(material)
            for polygon, mat, smooth in zip(mesh.polygons, group['material_indices'], group['smooth']):
                polygon.material_index = mat
                polygon.use_smooth = smooth
            mesh.update()
            meshes.append(mesh)
            obj = bpy.data.objects.new(name + 'Mesh', mesh)
            scene.collection.objects.link(obj)
            obj.parent = pivot
            copies.append(obj)

        marker = bpy.data.objects.new('ForwardMarker', None)
        scene.collection.objects.link(marker)
        marker.parent = root
        marker.location = (0, 1, 0)
        copies.append(marker)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in [root] + list(root.children_recursive):
            obj.select_set(True)
        bpy.context.view_layer.objects.active = root
        bpy.ops.export_scene.fbx(filepath=str(OUTPUT / (asset_name + '.fbx')), use_selection=True,
                                 object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 add_leaf_bones=False, bake_anim=False)
        report = {'source': bpy.data.filepath, 'sourceRider': source_name,
                  'sourceVertexCount': sum(len(o.data.vertices) for o in objects),
                  'vertexCount': sum(len(group['vertices']) for group in groups.values()),
                  'meshCount': len(groups), 'height': 1.35,
                  'rig': 'articulated transform pivots; no skeleton or skinning',
                  'materials': list(materials.values())}
        (OUTPUT / (asset_name + '.materials.json')).write_text(json.dumps(report, indent=2))
        print(asset_name + '_EXPORT_OK', report['sourceVertexCount'], '->', report['vertexCount'], 'vertices;', len(groups), 'meshes')
    finally:
        bpy.context.window.scene = original_scene
        for obj in copies:
            bpy.data.objects.remove(obj, do_unlink=True)
        for mesh in meshes:
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)
        bpy.data.scenes.remove(scene)

for index, asset_name in globals().get('RIDERS', enumerate(RIDER_ASSETS)):
    export_rider(index, asset_name)
