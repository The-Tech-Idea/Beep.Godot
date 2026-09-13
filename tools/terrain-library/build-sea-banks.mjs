import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {writeJson,digest} from './library.mjs';
import {MASKS} from './topology.mjs';
import {seaPixel} from './sea-motion.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=process.cwd();
const source='addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes=fs.readFileSync(source),pixels=await sharp(bytes).ensureAlpha().raw().toBuffer(),mean=[0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c]+=pixels[i+c]/(pixels.length/4);
const base='addons/beep_game_builder_cs/generated/dev/cartoon/water/sea_banks_v1/';
for(const projection of ['square','isometric']) {
  const height=projection==='square'?64:32;
  const width=512,atlasHeight=height*6*16;
  const data=Buffer.alloc(width*atlasHeight*4);
  for(let frame=0;frame<16;frame++) for(let index=0;index<48;index++) for(let y=0;y<height;y++) for(let x=0;x<64;x++) {
    let px=x+.5,py=y+.5;
    if(projection==='isometric') {
      const dx=(px-32)/64,dy=(py-16)/32;
      px=(dx+dy+.5)*64;py=(-dx+dy+.5)*64;
      if(px<0||py<0||px>=64||py>=64) continue;
    }
    const rgba=index===47?[0,0,255,255]:seaPixel(MASKS[index],px,py,frame,mean);
    const offset=(((frame*6+Math.floor(index/8))*height+y)*width+(index%8)*64+x)*4;
    for(let c=0;c<4;c++) data[offset+c]=rgba[c];
  }
  fs.mkdirSync(base+projection,{recursive:true});
  await sharp(data,{raw:{width,height:atlasHeight,channels:4}}).png().toFile(base+projection+'/grass_sea_16.png');
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'technical_candidate',style:'cartoon',projections:['square','isometric'],source,sourceSha256:digest(bytes),editableMaster:'tools/terrain-library/sea-motion.mjs',frames:16,periodSeconds:1.2,masks:MASKS,bankMaterial:'grass',motionRoles:['curved_swell_crests','shoreward_breaking_surf'],terrainFixed:true,approvalEvidence:null,productionReady:false,pending:['Visual surf approval','Large-area crest repetition review','River estuaries','Separate depth boundaries','Sand and rock coasts','Cross-pack joins','Production promotion']});
console.log('Built separate sea swell/surf atlases: 47 grass coast masks per projection.');
