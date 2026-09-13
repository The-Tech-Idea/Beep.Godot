import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createRequire} from 'node:module';
import {GENERATED,inside,writeJson} from './library.mjs';
import {MASKS,normalize,maskAt,signedDistance} from './topology.mjs';
const require=createRequire(import.meta.url);let sharp;
try{sharp=require('sharp')}catch{sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'))}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const base=`${GENERATED}/dev/cartoon/water/staging`,result=[];
if(MASKS.length!==47)throw Error('Expected 47 masks');
for(let m=0;m<256;m++)if(normalize(normalize(m))!==normalize(m))throw Error('Normalization not idempotent');
// Enumerate all 3x4 neighbourhoods containing two adjacent foreground cells.
let connections=0;
for(let bits=0;bits<4096;bits++){
 const map=Array.from({length:3},(_,y)=>Array.from({length:4},(_,x)=>Number(!!(bits&(1<<(y*4+x))))));
 if(!map[1][1]||!map[1][2])continue;
 const a=maskAt(map,1,1),b=maskAt(map,2,1);
 for(let y=0;y<64;y++)if(Math.abs(Math.min(1,signedDistance(a,63,y))-Math.min(1,signedDistance(b,0,y)))>1e-6)throw Error(`Horizontal boundary mismatch ${a}/${b}`);
 connections++;
}
let verticalConnections=0;
for(let bits=0;bits<4096;bits++){
 const map=Array.from({length:4},(_,y)=>Array.from({length:3},(_,x)=>Number(!!(bits&(1<<(y*3+x))))));
 if(!map[1][1]||!map[2][1])continue;
 const a=maskAt(map,1,1),b=maskAt(map,1,2);
 for(let x=0;x<64;x++)if(Math.abs(Math.min(1,signedDistance(a,x,63))-Math.min(1,signedDistance(b,x,0)))>1e-6)throw Error(`Vertical boundary mismatch ${a}/${b}`);
 verticalConnections++;
}
const manifest=JSON.parse(fs.readFileSync(inside(root,`${base}/manifest.json`),'utf8'));
for(const asset of manifest.assets){
 const {data,info}=await sharp(inside(root,`${base}/${asset.file}`)).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 if(info.width!==512||info.height!==384*asset.frames)throw Error('Bad dimensions');
 let green=0,transparent=0,animatedChanges=0,bankChanges=0;
 const stride=512*384*4;
 for(let i=0;i<data.length;i+=4){if(data[i]<20&&data[i+1]>235&&data[i+2]<20)green++;if(data[i+3]!==255)transparent++;}
 if(green||transparent)throw Error(`Backing color or holes in ${asset.id}`);
 if(asset.frames>1){
  for(let k=0;k<47;k++)for(let y=0;y<64;y++)for(let x=0;x<64;x++){
   const i=((Math.floor(k/8)*64+y)*512+k%8*64+x)*4;
   const changed=[0,1,2].some(c=>data[i+c]!==data[i+stride*5+c]);
   if(changed)animatedChanges++;
   if(changed&&!asset.background.includes('water')&&signedDistance(MASKS[k],x,y)<-1)bankChanges++;
  }
  if(!animatedChanges||bankChanges)throw Error('Water motion/static-bank check failed');
 }
 result.push({id:asset.id,width:info.width,height:info.height,green,transparent,animatedChanges,bankChanges});
}
writeJson(root,`${GENERATED}/test/cartoon/water/output/foundation_validation.json`,{status:'passed',legalMasks:47,horizontalNeighbourhoods:connections,verticalNeighbourhoods:verticalConnections,atlases:result,visualApproval:false});
console.log(JSON.stringify({legalMasks:47,connections,verticalConnections,atlases:result}));
