import test from 'node:test';
import assert from 'node:assert/strict';
import {RIVER_PROFILES,flowCoordinates,riverLight,riverFrame} from '../tools/terrain-library/river-motion.mjs';
import {signedDistance} from '../tools/terrain-library/topology.mjs';
const endpoints={north:[32,0],south:[32,64],west:[0,32],east:[64,32]};
test('all twelve flow directions run from the named inlet to outlet',()=>{
 assert.equal(RIVER_PROFILES.length,12);
 for(const p of RIVER_PROFILES){const [from,to]=p.id.split('_');assert.ok(Math.abs(flowCoordinates(p,...endpoints[from])[0])<1e-8,p.id);assert.ok(Math.abs(flowCoordinates(p,...endpoints[to])[0]-64)<1e-8,p.id);}
});
test('current advects forward, repeats exactly, and endpoint phase agrees',()=>{
 for(let f=0;f<16;f++)for(const n of [-13,-4,4,13])for(const s of [0,1.5,18.75,47]){
  assert.ok(Math.abs(riverLight(s,n,f)-riverLight(s+2,n,f+1))<1e-10);
  assert.ok(Math.abs(riverLight(s,n,f)-riverLight(s,n,f+16))<1e-10);
 }
 for(let f=0;f<16;f++)for(let n=-16;n<=16;n++)assert.ok(Math.abs(riverLight(0,n,f)-riverLight(64,-n,f))<1e-10);
});
test('all profiles preserve land and loop raster frames exactly',()=>{
 const grass=Buffer.alloc(16384),water=Buffer.alloc(16384);
 for(let i=0;i<16384;i+=4){grass.set([110,164,71,255],i);water.set([22,178,214,255],i);}
 for(const profile of RIVER_PROFILES){
  const a=riverFrame(grass,water,profile,0),b=riverFrame(grass,water,profile,5);
  assert.deepEqual(a,riverFrame(grass,water,profile,16));assert.notDeepEqual(a,b);
  for(let y=0;y<64;y++)for(let x=0;x<64;x++)if(signedDistance(profile.mask,x,y)<0){const i=(y*64+x)*4;assert.deepEqual(a.subarray(i,i+4),b.subarray(i,i+4));}
 }
});
