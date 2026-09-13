import test from 'node:test';
import assert from 'node:assert/strict';
import {MASKS,maskAt} from '../tools/terrain-library/topology.mjs';
import {seaPixel,seaSwell,surfStrength,seaDistance} from '../tools/terrain-library/sea-motion.mjs';
const base=[35,163,192];

test('sea loop closes and keeps grass and bank pixels fixed across all 47 masks',()=>{
  for(const mask of MASKS) for(let y=0;y<64;y++) for(let x=0;x<64;x++) {
    const a=seaPixel(mask,x+.5,y+.5,0,base),b=seaPixel(mask,x+.5,y+.5,5,base);
    assert.deepEqual(a,seaPixel(mask,x+.5,y+.5,16,base));
    assert.equal(a[3],b[3]);
    if(seaDistance(mask,x+.5,y+.5)<=1.5) assert.deepEqual(a,b);
  }
});

test('ocean swells repeat in both logical axes, while surf travels toward the coast',()=>{
  for(let frame=0;frame<16;frame++) {
    assert.ok(Math.abs(seaSwell(13,27,frame)-seaSwell(77,27,frame))<1e-12);
    assert.ok(Math.abs(seaSwell(13,27,frame)-seaSwell(13,91,frame))<1e-12);
  }
  const peak=frame=>Array.from({length:181},(_,i)=>i/10).reduce((best,d)=>surfStrength(d,10,15,frame)>surfStrength(best,10,15,frame)?d:best,0);
  assert.ok(peak(2)>peak(10)+5);
  assert.throws(()=>seaPixel(16,0,0,0,base));
});

test('compatible coastline tiles keep identical water colors along common boundaries',()=>{
  let comparisons=0;
  const map=Array.from({length:9},(_,y)=>Array.from({length:9},(_,x)=>((x*13+y*7+x*y)%11)>2));
  for(let y=1;y<7;y++) for(let x=1;x<7;x++) {
    if(!map[y][x]) continue;
    for(const [dx,dy] of [[1,0],[0,1]]) {
      if(!map[y+dy][x+dx]) continue;
      const a=maskAt(map,x,y),b=maskAt(map,x+dx,y+dy);
      for(let frame=0;frame<16;frame++) for(let t=0;t<64;t++) {
        const ca=seaPixel(a,dx?64:t+.5,dy?64:t+.5,frame,base);
        const cb=seaPixel(b,dx?0:t+.5,dy?0:t+.5,frame,base);
        assert.deepEqual(ca,cb,`${a}/${b} edge ${dx},${dy} at ${t} frame ${frame}`);
        comparisons++;
      }
    }
  }
  assert.ok(comparisons>10000);
});

test('every realizable occupied-neighbor coast pair has continuous animated edges',()=>{
  let comparisons=0;
  for(const [dx,dy] of [[1,0],[0,1]]) {
    const width=dx?4:3,height=dy?4:3,pairs=new Set();
    const free=[];
    for(let y=0;y<height;y++) for(let x=0;x<width;x++) {
      if((x===1&&y===1)||(x===1+dx&&y===1+dy)) continue;
      free.push([x,y]);
    }
    for(let bits=0;bits<(1<<free.length);bits++) {
      const map=Array.from({length:height},()=>Array(width).fill(false));
      map[1][1]=true;map[1+dy][1+dx]=true;
      free.forEach(([x,y],i)=>map[y][x]=!!(bits&(1<<i)));
      pairs.add(`${maskAt(map,1,1)},${maskAt(map,1+dx,1+dy)}`);
    }
    for(const pair of pairs) {
      const [a,b]=pair.split(',').map(Number);
      for(let frame=0;frame<16;frame++) for(let t=0;t<64;t++) {
        assert.deepEqual(seaPixel(a,dx?64:t+.5,dy?64:t+.5,frame,base),seaPixel(b,dx?0:t+.5,dy?0:t+.5,frame,base),`${pair}/${dx},${dy}/${frame}/${t}`);
        comparisons++;
      }
    }
  }
  assert.ok(comparisons>100000);
});
