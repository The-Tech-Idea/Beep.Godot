import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson,digest} from './library.mjs';
import {JUNCTIONS,junctionPixel} from './river-junctions.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const source='addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(path.join(root,source));
const pixels=await sharp(bytes).ensureAlpha().raw().toBuffer(),mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c]+=pixels[i+c]/(pixels.length/4);
const base='addons/beep_game_builder_cs/generated/dev/cartoon/water/river_junctions_v1/';
for(const projection of ['square','isometric']) {
  const height=projection==='square'?64:32,width=512,atlasHeight=height*4*16;
  const data=Buffer.alloc(width*atlasHeight*4);
  for(let frame=0;frame<16;frame++) for(let index=0;index<JUNCTIONS.length;index++)
    for(let y=0;y<height;y++) for(let x=0;x<64;x++) {
      let px=x+.5,py=y+.5;
      if(projection==='isometric') {
        const dx=(px-32)/64,dy=(py-16)/32;
        px=(dx+dy+.5)*64;py=(-dx+dy+.5)*64;
        if(px<0||py<0||px>=64||py>=64) continue;
      }
      const rgba=junctionPixel(JUNCTIONS[index],px,py,frame,mean);
      const offset=(((frame*4+Math.floor(index/8))*height+y)*width+(index%8)*64+x)*4;
      for(let c=0;c<4;c++) data[offset+c]=rgba[c];
    }
  fs.mkdirSync(path.join(root,base,projection),{recursive:true});
  await sharp(data,{raw:{width,height:atlasHeight,channels:4}}).png().toFile(path.join(root,base,projection,'river_junctions_16.png'));
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'candidate',source,sourceSha256:digest(bytes),frames:16,periodSeconds:1.2,
  profiles:JUNCTIONS.map((p,i)=>({id:p.id,inflows:p.inflows,outflows:p.outflows,routes:p.routes.map(r=>r.id),atlas:[i%8,Math.floor(i/8)]})),
  coverageScope:'Single-cell directed T branches/confluences and one-to-three/three-to-one crossings. No inferred flow or wide junction coverage.',
  godotScenes:['square/river_junctions_review.tscn','isometric/river_junctions_review.tscn'],
  godotTileSets:['square/river_junctions.tres','isometric/river_junctions.tres'],approvalEvidence:null,
  pending:['Rendered animation approval','Wide junctions','Explicit two-to-two route artwork','Waterfall/lake/sea connectors']});
console.log('Built 32 explicit junction candidates per projection.');
