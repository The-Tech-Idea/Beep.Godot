import test from 'node:test';
import assert from 'node:assert/strict';
import {sharedSwell,seaControlPixel,inletControlPixel} from '../tools/terrain-library/sea-shared-surface.mjs';
import {SEA_INLETS} from '../tools/terrain-library/river-sea-connectors.mjs';
import {sectionPixel} from '../tools/terrain-library/river-sections.mjs';
import {MASKS} from '../tools/terrain-library/topology.mjs';
const base=[25,164,209];
test('shared swells loop without repeating each terrain cell, including negative coordinates',()=>{
  let different=0,moving=0;
  for(let y=-256;y<512;y+=3) for(let x=-256;x<512;x+=3) {
    const value=sharedSwell(x,y,0);
    assert.ok(Math.abs(value-sharedSwell(x,y,16))<1e-10);
    if(Math.abs(value-sharedSwell(x+64,y,0))>.001) different++;
    if(Math.abs(value-sharedSwell(x,y,5))>.001) moving++;
    assert.ok(value>=0&&value<=.3);
  }
  assert.ok(different>500);
  assert.ok(moving>500);
});
test('compact wave support has no discontinuity at field partition boundaries',()=>{
  for(let edge=-256;edge<=512;edge+=128) for(let cross=-256;cross<512;cross+=.5) for(const frame of [0,5,10,15]) {
    assert.ok(Math.abs(sharedSwell(edge-1e-5,cross,frame)-sharedSwell(edge+1e-5,cross,frame))<1e-4);
    assert.ok(Math.abs(sharedSwell(cross,edge-1e-5,frame)-sharedSwell(cross,edge+1e-5,frame))<1e-4);
  }
});
test('all 47 coast control masks have only declared tags and fixed bank pixels',()=>{
  for(const mask of MASKS) for(let y=.5;y<64;y++) for(let x=.5;x<64;x++) {
    const color=seaControlPixel(mask,x,y);
    assert.equal(color[3],255);
    assert.ok(color.every(v=>Number.isInteger(v)&&v>=0&&v<=255));
    assert.ok(color[2]===254||JSON.stringify(color)==='[0,0,255,255]'||JSON.stringify(color)==='[189,167,121,255]');
  }
});
test('explicit inlet controls preserve river and sea boundary contracts',()=>{
  for(const p of SEA_INLETS) for(let f=0;f<16;f++) for(let cross=.5;cross<64;cross++) for(const t of [.5,8,32,63.5]) {
    const [x,y]={north:[cross,t],east:[64-t,cross],south:[cross,64-t],west:[t,cross]}[p.port];
    const expected=t===.5?sectionPixel('narrow',p.incoming,x,y,f,base):seaControlPixel(p.mask,x,y);
    assert.deepEqual(inletControlPixel(p,x,y,f,base),expected);
  }
});
