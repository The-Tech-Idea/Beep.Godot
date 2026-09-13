import {seaDistance} from './sea-motion.mjs';
import {SEA_INLETS} from './river-sea-connectors.mjs';
import {sectionPixel,SECTION_KINDS} from './river-sections.mjs';
const clamp=v=>Math.max(0,Math.min(1,v));
const fract=v=>v-Math.floor(v);
export function waveHash(x,y) {
  return fract(Math.sin(x*127.1+y*311.7)*43758.5453);
}

// Compact wavelets use map coordinates, not one repeated field per tile.
export function sharedSwell(x,y,frame) {
  const gx=Math.floor(x/128),gy=Math.floor(y/128);
  let result=0;
  for(let j=-1;j<=1;j++) for(let i=-1;i<=1;i++) {
    const a=gx+i,b=gy+j;
    const seed=waveHash(a,b),other=waveHash(a+19,b+7);
    const age=fract(frame/16+seed);
    const cx=(a+.15+.7*seed)*128;
    const cy=(b+.2+.6*other)*128+(age-.5)*24;
    const length=16+other*25,dx=(x-cx)/length;
    if(Math.abs(dx)>=1) continue;
    const dy=y-cy-(2+seed*4)*dx*dx;
    if(Math.abs(dy)>=3) continue;
    const fade=Math.sin(age*Math.PI)**2;
    result=Math.max(result,(1-dx*dx)**2*(1-Math.abs(dy)/3)*fade*(.16+other*.14));
  }
  return result;
}

function bankOrControl(distance,weight,tag) {
  if(distance < -1) return [0,0,255,255];
  if(distance<=1.5) return [189,167,121,255];
  return [Math.round(clamp(weight)*255),Math.round(clamp(distance/64)*255),tag,255];
}
export function seaControlPixel(mask,x,y) {
  return bankOrControl(seaDistance(mask,x,y),1,254);
}
export function depthControlPixel(mask,x,y) {
  let amount=mask===null?0:clamp(seaDistance(mask,x,y)/18);
  amount=amount*amount*(3-2*amount);
  return [Math.round(amount*255),0,249,255];
}
export function inletControlPixel(profile,x,y,frame,base) {
  return inletSectionControlPixel(profile,'narrow',x,y,frame,base);
}
export function inletSectionMask(profile,kind) {
  const index=SEA_INLETS.findIndex(p=>Object.keys(p).every(key=>p[key]===profile[key]));
  if(index<0||!SECTION_KINDS.includes(kind)) throw Error('Unknown shared-sea inlet section');
  if(kind==='narrow') return profile.mask;
  if(kind==='middle') return 255;
  const masks=[[127,239],[239,223],[191,223],[127,191]];
  return masks[index][kind==='low_bank'?0:1];
}
export function inletSectionControlPixel(profile,kind,x,y,frame,base) {
  const mask=inletSectionMask(profile,kind);
  const index=SEA_INLETS.findIndex(p=>p.id===profile.id);
  const t={north:y,east:64-x,south:64-y,west:x}[profile.port];
  if(t<=.5) return sectionPixel(kind,profile.incoming,x,y,frame,base);
  if(t>=8) return seaControlPixel(mask,x,y);
  const cross=index%2===0?x:y;
  let weight=clamp((t-.5)/7.5);
  weight=weight*weight*(3-2*weight);
  const low=kind==='narrow'||kind==='low_bank'?cross-10.94:100;
  const high=kind==='narrow'||kind==='high_bank'?53.06-cross:100;
  const riverDistance=Math.min(low,high);
  const distance=riverDistance+(seaDistance(mask,x,y)-riverDistance)*weight;
  if(distance < -1) return [0,0,255,255];
  if(distance<=1+weight*.5) return [189,167,121,255];
  return [Math.round(weight*255),Math.round(clamp(distance/64)*255),250+index,255];
}
