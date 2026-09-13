import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson,digest} from './library.mjs';
import {SECTION_KINDS,sectionSequence} from './river-sections.mjs';
import {FALL_RISES,waterfallPixel} from './waterfall-sections.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const source='addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(path.join(root,source));
const pixels=await sharp(bytes).ensureAlpha().raw().toBuffer(),mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c]+=pixels[i+c]/(pixels.length/4);
const base='addons/beep_game_builder_cs/generated/dev/cartoon/water/waterfall_sections_v1/';
fs.mkdirSync(path.join(root,base),{recursive:true});
for(const rise of FALL_RISES) {
  const height=128+rise,width=256,atlasHeight=height*16;
  const data=Buffer.alloc(width*atlasHeight*4);
  for(let frame=0;frame<16;frame++) for(let kind=0;kind<4;kind++) for(let y=0;y<height;y++) for(let x=0;x<64;x++) {
    const rgba=waterfallPixel(SECTION_KINDS[kind],rise,x+.5,y+.5,frame,mean);
    const offset=((frame*height+y)*width+kind*64+x)*4;
    for(let c=0;c<4;c++) data[offset+c]=rgba[c];
  }
  await sharp(data,{raw:{width,height:atlasHeight,channels:4}}).png().toFile(path.join(root,base,`water_rise_${rise}_16.png`));
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'technical_candidate',projection:'square',style:'cartoon',
  source,sourceSha256:digest(bytes),method:'Analytic water-only connector animation; existing painted waterfall artwork is retained unchanged.',
  editableMaster:'tools/terrain-library/waterfall-sections.mjs',frames:16,periodSeconds:1.2,
  rises:FALL_RISES.map(rise=>({risePixels:rise,frameSize:[64,128+rise],lipY:64,impactY:64+rise,
    sheet:`water_rise_${rise}_16.png`,spriteFrames:`water_rise_${rise}.tres`})),
  sections:SECTION_KINDS,widthExamples:[1,2,3,5].map(width=>({width,sections:sectionSequence(width)})),
  connector:{upstream:'north',downstream:'south',upstreamPlaneLength:64,downstreamPlaneLength:64,
    horizontalContract:'river_sections_v1',visualRiseIsNavigation:false},
  approvalEvidence:null,pending:['Authored cliff terrain','Isometric water modules','Visual foam review','Lake/sea outlet adapters','Production promotion'],
  terrainIncluded:false,showcase:'water_only_review.tscn'});
console.log('Built water-only repeatable waterfall sections at 16/32/64px rise.');
