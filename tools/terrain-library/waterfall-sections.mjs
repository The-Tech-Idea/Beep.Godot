import {sectionPixel,SECTION_KINDS} from './river-sections.mjs';

export const FALL_RISES = [16,32,64];
const clamp=v=>Math.max(0,Math.min(1,v));
const wrap=v=>((v%1)+1)%1;

export const laneNoise=(lane,salt=0)=>{
  const index=((lane%64)+64)%64;
  const value=Math.sin(index*127.1+salt*311.7)*43758.5453;
  return value-Math.floor(value);
};

export function sharedCurtainLight(x,y,frame) {
  let light=0;
  const center=Math.floor(x/6);
  for(let lane=center-2;lane<=center+2;lane++) {
    const dx=x-(lane*6+laneNoise(lane)*3);
    const cycles=2+Math.floor(laneNoise(lane,1)*2);
    const dy=(wrap(y/48-frame/16*cycles+laneNoise(lane,2))-.5)*48;
    const length=5+laneNoise(lane,3)*7;
    light=Math.max(light,Math.exp(-((dx/(.7+laneNoise(lane,4)*.5))**2+(dy/length)**2))*(.45+laneNoise(lane,5)*.4));
  }
  return light;
}

export function curtainLight(x,y,frame) {
  let light=0;
  for(let lane=0;lane<16;lane++) {
    const dx=((x-lane*4+96)%64)-32;
    const dy=((y-frame*3-lane*7+4800)%48)-24;
    light=Math.max(light,Math.exp(-((dx/0.8)**2+(dy/9)**2))*.8);
  }
  return light;
}

// Water-only front-facing connectors. Terrain is supplied by separate cliff art.
export function waterfallPixel(kind,rise,x,y,frame,base,surfaceOffset) {
  if(!SECTION_KINDS.includes(kind)||!FALL_RISES.includes(rise)) throw Error('Unsupported waterfall section');
  if(x<0||x>64||y<0||y>=128+rise) return [0,0,0,0];
  const distance=Math.min(kind==='narrow'||kind==='low_bank'?x-10.94:100,
    kind==='narrow'||kind==='high_bank'?53.06-x:100);
  if(distance<1) return [0,0,0,0];
  const shared=surfaceOffset!==undefined;
  if(shared&&!Number.isFinite(surfaceOffset)) throw Error('Invalid waterfall surface offset');
  const phaseX=shared?x+surfaceOffset:x%64;
  let color;
  if(y<64) color=sectionPixel(kind,'north_south',x,y,frame,base);
  else if(y>=64+rise) color=sectionPixel(kind,'north_south',x,y-64-rise,frame,base);
  else {
    const light=(shared?sharedCurtainLight:curtainLight)(phaseX,y-64,frame)*clamp(distance/4);
    color=[...base.map((v,c)=>Math.round(v*.84+([191,244,251][c]-v*.84)*light)),255];
  }
  let foam=0;
  // Fixed lip position with changing highlights; no displacement of the curtain.
  if(y>=59&&y<68) {
    const phase=Math.sin((phaseX*Math.PI/16-frame*Math.PI/4));
    foam=clamp(1-Math.abs(y-64)/5)*(0.45+0.25*phase);
  }
  const poolY=y-64-rise;
  if(poolY>=-Math.min(10,rise*.5)&&poolY<42) {
    // Staggered impact lobes rise, spread, and expire locally. Repeating x uses
    // global lane centers across section edges, so middles can be repeated.
    const center=shared?Math.floor(phaseX/8):4;
    for(let lane=center-6;lane<=center+6;lane++) {
      const age=wrap(frame/16+(shared?laneNoise(lane,7):((lane%8+8)%8)*0.38196601125));
      const cx=lane*8+age*3;
      const cy=-4*age*(1-age)*Math.min(8,rise*.3)+age*12;
      const life=Math.min(1,age*8,(1-age)*8);
      foam=Math.max(foam,clamp(1-Math.hypot((phaseX-cx)/(3+age*3),(poolY-cy)/(2+age*2)))*life);
    }
    const age=wrap(frame/16);
    const front=8+age*28;
    const broken=0.5+0.5*Math.sin(phaseX*Math.PI/8+age*Math.PI*2);
    foam=Math.max(foam,clamp(1-Math.abs(poolY-front)/2)*Math.sin(age*Math.PI)*broken*.5);
  }
  foam*=clamp(distance/4);
  return color.map((v,c)=>c===3?255:Math.round(v+([240,254,255][c]-v)*foam));
}
