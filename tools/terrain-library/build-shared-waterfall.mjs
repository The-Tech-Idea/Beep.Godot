import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {waterfallPixel,laneNoise} from './waterfall-sections.mjs';
import {SECTION_KINDS} from './river-sections.mjs';
import {ISO_FALL_DIRECTIONS,isometricWaterfallSample,projectFallPoint} from './isometric-waterfall.mjs';
import {writeJson,digest} from './library.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const source='addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(path.join(root,source));
const pixels=await sharp(bytes).ensureAlpha().raw().toBuffer();
const mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c]+=pixels[i+c]/(pixels.length/4);
const folder='addons/beep_game_builder_cs/generated/dev/cartoon/water/shared_waterfall_v1/';
fs.mkdirSync(path.join(root,folder),{recursive:true});
writeJson(root,folder+'motion_parameters.json',{baseColorBytes:mean,noise:Array.from({length:8},(_,salt)=>Array.from({length:64},(_,lane)=>laneNoise(lane,salt))),periodPixels:1536});
const width=1280,height=3072,data=Buffer.alloc(width*height*4);
for(let frame=0;frame<16;frame++) for(let offset=0;offset<5;offset++) for(let kind=0;kind<4;kind++) for(let y=0;y<192;y++) for(let x=0;x<64;x++) {
  const rgba=waterfallPixel(SECTION_KINDS[kind],64,x+.5,y+.5,frame,mean,offset*64);
  const index=((frame*192+y)*width+(offset*4+kind)*64+x)*4;
  for(let c=0;c<4;c++) data[index+c]=rgba[c];
}
await sharp(data,{raw:{width,height,channels:4}}).png().toFile(path.join(root,folder,'shared_curtain_16.png'));
writeJson(root,folder+'manifest.json',{status:'candidate',source,sourceSha256:digest(bytes),frames:16,periodSeconds:1.2,risePixels:64,frameSize:[64,192],offsetsCells:[0,1,2,3,4],kinds:SECTION_KINDS,atlasColumn:'offsetCells * 4 + kindIndex',approvalEvidence:null,pending:['visual_review','arbitrary_width_runtime_sampling','isometric_export','other_rises','production_integration']});
console.log('Exported shared-coordinate waterfall review atlas; legacy sheets unchanged.');

const exports=[];
for(const projection of ['square','isometric']) for(const rise of [16,32,64]) {
  const directions=projection==='square'?['north_south']:ISO_FALL_DIRECTIONS;
  const profiles=directions.flatMap(direction=>Array.from({length:5},(_,offset)=>SECTION_KINDS.map(kind=>({direction,offset,kind})))).flat();
  const cellWidth=projection==='square'?64:96,cellHeight=(projection==='square'?128:48)+rise;
  const atlasWidth=cellWidth*profiles.length,atlasHeight=cellHeight*16;
  const sheet=Buffer.alloc(atlasWidth*atlasHeight*4);
  for(let f=0;f<16;f++) for(let i=0;i<profiles.length;i++) for(let y=0;y<cellHeight;y++) for(let x=0;x<cellWidth;x++) {
    const p=profiles[i];
    const rgba=projection==='square'?waterfallPixel(p.kind,rise,x+.5,y+.5,f,mean,p.offset*64):isometricWaterfallSample(p.kind,p.direction,rise,x+.5,y+.5,f,mean,p.offset*64).rgba;
    const index=((f*cellHeight+y)*atlasWidth+i*cellWidth+x)*4;
    for(let c=0;c<4;c++) sheet[index+c]=rgba[c];
  }
  const file=projection==='square'&&rise===64?'shared_curtain_16.png':`${projection}_rise_${rise}.png`;
  await sharp(sheet,{raw:{width:atlasWidth,height:atlasHeight,channels:4}}).png().toFile(path.join(root,folder,file));
  exports.push({projection,risePixels:rise,sheet:file,frameSize:[cellWidth,cellHeight],profiles:profiles.map((p,index)=>({...p,column:index,id:`${p.direction}.${p.kind}.offset_${p.offset}`})),connectors:directions.map(direction=>({direction,upstreamAnchor:projection==='square'?[32,32]:projectFallPoint(direction,32,32),downstreamAnchor:projection==='square'?[32,96+rise]:projectFallPoint(direction,32,96,rise)}))});
}
writeJson(root,folder+'projections_manifest.json',{schemaVersion:1,status:'candidate',source,sourceSha256:digest(bytes),frames:16,periodSeconds:1.2,exports,approvalEvidence:null,pending:['GPU_projection_review','arbitrary_width_sampling','visual_approval','production_integration']});
console.log('Exported both projections at 16/32/64px rise with five shared surface offsets.');
