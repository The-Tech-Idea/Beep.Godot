"""Extract existing transparent artwork into named sprites and Godot resources.

This step only crops and packages the image-generated artwork; it does not
paint terrain, replace materials, key backgrounds, or reshape the sprites.
"""
import json
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'addons/beep_game_builder_cs/generated/terrain_templates/v1'
THEMES = ['grass_granite', 'grey_rock', 'sandstone', 'volcanic_basalt',
          'meadow_hill', 'red_rock_mesa', 'alpine_snow']
NAMES = ['prefab_1_plate_large', 'prefab_1_plate_medium', 'prefab_1_plate_small',
         'prefab_2_plate_large', 'prefab_2_plate_medium', 'prefab_2_plate_small',
         'hill_round', 'hill_square_sloped', 'hill_small']


def spans(values, minimum=12):
    changes = np.diff(np.r_[False, values, False].astype(np.int8))
    return [(int(a), int(b)) for a,b in zip(np.flatnonzero(changes == 1),
                                           np.flatnonzero(changes == -1)) if b-a >= minimum]


def regions(image):
    assert image.mode == 'RGBA', 'Export requires real alpha, not a checkerboard.'
    alpha = np.asarray(image.getchannel('A'))
    assert (alpha == 0).mean() > .25, 'Background is not sufficiently transparent.'
    mask = alpha > 32
    rows = spans(mask.sum(axis=1) > 12, 30)
    assert len(rows) == 3, ('Expected three separated sprite rows', rows)
    boxes = []
    for top,bottom in rows:
        columns = spans(mask[top:bottom].sum(axis=0) > 8, 30)
        assert len(columns) == 3, ('Expected three separated sprite columns', columns)
        for left,right in columns:
            ys,xs = np.nonzero(mask[top:bottom,left:right])
            x0,y0=left+int(xs.min()),top+int(ys.min())
            x1,y1=left+int(xs.max())+1,top+int(ys.max())+1
            boxes.append((max(0,x0-4),max(0,y0-4),min(image.width,x1+4),min(image.height,y1+4)))
    return boxes


def main(ramps=False):
    target=OUT/'ramps' if ramps else OUT
    names=([f'ramp_{height}_{direction}' for height in ['quarter','half','full']
            for direction in ['left','front','right']] if ramps else NAMES)
    target.mkdir(parents=True,exist_ok=True)
    background=(25,33,36,255)
    overview=Image.new('RGBA',(1680,1320),background)
    draw=ImageDraw.Draw(overview)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',22)
    title=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',30)
    heading='SEPARATE RAMPS / TEMPLATE COLLECTION' if ramps else 'MOUNTAINS AND HILLS / TEMPLATE COLLECTION'
    draw.text((24,16),heading,font=title,fill='white')
    catalog={'version':1,'themes':[], 'asset_count':0,
             'references':[
                 {'file':'Art/TileSets/ForestTileSet/Tilemap_color5.png','role':'Top/front/side relationships; not exact 17-piece output layout'},
                 {'file':'Art/TileSets/isometric Cliff and Mountain Tileset Atlas.jfif','role':'Primary painted cliff style and volume'},
                 {'file':'Art/TileSets/Mountain Cliff Terrain Tile Atlas.jfif','role':'Material variation reference'}],
             'projection':'Front-facing elevated 2.5D; artist-authored, not calibrated geometry',
             'heights_calibrated':False,'existing_prefabs_replaced':False}
    for theme_index,theme in enumerate(THEMES):
        folder=target/theme
        folder.mkdir(exist_ok=True)
        image=Image.open(target/'source_sheets'/(theme+'.png'))
        boxes=regions(image)
        manifest={'theme':theme,'assets':[],'heights_calibrated':False}
        prefix='res://'+folder.relative_to(ROOT).as_posix()
        default_scale=(64/max(boxes[i][2]-boxes[i][0] for i in [1,4,7])) if ramps else 1
        sheet=Image.new('RGBA',image.size,background)
        # Preserve source pixels and alpha; exclude only the unused sheet gutters.
        for index,(name,box) in enumerate(zip(names,boxes)):
            crop=image.crop(box)
            crop.save(folder/(name+'.png'))
            sheet.alpha_composite(crop,(box[0],box[1]))
            x,y,x1,y1=box
            resource='[gd_resource type="AtlasTexture" load_steps=2 format=3]\n\n'
            resource+='[ext_resource type="Texture2D" path="'+prefix+'/../source_sheets/'+theme+'.png" id="1"]\n\n'
            resource+='[resource]\natlas = ExtResource("1")\n'
            resource+='region = Rect2('+', '.join(map(str,[x,y,x1-x,y1-y]))+')\nfilter_clip = true\n'
            (folder/(name+'.tres')).write_text(resource)
            entry={'id':name,'png':name+'.png','texture':name+'.tres',
                   'region':[x,y,x1-x,y1-y], 'ground_pivot':[(x1-x)/2,y1-y-4],
                   'nominal_height_ratio':([.25,.5,1][index//3] if ramps else [1,.5,.25][index%3] if index<6 else None),
                   'measured_cliff_height':None,
                   'family':('ramp' if ramps else 'prefab_1' if index<3 else 'prefab_2' if index<6 else 'hill')}
            if ramps:
                entry['approach_direction']=['left','front','right'][index%3]
                entry['placement']='manual'
                entry['plate_fit_validated']=False
                entry['default_scale']=default_scale
                entry['scene']=name+'.tscn'
                scene='[gd_scene load_steps=2 format=3]\n\n'
                scene+='[ext_resource type="Texture2D" path="'+prefix+'/'+name+'.tres" id="1"]\n\n'
                scene+='[node name="'+name+'" type="Node2D"]\n\n'
                scene+='[node name="Sprite" type="Sprite2D" parent="."]\n'
                scene+='position = Vector2(0, '+str(round(-(y1-y)*default_scale/2,6))+')\n'
                scene+='scale = Vector2('+str(default_scale)+', '+str(default_scale)+')\n'
                scene+='texture = ExtResource("1")\n'
                (folder/(name+'.tscn')).write_text(scene)
            manifest['assets'].append(entry)
        sheet.save(folder/'preview.png')
        (folder/'manifest.json').write_text(json.dumps(manifest,indent=2))
        px,py=(theme_index%3)*560,82+(theme_index//3)*410
        thumb=sheet.copy()
        thumb.thumbnail((536,358))
        overview.alpha_composite(thumb,(px+12,py+34))
        draw.text((px+16,py),theme.replace('_',' ').title(),font=font,fill='white')
        catalog['themes'].append({'id':theme,'manifest':theme+'/manifest.json','count':9})
        catalog['asset_count']+=9
        print(theme, '9 sprites', [list((b[2]-b[0],b[3]-b[1])) for b in boxes])
    overview.save(target/'all_themes_preview.png')
    (target/'catalog.json').write_text(json.dumps(catalog,indent=2))
    assert catalog['asset_count']==63
    print('Validated 7 transparent sheets, 63 sprite crops and 63 atlas regions.')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--ramps',action='store_true')
    main(parser.parse_args().ramps)
