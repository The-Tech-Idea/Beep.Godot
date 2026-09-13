import test from 'node:test';
import assert from 'node:assert/strict';
import {transitionBoundary,transitionPixel} from '../tools/terrain-library/river-width-transitions.mjs';
import {sectionSequence,sectionPixel} from '../tools/terrain-library/river-sections.mjs';
const water=[30,170,205];

test('width transition endpoints match assembled river sections',()=>{
  for(const [from,to] of [[1,2],[2,1],[2,3],[3,2],[4,5],[5,4]]) for(const [along,width] of [[0,from],[128,to]]) {
    const pieces=sectionSequence(width);
    for(let x=0;x<width*64;x++) for(const frame of [0,3,9,15])
      assert.deepEqual(transitionPixel(from,to,x+.5,along,frame,water),sectionPixel(pieces[Math.floor(x/64)],'north_south',x%64+.5,along,frame,water));
  }
});
test('banks are monotonic, fixed over the loop, and keep constant outside margin',()=>{
  for(const [from,to] of [[1,2],[2,1],[4,5],[5,4]]) {
    let previous=transitionBoundary(from,to,0).high;
    for(let y=0;y<=128;y++) {
      const edge=transitionBoundary(from,to,y);
      assert.equal(edge.low,10.94);
      assert.ok(to>from ? edge.high>=previous : edge.high<=previous);
      previous=edge.high;
      for(let x=0;x<Math.max(from,to)*64;x+=3) {
        const pixel=transitionPixel(from,to,x+.5,y,0,water);
        assert.deepEqual(pixel,transitionPixel(from,to,x+.5,y,16,water));
        if(pixel[2]===255||pixel[0]===189) assert.deepEqual(pixel,transitionPixel(from,to,x+.5,y,7,water));
      }
    }
  }
});
test('unsupported width jumps are explicit errors',()=>{
  for(const widths of [[0,1],[1,3],[2,2],[1.5,2]]) assert.throws(()=>transitionBoundary(...widths,64));
});
