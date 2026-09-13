import {seaDistance,seaPixel} from './sea-motion.mjs';
import {sectionPixel} from './river-sections.mjs';
import {riverLight} from './river-motion.mjs';

export const SEA_INLETS = [
  {id:'north_inlet',port:'north',mask:111,incoming:'north_south'},
  {id:'east_inlet',port:'east',mask:207,incoming:'east_west'},
  {id:'south_inlet',port:'south',mask:159,incoming:'south_north'},
  {id:'west_inlet',port:'west',mask:63,incoming:'west_east'}
];
const clamp=v=>Math.max(0,Math.min(1,v));
export function seaInletPixel(profile,x,y,frame,base) {
  if(!SEA_INLETS.some(p=>Object.keys(p).every(key=>p[key]===profile[key])))
    throw Error('Unknown explicit sea inlet');
  const t={north:y,east:64-x,south:64-y,west:x}[profile.port];
  if(t<=.5) return sectionPixel('narrow',profile.incoming,x,y,frame,base);
  if(t>=8) return seaPixel(profile.mask,x,y,frame,base);
  const cross=profile.port==='north'||profile.port==='south'?x:y;
  let weight=clamp((t-.5)/7.5);
  weight=weight*weight*(3-2*weight);
  const riverDistance=Math.min(cross-10.94,53.06-cross);
  const distance=riverDistance+(seaDistance(profile.mask,x,y)-riverDistance)*weight;
  if(distance < -1) return [0,0,255,255];
  if(distance <= 1+weight*.5) return [189,167,121,255];
  const light=riverLight(t,cross-32,frame)*clamp(distance/4);
  const sea=seaPixel(profile.mask,x,y,frame,base);
  return [...base.map((value,c)=>{
    const river=Math.round(value+([181,239,247][c]-value)*light);
    return Math.round(river+(sea[c]-river)*weight);
  }),255];
}
