import test from 'node:test';
import assert from 'node:assert/strict';
import {JUNCTIONS, PORTS, makeJunction, junctionPixel} from '../tools/terrain-library/river-junctions.mjs';
import {sectionPixel} from '../tools/terrain-library/river-sections.mjs';
const base = [20,170,201];
test('junction presets declare all routes without ambiguous two-to-two crossings', () => {
  assert.equal(JUNCTIONS.length,32);
  assert.equal(new Set(JUNCTIONS.map(p=>p.id)).size,32);
  assert.throws(()=>makeJunction(['north'],['south','east']));
  assert.throws(()=>makeJunction(['north'],['south','east'],['north_south']));
  assert.throws(()=>makeJunction(['north'],['south','east'],['south_north','north_east']));
  assert.throws(()=>makeJunction(['north'],['north','east'],['north_east']));
});
test('every open junction edge matches a directed narrow channel in every frame', () => {
  for(const p of JUNCTIONS) for(const port of [...p.inflows,...p.outflows]) {
    const incoming=p.inflows.includes(port);
    const directions={north:incoming?'north_south':'south_north',south:incoming?'south_north':'north_south',
      west:incoming?'west_east':'east_west',east:incoming?'east_west':'west_east'};
    for(let frame=0;frame<16;frame++) for(let t=0;t<64;t++) {
      const [x,y]={north:[t+.5,0],south:[t+.5,64],west:[0,t+.5],east:[64,t+.5]}[port];
      assert.deepEqual(junctionPixel(p,x,y,frame,base),sectionPixel('narrow',directions[port],x,y,frame,base),`${p.id} ${port} ${frame} ${t}`);
    }
  }
});
test('loop closes exactly and land stays fixed', () => {
  let changed=0;
  for(const p of JUNCTIONS) for(let y=0;y<64;y++) for(let x=0;x<64;x++) {
    const a=junctionPixel(p,x+.5,y+.5,0,base), b=junctionPixel(p,x+.5,y+.5,7,base);
    assert.deepEqual(a,junctionPixel(p,x+.5,y+.5,16,base));
    if(a[0]===0 || a[0]===189) assert.deepEqual(a,b);
    else if(a.some((v,c)=>v!==b[c])) changed++;
  }
  assert.ok(changed>1000);
  for(const p of JUNCTIONS) for(const port of PORTS.filter(v=>!p.inflows.includes(v)&&!p.outflows.includes(v))) {
    const [x,y]={north:[32,0],south:[32,64],west:[0,32],east:[64,32]}[port];
    assert.deepEqual(junctionPixel(p,x,y,0,base),[0,0,255,255]);
  }
});
