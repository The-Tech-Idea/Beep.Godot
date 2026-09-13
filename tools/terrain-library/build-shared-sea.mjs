import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson,digest} from './library.mjs';
import {MASKS} from './topology.mjs';
import {SEA_INLETS} from './river-sea-connectors.mjs';
import {seaControlPixel,inletControlPixel,inletSectionControlPixel,inletSectionMask,depthControlPixel} from './sea-shared-surface.mjs';
import {SECTION_KINDS,sectionSequence} from './river-sections.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const source='addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(path.join(root,source));
const pixels=await sharp(bytes).ensureAlpha().raw().toBuffer(),mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c]+=pixels[i+c]/(pixels.length/4);
const base='addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_sea_v1/';
for(const projection of ['square','isometric']) {
  const height=projection==='square'?64:32;
  fs.mkdirSync(path.join(root,base,projection),{recursive:true});
  for(const kind of ['sea','inlets','wide_inlets','depth']) {
    const boundary=kind==='sea'||kind==='depth';
    const columns=boundary?8:(kind==='inlets'?4:3),rows=boundary?6:(kind==='inlets'?1:4),width=columns*64;
    const data=Buffer.alloc(width*height*rows*16*4);
    for(let frame=0;frame<16;frame++) for(let index=0;index<rows*columns;index++) for(let y=0;y<height;y++) for(let x=0;x<64;x++) {
      let px=x+.5,py=y+.5;
      if(projection==='isometric') {
        const dx=(px-32)/64,dy=(py-16)/32;
        px=(dx+dy+.5)*64;py=(-dx+dy+.5)*64;
        if(px<0||py<0||px>=64||py>=64) continue;
      }
      const color=kind==='depth'?depthControlPixel(index===47?null:MASKS[index],px,py):kind==='sea'?(index===47?[0,0,255,255]:seaControlPixel(MASKS[index],px,py)):
        (kind==='inlets'?inletControlPixel(SEA_INLETS[index],px,py,frame,mean):
          inletSectionControlPixel(SEA_INLETS[Math.floor(index/3)],SECTION_KINDS[index%3+1],px,py,frame,mean));
      const offset=(((frame*rows+Math.floor(index/columns))*height+y)*width+(index%columns)*64+x)*4;
      for(let c=0;c<4;c++) data[offset+c]=color[c];
    }
    await sharp(data,{raw:{width,height:height*rows*16,channels:4}}).png().toFile(path.join(root,base,projection,kind+'_control_16.png'));
  }
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'technical_candidate',style:'cartoon',source,sourceSha256:digest(bytes),waterBase:mean,
  frames:16,periodSeconds:1.2,projections:['square','isometric'],coastConfigurations:47,
  shader:'addons/beep_game_builder_cs/shaders/terrain_shared_sea.gdshader',
  surfaceCoordinates:'shared_logical_map_plane',clock:'native_tile_atlas_frame_no_independent_TIME',
  controlEncoding:{grass:[0,0,255],bank:[189,167,121],seaBlue:254,inletBlue:[250,251,252,253],depthBlue:249,red:'sea_mix_or_shallow_depth_0_255',green:'distance_0_64_pixels'},
  depth:{configurations:47,background:'deep_sea',layer:'Depth',placement:'full_open_sea_cells_only',guard:'addons/beep_game_builder_cs/ecs/terrain/TerrainWaterDepthGuard.gd',scenes:['square/depth_review.tscn','isometric/depth_review.tscn'],approvalEvidence:null},
  coastalDepth:{placement:'sea_cells_including_grass_coasts',encoding:'one_rgba_texel_per_shallow_cell_reuses_depth_masks',binding:'addons/beep_game_builder_cs/ecs/terrain/TerrainSeaDepthBinding.gd',scenes:['square/coastal_depth_review.tscn','isometric/coastal_depth_review.tscn'],validNeighborhoodPairsPerProjection:650,maxFieldSideDefault:1024,approvalEvidence:null},
  profiles:SEA_INLETS,
  widthExamples:[1,2,3,5].map(width=>({width,sections:sectionSequence(width)})),
  widthModules:SEA_INLETS.flatMap((p,row)=>SECTION_KINDS.slice(1).map((kind,column)=>({id:p.id+'.'+kind,port:p.port,flow:p.incoming,kind,mask:inletSectionMask(p,kind),sourceId:3,atlas:[column,row],elevationPixels:0}))),
  widthScenes:SEA_INLETS.flatMap(p=>['square','isometric'].map(projection=>projection+'/mouth_widths_'+p.port+'.tscn')),
  approvalEvidence:null,
  pending:['Visual swell/surf, depth and wide-mouth approval','Sand/rock banks','River depth contacts and lake depth','Large-map field cost and chunking','Production promotion'],
  godotScenes:['square/sea_review.tscn','isometric/sea_review.tscn','square/river_sea_review.tscn','isometric/river_sea_review.tscn']});
console.log('Shared-sea control atlases exported for both projections.');
