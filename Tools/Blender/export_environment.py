"""Export reusable source scenery and distant landmarks, preserving the open scene."""
import bpy, json
from pathlib import Path
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets/Art/Models/Environment'
OUTPUT.mkdir(parents=True, exist_ok=True)
original_scene = bpy.context.scene
source = list(original_scene.objects)
depsgraph = bpy.context.evaluated_depsgraph_get()

def bounds(obj):
    points = [obj.matrix_world @ Vector(p) for p in obj.bound_box]
    return Vector([min(p[i] for p in points) for i in range(3)]), Vector([max(p[i] for p in points) for i in range(3)])

def export(name, objects, origin):
    scene = bpy.data.scenes.new(name + '_Export_Temporary')
    copies, meshes, materials = [], [], {}
    vertices, faces, indices, smooth, slots = [], [], [], [], []
    try:
        bpy.context.window.scene = scene
        for obj in objects:
            mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph), depsgraph=depsgraph)
            meshes.append(mesh)
            offset = len(vertices)
            for vertex in mesh.vertices:
                p = obj.matrix_world @ vertex.co - origin
                vertices.append((p.x, p.y, p.z))
            for face in mesh.polygons:
                faces.append([offset + i for i in face.vertices])
                material = mesh.materials[face.material_index]
                if material not in slots: slots.append(material)
                indices.append(slots.index(material)); smooth.append(face.use_smooth)
            for material in mesh.materials:
                if not material: continue
                bsdf = material.node_tree.nodes.get('Principled BSDF') if material.use_nodes else None
                materials[material.name] = {'name':material.name,'linearColor':list(bsdf.inputs['Base Color'].default_value) if bsdf else list(material.diffuse_color),'roughness':float(bsdf.inputs['Roughness'].default_value) if bsdf else float(material.roughness),'metallic':float(bsdf.inputs['Metallic'].default_value) if bsdf else float(material.metallic)}
        mesh = bpy.data.meshes.new(name + 'Mesh')
        meshes.append(mesh); mesh.from_pydata(vertices, [], faces)
        for material in slots: mesh.materials.append(material)
        for polygon, index, shading in zip(mesh.polygons, indices, smooth):
            polygon.material_index=index; polygon.use_smooth=shading
        mesh.update()
        root=bpy.data.objects.new(name, None); scene.collection.objects.link(root); copies.append(root)
        visual=bpy.data.objects.new(name+'Mesh',mesh);scene.collection.objects.link(visual);visual.parent=root;copies.append(visual)
        for marker_name, location in [('ForwardMarker',(0,1,0)),('RightMarker',(1,0,0))]:
            marker=bpy.data.objects.new(marker_name,None);scene.collection.objects.link(marker);marker.parent=root;marker.location=location;copies.append(marker)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in copies: obj.select_set(True)
        bpy.context.view_layer.objects.active=root
        bpy.ops.export_scene.fbx(filepath=str(OUTPUT/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,bake_anim=False)
        report={'source':bpy.data.filepath,'sourceObjects':[o.name for o in objects],'vertexCount':len(vertices),'meshCount':1,'materials':list(materials.values())}
        (OUTPUT/(name+'.materials.json')).write_text(json.dumps(report,indent=2))
        print('ENVIRONMENT_EXPORT_OK',name,len(vertices))
    finally:
        bpy.context.window.scene=original_scene
        for obj in copies: bpy.data.objects.remove(obj,do_unlink=True)
        for mesh in meshes:
            if mesh.users==0: bpy.data.meshes.remove(mesh)
        bpy.data.scenes.remove(scene)

for index in range(3):
    suffix='' if index==0 else '.'+str(index).zfill(3)
    house=original_scene.objects['House'+suffix];roof=original_scene.objects['Roof'+suffix]
    low,high=bounds(house)
    export('CoastalHouse'+str(index),[house,roof],Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z)))
trunk=original_scene.objects['PalmTrunk'];base=Vector((trunk.matrix_world.translation.x,trunk.matrix_world.translation.y,0))
palm=[o for o in source if o.name.split('.')[0] in ['PalmTrunk','Frond'] and (Vector((sum(p.x for p in [o.matrix_world@Vector(c) for c in o.bound_box])/8,sum(p.y for p in [o.matrix_world@Vector(c) for c in o.bound_box])/8,0))-base).length<3]
lowest=min(bounds(o)[0].z for o in palm);base.z=lowest
export('CoastalPalm',palm,base)
foot=original_scene.objects['BarrierFoot'];base=foot.matrix_world.translation.copy();base.z=0
barrier=[o for o in source if o.name.split('.')[0] in ['Barrier','BarrierFoot'] and abs(o.matrix_world.translation.y-base.y)<1.2]
export('CoastalBarrier',barrier,base)
background=[o for o in source if o.type=='MESH' and o.name.split('.')[0] in ['Sail','Hull','BridgeTower','TowerBeam','BridgeDeck','Cable','Tower','FarHill']]
export('CoastalBackdrop',background,Vector((0,0,0)))
