import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {MASKS} from './topology.mjs';
import {coverageReport} from './coverage.mjs';
import {writeJson} from './library.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const read = file => JSON.parse(fs.readFileSync(path.join(root,file),'utf8'));
const spec = read('tools/terrain-library/specification.json');
const ledger = read(spec.coverage.bindingsFile);
const ground = 'addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/';
const lake = 'addons/beep_game_builder_cs/generated/dev/cartoon/water/lake_banks_v1/';
let added = 0, updated = 0;
for (const projection of ['square','isometric']) for (const family of ['ground','lake']) {
  const height = projection === 'square' ? 64 : 32;
  const folder = family === 'ground' ? `${ground}${projection}/surface_candidate_v1/` : `${lake}${projection}/`;
  for (const [index,mask] of MASKS.entries()) {
    const requirementId = `cartoon.${projection}.${family === 'ground' ? 'ground_boundaries.grass_to_dirt' : 'water_boundaries.grass_to_shallow_water'}.binary_mask.mask_${mask}.0.static_mask`;
    const binding = {requirementId,status:'candidate',
      source:family === 'ground' ? ground+'sources/plain_grass_surface_v1.png' : lake+'lake_surface_frames.png',
      runtimeFile:folder+(family === 'ground' ? 'grass_dirt_masks.png' : 'grass_lake_16.png'),
      runtimeRegion:{x:index%8*64,y:Math.floor(index/8)*height,width:64,height},
      godotResource:folder+(family === 'ground' ? 'grass_dirt_masks.tres' : 'grass_lake.tres'),
      provenance:family === 'ground' ? ground+'surface_candidates_v1.json' : lake+'manifest.json',
      validation:{status:'not_run',report:null},approvalEvidence:null,
      conversionRequired:family === 'ground' ? ['Composed-map acceptance and appearance approval remain open.'] : ['Complete rendered bank coverage and animation approval remain open.'],
      coverageScope:'Binary boundary region only; does not satisfy flow, depth, elevation or structural connector requirements.'};
    const existing = ledger.bindings.findIndex(entry=>entry.requirementId===requirementId);
    if (existing < 0) {ledger.bindings.push(binding);added++;continue;}
    const previous = ledger.bindings[existing];
    if (previous.runtimeFile === binding.runtimeFile || previous.status !== 'candidate') continue;
    const {alternatives = [], ...retained} = previous;
    binding.alternatives = [...alternatives, retained];
    ledger.bindings[existing] = binding;
    updated++;
  }
}
const report = coverageReport(root,spec,ledger);
if (report.records.some(record=>record.issues.length)) throw Error('Reconciliation has file/region issues; ledger not written');
writeJson(root,spec.coverage.bindingsFile,ledger);
writeJson(root,spec.coverage.reportFile,report);
console.log(JSON.stringify({added,updated,...report.summary}));
