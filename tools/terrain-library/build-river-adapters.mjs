import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {transitionPixel} from './river-width-transitions.mjs';
import {FLOW_DIRECTIONS} from './river-sections.mjs';
import {writeJson,digest} from './library.mjs';
const require=createRequire(import.meta.url);
let sharp;
try{sharp=require('sharp')}catch{sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'))}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const source='addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(path.join(root,source));
const pixels=await sharp(bytes).ensureAlpha().raw().toBuffer(),mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4)for(let c=0;c<3;c++)mean[c]+=pixels[i+c]/(pixels.length/4);
const shapes=[{kind:'narrow_expand',from:1,to:2,offset:0},{kind:'narrow_contract',from:2,to:1,offset:0},
  {kind:'edge_expand',from:2,to:3,offset:64},{kind:'edge_contract',from:3,to:2,offset:64}];
const profiles=FLOW_DIRECTIONS.flatMap(direction=>shapes.map(shape=>({...shape,direction,id:`${direction}.${shape.kind}`})))
  .map((profile,index)=>({...profile,patternIndex:index,cells:[0,1,2,3].map(cell=>({local:[cell%2,Math.floor(cell/2)],atlas:[index%4*2+cell%2,Math.floor(index/4)*2+Math.floor(cell/2)]}))}));
const base='addons/beep_game_builder_cs/generated/dev/cartoon/water/river_adapters_v1/';
for(const projection of ['square','isometric']){
  const height=projection==='square'?64:32, width=512,atlasHeight=height*8*16;
  const data=Buffer.alloc(width*atlasHeight*4);
  for(let frame=0;frame<16;frame++)for(const profile of profiles)for(const cell of profile.cells)
    for(let y=0;y<height;y++)for(let x=0;x<64;x++){
      let px=x+.5,py=y+.5;
      if(projection==='isometric'){
        const dx=(px-32)/64,dy=(py-16)/32;px=(dx+dy+.5)*64;py=(-dx+dy+.5)*64;
        if(px<0||py<0||px>=64||py>=64)continue;
      }
      px+=cell.local[0]*64;py+=cell.local[1]*64;
      const vertical=profile.direction==='north_south'||profile.direction==='south_north';
      const across=(vertical?px:py)+profile.offset;
      let along=vertical?py:px;
      if(profile.direction==='south_north'||profile.direction==='east_west')along=128-along;
      const color=transitionPixel(profile.from,profile.to,across,along,frame,mean);
      const offset=(((frame*8+cell.atlas[1])*height+y)*width+cell.atlas[0]*64+x)*4;
      for(let c=0;c<4;c++)data[offset+c]=color[c];
    }
  fs.mkdirSync(path.join(root,base,projection),{recursive:true});
  await sharp(data,{raw:{width,height:atlasHeight,channels:4}}).png().toFile(path.join(root,base,projection,'river_adapters_16.png'));
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'candidate',source,sourceSha256:digest(bytes),frames:16,periodSeconds:1.2,profiles,
  composition:'edge variants cover only the changing outer bank; combine with low_bank and repeatable middle sections for wider rivers.',
  godotScenes:['square/river_adapter_review.tscn','isometric/river_adapter_review.tscn'],
  directionalScenes:['square','isometric'].flatMap(projection=>FLOW_DIRECTIONS.map(direction=>({projection,direction,path:`${projection}/river_adapter_${direction==='north_south'?'review':direction}.tscn`}))),
  godotTileSets:['square/river_adapters.tres','isometric/river_adapters.tres'],
  approvalEvidence:null,pending:['Rendered review of every directional pattern','Wide bends and junctions','Cross-role connectors','Visual approval']});
console.log('Built 16 directed adapter patterns, four cells each, in both projections.');
