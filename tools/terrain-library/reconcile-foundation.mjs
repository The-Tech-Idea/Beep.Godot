import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {coverageReport} from './coverage.mjs';
import {writeJson} from './library.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const read = file => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));
const base = 'addons/beep_game_builder_cs/generated/dev/cartoon/water/staging/';
const spec = read('tools/terrain-library/specification.json');
const ledger = read(spec.coverage.bindingsFile);
const manifest = read(base + 'manifest.json');
const existing = new Set(ledger.bindings.map(binding => binding.requirementId));
const families = {grass_to_dirt:'ground_boundaries', grass_to_shallow_water:'water_boundaries', shallow_water_to_deep_water:'water_boundaries'};
let added = 0;
for (const asset of manifest.assets) {
  const family = families[asset.id];
  if (!family) continue;
  for (const region of asset.regions) {
    const requirementId = `cartoon.square.${family}.${asset.id}.binary_mask.mask_${region.mask}.0.static_mask`;
    if (existing.has(requirementId)) continue;
    ledger.bindings.push({requirementId, status:'candidate',
      source:base + 'sources/material_master_v1.png', runtimeFile:base + asset.file,
      runtimeRegion:{x:region.atlas[0] * asset.tileSize, y:region.atlas[1] * asset.tileSize, width:region.size[0], height:region.size[1]},
      godotResource:base + asset.resource, validation:{status:'not_run', report:null}, approvalEvidence:null,
      conversionRequired:['Separate baked surfaces from connection masks.', 'Review joins and shared-surface alignment in Godot.', 'Water motion requires separate role-specific visual approval.'],
      provenance:base + 'manifest.json'});
    existing.add(requirementId);
    added++;
  }
}
// Validate the proposed ledger before changing its maintained file.
const report = coverageReport(root, spec, ledger);
if (report.records.some(record => record.issues.length)) throw Error('Candidate reconciliation has unresolved file or region issues');
writeJson(root, spec.coverage.bindingsFile, ledger);
writeJson(root, spec.coverage.reportFile, report);
console.log(JSON.stringify({added, ...report.summary}));
