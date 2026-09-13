import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {MASKS} from './topology.mjs';
import {inside, writeJson, digest, pngSize} from './library.mjs';

function flowConfigurations(piece) {
  const result = [], directions = ['N', 'E', 'S', 'W'];
  const count = mask => directions.filter((_, i) => mask & (1 << i)).length;
  const label = mask => directions.filter((_, i) => mask & (1 << i)).join('');
  for (let input = 1; input < 16; input++) for (let output = 1; output < 16; output++) {
    if (input & output) continue;
    if (piece === 'confluence' && !(count(input) >= 2 && count(output) === 1)) continue;
    if (piece === 'branch' && !(count(input) === 1 && count(output) >= 2)) continue;
    if (piece === 'crossing' && (input | output) !== 15) continue;
    result.push(`in_${label(input)}_out_${label(output)}`);
  }
  return result;
}

export function requirements(spec) {
  if (spec.schemaVersion !== 2 || spec.binaryConfigurations !== MASKS.length) throw Error('Unsupported terrain coverage contract');
  const rows = [];
  function add(style, projection, family, material, piece, configuration, elevationPixels, animationRole) {
    const id = [style, projection, family, material, piece, configuration, elevationPixels, animationRole].join('.');
    rows.push({id, style, projection, family, material, piece, configuration, elevationPixels, animationRole});
  }
  for (const style of Object.keys(spec.styles)) for (const projection of Object.keys(spec.projections)) {
    for (const [family, definition] of Object.entries(spec.coverage.families)) {
      const materials = Array.isArray(definition.materials) ? definition.materials : spec[definition.materials];
      if (!Array.isArray(materials)) throw Error(`Unknown material domain: ${family}`);
      for (const material of materials) for (const piece of definition.pieces) {
        const configurations = family === 'river' && ['branch', 'confluence', 'crossing'].includes(piece)
          ? flowConfigurations(piece) : definition.directions ?? ['authored'];
        const rises = piece === 'narrow_stairs' ? [...definition.rises, ...(definition.additionalStairRises ?? [])] : definition.rises;
        for (const rise of rises) for (const configuration of configurations)
          add(style, projection, family, material, piece, configuration, rise, definition.animationRole);
      }
    }
    for (const [family, transitions] of [['ground_boundaries', spec.transitions], ['water_boundaries', spec.waterTransitions]])
      for (const [a, b] of transitions) for (const mask of MASKS)
        add(style, projection, family, `${a}_to_${b}`, 'binary_mask', `mask_${mask}`, 0, 'static_mask');
  }
  if (new Set(rows.map(row => row.id)).size !== rows.length) throw Error('Duplicate requirement IDs');
  return rows;
}

export function coverageReport(root, spec, ledger) {
  const rows = requirements(spec), known = new Set(rows.map(row => row.id)), bindings = new Map();
  for (const binding of ledger.bindings) {
    if (!known.has(binding.requirementId)) throw Error(`Unknown requirement: ${binding.requirementId}`);
    if (bindings.has(binding.requirementId)) throw Error(`Duplicate binding: ${binding.requirementId}`);
    if (!['candidate', 'validated', 'approved'].includes(binding.status)) throw Error('Invalid binding status');
    bindings.set(binding.requirementId, binding);
  }
  const records = rows.map(row => {
    const binding = bindings.get(row.id);
    const record = {...row, source:null, runtimeFile:null, runtimeRegion:null, godotResource:null,
      validation:{status:'not_run', report:null}, approvalEvidence:null, status:'missing', issues:[]};
    if (!binding) return record;
    for (const field of ['source', 'runtimeFile', 'runtimeRegion', 'godotResource', 'validation', 'approvalEvidence', 'status', 'conversionRequired', 'provenance', 'alternatives', 'coverageScope'])
      if (binding[field] !== undefined) record[field] = binding[field];
    for (const field of ['source', 'runtimeFile', 'godotResource']) {
      if (typeof record[field] !== 'string' || !fs.existsSync(inside(root, record[field]))) record.issues.push(`Missing ${field}`);
    }
    const region = record.runtimeRegion;
    if (!region || !['x', 'y', 'width', 'height'].every(key => Number.isInteger(region[key]))
        || region.x < 0 || region.y < 0 || region.width <= 0 || region.height <= 0) record.issues.push('Missing/invalid runtime region');
    else if (record.runtimeFile && fs.existsSync(inside(root, record.runtimeFile))) {
      const size = pngSize(fs.readFileSync(inside(root, record.runtimeFile)));
      if (!size || region.x + region.width > size[0] || region.y + region.height > size[1]) record.issues.push('Runtime region exceeds PNG bounds');
    }
    if (['validated', 'approved'].includes(record.status)) {
      if (record.validation?.status !== 'passed' || typeof record.validation.report !== 'string'
          || !fs.existsSync(inside(root, record.validation.report))) record.issues.push('No recorded validation report');
    }
    if (record.status === 'approved' && (record.approvalEvidence?.scope !== 'production'
        || !record.approvalEvidence.reviewedBy || !record.approvalEvidence.reference))
      record.issues.push('No production approval evidence');
    if (record.issues.length) record.status = 'candidate';
    return record;
  });
  const totals = {missing:0, candidate:0, validated:0, approved:0}, families = {};
  for (const record of records) {
    totals[record.status]++;
    const key = `${record.style}.${record.projection}.${record.family}`;
    families[key] ??= {required:0, missing:0, candidate:0, validated:0, approved:0};
    families[key].required++; families[key][record.status]++;
  }
  const evidence = (ledger.evidence ?? []).map(entry => {
    const location = inside(root, entry.path), exists = fs.existsSync(location);
    return {...entry, exists, sha256:exists ? digest(fs.readFileSync(location)) : null, satisfiesRequirements:false};
  });
  return {schemaVersion:1, specificationVersion:spec.schemaVersion, summary:{required:records.length, ...totals}, families,
    evidence, records, limitations:[
      'Logical cases can share verified artwork or patterns; requirement count is not an image count.',
      'Bindings are explicit review records. File/region checks do not perform Godot, shader or visual validation.',
      'Existing candidates are not discarded or deemed unused when they lack a binding.',
      'Arbitrary widths/heights require the composed showcase tests in the specification, not only per-piece coverage.'
    ]};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
  const spec = JSON.parse(fs.readFileSync(inside(root, 'tools/terrain-library/specification.json'), 'utf8'));
  const ledger = JSON.parse(fs.readFileSync(inside(root, spec.coverage.bindingsFile), 'utf8'));
  const report = coverageReport(root, spec, ledger);
  writeJson(root, spec.coverage.reportFile, report);
  console.log(JSON.stringify({output:spec.coverage.reportFile, ...report.summary, evidence:report.evidence.length}));
}
