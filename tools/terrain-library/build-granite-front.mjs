import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson,digest} from './library.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const base='addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_front_v1/';
const source=base+'sources/front_wall_source.png';
const bytes=fs.readFileSync(path.join(root,source));
const crop={left:55,top:234,width:1664,height:416};
const {data,info}=await sharp(bytes).extract(crop).ensureAlpha().raw().toBuffer({resolveWithObject:true});
// Runtime chroma removal is confined to the saturated source backing, before resize.
for(let i=0;i<data.length;i+=4) if(data[i+1]>200&&data[i]<60&&data[i+2]<70) {
  data[i]=0;data[i+1]=0;data[i+2]=0;data[i+3]=0;
}
fs.mkdirSync(path.join(root,base,'runtime'),{recursive:true});
const master=await sharp(data,{raw:{width:info.width,height:info.height,channels:4}}).resize(640,160,{kernel:'lanczos3'}).png().toBuffer();
await sharp(master).toFile(path.join(root,base,'sources/front_wall_calibrated_640.png'));
await sharp(master).resize(320,80,{kernel:'lanczos3'}).png().toFile(path.join(root,base,'runtime/front_wall_320.png'));
const runtime=await sharp(path.join(root,base,'runtime/front_wall_320.png')).ensureAlpha().raw().toBuffer();
const mask=Buffer.alloc(320*80*3);
for(let y=0;y<24;y++) for(let x=0;x<320;x++) {
  const i=(y*320+x)*4;
  const green=runtime[i+1]>runtime[i]+5 && runtime[i+1]>runtime[i+2]*1.2;
  // The top two rows are the artificial source cut, not the authored soil rim.
  if(y<2 || green) mask.fill(255,(y*320+x)*3,(y*320+x+1)*3);
}
await sharp(mask,{raw:{width:320,height:80,channels:3}}).png().toFile(path.join(root,base,'runtime/grass_surface_mask.png'));
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'candidate',style:'cartoon',projection:'square',material:'grass_granite',
  source,sourceSha256:digest(bytes),crop,masterSize:[640,160],runtimeSize:[320,80],risePixels:64,nominalRimY:16,bottomY:80,
  calibration:'Nominal rise is measured from the authored irregular rim around y=16 to the contact baseline y=80; plateau footprint is separate.',
  regions:Array.from({length:5},(_,i)=>({id:`front_${i}`,region:[i*64,0,64,80]})),
  validAdjacency:'Only original consecutive regions 0->1->2->3->4 are source-continuous. End-to-start and arbitrary alternatives are unvalidated.',
  runtime:'runtime/front_wall_320.png',godotResource:'runtime/front_wall.tres',approvalEvidence:null,
  surfaceMask:'runtime/grass_surface_mask.png',surfaceShader:'addons/beep_game_builder_cs/shaders/terrain_masked_art_surface.gdshader',
  surfaceMaskRule:'Top artificial cut plus green plateau pixels above row 24; painted rock and soil remain unmasked.',
  reviewScene:'river_waterfall_review.tscn',
  bottomContact:'../grass_granite_contact_v1/manifest.json',
  pending:['Visual approval including bottom contact','Outer repeat seam','Corners and outward side faces','Height extensions','Isometric counterpart'],
  productionReady:false});
console.log('Calibrated five source-contiguous cliff regions; outer repeat seam remains unvalidated.');
