import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson,digest} from './library.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const base='addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_contact_v1/';
const source=base+'sources/contact_original_rgba.png';
const bytes=fs.readFileSync(path.join(root,source));
const {data,info}=await sharp(bytes).ensureAlpha().raw().toBuffer({resolveWithObject:true});
let left=info.width,top=info.height,right=-1,bottom=-1;
for(let y=0;y<info.height;y++) for(let x=0;x<info.width;x++) if(data[(y*info.width+x)*4+3]>8) {
  left=Math.min(left,x);top=Math.min(top,y);right=Math.max(right,x);bottom=Math.max(bottom,y);
}
if(right<left) throw Error('Contact source is empty');
left=Math.max(0,left-3);top=Math.max(0,top-3);
right=Math.min(info.width-1,right+3);bottom=Math.min(info.height-1,bottom+3);
const crop={left,top,width:right-left+1,height:bottom-top+1};
const {data:scaled,info:size}=await sharp(bytes).extract(crop).resize({width:640,kernel:'lanczos3'}).png().toBuffer({resolveWithObject:true});
if(size.height>80) throw Error('Contact exceeds its declared footprint; do not stretch it');
const pad=Math.floor((80-size.height)/2);
const master=await sharp(scaled).extend({top:pad,bottom:80-size.height-pad,left:0,right:0,background:'#00000000'}).png().toBuffer();
fs.mkdirSync(path.join(root,base,'runtime'),{recursive:true});
await sharp(master).png().toFile(path.join(root,base,'sources/contact_master_rgba_640.png'));
await sharp(master).flatten({background:'#00ff00'}).png().toFile(path.join(root,base,'sources/contact_master_green_640.png'));
await sharp(master).resize(320,40,{kernel:'lanczos3'}).png().toFile(path.join(root,base,'runtime/contact_320.png'));
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'candidate',style:'cartoon',projection:'square',material:'grass_granite',
  source,sourceSha256:digest(bytes),crop,cropAlphaThreshold:8,cropPaddingPixels:3,masterSize:[640,80],runtimeSize:[320,40],uniformScale:640/crop.width,
  nominalContactY:20,visualRiseContribution:0,role:'separate_bottom_contact_overlay',
  greenSource:'sources/contact_master_green_640.png',alphaMaster:'sources/contact_master_rgba_640.png',runtime:'runtime/contact_320.png',
  provenance:'provenance.json',approvalEvidence:null,
  regions:Array.from({length:5},(_,i)=>({id:`contact_${i}`,region:[i*64,0,64,40]})),
  adjacency:'Original consecutive source regions only; arbitrary and outer joins remain unvalidated.',
  pending:['Visual approval','Outer repeat seam','Side/corner contacts','Isometric counterpart'],productionReady:false});
console.log(JSON.stringify({crop,scaledSize:[size.width,size.height],runtime:[320,40]}));
