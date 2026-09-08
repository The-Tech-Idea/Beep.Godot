"""Run with Blender 3.1: --background --python tools/build_blender_terrain.py."""
import argparse
import json
import math
import random
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'addons/beep_game_builder_cs/generated/terrain_blender/v1'
SOURCE = ROOT / 'art_sources/terrain_blender/v1'
TEXTURES = ROOT.parent / 'Art/textures'
PPU = 64
ANGLE = math.radians(38)
THEMES = {
    'grass_granite': ('Natural Grassy Meadow Texture.png', (1, 0)),
    'grey_rock': ('Seamless Neutral Gravel Ground Texture.png', (0, 0)),
    'alpine_snow': ('Seamless Frosted Snow and Ice Texture.png', (1, 2)),
}


def material(name, file, cell=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    n, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = n.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value = .92
    bsdf.inputs['Specular'].default_value = .12
    tex = n.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(str(TEXTURES / file), check_existing=True)
    uv = n.new('ShaderNodeTexCoord')
    if cell is None:
        links.new(uv.outputs['UV'], tex.inputs['Vector'])
    else:
        # Sample only the interior of one atlas swatch; exclude gutters.
        fract = n.new('ShaderNodeVectorMath')
        fract.operation = 'FRACTION'
        scale = n.new('ShaderNodeVectorMath')
        scale.operation = 'MULTIPLY'
        scale.inputs[1].default_value = (.216, .216, 1)
        offset = n.new('ShaderNodeVectorMath')
        offset.operation = 'ADD'
        offset.inputs[1].default_value = (.02 + cell[0] * .247, .764 - cell[1] * .247, 0)
        links.new(uv.outputs['UV'], fract.inputs[0])
        links.new(fract.outputs[0], scale.inputs[0])
        links.new(scale.outputs[0], offset.inputs[0])
        links.new(offset.outputs[0], tex.inputs['Vector'])
    links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bump = n.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = .22
    bump.inputs['Distance'].default_value = .065
    links.new(tex.outputs['Color'], bump.inputs['Height'])
    links.new(bump.outputs[0], bsdf.inputs['Normal'])
    return mat


def rock_material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    n, links = mat.node_tree.nodes, mat.node_tree.links
    shader = n.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = .94
    shader.inputs['Specular'].default_value = .16
    coords = n.new('ShaderNodeTexCoord')
    noise = n.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 4.2
    noise.inputs['Detail'].default_value = 5
    noise.inputs['Roughness'].default_value = .72
    links.new(coords.outputs['Object'], noise.inputs['Vector'])
    colors = n.new('ShaderNodeValToRGB')
    colors.color_ramp.elements[0].position = .22
    colors.color_ramp.elements[0].color = (.105,.113,.112,1)
    colors.color_ramp.elements[1].position = .78
    colors.color_ramp.elements[1].color = (.38,.39,.365,1)
    links.new(noise.outputs['Fac'],colors.inputs[0])
    links.new(colors.outputs[0],shader.inputs['Base Color'])
    grain = n.new('ShaderNodeTexNoise')
    grain.inputs['Scale'].default_value = 85
    grain.inputs['Detail'].default_value = 3
    links.new(coords.outputs['Object'],grain.inputs['Vector'])
    bump = n.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = .38
    bump.inputs['Distance'].default_value = .075
    links.new(noise.outputs['Fac'],bump.inputs['Height'])
    fine = n.new('ShaderNodeBump')
    fine.inputs['Strength'].default_value = .28
    fine.inputs['Distance'].default_value = .015
    links.new(grain.outputs['Fac'],fine.inputs['Height'])
    links.new(bump.outputs[0],fine.inputs['Normal'])
    links.new(fine.outputs[0],shader.inputs['Normal'])
    return mat


def rim_material(top_mat):
    mat = top_mat.copy()
    mat.name = top_mat.name+'_soil_transition'
    n, links = mat.node_tree.nodes, mat.node_tree.links
    shader = n.get('Principled BSDF')
    tex = next(node for node in n if node.type == 'TEX_IMAGE')
    attribute = n.new('ShaderNodeAttribute')
    attribute.attribute_name = 'turf'
    mix = n.new('ShaderNodeMixRGB')
    mix.inputs[1].default_value = (.11,.072,.035,1)
    links.new(attribute.outputs['Fac'],mix.inputs[0])
    links.new(tex.outputs['Color'],mix.inputs[2])
    links.new(mix.outputs[0],shader.inputs['Base Color'])
    return mat


def mesh(name, verts, faces, mats, indices, wall_uv=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.materials.clear()
    for mat in mats:
        data.materials.append(mat)
    uv = data.uv_layers.new()
    for face, index in zip(data.polygons, indices):
        face.material_index = index
        face.use_smooth = index == 1
        for li in face.loop_indices:
            v = data.vertices[data.loops[li].vertex_index].co
            if index == 1 and wall_uv:
                a = math.atan2(v.y, v.x) / (2 * math.pi)
                uv.data[li].uv = (a * 5, v.z * .72 + .1)
            elif index == 1:
                along = v.y if abs(face.normal.x) > .5 else v.x
                uv.data[li].uv = (along / 2 + .5, v.z * .72 + .1)
            else:
                uv.data[li].uv = (v.x / 4 + .5, v.y / 4 + .5)
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return obj


def outline(a, rx, ry, square=False):
    power = .52 if square else 1.0
    c, s = math.cos(a), math.sin(a)
    noise = 1 + .016 * math.sin(7*a+.3) + .012 * math.cos(13*a)
    return (rx * math.copysign(abs(c)**power, c) * noise,
            ry * math.copysign(abs(s)**power, s) * noise)


def plate(name, rx, ry, height, mats):
    rng = random.Random(103)
    verts, faces, indices, turf = [], [], [], []
    count = max(13, round(rx*7))
    rock_mesh=bmesh.new()
    bmesh.ops.create_icosphere(rock_mesh,subdivisions=3,radius=1)
    rock_mesh.verts.ensure_lookup_table()
    rock_mesh.verts.index_update()
    rock_vertices=[v.co.copy() for v in rock_mesh.verts]
    rock_faces=[tuple(v.index for v in f.verts) for f in rock_mesh.faces]
    rock_mesh.free()
    weights=[rng.uniform(.7,1.5) for _ in range(count)]
    angles=[0]
    for weight in weights:
        angles.append(angles[-1]+weight/sum(weights)*2*math.pi)
    # Unequal rock masses with chipped silhouettes, rather than regular wall blocks.
    for i in range(count):
        angle=(angles[i]+angles[i+1])/2
        cut = rng.uniform(.35,.65)
        levels = [0,cut,1] if rng.random() > .7 else [0,1]
        tangent=Vector((-rx*math.sin(angle),ry*math.cos(angle),0))
        half_width=tangent.length*(angles[i+1]-angles[i])*.61
        tangent.normalize()
        radial=Vector((math.cos(angle),math.sin(angle),0))
        x,y=outline(angle,rx*.975,ry*.975)
        for j in range(len(levels)-1):
            start = len(verts)
            z0, z1 = levels[j]*height, levels[j+1]*height
            depth=rng.uniform(.20,.34)*min(1,rx/2)
            skew=rng.uniform(-.16,.16)
            phase=rng.random()*12
            for v in rock_vertices:
                distortion=1+.08*math.sin(v.x*8+phase)*math.cos(v.z*7-phase)+.05*math.sin(v.y*13+phase)
                side=tangent*(v.x*half_width*distortion+v.z*skew)
                outward=radial*(v.y*depth*distortion)
                z=(z0+z1)/2+v.z*(z1-z0)*.64*distortion
                verts.append((x+side.x+outward.x,y+side.y+outward.y,max(0,min(height-.035,z))))
                turf.append(0)
            for f in rock_faces:
                faces.append(tuple(start+k for k in f))
                indices.append(1)
    # A shallow irregular turf lip wraps over the rock rim into exposed soil.
    rim_start=len(verts)
    samples=192
    for radius,drop,coverage in [(.945,0,1),(.986,.007,.96),(1.006,.035,.72),(1.002,.080,0)]:
        for i in range(samples):
            a=2*math.pi*i/samples
            x,y=outline(a,rx,ry)
            roughness=(.009*math.sin(37*a)+.008*math.sin(61*a))*(drop/.085)
            verts.append((x*radius,y*radius,height-drop*min(height/.5,1)+roughness*height))
            turf.append(coverage)
    faces.append(tuple(rim_start+i for i in range(samples)))
    indices.append(0)
    for j in range(3):
        for i in range(samples):
            a=rim_start+j*samples+i
            b=rim_start+j*samples+(i+1)%samples
            faces.append((a,a+samples,b+samples,b))
            indices.append(2)
    # Inner soil core closes cracks without a transparent gap through the cliff.
    core=len(verts)
    for z in [0,height-.04]:
        for i in range(samples):
            x,y=outline(2*math.pi*i/samples,rx*.954,ry*.954)
            verts.append((x,y,z))
            turf.append(0)
    for i in range(samples):
        a,b=core+i,core+(i+1)%samples
        faces.append((a,b,b+samples,a+samples))
        indices.append(1)
    obj=mesh(name,verts,faces,mats+[rim_material(mats[0])],indices,True)
    colors=obj.data.vertex_colors.new(name='turf')
    for p in obj.data.polygons:
        p.use_smooth = True
        for li in p.loop_indices:
            weight=turf[obj.data.loops[li].vertex_index]
            colors.data[li].color=(weight,weight,weight,1)
    return obj


def hill(name, mats, square=False):
    count, rings = 160, 36
    verts = [(0, 0, 1)]
    for j in range(1, rings+1):
        t = j/rings
        for i in range(count):
            x, y = outline(2*math.pi*i/count, 3.7*t, 2.7*t, square)
            z = (1-t**4)**2
            verts.append((x, y, z))
    faces = [(0, 1+i, 1+(i+1)%count) for i in range(count)]
    for j in range(rings-1):
        for i in range(count):
            a, b = 1+j*count+i, 1+j*count+(i+1)%count
            faces.append((a, a+count, b+count, b))
    obj = mesh(name, verts, faces, mats, [0]*len(faces))
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def ramp(name, height, direction, mats):
    width, run = .85, 1.65
    verts = [(-width/2, -run/2, .02), (width/2, -run/2, .02),
             (width/2, run/2, height), (-width/2, run/2, height),
             (-width/2, -run/2, 0), (width/2, -run/2, 0),
             (width/2, run/2, 0), (-width/2, run/2, 0)]
    obj = mesh(name, verts, [(0,1,2,3), (0,3,7,4), (1,5,6,2), (3,2,6,7)],
               mats, [0,1,1,1])
    obj.rotation_euler.z = {'front':0, 'left':-math.pi/3, 'right':math.pi/3}[direction]
    bevel = obj.modifiers.new('Soft stone edges', 'BEVEL')
    bevel.width, bevel.segments = .018, 3
    return obj


def setup():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = 1.5
    scene.eevee.gtao_factor = 1.15
    scene.eevee.taa_render_samples = 48
    scene.render.resolution_x, scene.render.resolution_y = 800, 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.film_transparent = True
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'Medium High Contrast'
    scene.world.color = (.35, .35, .35)
    bpy.ops.object.camera_add(location=(0, -20*math.cos(ANGLE), 20*math.sin(ANGLE)))
    camera = bpy.context.object
    camera.rotation_euler = (-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 800/PPU
    scene.camera = camera
    for location, energy, size in [((-5,-8,12),1700,8), ((6,1,9),900,7)]:
        bpy.ops.object.light_add(type='AREA', location=location)
        lamp = bpy.context.object
        lamp.data.energy, lamp.data.size = energy, size
        lamp.rotation_euler = (-lamp.location).to_track_quat('-Z','Y').to_euler()
    return scene


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--theme', choices=list(THEMES), default=None)
    parser.add_argument('--preview-only', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    OUT.mkdir(parents=True, exist_ok=True)
    for theme in ([args.theme] if args.theme else THEMES):
        scene = setup()
        top_file, cell = THEMES[theme]
        mats = [material(theme+'_surface', top_file), rock_material(theme+'_cliff')]
        folder = OUT/theme
        folder.mkdir(exist_ok=True)
        assets = [plate('plate_base', 5, 4, 1, mats),
                  plate('plate_middle', 3.65, 2.85, 1, mats),
                  plate('plate_top', 2.25, 1.7, 1, mats),
                  plate('step_half', .95, .7, .5, mats),
                  plate('step_quarter', .95, .7, .25, mats),
                  hill('hill_round', mats), hill('hill_square', mats, True)]
        for label, height in [('quarter',.25), ('half',.5), ('full',1)]:
            for direction in ['front', 'left', 'right']:
                assets.append(ramp('ramp_'+label+'_'+direction, height, direction, mats))
        for obj in assets:
            zs = [v.co.z for v in obj.data.vertices]
            expected = .25 if 'quarter' in obj.name else .5 if 'half' in obj.name else 1
            assert abs(max(zs)-min(zs)-expected) < .001, obj.name
        for obj in assets:
            obj.hide_render = True
            obj.hide_set(True)
        if not args.preview_only:
            for obj in assets:
                obj.hide_render = False
                scene.render.filepath = str(folder/(obj.name+'.png'))
                bpy.ops.render.render(write_still=True)
                obj.hide_render = True
        for index, obj in enumerate(assets[:3]):
            obj.hide_render = False
            obj.hide_set(False)
            obj.location.z = index
        scene.render.filepath = str(folder/'assembled.png')
        bpy.ops.render.render(write_still=True)
        source_folder = SOURCE/theme
        source_folder.mkdir(parents=True, exist_ok=True)
        bpy.ops.file.pack_all()
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(source_folder/'terrain_source.blend'))
        if not args.preview_only:
            for obj in assets:
                obj.hide_render = True
            demo = plate('ramp_fit_plate', 2, 1.7, 1, mats)
            demo.location.y = 2.50
            fit_ramp = next(obj for obj in assets if obj.name == 'ramp_full_front')
            fit_ramp.hide_render = False
            scene.render.filepath = str(folder/'ramp_fit.png')
            bpy.ops.render.render(write_still=True)
            demo.hide_render = True
        manifest = {'theme':theme, 'pixels_per_unit':PPU, 'camera_elevation_degrees':38,
                    'canvas':[800,720], 'origin':[400,360], 'level_height':1,
                    'level_screen_rise':PPU*math.cos(ANGLE), 'ramp_width':.85,
                    'ramp_run':1.65, 'height_ratios':[.25,.5,1],
                    'assets':[obj.name+'.png' for obj in assets],
                    'textures':[top_file],
                    'cliff_material':'modeled rock volumes with procedural granite grain',
                    'blender_source':str(source_folder.relative_to(ROOT)/'terrain_source.blend'),
                    'cliff_atlas_cell':None}
        (folder/'manifest.json').write_text(json.dumps(manifest, indent=2))
        print('TERRAIN COMPLETE:', theme, flush=True)


if __name__ == '__main__':
    main()
