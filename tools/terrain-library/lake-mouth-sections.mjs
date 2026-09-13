import {signedDistance} from './topology.mjs';
import {riverLight} from './river-motion.mjs';
import {sectionPixel} from './river-sections.mjs';
import {LAKE_CONNECTORS} from './river-lake-connectors.mjs';

export const LAKE_MOUTH_KINDS=['low_bank','middle','high_bank'];
const ports=['north','east','south','west'];
const masks=[[127,255,239],[239,255,223],[191,255,223],[127,255,191]];
export function lakeMouthMask(profile,kind) {
  const known=LAKE_CONNECTORS.find(p=>p.id===profile.id);
  if(!known||known.port!==profile.port||known.mask!==profile.mask||known.role!==profile.role||known.incoming!==profile.incoming||known.outgoing!==profile.outgoing) throw Error('Unknown lake mouth profile');
  const k=LAKE_MOUTH_KINDS.indexOf(kind);
  if(k<0) throw Error('Unknown lake mouth section');
  return masks[ports.indexOf(profile.port)][k];
}

export function lakeMouthPixel(profile,kind,x,y,frame,base,lakeColor) {
  const mask=lakeMouthMask(profile,kind);
  const t={north:y,east:64-x,south:64-y,west:x}[profile.port];
  const direction=profile.role==='inlet'?profile.incoming:profile.outgoing;
  if(t<=0.5) return sectionPixel(kind,direction,x,y,frame,base);
  const vertical=profile.port==='north'||profile.port==='south';
  const cross=vertical?x:y;
  const riverDistance=Math.min(kind==='low_bank'?cross-10.94:100,kind==='high_bank'?53.06-cross:100);
  let weight=Math.max(0,Math.min(1,(t-0.5)/7.5));
  weight=weight*weight*(3-2*weight);
  const distance=riverDistance+(signedDistance(mask,x-0.5,y-0.5)-riverDistance)*weight;
  if(distance < -1) return [0,0,255,255];
  if(distance<=1+weight*0.5) return [189,167,121,255];
  const along=vertical?y:x;
  const s=direction==='south_north'||direction==='east_west'?64-along:along;
  const light=riverLight(s,cross-32,frame)*Math.max(0,Math.min(1,distance/4));
  return [...base.map((value,c)=>{
    const river=Math.round(value+([181,239,247][c]-value)*light);
    return Math.round(river+(lakeColor[c]-river)*weight);
  }),255];
}

export const LAKE_MOUTH_MODULES=LAKE_CONNECTORS.flatMap((profile,row)=>LAKE_MOUTH_KINDS.map((kind,column)=>({
  ...profile,id:profile.id+'.'+kind,profileId:profile.id,kind,mask:lakeMouthMask(profile,kind),atlas:[column,row]
})));
