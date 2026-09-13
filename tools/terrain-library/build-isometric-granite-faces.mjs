import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {writeJson,digest} from './library.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=process.cwd();
const base='addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/isometric_granite_faces_v1/';
const source=base+'sources/faces_source.png';
const bytes=fs.readFileSync(source);
const metadata=await sharp(bytes).metadata();
if(metadata.width!==1536||metadata.height!==1024) throw Error('Source geometry differs from calibration guide');
fs.mkdirSync(base+'runtime',{recursive:true});
const faces=[];
for(const [name,left] of [['light',32],['shade',864]]) {
  const crop={left,top:80,width:640,height:640};
  const {data}=await sharp(bytes).extract(crop).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  for(let i=0;i<data.length;i+=4) if(data[i+1]>200&&data[i]<65&&data[i+2]<75) data.fill(0,i,i+4);
  const master=await sharp(data,{raw:{width:640,height:640,channels:4}}).resize(256,256).png().toBuffer();
  await sharp(master).toFile(base+`sources/${name}_master_rgba.png`);
  await sharp(master).flatten({background:'#00ff00'}).toFile(base+`sources/${name}_master_green.png`);
  const runtime=await sharp(master).resize(128,128).raw().toBuffer();
  for(let i=3;i<runtime.length;i+=4) if(runtime[i]>=250) runtime[i]=255;
  // Authored faces must meet at opaque atlas boundaries, not blended chroma edges.
  for(let y=0;y<128;y++) for(let x=0;x<128;x++) {
    const top=name==='light'?(x+0.5)/2:64-(x+0.5)/2;
    if(y+0.5<top||y+0.5>top+64) continue;
    const alpha=(y*128+x)*4+3;
    if(runtime[alpha]<128) throw Error(`Missing painted face at ${name}:${x},${y}`);
    runtime[alpha]=255;
  }
  const png=await sharp(runtime,{raw:{width:128,height:128,channels:4}}).png().toBuffer();
  fs.writeFileSync(base+`runtime/${name}_span.png`,png);
  const regions=[];
  for(let i=0;i<4;i++) regions.push({index:i,x:i*32,y:name==='light'?i*16:(3-i)*16,width:32,height:80,godotResource:`runtime/${name}_module_${i}.tres`});
  faces.push({name,crop,runtime:`runtime/${name}_span.png`,runtimeSize:[128,128],regions,allowedSequence:[0,1,2,3],repeatEndValidated:false});
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'candidate',style:'cartoon',projection:'isometric',material:'grass_granite',source,sourceSha256:digest(bytes),risePixels:64,spanCells:4,faces,approvalEvidence:null,productionReady:false,pending:['Visual approval','Outer end seams','Corner contacts','Arbitrary length assembly','Cross-family joins']});
writeJson(root,base+'provenance.json',{source,sourceSha256:digest(bytes),generator:'built-in imagegen',generatedFile:'exec-08192996-c3b1-43af-833a-996ce5dc6aa9.png',guide:{canvas:[1536,1024],light:[[32,80],[672,400],[672,720],[32,400]],shade:[[864,400],[1504,80],[1504,400],[864,720]]},request:'Original long isometric granite faces, varied large fractured slabs, no repeated masonry or pillars, separate light/shade art, fixed guide silhouette, green background, no grass top.',conversion:'Uniform 640 to 256 master to 128 runtime; chroma removal; near-opaque alpha normalization; no square-art projection warp.'});
console.log('Exported two authored 4-cell isometric faces; outer end repeat remains unvalidated.');
