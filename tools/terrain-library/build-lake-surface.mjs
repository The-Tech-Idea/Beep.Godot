import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {lakeFrame} from './water-motion.mjs';
import {writeJson, digest} from './library.mjs';
const require = createRequire(import.meta.url);
let sharp;
try { sharp = require('sharp'); } catch { sharp = require(path.join(process.env.TERRAIN_NODE_MODULES, 'sharp')); }
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const source = 'addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/runtime/shallow_water_64.png';
const output = 'addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/';
const bytes = fs.readFileSync(path.join(root, source));
const {data, info} = await sharp(bytes).ensureAlpha().raw().toBuffer({resolveWithObject:true});
if (info.width !== 64 || info.height !== 64) throw Error('Lake source must retain its authored 64px cell');
const frames = Array.from({length:16}, (_, frame) => lakeFrame(data, frame));
if (!frames[0].equals(lakeFrame(data,16))) throw Error('Lake loop does not close');
fs.mkdirSync(path.join(root, output), {recursive:true});
await sharp(Buffer.concat(frames), {raw:{width:64,height:1024,channels:4}}).png().toFile(path.join(root, output, 'lake_surface_frames.png'));
writeJson(root, output + 'manifest.json', {schemaVersion:1,status:'candidate',role:'lake',source,sourceSha256:digest(bytes),
  frames:16,periodSeconds:1.2,motion:'Stationary base with staggered local ripples; no bulk current or surf.',
  surface:'lake_surface_frames.png',bankMaterial:'grass',projections:['square','isometric'],
  requirements:['47 binary bank configurations per projection','Native animated TileSets','Static grass and bank pixels'],
  godotScenes:['square/lake_review.tscn','isometric/lake_review.tscn'],
  godotTileSets:['square/grass_lake.tres','isometric/grass_lake.tres'],
  approvalEvidence:null,pending:['Visual animation approval','River inlets/outlets','Sand and rock banks','Depth transitions','Nested islands and mixed-width showcase']});
console.log('Lake motion exported: 16 frames, closed loop, existing motion implementation retained');
