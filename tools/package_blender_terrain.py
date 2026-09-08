"""Pack Blender's rendered sprites without altering their appearance or scale."""
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'addons/beep_game_builder_cs/generated/terrain_blender/v1'
BACKGROUND = (28, 37, 39, 255)


def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', size)


def main():
    overview = Image.new('RGBA', (1440, 1100), BACKGROUND)
    draw = ImageDraw.Draw(overview)
    draw.text((28, 16), 'FRONT 2.5D TERRAIN / BLENDER STARTER PACK', font=font(26), fill='white')
    for col, theme in enumerate(['grass_granite', 'grey_rock', 'alpine_snow']):
        folder = OUT/theme
        manifest = json.loads((folder/'manifest.json').read_text())
        regions, crops = {}, []
        for filename in manifest['assets']:
            im = Image.open(folder/filename).convert('RGBA')
            box = im.getbbox()
            assert box and box[0] > 0 and box[1] > 0 and box[2] < im.width and box[3] < im.height, filename
            crop = im.crop(box)
            crops.append((filename, crop, box))
        atlas = Image.new('RGBA', (2048, 2048))
        x, y, row_height = 8, 8, 0
        for filename, crop, box in crops:
            if x+crop.width+8 > atlas.width:
                x, y, row_height = 8, y+row_height+8, 0
            assert y+crop.height+8 <= atlas.height
            atlas.alpha_composite(crop, (x,y))
            regions[filename[:-4]] = {'rect':[x,y,crop.width,crop.height],
                                     'pivot':[400-box[0],360-box[1]]}
            x += crop.width+8
            row_height = max(row_height,crop.height)
        atlas = atlas.crop((0,0,2048,y+row_height+8))
        atlas.save(folder/'sprite_atlas.png')
        manifest['atlas'] = 'sprite_atlas.png'
        manifest['regions'] = regions
        (folder/'manifest.json').write_text(json.dumps(manifest, indent=2))
        sheet = Image.new('RGBA',(1120,1100),BACKGROUND)
        sd = ImageDraw.Draw(sheet)
        sd.text((20,12),theme.replace('_',' ').title(),font=font(28),fill='white')
        for i,(filename,crop,_) in enumerate(crops):
            cell_x,cell_y = (i%4)*280, 60+(i//4)*255
            factor = .38 if i < 7 else 1
            thumb=crop.resize((round(crop.width*factor), round(crop.height*factor)),Image.Resampling.LANCZOS)
            sheet.alpha_composite(thumb,(cell_x+(280-thumb.width)//2,cell_y+(205-thumb.height)//2))
            sd.text((cell_x+12,cell_y+210),filename[:-4].replace('_',' '),font=font(16),fill='white')
        sheet.save(folder/'contact_sheet.png')
        ramp_sheet = Image.new('RGBA',(800,800),BACKGROUND)
        rd = ImageDraw.Draw(ramp_sheet)
        rd.text((18,12),'Ramps: constant width / quarter, half, full rise',font=font(22),fill='white')
        for i, (filename,crop,_) in enumerate(crops[7:]):
            thumb=crop.resize((crop.width*2,crop.height*2),Image.Resampling.LANCZOS)
            x,y=(i%3)*266,60+(i//3)*245
            ramp_sheet.alpha_composite(thumb,(x+(266-thumb.width)//2,y+(205-thumb.height)//2))
            rd.text((x+16,y+210),filename[:-4].replace('ramp_','').replace('_',' '),font=font(18),fill='white')
        ramp_sheet.save(folder/'ramps_preview.png')
        draw.text((col*480+24,66),theme.replace('_',' ').title(),font=font(24),fill='white')
        for filename, region in [('assembled.png',(col*480+10,110,460,390)),
                                 ('hill_round.png',(col*480+18,520,218,160)),
                                 ('hill_square.png',(col*480+244,520,218,160)),
                                 ('ramp_fit.png',(col*480+50,745,380,300))]:
            im = Image.open(folder/filename).convert('RGBA')
            im = im.crop(im.getbbox())
            x,y,w,h = region
            im.thumbnail((w,h))
            overview.alpha_composite(im,(x+(w-im.width)//2,y+(h-im.height)//2))
        draw.text((col*480+22,492),'Rounded hill / square hill',font=font(18),fill='#b8c5c6')
        draw.text((col*480+22,704),'Separate full-height ramp: fit example',font=font(18),fill='#b8c5c6')
        prefix = 'res://'+str(folder.relative_to(ROOT)).replace('\\','/')
        resources = '\n'.join('[ext_resource type="Texture2D" path="'+prefix+'/'+name+'.png" id="'+str(i+1)+'"]'
                              for i,name in enumerate(['plate_base','plate_middle','plate_top']))
        nodes = ['[gd_scene load_steps=4 format=3]',resources,
                 '[node name="BlenderMountain" type="Node2D"]']
        for i,name in enumerate(['plate_base','plate_middle','plate_top']):
            nodes.append('[node name="'+name+'" type="Sprite2D" parent="."]\n'
                         'position = Vector2(0, '+str(round(-i*manifest['level_screen_rise'],4))+')\n'
                         'texture = ExtResource("'+str(i+1)+'")')
        (folder/'mountain_layers.tscn').write_text('\n\n'.join(nodes)+'\n')
    overview.save(OUT/'preview.png')
    print('Packaged 48 sprites, three atlases, three layer scenes and preview.')


if __name__ == '__main__':
    main()
