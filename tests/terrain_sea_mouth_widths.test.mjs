import test from 'node:test';
import assert from 'node:assert/strict';
import {SEA_INLETS} from '../tools/terrain-library/river-sea-connectors.mjs';
import {SECTION_KINDS,sectionSequence,sectionPixel} from '../tools/terrain-library/river-sections.mjs';
import {inletSectionMask,inletSectionControlPixel,seaControlPixel} from '../tools/terrain-library/sea-shared-surface.mjs';
import {maskAt} from '../tools/terrain-library/topology.mjs';
const base=[29.25830078125,193.151611328125,235.839599609375];
const point=(port,cross,t)=>({north:[cross,t],east:[64-t,cross],south:[cross,64-t],west:[t,cross]}[port]);
const mapPoint=(port,cross,t)=>({north:[cross+5,t+5],east:[25-t,cross+5],south:[cross+5,25-t],west:[t+5,cross+5]}[port]);
test('width section masks match actual occupied neighbors in all inlet directions',()=>{
  for(const p of SEA_INLETS) for(const width of [1,2,3,5]) {
    const map=Array.from({length:32},()=>Array(32).fill(false));
    for(let c=0;c<16;c++) for(let t=0;t<14;t++) if(t>=4||(c>=4&&c<4+width)) {
      const [x,y]=mapPoint(p.port,c,t);map[y][x]=true;
    }
    sectionSequence(width).forEach((kind,i)=>{
      const [x,y]=mapPoint(p.port,4+i,4);
      assert.equal(inletSectionMask(p,kind),maskAt(map,x,y),`${p.id} width ${width} ${kind}`);
    });
  }
});
test('every width module preserves incoming river and outgoing coast pixels in all frames',()=>{
  for(const p of SEA_INLETS) for(const kind of SECTION_KINDS) for(let frame=0;frame<16;frame++) for(let c=.5;c<64;c++) {
    for(const t of [0,.5,8,32,64]) {
      const [x,y]=point(p.port,c,t);
      const expected=t<=.5?sectionPixel(kind,p.incoming,x,y,frame,base):seaControlPixel(inletSectionMask(p,kind),x,y);
      assert.deepEqual(inletSectionControlPixel(p,kind,x,y,frame,base),expected);
    }
  }
});
test('repeatable middle and edge joins have identical effective shader controls',()=>{
  // Depths above 20px have identical sea shading and no surf in this shader.
  const effective=color=>color[2]>=250&&color[2]<=254?[color[0],Math.min(color[1],80),color[2],color[3]]:color;
  for(const p of SEA_INLETS) for(const width of [2,3,5]) {
    const sections=sectionSequence(width);
    for(let i=0;i<width-1;i++) for(let frame=0;frame<16;frame++) for(let t=.5;t<64;t++) {
      const a=point(p.port,64,t),b=point(p.port,0,t);
      assert.deepEqual(effective(inletSectionControlPixel(p,sections[i],...a,frame,base)),
        effective(inletSectionControlPixel(p,sections[i+1],...b,frame,base)),`${p.id} ${sections[i]} -> ${sections[i+1]}`);
    }
  }
});
test('width module bank pixels and alpha remain fixed and loops close',()=>{
  for(const p of SEA_INLETS) for(const kind of SECTION_KINDS) for(let y=.5;y<64;y+=2) for(let x=.5;x<64;x+=2) {
    const first=inletSectionControlPixel(p,kind,x,y,0,base);
    assert.deepEqual(first,inletSectionControlPixel(p,kind,x,y,16,base));
    for(const frame of [3,7,12,15]) {
      const next=inletSectionControlPixel(p,kind,x,y,frame,base);
      assert.equal(next[3],first[3]);
      if(first[2]===255||first[2]===121) assert.deepEqual(next,first);
    }
  }
  assert.throws(()=>inletSectionMask(SEA_INLETS[0],'unknown'),/section/);
});
