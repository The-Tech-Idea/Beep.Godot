import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {GENERATED,inside,writeJson,digest} from './library.mjs';
import {MASKS,maskAt,signedDistance} from './topology.mjs';
import {lakeFrame} from './water-motion.mjs';
const require=createRequire(import.meta.url);
let sharp;
try{sharp=require('sharp')}catch{if(!process.env.TERRAIN_NODE_MODULES)throw Error('Set TERRAIN_NODE_MODULES to a directory containing sharp, or install sharp in your tool runtime.');sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const base=`${GENERATED}/dev/cartoon/water/staging`,src=`${base}/sources/material_master_v1.png`;
const output=`${base}/runtime`;
fs.mkdirSync(inside(root,output),{recursive:true});
const bounds={grass:[220,42,440,440],dirt:[800,42,440,440],shallow_water:[220,540,440,440],deep_water:[800,540,440,440]};
const textures={};
for(const [name,[left,top,width,height]] of Object.entries(bounds)){
 const {data}=await sharp(inside(root,src)).extract({left,top,width,height}).resize(128,128).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 // Condition only texture margins, preserving a shared, exactly matching border.
 const avg=[0,0,0];for(let i=0;i<data.length;i+=4)for(let c=0;c<3;c++)avg[c]+=data[i+c]/(128*128);
 for(let y=0;y<128;y++)for(let x=0;x<128;x++){
  const weight=Math.min(1,Math.min(x,y,127-x,127-y)/12);
  for(let c=0;c<3;c++)data[(y*128+x)*4+c]=Math.round(avg[c]*(1-weight)+data[(y*128+x)*4+c]*weight);
 }
 await sharp(data,{raw:{width:128,height:128,channels:4}}).png().toFile(inside(root,`${output}/${name}_master128.png`));
 const small=await sharp(data,{raw:{width:128,height:128,channels:4}}).resize(64,64).raw().toBuffer();
 // Explicit border contract after resampling.
 for(let y=0;y<64;y++)for(let x=0;x<64;x++)if(x===0||y===0||x===63||y===63)for(let c=0;c<3;c++)small[(y*64+x)*4+c]=Math.round(avg[c]);
 textures[name]=small;
 await sharp(small,{raw:{width:64,height:64,channels:4}}).png().toFile(inside(root,`${output}/${name}_64.png`));
}
const mix=(a,b,w)=>Math.round(a+(b-a)*Math.max(0,Math.min(1,w)));
const waterFrames=Object.fromEntries(Object.entries(textures).filter(([name])=>name.includes('water')).map(([name,texture])=>[name,Array.from({length:16},(_,frame)=>lakeFrame(texture,frame))]));
const palettes={grass:[77,116,50],dirt:[172,125,68],shallow_water:[123,221,225]};
function tile(bg,fg,mask,frame){
 const out=Buffer.alloc(64*64*4),water=fg.includes('water'),depth=bg.includes('water');
 for(let y=0;y<64;y++)for(let x=0;x<64;x++){
  const i=(y*64+x)*4,s=signedDistance(mask,x,y),coverage=Math.max(0,Math.min(1,depth?(s+8)/16:s+0.5));
  for(let c=0;c<3;c++){
   const front=(water?waterFrames[fg][frame%16]:textures[fg])[i+c],back=(depth?waterFrames[bg][frame%16]:textures[bg])[i+c];
   let color=mix(back,front,coverage);
   if(water&&bg!=='shallow_water'&&s>-3.5&&s<0)color=mix(color,[223,184,114][c],0.9);
   if(!depth&&s>-0.8&&s<0.7)color=mix(color,(water?[174,231,221]:palettes[bg])[c],0.55);
   out[i+c]=color;
  }out[i+3]=255;
 }return out;
}
const transitions=[['grass','dirt'],['grass','shallow_water'],['dirt','shallow_water'],['shallow_water','deep_water']];
const entries=[];
for(const [bg,fg] of transitions){
 const id=`${bg}_to_${fg}`,count=fg.includes('water')?16:1,tiles=[];
 for(let frame=0;frame<count;frame++)for(let k=0;k<48;k++)tiles.push({input:k===47?(bg.includes('water')?tile(bg,bg,255,frame):textures[bg]):tile(bg,fg,MASKS[k],frame),raw:{width:64,height:64,channels:4},left:k%8*64,top:(frame*6+Math.floor(k/8))*64});
 await sharp({create:{width:512,height:384*count,channels:4,background:'#00ff00'}}).composite(tiles).png().toFile(inside(root,`${output}/${id}.png`));
 entries.push({id,background:bg,foreground:fg,waterBody:count>1?'lake':null,file:`runtime/${id}.png`,resource:`godot/${id}.tres`,tileSize:64,columns:8,rowsPerFrame:6,frames:count,periodSeconds:count===1?null:1.2,masks:MASKS,backgroundIndex:47,
  regions:MASKS.map((mask,k)=>({id:`cartoon.${id}.mask_${mask}`,mask,atlas:[k%8,Math.floor(k/8)],size:[64,64],pivot:[32,32]}))});
}
const map=Array.from({length:12},()=>Array(18).fill(0));
for(let y=0;y<12;y++)for(let x=0;x<18;x++)map[y][x]=Number(((x-8.5)/7)**2+((y-5.5)/4.7)**2<1);
for(const [x,y] of [[8,5],[9,5],[8,6],[9,6],[3,3],[3,4]])map[y][x]=0;
const comp=[];
for(let y=0;y<12;y++)for(let x=0;x<18;x++)comp.push({input:map[y][x]?tile('grass','shallow_water',maskAt(map,x,y),0):textures.grass,raw:{width:64,height:64,channels:4},left:x*64,top:y*64});
await sharp({create:{width:1152,height:768,channels:4,background:'#00ff00'}}).composite(comp).png().toFile(inside(root,`${output}/connection_map.png`));
writeJson(root,`${base}/manifest.json`,{schemaVersion:1,id:'cartoon.foundation.v1',status:'candidate',approval:null,gameCell:64,source:{file:'sources/material_master_v1.png',sha256:digest(fs.readFileSync(inside(root,src))),regions:bounds},assets:entries,
 animation:'Lake water only: stationary low-contrast substrate with six staggered local ripple highlights per cell. Highlights drift 2.5 pixels, form and fade over a 16-frame loop. No scrolling texture or whole-surface brightness wave. Not a river current, waterfall or sea animation.',
 limitations:['Needs visual approval.','Only four foundation transitions; full library is not complete.','No cliff or waterfall inlet/outlet connectors yet.','Margin-conditioned textures need repeated-pattern review.'],map});
console.log(JSON.stringify({status:'candidate',transitions:entries.length,configurationsPerTransition:MASKS.length,animatedTransitions:3,output:base}));
