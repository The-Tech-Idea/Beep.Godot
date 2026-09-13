import {waterfallPixel,FALL_RISES} from './waterfall-sections.mjs';
import {SECTION_KINDS} from './river-sections.mjs';

export const ISO_FALL_DIRECTIONS=['north_south','west_east'];

// Project the physical water path, folding only its vertical face at the lip.
// along=0..64 is upstream, 64..128 is downstream; drop is vertical game pixels.
export function projectFallPoint(direction,cross,along,drop=0) {
  if(!ISO_FALL_DIRECTIONS.includes(direction)) throw Error('Unsupported visible waterfall direction');
  const x=64+(cross-along)/2;
  return [direction==='north_south'?x:96-x,(cross+along)/4+drop];
}

export function isometricWaterfallSample(kind,direction,rise,sx,sy,frame,base,surfaceOffset) {
  if(!SECTION_KINDS.includes(kind)||!ISO_FALL_DIRECTIONS.includes(direction)||!FALL_RISES.includes(rise)) throw Error('Unsupported isometric waterfall profile');
  const x=direction==='north_south'?sx:96-sx;
  const cross=2*(x-32),lip=16+cross/4;
  if(cross>=0&&cross<64&&sy>=lip&&sy<lip+rise) {
    return {role:'curtain',rgba:waterfallPixel(kind,rise,cross,64+sy-lip,frame,base,surfaceOffset)};
  }
  const upstreamCross=x-64+2*sy,upstreamAlong=2*sy-(x-64);
  if(upstreamCross>=0&&upstreamCross<64&&upstreamAlong>=0&&upstreamAlong<64) {
    return {role:'upstream',rgba:waterfallPixel(kind,rise,upstreamCross,upstreamAlong,frame,base,surfaceOffset)};
  }
  const downstreamCross=x-64+2*(sy-rise),downstreamAlong=2*(sy-rise)-(x-64);
  if(downstreamCross>=0&&downstreamCross<64&&downstreamAlong>=64&&downstreamAlong<128) {
    return {role:'pool',rgba:waterfallPixel(kind,rise,downstreamCross,downstreamAlong+rise,frame,base,surfaceOffset)};
  }
  return {role:'outside',rgba:[0,0,0,0]};
}
