import {riverLight} from './river-motion.mjs';

const clamp = value => Math.max(0,Math.min(1,value));
const wrap = value => ((value%64)+64)%64;

// A two-cell-long bank adapter adds/removes one repeatable middle column.
// Widths are footprints; each outside bank reserves 10.94 game pixels.
export function transitionBoundary(fromWidth,toWidth,along) {
  if (![fromWidth,toWidth].every(width=>Number.isInteger(width)&&width>=1) || Math.abs(fromWidth-toWidth)!==1)
    throw Error('Width adapters connect adjacent positive cell widths');
  const t=clamp(along/128),eased=t*t*(3-2*t);
  return {low:10.94,high:(fromWidth+(toWidth-fromWidth)*eased)*64-10.94};
}

export function transitionPixel(fromWidth,toWidth,across,along,frame,base) {
  const boundary=transitionBoundary(fromWidth,toWidth,along);
  const distance=Math.min(across-boundary.low,boundary.high-across);
  if(distance < -1) return [0,0,255,255];
  if(distance < 1) return [189,167,121,255];
  const light=riverLight(along,wrap(across)-32,frame)*Math.min(1,distance/4);
  return [...base.map((value,channel)=>Math.round(value+([181,239,247][channel]-value)*light)),255];
}
