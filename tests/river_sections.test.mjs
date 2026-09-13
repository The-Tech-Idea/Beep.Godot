import test from 'node:test';
import assert from 'node:assert/strict';
import {SECTION_KINDS,FLOW_DIRECTIONS,sectionPixel,sectionSequence} from '../tools/terrain-library/river-sections.mjs';
const water=[30,170,205];
test('arbitrary widths use banks and repeatable middles',()=>{
  for(const width of [1,2,3,5,9]) {
    const pieces=sectionSequence(width);
    assert.equal(pieces.length,width);
    if(width>1) {assert.equal(pieces[0],'low_bank');assert.equal(pieces.at(-1),'high_bank');assert.ok(pieces.slice(1,-1).every(p=>p==='middle'));}
  }
  assert.throws(()=>sectionSequence(0));
});
test('all sections loop and their terrain remains fixed',()=>{
  for(const direction of FLOW_DIRECTIONS) for(const kind of SECTION_KINDS) for(let y=0;y<64;y++) for(let x=0;x<64;x++) {
    const start=sectionPixel(kind,direction,x+.5,y+.5,0,water);
    assert.deepEqual(start,sectionPixel(kind,direction,x+.5,y+.5,16,water));
    if(start[2]===255||start[0]===189) for(let frame=1;frame<16;frame++) assert.deepEqual(start,sectionPixel(kind,direction,x+.5,y+.5,frame,water));
  }
});
test('open cross-channel edges match and longitudinal phase continues',()=>{
  for(const frame of [0,1,7,15]) for(let along=0;along<=64;along++) {
    assert.deepEqual(sectionPixel('low_bank','north_south',64,along,frame,water),sectionPixel('middle','north_south',0,along,frame,water));
    assert.deepEqual(sectionPixel('middle','north_south',64,along,frame,water),sectionPixel('high_bank','north_south',0,along,frame,water));
    assert.deepEqual(sectionPixel('middle','north_south',along,0,frame,water),sectionPixel('middle','north_south',along,64,frame,water));
  }
});
