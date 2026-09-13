import test from 'node:test';
import assert from 'node:assert/strict';
import {mergeCandidates,waterElevationCandidates} from '../tools/terrain-library/reconcile-water-elevation.mjs';
import {requirements} from '../tools/terrain-library/coverage.mjs';
import fs from 'node:fs';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
test('candidate reconciliation is idempotent and preserves existing decisions',()=>{
  const previous={bindings:[{requirementId:'keep',status:'approved',approvalEvidence:{scope:'production'}}],evidence:[]};
  const incoming=[{requirementId:'keep',status:'candidate',approvalEvidence:null},{requirementId:'new',status:'candidate',approvalEvidence:null}];
  const a=mergeCandidates(previous,incoming,[{path:'evidence'}]);
  const b=mergeCandidates(a.ledger,incoming,[{path:'evidence'}]);
  assert.equal(a.added,1);assert.equal(b.added,0);assert.deepEqual(a.ledger,b.ledger);
  assert.equal(previous.bindings.length,1);assert.equal(a.ledger.bindings[0].status,'approved');
  assert.throws(()=>mergeCandidates(previous,[{requirementId:'bad',status:'approved',approvalEvidence:null}]));
  assert.throws(()=>mergeCandidates(previous,[incoming[1],incoming[1]]));
});
test('current bindings reference real logical cases without overclaiming styles or structures',()=>{
  const spec=JSON.parse(fs.readFileSync(new URL('../tools/terrain-library/specification.json',import.meta.url),'utf8'));
  const known=new Set(requirements(spec).map(r=>r.id));
  const {bindings,evidence}=waterElevationCandidates(root);
  assert.equal(bindings.length,245);
  for(const b of bindings) {
    assert.ok(known.has(b.requirementId),b.requirementId);
    assert.equal(b.status,'candidate');assert.equal(b.approvalEvidence,null);
    assert.ok(!b.requirementId.startsWith('pixel.'));
    assert.ok(!b.requirementId.includes('.waterfall.'));
  }
  assert.equal(bindings.filter(b=>b.requirementId.includes('.river.grass.crossing.')).length,16);
  assert.equal(bindings.filter(b=>b.requirementId.includes('.cliffs.')).length,3);
  const estuaries=bindings.filter(b=>b.requirementId.includes('.sea.grass.estuary.'));
  assert.equal(estuaries.length,8);
  for(const binding of estuaries) {
    assert.ok(binding.godotResource.endsWith('.tscn'));
    assert.match(binding.coverageScope,/requires the supplied control shader/);
  }
  const depths=bindings.filter(b=>b.requirementId.includes('.shallow_water_to_deep_water.'));
  assert.equal(depths.length,94);
  for(const binding of depths) {
    assert.ok(binding.godotResource.endsWith('/depth_review.tscn'));
    assert.match(binding.coverageScope,/Open-sea cells only/);
  }
  assert.equal(evidence.length,6);
  assert.equal(evidence.find(e=>e.path.endsWith('/shared_sea_v1/manifest.json')).requirementIds.length,94);
  assert.equal(evidence.find(e=>e.path.endsWith('/lake_depth_v1/manifest.json')).requirementIds.length,94);
  assert.equal(evidence.find(e=>e.path.endsWith('/lake_river_depth_v1/manifest.json')).requirementIds.length,16);
  assert.equal(evidence.find(e=>e.path.endsWith('/sea_river_depth_v1/manifest.json')).requirementIds.length,8);
  assert.equal(evidence.find(e=>e.path.endsWith('/lake_mouth_sections_v1/manifest.json')).requirementIds.length,16);
});
