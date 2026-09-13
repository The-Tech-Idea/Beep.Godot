import test from 'node:test';
import assert from 'node:assert/strict';
import {depthControlPixel} from '../tools/terrain-library/sea-shared-surface.mjs';
import {MASKS,maskAt} from '../tools/terrain-library/topology.mjs';

test('depth masks are opaque water data, with no grass or bank pixels',()=>{
  for(const mask of [...MASKS,null]) for(let y=.5;y<64;y++) for(let x=.5;x<64;x++) {
    const value=depthControlPixel(mask,x,y);
    assert.equal(value[1],0);assert.equal(value[2],249);assert.equal(value[3],255);
    assert.ok(Number.isInteger(value[0])&&value[0]>=0&&value[0]<=255);
  }
  assert.deepEqual(depthControlPixel(null,32,32),[0,0,249,255]);
  assert.deepEqual(depthControlPixel(255,32,32),[255,0,249,255]);
  assert.equal(depthControlPixel(0,0,32)[0],0);
  assert.equal(depthControlPixel(0,32,32)[0],255);
});

test('all realizable shallow/deep neighbor pairs share exact depth edge values',()=>{
  for(let occupancy=0;occupancy<4096;occupancy++) {
    const map=Array.from({length:3},(_,y)=>Array.from({length:4},(_,x)=>Boolean(occupancy&(1<<(y*4+x)))));
    const transposed=Array.from({length:4},(_,y)=>Array.from({length:3},(_,x)=>map[x][y]));
    const left=map[1][1]?maskAt(map,1,1):null,right=map[1][2]?maskAt(map,2,1):null;
    const top=transposed[1][1]?maskAt(transposed,1,1):null,bottom=transposed[2][1]?maskAt(transposed,1,2):null;
    for(let position=.5;position<64;position++) {
      assert.deepEqual(depthControlPixel(left,64,position),depthControlPixel(right,0,position));
      assert.deepEqual(depthControlPixel(top,position,64),depthControlPixel(bottom,position,0));
    }
  }
});
