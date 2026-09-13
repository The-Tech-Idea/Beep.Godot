import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {requirements, coverageReport} from '../tools/terrain-library/coverage.mjs';
const spec = JSON.parse(fs.readFileSync(new URL('../tools/terrain-library/specification.json', import.meta.url)));
const root = process.cwd();

test('coverage includes both styles/projections and exact binary masks', () => {
  const rows = requirements(spec);
  for (const style of ['cartoon', 'pixel']) for (const projection of ['square', 'isometric']) {
    const masks = rows.filter(row => row.style === style && row.projection === projection && row.material === 'grass_to_dirt');
    assert.equal(masks.length, 47);
    for (const family of Object.keys(spec.coverage.families)) assert.ok(rows.some(row => row.style === style && row.projection === projection && row.family === family));
  }
  assert.equal(spec.surfaceWorkflow.mandatoryAlternativeCount, 0);
  assert.equal(spec.fillAlternatives, undefined);
});
test('rises and flow cases are additional requirements, not generic 47 masks', () => {
  const rows = requirements(spec);
  for (const rise of [16, 32, 64]) assert.ok(rows.some(row => row.family === 'cliffs' && row.elevationPixels === rise));
  assert.ok(rows.some(row => row.piece === 'narrow_stairs' && row.elevationPixels === 64));
  assert.equal(rows.some(row => row.piece === 'separate_platform' && row.elevationPixels === 64), false);
  const flows = rows.filter(row => row.family === 'river' && row.piece === 'confluence');
  assert.ok(flows.some(row => row.configuration === 'in_NE_out_S'));
  for (const row of flows) {
    const [, input, , output] = row.configuration.split('_');
    assert.ok(input.length >= 2 && output.length === 1);
    assert.equal([...input].some(port => output.includes(port)), false);
  }
});
test('unbound artwork stays missing even with appearance approval', () => {
  const ledger = JSON.parse(fs.readFileSync(new URL('../tools/terrain-library/coverage-bindings.json', import.meta.url)));
  const report = coverageReport(root, spec, {...ledger, bindings:[]});
  assert.equal(report.summary.missing, report.summary.required);
  assert.equal(report.summary.approved, 0);
  assert.ok(report.evidence.every(entry => !entry.satisfiesRequirements));
});
test('unknown and duplicate bindings fail rather than disappearing', () => {
  assert.throws(() => coverageReport(root, spec, {bindings:[{requirementId:'unknown', status:'approved'}]}), /Unknown/);
  const binding = {requirementId:requirements(spec)[0].id, status:'candidate'};
  assert.throws(() => coverageReport(root, spec, {bindings:[binding,binding]}), /Duplicate/);
});
test('missing resources cannot count as validated or approved', () => {
  const binding = {requirementId:requirements(spec)[0].id, status:'approved', approvalEvidence:'appearance only'};
  const report = coverageReport(root, spec, {bindings:[binding]});
  assert.equal(report.records[0].status, 'candidate');
  assert.ok(report.records[0].issues.includes('No recorded validation report'));
  assert.ok(report.records[0].issues.includes('No production approval evidence'));
  assert.equal(report.summary.approved, 0);
});
