import {signedDistance} from './topology.mjs';
import {riverLight} from './river-motion.mjs';

export const LAKE_PORTS = [
  {port:'north',mask:111,incoming:'north_south',outgoing:'south_north'},
  {port:'east',mask:207,incoming:'east_west',outgoing:'west_east'},
  {port:'south',mask:159,incoming:'south_north',outgoing:'north_south'},
  {port:'west',mask:63,incoming:'west_east',outgoing:'east_west'}
];
export const LAKE_CONNECTORS = LAKE_PORTS.flatMap(p=>['inlet','outlet'].map(role=>({...p,role,id:`${p.port}_${role}`})));
const clamp=v=>Math.max(0,Math.min(1,v));
export function lakeConnectorPixel(profile,x,y,frame,base,lakeColor) {
  if(!LAKE_CONNECTORS.some(p=>p.id===profile.id&&p.mask===profile.mask)) throw Error('Unknown explicit lake connector');
  const t={north:y,east:64-x,south:64-y,west:x}[profile.port];
  const vertical=profile.port==='north'||profile.port==='south';
  const cross=vertical?x:y;
  const riverDistance=Math.min(cross-10.94,53.06-cross);
  let weight=clamp((t-.5)/7.5);
  weight=weight*weight*(3-2*weight);
  const lakeDistance=signedDistance(profile.mask,x-.5,y-.5);
  const distance=riverDistance+(lakeDistance-riverDistance)*weight;
  if(distance < -1) return [0,0,255,255];
  if(distance <= 1+weight*.5) return [189,167,121,255];
  const direction=profile.role==='inlet'?profile.incoming:profile.outgoing;
  const along=vertical?y:x;
  const s=direction==='south_north'||direction==='east_west'?64-along:along;
  const light=riverLight(s,cross-32,frame)*clamp(distance/4);
  return [...base.map((v,c)=>{
    const river=Math.round(v+([181,239,247][c]-v)*light);
    return Math.round(river+(lakeColor[c]-river)*weight);
  }),255];
}
