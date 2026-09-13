import test from 'node:test';
import assert from 'node:assert/strict';
import {SEA_INLETS,seaInletPixel} from '../tools/terrain-library/river-sea-connectors.mjs';
import {seaPixel} from '../tools/terrain-library/sea-motion.mjs';
import {sectionPixel} from '../tools/terrain-library/river-sections.mjs';
const base=[25,164,209];
const point=(port,cross,t)=>({north:[cross,t],east:[64-t,cross],south:[cross,64-t],west:[t,cross]}[port]);
test('all four explicit inlet edges preserve incoming river frames and downstream sea pixels',()=>{
  for(const p of SEA_INLETS) for(let f=0;f<16;f++) for(let c=.5;c<64;c++) {
    for(const t of [0,.5]) {
      const [x,y]=point(p.port,c,t);
      assert.deepEqual(seaInletPixel(p,x,y,f,base),sectionPixel('narrow',p.incoming,x,y,f,base));
    }
    for(const t of [8,16,32,63.5,64]) {
      const [x,y]=point(p.port,c,t);
      assert.deepEqual(seaInletPixel(p,x,y,f,base),seaPixel(p.mask,x,y,f,base));
    }
  }
});
test('estuary bank silhouette stays fixed and animation loops exactly',()=>{
  let moving=0;
  for(const p of SEA_INLETS) for(let y=.5;y<64;y++) for(let x=.5;x<64;x++) {
    const first=seaInletPixel(p,x,y,0,base);
    assert.deepEqual(first,seaInletPixel(p,x,y,16,base));
    for(let f=1;f<16;f++) {
      const next=seaInletPixel(p,x,y,f,base);
      assert.equal(next[3],first[3]);
      if(first[0]===189||first[2]===255) assert.deepEqual(next,first);
      else if(first.some((v,c)=>v!==next[c])) moving++;
    }
  }
  assert.ok(moving>1000);
});
test('mouth lateral edges retain sea-bank contracts and invalid flow is rejected',()=>{
  for(const p of SEA_INLETS) for(let f=0;f<16;f++) for(let t=.5;t<64;t++) for(const c of [0,64]) {
    const [x,y]=point(p.port,c,t);
    assert.deepEqual(seaInletPixel(p,x,y,f,base),seaPixel(p.mask,x,y,f,base));
  }
  assert.throws(()=>seaInletPixel({...SEA_INLETS[0],incoming:'south_north'},32,2,0,base),/explicit/);
});
