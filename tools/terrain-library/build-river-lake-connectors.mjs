import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson,digest} from './library.mjs';
import {MASKS} from './topology.mjs';
import {LAKE_CONNECTORS,lakeConnectorPixel} from './river-lake-connectors.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const source='addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(path.join(root,source));
const pixels=await sharp(bytes).ensureAlpha().raw().toBuffer(),mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c]+=pixels[i+c]/(pixels.length/4);
const lakeBase='addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/';
const frames=await sharp(path.join(root,lakeBase,'lake_surface_frames.png')).ensureAlpha().raw().toBuffer();
const base='addons/beep_game_builder_cs/generated/dev/cartoon/water/river_lake_connectors_v1/';
const results=[];
for(const projection of ['square','isometric']) {
  const height=projection==='square'?64:32,width=512;
  const data=Buffer.alloc(width*height*16*4);
  const original=await sharp(path.join(root,lakeBase,projection,'grass_lake_16.png')).ensureAlpha().raw().toBuffer();
  let compared=0;
  for(let frame=0;frame<16;frame++) for(let index=0;index<8;index++) for(let y=0;y<height;y++) for(let x=0;x<64;x++) {
    let px=x+.5,py=y+.5;
    if(projection==='isometric') {
      const dx=(px-32)/64,dy=(py-16)/32;
      px=(dx+dy+.5)*64;py=(-dx+dy+.5)*64;
      if(px<0||py<0||px>=64||py>=64) continue;
    }
    const profile=LAKE_CONNECTORS[index];
    const sx=Math.max(0,Math.min(63,Math.round(px-.5))),sy=Math.max(0,Math.min(63,Math.round(py-.5)));
    const fi=((frame*64+sy)*64+sx)*4;
    const color=lakeConnectorPixel(profile,px,py,frame,mean,[...frames.subarray(fi,fi+4)]);
    const t={north:py,south:64-py,east:64-px,west:px}[profile.port];
    if(t>=8) {
      const slot=MASKS.indexOf(profile.mask);
      const oi=(((frame*6+Math.floor(slot/8))*height+y)*512+(slot%8)*64+x)*4;
      if(color.some((v,c)=>v!==original[oi+c])) throw Error(`Lake atlas mismatch ${projection} ${profile.id} ${frame} ${x},${y}`);
      compared++;
    }
    const offset=((frame*height+y)*width+index*64+x)*4;
    for(let c=0;c<4;c++) data[offset+c]=color[c];
  }
  fs.mkdirSync(path.join(root,base,projection),{recursive:true});
  await sharp(data,{raw:{width,height:height*16,channels:4}}).png().toFile(path.join(root,base,projection,'river_lake_16.png'));
  results.push({projection,lakePixelsCompared:compared,mismatches:0});
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'candidate',style:'cartoon',source,sourceSha256:digest(bytes),
  frames:16,periodSeconds:1.2,profiles:LAKE_CONNECTORS.map((p,i)=>({...p,atlas:[i,0]})),
  projections:['square','isometric'],verification:results,approvalEvidence:null,
  scope:'Single-cell narrow river inlet/outlet contacts with existing grass lake banks; explicit flow selection required.',
  pending:['Visual current transition review','Wide mouths','Sand/rock banks','Sea estuaries'],
  godotScenes:['square/river_lake_review.tscn','isometric/river_lake_review.tscn']});
console.log(JSON.stringify(results));
