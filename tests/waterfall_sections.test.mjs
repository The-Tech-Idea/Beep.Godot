import test from 'node:test';
import assert from 'node:assert/strict';
import {waterfallPixel,FALL_RISES,curtainLight} from '../tools/terrain-library/waterfall-sections.mjs';
import {sectionPixel,SECTION_KINDS} from '../tools/terrain-library/river-sections.mjs';
const base=[20,170,201];
test('curtain streaks advect downwards without horizontal displacement',()=>{
  for(let frame=0;frame<16;frame++) for(let x=0;x<64;x+=2) for(let y=0;y<64;y+=2) {
    assert.ok(Math.abs(curtainLight(x,y,frame)-curtainLight(x,y+3,frame+1))<1e-10);
    assert.ok(Math.abs(curtainLight(x,y,frame)-curtainLight(x+64,y,frame))<1e-10);
  }
});
test('waterfall ports retain river phase and exact rise',()=>{
  for(const rise of FALL_RISES) for(const kind of SECTION_KINDS) for(let frame=0;frame<16;frame++) for(let x=0;x<64;x++) {
    for(const [fallY,riverY] of [[.5,.5],[127.5+rise,63.5]]) {
      const expected=sectionPixel(kind,'north_south',x+.5,riverY,frame,base);
      const actual=waterfallPixel(kind,rise,x+.5,fallY,frame,base);
      if(expected[0]===0||expected[0]===189) assert.equal(actual[3],0);
      else assert.deepEqual(actual,expected);
    }
    assert.equal(waterfallPixel(kind,rise,x+.5,128+rise,frame,base)[3],0);
  }
});
test('upstream curtain foam and pool change independently, with stable alpha and exact loop',()=>{
  for(const rise of FALL_RISES) {
    const counts=[0,0,0,0];
    for(let y=0;y<128+rise;y++) for(let x=0;x<64;x++) {
      const a=waterfallPixel('narrow',rise,x+.5,y+.5,0,base);
      const b=waterfallPixel('narrow',rise,x+.5,y+.5,7,base);
      assert.deepEqual(a,waterfallPixel('narrow',rise,x+.5,y+.5,16,base));
      assert.equal(a[3],b[3]);
      if(a.some((v,c)=>v!==b[c])) counts[y<59?0:y<64+rise-4?1:y<64+rise+15?2:3]++;
    }
    assert.ok(counts.every(n=>n>20),`${rise}: ${counts}`);
  }
});
test('unsupported rises and section names fail explicitly',()=>{
  assert.throws(()=>waterfallPixel('middle',48,1,1,0,base));
  assert.throws(()=>waterfallPixel('unknown',64,1,1,0,base));
});
test('repeatable middles join at every height and animation phase',()=>{
  for(const rise of FALL_RISES) for(let frame=0;frame<16;frame++) for(let y=0;y<128+rise;y++) {
    const left=waterfallPixel('middle',rise,0,y+.5,frame,base);
    const right=waterfallPixel('middle',rise,64,y+.5,frame,base);
    assert.deepEqual(left,right,`${rise} ${frame} ${y}`);
    assert.deepEqual(waterfallPixel('low_bank',rise,64,y+.5,frame,base),left);
    assert.deepEqual(waterfallPixel('high_bank',rise,0,y+.5,frame,base),right);
  }
});
