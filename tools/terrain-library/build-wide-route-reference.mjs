import fs from 'node:fs';
import path from 'node:path';
import {waterfallPixel} from './waterfall-sections.mjs';
import {sectionPixel,sectionSequence} from './river-sections.mjs';
import {isometricWaterfallSample} from './isometric-waterfall.mjs';
const folder='addons/beep_game_builder_cs/generated/';
const base=JSON.parse(fs.readFileSync(folder+'dev/cartoon/water/shared_waterfall_v1/motion_parameters.json','utf8')).baseColorBytes;
const cases=[];
for(const width of [1,2,3,5,9]) {
  const refs=[];
  for(const [index,kind] of sectionSequence(width).entries()) for(const role of ['fall','river']) {
    const sample=(x,y,frame)=>role==='fall'?waterfallPixel(kind,64,x+.5,y+.5,frame,base,index*64):sectionPixel(kind,'north_south',x+.5,y+.5,frame,base);
    const points=[];
    for(const y of role==='fall'?[0,63,64,80,104,127,128,140,191]:[0,7,19,31,43,55,63]) for(const x of [0,16,24,32,40,48,63]) {
      if(Array.from({length:16},(_,f)=>sample(x,y,f)).every(c=>c[3]===255&&c[2]>c[0]&&c[1]>90)) points.push([x,y]);
    }
    refs.push({section:index,role,points,frames:Array.from({length:16},(_,f)=>points.map(([x,y])=>sample(x,y,f).slice(0,3)))});
  }
  cases.push({width,refs});
}
const output=folder+'test/library/output/wide_route_reference.json';
fs.mkdirSync(path.dirname(output),{recursive:true});
fs.writeFileSync(output,JSON.stringify({cases}));
console.log('Built independent CPU samples for wide-route phase and connector pixels.');

const isometric=[];
for(const direction of ['north_south','west_east']) for(const width of [1,2,3,5,9]) {
  const kinds=sectionSequence(width),refs=[];
  for(const [index,kind] of kinds.entries()) for(const role of ['fall','river']) {
    const sample=(x,y,f)=>{
      if(role==='fall') return isometricWaterfallSample(kind,direction,64,x+.5,y+.5,f,base,index*64).rgba;
      const sx=x+.5,sy=y+.5,px=sx+2*sy-32,py=-sx+2*sy+32;
      return px<0||py<0||px>=64||py>=64?[0,0,0,0]:sectionPixel(kind,direction,px,py,f,base);
    };
    const points=[];
    for(let y=2;y<(role==='fall'?112:32);y+=5) for(let x=2;x<(role==='fall'?96:64);x+=5) {
      if(!Array.from({length:16},(_,f)=>sample(x,y,f)).every(c=>c[3]===255&&c[2]>c[0]&&c[1]>90)) continue;
      let hidden=false;
      if(role==='fall') for(let other=index+1;other<width;other++) {
        const step=other-index;
        if(isometricWaterfallSample(kinds[other],direction,64,x+.5-step*(direction==='north_south'?32:-32),y+.5-step*16,0,base,other*64).rgba[3]>0) hidden=true;
      }
      if(!hidden) points.push([x,y]);
    }
    if(points.length<10) throw Error('Insufficient visible isometric samples');
    refs.push({section:index,role,points,frames:Array.from({length:16},(_,f)=>points.map(([x,y])=>sample(x,y,f).slice(0,3)))});
  }
  isometric.push({direction,width,refs});
}
fs.writeFileSync(folder+'test/library/output/isometric_wide_route_reference.json',JSON.stringify({cases:isometric}));
console.log('Built projection-specific isometric references with fixed-alpha visibility filtering.');
