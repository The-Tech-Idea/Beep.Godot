import test from 'node:test';
import assert from 'node:assert/strict';
import {LAKE_CONNECTORS} from '../tools/terrain-library/river-lake-connectors.mjs';
import {lakeMouthMask,lakeMouthPixel,LAKE_MOUTH_KINDS,LAKE_MOUTH_MODULES} from '../tools/terrain-library/lake-mouth-sections.mjs';
import {sectionPixel,sectionSequence} from '../tools/terrain-library/river-sections.mjs';
import {signedDistance} from '../tools/terrain-library/topology.mjs';
const base=[29,193,236],lake=[31,188,230,255];
const ports=['north','east','south','west'];
function point(port,cross,along) {
  return [[cross,along],[64-along,cross],[cross,64-along],[along,cross]][ports.indexOf(port)];
}
test('wide lake mouths declare distinct inlet/outlet edge and middle profiles',()=>{
  assert.equal(LAKE_MOUTH_MODULES.length,24);
  assert.equal(new Set(LAKE_MOUTH_MODULES.map(p=>p.id)).size,24);
  for(const profile of LAKE_CONNECTORS) {
    assert.equal(lakeMouthMask(profile,'middle'),255);
    assert.throws(()=>lakeMouthMask({...profile,role:'ambiguous'},'middle'));
    assert.throws(()=>lakeMouthMask(profile,'unknown'));
  }
});
test('every wide mouth preserves directed river edges and existing lake-facing pixels',()=>{
  for(const profile of LAKE_CONNECTORS) for(const kind of LAKE_MOUTH_KINDS) for(let frame=0;frame<16;frame++) for(let cross=0.5;cross<64;cross++) {
    const [x,y]=point(profile.port,cross,0.5);
    assert.deepEqual(lakeMouthPixel(profile,kind,x,y,frame,base,lake),sectionPixel(kind,profile.role==='inlet'?profile.incoming:profile.outgoing,x,y,frame,base));
    for(const t of [8,24,63.5]) {
      const [lx,ly]=point(profile.port,cross,t);
      const d=signedDistance(lakeMouthMask(profile,kind),lx-0.5,ly-0.5);
      const expected=d < -1?[0,0,255,255]:d<=1.5?[189,167,121,255]:lake;
      assert.deepEqual(lakeMouthPixel(profile,kind,lx,ly,frame,base,lake),expected);
    }
  }
});
test('width middles share exact internal contacts and fixed banks through the full loop',()=>{
  for(const profile of LAKE_CONNECTORS) for(const width of [2,3,5]) {
    const sequence=sectionSequence(width);
    for(let i=1;i<sequence.length;i++) for(let t=0.5;t<64;t++) for(let frame=0;frame<16;frame++) {
      const a=point(profile.port,64,t),b=point(profile.port,0,t);
      assert.deepEqual(lakeMouthPixel(profile,sequence[i-1],...a,frame,base,lake),lakeMouthPixel(profile,sequence[i],...b,frame,base,lake));
    }
  }
  for(const profile of LAKE_CONNECTORS) for(const kind of LAKE_MOUTH_KINDS) for(let y=0.5;y<64;y+=2) for(let x=0.5;x<64;x+=2) {
    const first=lakeMouthPixel(profile,kind,x,y,0,base,lake);
    assert.deepEqual(first,lakeMouthPixel(profile,kind,x,y,16,base,lake));
    if(first[0]===0||first[0]===189) for(let frame=1;frame<16;frame++) assert.deepEqual(first,lakeMouthPixel(profile,kind,x,y,frame,base,lake));
  }
});
