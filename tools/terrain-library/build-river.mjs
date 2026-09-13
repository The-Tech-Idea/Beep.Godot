import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {GENERATED,inside,writeJson,digest} from './library.mjs';
import {RIVER_PROFILES,riverFrame} from './river-motion.mjs';
const require=createRequire(import.meta.url);let sharp;
try{sharp=require('sharp')}catch{sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'))}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const base=`${GENERATED}/dev/cartoon/water/river_staging`,lake=`${GENERATED}/dev/cartoon/water/staging`;
fs.mkdirSync(inside(root,`${base}/runtime`),{recursive:true});
const textures={};
for(const [name,file] of [['grass','grass_64.png'],['water','shallow_water_64.png']])textures[name]=await sharp(inside(root,`${lake}/runtime/${file}`)).ensureAlpha().raw().toBuffer();
fs.copyFileSync(inside(root,`${lake}/runtime/grass_64.png`),inside(root,`${base}/runtime/grass_64.png`));
const inputs=[];
for(let f=0;f<16;f++)for(let i=0;i<RIVER_PROFILES.length;i++)inputs.push({input:riverFrame(textures.grass,textures.water,RIVER_PROFILES[i],f),raw:{width:64,height:64,channels:4},left:i%4*64,top:(Math.floor(i/4)+f*3)*64});
await sharp({create:{width:256,height:3072,channels:4,background:'#00ff00'}}).composite(inputs).png().toFile(inside(root,`${base}/runtime/river_current_16frames.png`));
const map=[];
for(let y=0;y<3;y++)map.push({x:2,y,profile:'north_south'});
map.push({x:2,y:3,profile:'north_east'});
for(let x=3;x<7;x++)map.push({x,y:3,profile:'west_east'});
map.push({x:7,y:3,profile:'west_south'});
for(let y=4;y<9;y++)map.push({x:7,y,profile:'north_south'});
const manifest={schemaVersion:1,id:'cartoon.river.current.v1',status:'candidate',approval:null,waterBody:'river',gameCell:64,frames:16,periodSeconds:1.2,columns:4,rowsPerFrame:3,
 source:{file:`${lake}/runtime/shallow_water_64.png`,sha256:digest(fs.readFileSync(inside(root,`${lake}/runtime/shallow_water_64.png`)))},
 file:'runtime/river_current_16frames.png',profiles:RIVER_PROFILES.map((p,i)=>({id:p.id,mask:p.mask,atlas:[i%4,Math.floor(i/4)]})),map,
 motion:'Stationary water substrate. Local crests advect along analytic straight/quarter-turn stream coordinates; tile endpoints share phase. Banks are fixed.',
 limitations:['Single-cell channel proof only. Wide rivers, junctions, wakes and waterfall connectors are not included.','This is not lake, sea or waterfall animation.','Visual approval pending.']};
writeJson(root,`${base}/manifest.json`,manifest);
const data={manifest,atlas:'data:image/png;base64,'+fs.readFileSync(inside(root,`${base}/${manifest.file}`)).toString('base64'),grass:'data:image/png;base64,'+fs.readFileSync(inside(root,`${base}/runtime/grass_64.png`)).toString('base64')};
fs.writeFileSync(inside(root,`${base}/preview-data.js`),'const DATA='+JSON.stringify(data)+';\n');
console.log('Built separate river candidate: 12 directional modules, 16 frames, two-bend example. Lake unchanged.');
