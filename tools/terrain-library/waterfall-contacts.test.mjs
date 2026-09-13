import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {createHash} from 'node:crypto';

const require = createRequire(import.meta.url);
let sharp;
try { sharp = require('sharp'); }
catch { sharp = require(path.join(process.env.TERRAIN_NODE_MODULES, 'sharp')); }
const folder = 'addons/beep_game_builder_cs/generated/dev/cartoon/elevation/waterfall_contacts_v1/';
const read = name => JSON.parse(fs.readFileSync(folder + name, 'utf8'));
const spec = read('specification.json');
const calibration = read('calibration.json');
const manifest = read('manifest.json');

test('contact source and complete piece identities match the declared contracts', () => {
  const hash = createHash('sha256').update(fs.readFileSync(folder + manifest.source)).digest('hex');
  assert.equal(hash, spec.sourceSha256);
  assert.equal(hash, manifest.sourceSha256);
  assert.deepEqual(manifest.pieces.map(p => p.id).sort(), spec.pieceContracts.map(p => p.id).sort());
  assert.equal(new Set(manifest.pieces.map(p => p.id)).size, manifest.pieces.length);
  assert.equal(manifest.productionReady, false);
  assert.equal(manifest.approvalEvidence, null);
});

for (const piece of manifest.pieces) {
  test(`${piece.id}: bounds, pivot, rise and protected water edge`, async () => {
    const contract = spec.pieceContracts.find(p => p.id === piece.id);
    const crop = calibration.pieces.find(p => p.id === piece.id);
    assert.deepEqual(piece.runtimeBounds, contract.runtimeBounds);
    assert.deepEqual(piece.pivot, contract.pivot);
    assert.equal(piece.risePixels, contract.risePixels);
    assert.equal(piece.waterSide, contract.waterSide);
    assert.equal(piece.approvalEvidence, null);
    assert.ok(Number.isFinite(piece.uniformScale) && piece.uniformScale > 0);
    for (let axis = 0; axis < 2; axis++) {
      const anchor = piece.unroundedOrigin[axis] + (crop.sourceAnchor[axis] - crop.crop[axis]) * piece.uniformScale;
      assert.ok(Math.abs(anchor - contract.pivot[axis]) < 1e-9);
    }
    if (contract.risePixels) assert.ok(Math.abs((crop.sourceBaseY - crop.sourceAnchor[1]) * piece.uniformScale - contract.risePixels) < 1e-9);
    const {data, info} = await sharp(folder + piece.runtime).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    assert.deepEqual([info.width, info.height], contract.runtimeBounds);
    let visible = 0;
    for (let y = 0; y < info.height; y++) for (let x = 0; x < info.width; x++) {
      const alpha = data[(y * info.width + x) * 4 + 3];
      const water = contract.waterSide === 'right' ? x + 0.5 > contract.pivot[0] : x + 0.5 < contract.pivot[0];
      if (water) assert.equal(alpha, 0, `water pixel ${x},${y}`);
      if (alpha) visible++;
    }
    assert.ok(visible >= 10);
    assert.equal(visible, piece.visiblePixels);
  });
}
