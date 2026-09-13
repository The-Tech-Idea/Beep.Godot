import test from 'node:test';
import assert from 'node:assert/strict';
import {waterfallPixel} from '../tools/terrain-library/waterfall-sections.mjs';
import {isometricWaterfallSample,projectFallPoint} from '../tools/terrain-library/isometric-waterfall.mjs';
const base=[35,163,192];

test('shared field has a declared multi-cell period and supports negative offsets',()=>{
  for(const offset of [-3072,-1600,-64,0,320,1024,1536,8192]) for(const frame of [0,5,15]) for(let y=.5;y<192;y+=3) for(let x=.5;x<64;x+=3) {
    assert.deepEqual(waterfallPixel('middle',64,x,y,frame,base,offset),waterfallPixel('middle',64,x,y,frame,base,offset+1536));
  }
});

test('isometric shared offsets preserve projection, endpoints, alpha and loop at all rises',()=>{
  for(const direction of ['north_south','west_east']) for(const rise of [16,32,64]) {
    for(let y=.5;y<48+rise;y+=2) for(let x=.5;x<96;x+=2) {
      const a=isometricWaterfallSample('middle',direction,rise,x,y,0,base,128);
      const b=isometricWaterfallSample('middle',direction,rise,x,y,16,base,128);
      assert.deepEqual(a,b);
      const legacy=isometricWaterfallSample('middle',direction,rise,x,y,0,base);
      assert.equal(a.role,legacy.role);
      assert.equal(a.rgba[3],legacy.rgba[3]);
    }
    for(let frame=0;frame<16;frame++) for(const along of [.01,127.99]) for(const cross of [16,32,48]) {
      const p=projectFallPoint(direction,cross,along,along>64?rise:0);
      assert.deepEqual(isometricWaterfallSample('middle',direction,rise,...p,frame,base,192),isometricWaterfallSample('middle',direction,rise,...p,frame,base));
    }
  }
});

test('shared waterfall sections keep exact adjacent joins and river contacts',()=>{
  for(const rise of [16,32,64]) for(let frame=0;frame<16;frame++) {
    for(let offset=-128;offset<320;offset+=64) for(let y=.5;y<128+rise;y++) {
      assert.deepEqual(waterfallPixel('middle',rise,64,y,frame,base,offset),waterfallPixel('middle',rise,0,y,frame,base,offset+64));
    }
    for(const kind of ['narrow','low_bank','middle','high_bank']) for(let x=.5;x<64;x++) for(const y of [.5,127+rise+.5]) {
      assert.deepEqual(waterfallPixel(kind,rise,x,y,frame,base,128),waterfallPixel(kind,rise,x,y,frame,base));
    }
  }
});

test('shared waterfall loops exactly, keeps alpha and does not repeat each cell',()=>{
  let variations=0,foamVariations=0;
  for(let x=.5;x<64;x++) for(let y=.5;y<192;y++) {
    const first=waterfallPixel('middle',64,x,y,0,base,0);
    assert.deepEqual(first,waterfallPixel('middle',64,x,y,16,base,0));
    assert.equal(first[3],waterfallPixel('middle',64,x,y,7,base,128)[3]);
    if(JSON.stringify(first)!==JSON.stringify(waterfallPixel('middle',64,x,y,0,base,64))) {
      if(y>=64&&y<128) variations++;
      if(y>=128&&y<145) foamVariations++;
    }
  }
  assert.ok(variations>1000);
  assert.ok(foamVariations>100);
});
