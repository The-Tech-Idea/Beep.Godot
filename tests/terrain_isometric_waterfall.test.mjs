import test from 'node:test';
import assert from 'node:assert/strict';
import {ISO_FALL_DIRECTIONS,projectFallPoint,isometricWaterfallSample} from '../tools/terrain-library/isometric-waterfall.mjs';
import {sectionPixel,SECTION_KINDS} from '../tools/terrain-library/river-sections.mjs';
const base=[35,163,192];

test('isometric inlet and outlet use native adjacent-cell offsets plus vertical rise',()=>{
  for(const direction of ISO_FALL_DIRECTIONS) for(const rise of [16,32,64]) {
    const a=projectFallPoint(direction,32,32),b=projectFallPoint(direction,32,96,rise);
    assert.deepEqual([b[0]-a[0],b[1]-a[1]],[direction==='north_south'?-32:32,16+rise]);
  }
  assert.throws(()=>projectFallPoint('south_north',32,32));
});

test('outer water edges retain directed river phase for both projections of flow',()=>{
  for(const direction of ISO_FALL_DIRECTIONS) for(const kind of SECTION_KINDS) for(const rise of [16,32,64]) for(let frame=0;frame<16;frame++) {
    for(const cross of [16,24,32,40,48]) for(const along of [.01,127.99]) {
      const p=projectFallPoint(direction,cross,along,along>64?rise:0);
      const sample=isometricWaterfallSample(kind,direction,rise,...p,frame,base);
      const s=along>64?along-64:along;
      const expected=direction==='north_south'?sectionPixel(kind,direction,cross,s,frame,base):sectionPixel(kind,direction,s,cross,frame,base);
      assert.deepEqual(sample.rgba,expected);
    }
  }
});

test('all water roles move while alpha stays fixed and the loop closes',()=>{
  for(const direction of ISO_FALL_DIRECTIONS) for(const rise of [16,32,64]) {
    const changes={upstream:0,curtain:0,pool:0};
    for(let y=0;y<48+rise;y++) for(let x=0;x<96;x++) {
      const a=isometricWaterfallSample('narrow',direction,rise,x+.5,y+.5,0,base);
      const b=isometricWaterfallSample('narrow',direction,rise,x+.5,y+.5,5,base);
      const loop=isometricWaterfallSample('narrow',direction,rise,x+.5,y+.5,16,base);
      assert.equal(a.rgba[3],b.rgba[3]);
      assert.deepEqual(a.rgba,loop.rgba);
      if(a.rgba[3]&&a.rgba.some((v,i)=>v!==b.rgba[i])) changes[a.role]++;
    }
    for(const [role,count] of Object.entries(changes)) assert.ok(count>20,`${direction}/${rise}/${role}: ${count}`);
  }
});
