import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson,digest} from './library.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const base='addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_iso_corner_v1/';
const source=base+'sources/corner_source.png';
const bytes=fs.readFileSync(path.join(root,source));
const crop={left:260,top:182,width:652,height:978};
if(crop.width/128!==crop.height/192) throw Error('Corner calibration must use uniform scale');
const {data,info}=await sharp(bytes).extract(crop).ensureAlpha().raw().toBuffer({resolveWithObject:true});
for(let i=0;i<data.length;i+=4) if(data[i+1]>200&&data[i]<60&&data[i+2]<70) {
  data[i]=0;data[i+1]=0;data[i+2]=0;data[i+3]=0;
}
const master=await sharp(data,{raw:{width:info.width,height:info.height,channels:4}}).resize(128,192,{kernel:'lanczos3'}).png().toBuffer();
fs.mkdirSync(path.join(root,base,'runtime'),{recursive:true});
await sharp(master).toFile(path.join(root,base,'sources/corner_master_rgba_128.png'));
await sharp(master).flatten({background:'#00ff00'}).png().toFile(path.join(root,base,'sources/corner_master_green_128.png'));
const sampled=await sharp(master).resize(64,96,{kernel:'lanczos3'}).raw().toBuffer();
// Normalize near-opaque resampling rings without changing the transparent outline.
for(let i=3;i<sampled.length;i+=4) if(sampled[i]>=250) sampled[i]=255;
const runtime=await sharp(sampled,{raw:{width:64,height:96,channels:4}}).png().toBuffer();
await sharp(runtime).toFile(path.join(root,base,'runtime/corner_64.png'));
const rgba=await sharp(runtime).raw().toBuffer(),mask=Buffer.alloc(64*96*3);
for(let y=0;y<34;y++) for(let x=0;x<64;x++) {
  const i=(y*64+x)*4;
  if(rgba[i+1]>rgba[i]+5&&rgba[i+1]>rgba[i+2]*1.2) mask.fill(255,(y*64+x)*3,(y*64+x+1)*3);
}
await sharp(mask,{raw:{width:64,height:96,channels:3}}).png().toFile(path.join(root,base,'runtime/plateau_mask.png'));
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'candidate',style:'cartoon',projection:'isometric',material:'grass_granite',
  source,sourceSha256:digest(bytes),crop,masterSize:[128,192],runtimeSize:[64,96],footprint:[64,32],risePixels:64,
  topVertices:[[32,0],[64,16],[32,32],[0,16]],bottomFrontVertices:[[0,80],[32,96],[64,80]],
  groundAnchor:[32,80],spriteOffset:[-32,-80],tileTextureOrigin:[0,32],
  sourceGeometry:'Painted against an exact diamond/rise guide; uniform calibration, not skewed square art.',
  runtime:'runtime/corner_64.png',surfaceMask:'runtime/plateau_mask.png',godotResource:'runtime/corner.tres',
  approvalEvidence:null,pending:['Visual approval','Plateau connection review','Concave corners','Repeatable wall connections','Bottom contact overlays'],productionReady:false});
console.log('Exported guided isometric corner: 64x32 footprint, nominal 64px rise, 64x96 sprite.');
