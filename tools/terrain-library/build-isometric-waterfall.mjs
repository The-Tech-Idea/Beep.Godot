import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {writeJson,digest} from './library.mjs';
import {SECTION_KINDS,sectionSequence} from './river-sections.mjs';
import {FALL_RISES} from './waterfall-sections.mjs';
import {ISO_FALL_DIRECTIONS,isometricWaterfallSample,projectFallPoint} from './isometric-waterfall.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=process.cwd();
const source='addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(source),pixels=await sharp(bytes).ensureAlpha().raw().toBuffer(),mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c]+=pixels[i+c]/(pixels.length/4);
const base='addons/beep_game_builder_cs/generated/dev/cartoon/water/isometric_waterfall_v1/';
fs.mkdirSync(base,{recursive:true});
const profiles=ISO_FALL_DIRECTIONS.flatMap(direction=>SECTION_KINDS.map(kind=>({id:`${direction}.${kind}`,direction,kind})));
for(const rise of FALL_RISES) {
  const width=96*profiles.length,height=48+rise;
  const data=Buffer.alloc(width*height*16*4);
  for(let f=0;f<16;f++) for(let p=0;p<profiles.length;p++) for(let y=0;y<height;y++) for(let x=0;x<96;x++) {
    const profile=profiles[p];
    const sample=isometricWaterfallSample(profile.kind,profile.direction,rise,x+.5,y+.5,f,mean);
    const index=((f*height+y)*width+p*96+x)*4;
    for(let c=0;c<4;c++) data[index+c]=sample.rgba[c];
  }
  await sharp(data,{raw:{width,height:height*16,channels:4}}).png().toFile(base+`fall_${rise}_16.png`);
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'technical_candidate',style:'cartoon',projection:'isometric',source,sourceSha256:digest(bytes),frames:16,periodSeconds:1.2,terrainIncluded:false,approvalEvidence:null,productionReady:false,
  method:'Physical upstream and downstream isometric planes joined by a vertical screen-space curtain; shared water phases, no warped square sprite.',editableMaster:'tools/terrain-library/isometric-waterfall.mjs',profiles,
  rises:FALL_RISES.map(rise=>({risePixels:rise,frameSize:[96,48+rise],sheet:`fall_${rise}_16.png`,spriteFrames:`fall_${rise}.tres`,connectors:ISO_FALL_DIRECTIONS.map(direction=>({direction,upstreamAnchor:projectFallPoint(direction,32,32),downstreamAnchor:projectFallPoint(direction,32,96,rise),acrossCellStep:direction==='north_south'?[32,16]:[-32,16]}))})),
  widthExamples:[1,2,3,5].map(width=>({width,sections:sectionSequence(width)})),pending:['GPU animation review','Authored inlet and bank contacts','Cliff route integration','Stepped and split falls','Visual approval']});
console.log('Built isometric water-only modules: two visible flow directions, three rises, four section roles.');
