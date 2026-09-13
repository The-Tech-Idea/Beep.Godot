import test from 'node:test';
import assert from 'node:assert/strict';
import {LAKE_CONNECTORS,lakeConnectorPixel} from '../tools/terrain-library/river-lake-connectors.mjs';
import {sectionPixel} from '../tools/terrain-library/river-sections.mjs';
import {signedDistance} from '../tools/terrain-library/topology.mjs';
const base=[20,170,201],lake=[23,174,204,255];
test('all eight explicit ports match river edge samples throughout the loop',()=>{
  assert.equal(LAKE_CONNECTORS.length,8);
  for(const p of LAKE_CONNECTORS) for(let frame=0;frame<16;frame++) for(let n=0;n<64;n++) {
    const [x,y]={north:[n+.5,.5],south:[n+.5,63.5],east:[63.5,n+.5],west:[.5,n+.5]}[p.port];
    const direction=p.role==='inlet'?p.incoming:p.outgoing;
    assert.deepEqual(lakeConnectorPixel(p,x,y,frame,base,lake),sectionPixel('narrow',direction,x,y,frame,base));
  }
});
test('lake-facing region retains its lake mask and color, including side contacts',()=>{
  for(const p of LAKE_CONNECTORS) for(let y=0;y<64;y++) for(let x=0;x<64;x++) {
    const px=x+.5,py=y+.5,t={north:py,south:64-py,east:64-px,west:px}[p.port];
    if(t<8) continue;
    const d=signedDistance(p.mask,x,y);
    const expected=d>1.5?lake:d>=-1?[189,167,121,255]:[0,0,255,255];
    assert.deepEqual(lakeConnectorPixel(p,px,py,7,base,lake),expected);
  }
});
test('connector motion closes and shoreline categories remain fixed',()=>{
  for(const p of LAKE_CONNECTORS) for(let y=0;y<64;y++) for(let x=0;x<64;x++) {
    const a=lakeConnectorPixel(p,x+.5,y+.5,0,base,lake);
    assert.deepEqual(a,lakeConnectorPixel(p,x+.5,y+.5,16,base,lake));
    if(a[0]===0||a[0]===189) assert.deepEqual(a,lakeConnectorPixel(p,x+.5,y+.5,7,base,lake));
  }
});
