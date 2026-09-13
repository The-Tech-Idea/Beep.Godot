import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {writeJson, digest} from './library.mjs';
const require = createRequire(import.meta.url);
let sharp;
try { sharp = require('sharp'); } catch { sharp = require(path.join(process.env.TERRAIN_NODE_MODULES, 'sharp')); }
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const base = 'addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/sources/';
const results = [];
for (const material of ['grass', 'dirt']) {
  const source = base + `plain_${material}_surface_v1.png`;
  const bytes = fs.readFileSync(path.join(root, source));
  const {data, info} = await sharp(bytes).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const {width:w, height:h} = info;
  const difference = (a,b) => (Math.abs(data[a]-data[b])+Math.abs(data[a+1]-data[b+1])+Math.abs(data[a+2]-data[b+2])) / 3;
  let horizontal = 0, vertical = 0, interior = 0, samples = 0, transparent = 0;
  for (let y = 0; y < h; y++) horizontal += difference(y*w*4, (y*w+w-1)*4) / h;
  for (let x = 0; x < w; x++) vertical += difference(x*4, ((h-1)*w+x)*4) / w;
  for (let y = 1; y < h; y++) for (let x = 1; x < w; x++) {
    const i = (y*w+x)*4;
    interior += difference(i,i-4) + difference(i,i-w*4); samples += 2;
  }
  for (let i = 3; i < data.length; i += 4) if (data[i] !== 255) transparent++;
  const baseline = interior / samples;
  const repeatReviewRequired = Math.max(horizontal, vertical) > Math.max(2, baseline * 3);
  results.push({material, source, sha256:digest(bytes), dimensions:[w,h], transparentPixels:transparent,
    horizontalEdgeMeanDifference:horizontal, verticalEdgeMeanDifference:vertical, interiorMeanDifference:baseline,
    repeatReviewRequired, status:repeatReviewRequired ? 'repeat_boundary_review_required' : 'candidate_visual_review_required'});
}
writeJson(root, 'addons/beep_game_builder_cs/generated/test/library/output/plain_surface_seams.json', {
  results, visualApproval:false, limitations:['Edge statistics flag discontinuities; they do not prove visual seamlessness or production readiness.']
});
console.log(JSON.stringify(results, null, 2));
