import {normalize} from './topology.mjs';
const clamp=v=>Math.max(0,Math.min(1,v));
const wrap=v=>((v%1)+1)%1;

// Physical cell boundaries are 0 and 64; using pixel-index centers here creates
// different corner distances on opposite sides of the same joined tile edge.
export function seaDistance(mask,x,y) {
  const cx=32,dx=Math.abs(x-cx),dy=Math.abs(y-cx),radius=64*.34;
  const h=!!(mask&(1<<(x<cx?3:1))),v=!!(mask&(1<<(y<cx?0:2)));
  const diagonal=y<cx?(x<cx?7:4):(x<cx?6:5);
  if(h&&v&&(mask&(1<<diagonal))) return 64;
  if(h&&v) return Math.hypot(cx-dx,cx-dy)-(cx-radius);
  if(h) return radius-dy;
  if(v) return radius-dx;
  return radius-Math.hypot(dx,dy);
}

export function seaSwell(x,y,frame) {
  x=wrap(x/64)*64;y=wrap(y/64)*64;
  let crest=0;
  for(const [cx,cy,length] of [[15,12,13],[47,43,9]]) {
    const dx=wrap((x-cx+32)/64)*64-32;
    const curve=3*(dx/length)**2;
    const dy=wrap((y-cy-frame*4-curve+32)/64)*64-32;
    const envelope=Math.exp(-((dx/length)**4));
    crest=Math.max(crest,envelope*Math.exp(-((dy/1.8)**2)));
  }
  return .04+crest*.35;
}

export function surfStrength(distance,x,y,frame) {
  x=wrap(x/64)*64;y=wrap(y/64)*64;
  const age=wrap(frame/16);
  const front=18*(1-age);
  const breaking=clamp(1-Math.abs(distance-front)/2.5)*Math.sin(age*Math.PI);
  const fragments=.65+.35*Math.sin((x+y)*Math.PI/16+age*Math.PI*2)**2;
  return breaking*fragments;
}

export function seaPixel(mask,x,y,frame,base) {
  if(normalize(mask)!==mask) throw Error('Sea boundary requires a valid normalized mask');
  const distance=seaDistance(mask,x,y);
  if(distance< -1) return [0,0,255,255];
  if(distance<=1.5) return [189,167,121,255];
  const shallow=clamp(1-distance/20);
  const swell=seaSwell(x,y,frame);
  const foam=surfStrength(distance,x,y,frame);
  const deep=[base[0]*.55,base[1]*.74,base[2]*.9];
  return [...deep.map((v,c)=>{
    const water=v+(base[c]-v)*shallow*.55;
    const lit=water+([112,201,224][c]-water)*swell;
    return Math.round(lit+([233,252,249][c]-lit)*foam*.85);
  }),255];
}
