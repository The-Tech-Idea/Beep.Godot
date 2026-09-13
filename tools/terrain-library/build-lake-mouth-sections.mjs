import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson,digest} from './library.mjs';
import {MASKS} from './topology.mjs';
import {LAKE_CONNECTORS} from './river-lake-connectors.mjs';
import {LAKE_MOUTH_MODULES,lakeMouthPixel} from './lake-mouth-sections.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const water='addons/beep_game_builder_cs/generated/dev/cartoon/water/';
const source=water+'staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(path.join(root,source));
const pixels=await sharp(bytes).ensureAlpha().raw().toBuffer(),mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c]+=pixels[i+c]/(pixels.length/4);
const lake=water+'lake_banks_v1/';
const frames=await sharp(path.join(root,lake,'lake_surface_frames.png')).ensureAlpha().raw().toBuffer();
const base=water+'lake_mouth_sections_v1/';
const verification=[];
for(const projection of ['square','isometric']) {
  const height=projection==='square'?64:32,width=192;
  const data=Buffer.alloc(width*height*8*16*4);
  const original=await sharp(path.join(root,lake,projection,'grass_lake_16.png')).ensureAlpha().raw().toBuffer();
  let compared=0;
  for(let frame=0;frame<16;frame++) for(const module of LAKE_MOUTH_MODULES) for(let y=0;y<height;y++) for(let x=0;x<64;x++) {
    let px=x+0.5,py=y+0.5;
    if(projection==='isometric') {
      const dx=(px-32)/64,dy=(py-16)/32;
      px=(dx+dy+0.5)*64;py=(-dx+dy+0.5)*64;
      if(px<0||py<0||px>=64||py>=64) continue;
    }
    const sx=Math.max(0,Math.min(63,Math.round(px-0.5))),sy=Math.max(0,Math.min(63,Math.round(py-0.5)));
    const fi=((frame*64+sy)*64+sx)*4;
    const profile=LAKE_CONNECTORS.find(p=>p.id===module.profileId);
    const color=lakeMouthPixel(profile,module.kind,px,py,frame,mean,[...frames.subarray(fi,fi+4)]);
    const t={north:py,east:64-px,south:64-py,west:px}[profile.port];
    if(t>=8) {
      const slot=MASKS.indexOf(module.mask);
      const oi=(((frame*6+Math.floor(slot/8))*height+y)*512+(slot%8)*64+x)*4;
      if(color.some((value,c)=>value!==original[oi+c])) throw Error(`Lake boundary mismatch: ${projection} ${module.id} ${frame} ${x},${y}`);
      compared++;
    }
    const offset=(((frame*8+module.atlas[1])*height+y)*width+module.atlas[0]*64+x)*4;
    for(let c=0;c<4;c++) data[offset+c]=color[c];
  }
  fs.mkdirSync(path.join(root,base,projection),{recursive:true});
  await sharp(data,{raw:{width,height:height*8*16,channels:4}}).png().toFile(path.join(root,base,projection,'lake_mouth_sections_16.png'));
  verification.push({projection,lakePixelsCompared:compared,mismatches:0});
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'technical_candidate',style:'cartoon',source,sourceSha256:digest(bytes),
  lakeFrames:lake+'lake_surface_frames.png',lakeFramesSha256:digest(fs.readFileSync(path.join(root,lake,'lake_surface_frames.png'))),
  frames:16,periodSeconds:1.2,profiles:LAKE_MOUTH_MODULES,projections:['square','isometric'],reviewWidthsCells:[1,2,3,5],verification,
  construction:'low_bank_repeatable_middle_high_bank',approvalEvidence:null,
  pending:['native_packaging','depth_contact_binding','visual_approval','sand_rock_banks','production_integration']});
console.log(JSON.stringify(verification));
