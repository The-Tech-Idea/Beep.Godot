import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson, digest} from './library.mjs';
import {SECTION_KINDS, FLOW_DIRECTIONS, sectionPixel, sectionSequence} from './river-sections.mjs';
const require = createRequire(import.meta.url);
let sharp;
try { sharp = require('sharp'); } catch { sharp = require(path.join(process.env.TERRAIN_NODE_MODULES, 'sharp')); }
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const source = 'addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const bytes = fs.readFileSync(path.join(root,source));
const pixels = await sharp(bytes).ensureAlpha().raw().toBuffer();
const mean = [0,0,0];
for(let i=0;i<pixels.length;i+=4) for(let c=0;c<3;c++) mean[c] += pixels[i+c]/(pixels.length/4);
const profiles = FLOW_DIRECTIONS.flatMap(direction=>SECTION_KINDS.map(kind=>({id:`${direction}.${kind}`,direction,kind})));
const base = 'addons/beep_game_builder_cs/generated/dev/cartoon/water/river_sections_v1/';
for(const projection of ['square','isometric']) {
  const height = projection === 'square' ? 64 : 32;
  const width = 256, atlasHeight = height*4*16;
  const data = Buffer.alloc(width*atlasHeight*4);
  for(let frame=0;frame<16;frame++) for(let index=0;index<profiles.length;index++) {
    const profile=profiles[index];
    for(let y=0;y<height;y++) for(let x=0;x<64;x++) {
      let px=x+0.5, py=y+0.5;
      if(projection==='isometric') {
        const dx=(px-32)/64,dy=(py-16)/32;
        px=(dx+dy+0.5)*64;py=(-dx+dy+0.5)*64;
        if(px<0||py<0||px>=64||py>=64) continue;
      }
      const rgba=sectionPixel(profile.kind,profile.direction,px,py,frame,mean);
      const offset=(((frame*4+Math.floor(index/4))*height+y)*width+(index%4)*64+x)*4;
      for(let c=0;c<4;c++) data[offset+c]=rgba[c];
    }
  }
  fs.mkdirSync(path.join(root,base,projection),{recursive:true});
  await sharp(data,{raw:{width,height:atlasHeight,channels:4}}).png().toFile(path.join(root,base,projection,'river_sections_16.png'));
}
writeJson(root,base+'manifest.json',{schemaVersion:1,status:'candidate',source,sourceSha256:digest(bytes),frames:16,periodSeconds:1.2,
  profiles:profiles.map((p,i)=>({...p,atlas:[i%4,Math.floor(i/4)]})),
  widthExamples:[1,2,3,5].map(width=>({width,sections:sectionSequence(width)})),
  banks:'low/high refer to the cross-channel coordinate, not upstream/downstream',
  godotScenes:['square/river_widths_review.tscn','isometric/river_widths_review.tscn'],
  godotTileSets:['square/river_sections.tres','isometric/river_sections.tres'],
  approvalEvidence:null,pending:['Wide bends','Narrow/wide adapters','Junctions','Waterfall/lake connectors','Visual animation review']});
console.log('Built 16 repeatable directed sections per projection; no fixed wide-river shapes.');
