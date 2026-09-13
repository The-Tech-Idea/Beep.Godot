import {signedDistance} from './topology.mjs';
const wrap=(v,p)=>((v%p)+p)%p;
const clamp=v=>Math.max(0,Math.min(1,v));
export const RIVER_PROFILES=[
 {id:'north_south',mask:5,straight:'y',reverse:false},
 {id:'south_north',mask:5,straight:'y',reverse:true},
 {id:'west_east',mask:10,straight:'x',reverse:false},
 {id:'east_west',mask:10,straight:'x',reverse:true},
 {id:'north_east',mask:3,center:[64,0],angle:Math.PI,turn:-Math.PI/2},
 {id:'east_north',mask:3,center:[64,0],angle:Math.PI/2,turn:Math.PI/2},
 {id:'north_west',mask:9,center:[0,0],angle:0,turn:Math.PI/2},
 {id:'west_north',mask:9,center:[0,0],angle:Math.PI/2,turn:-Math.PI/2},
 {id:'west_south',mask:12,center:[0,64],angle:-Math.PI/2,turn:Math.PI/2},
 {id:'south_west',mask:12,center:[0,64],angle:0,turn:-Math.PI/2},
 {id:'south_east',mask:6,center:[64,64],angle:Math.PI,turn:Math.PI/2},
 {id:'east_south',mask:6,center:[64,64],angle:-Math.PI/2,turn:-Math.PI/2}
];
export function flowCoordinates(profile,x,y){
 if(profile.straight){const s=profile.straight==='x'?x:y,n=(profile.straight==='x'?y:x)-32;return [profile.reverse?64-s:s,n];}
 const dx=x-profile.center[0],dy=y-profile.center[1];
 const angle=wrap(Math.atan2(dy,dx)-profile.angle+Math.PI,Math.PI*2)-Math.PI;
 return [angle/profile.turn*64,Math.hypot(dx,dy)-32];
}
export function riverLight(s,n,frame){
 let light=0;
 // Two crests per cell cross its boundary with continuous phase. Opposite lane
 // offsets use the same phase so bend orientation cannot introduce a join jump.
 for(const lane of [-13,-4,4,13]){
  const ds=wrap(s-frame*2-Math.abs(lane)*1.7+16,32)-16,dn=n-lane;
  light+=Math.exp(-((ds/1.25)**2+(dn/3.2)**2))*0.42;
 }
 return clamp(light);
}
export function riverFrame(grass,water,profile,frame){
 const out=Buffer.alloc(64*64*4),avg=[0,0,0];
 for(let i=0;i<water.length;i+=4)for(let c=0;c<3;c++)avg[c]+=water[i+c]/4096;
 for(let y=0;y<64;y++)for(let x=0;x<64;x++){
  const i=(y*64+x)*4,s=signedDistance(profile.mask,x,y),coverage=clamp(s+0.5);
  const [along,across]=flowCoordinates(profile,x+0.5,y+0.5);
  const light=riverLight(along,across,frame)*clamp(s/4);
  for(let c=0;c<3;c++){
   const base=avg[c]+(water[i+c]-avg[c])*0.18;
   const moving=base+([181,239,247][c]-base)*light;
   let value=grass[i+c]+(moving-grass[i+c])*coverage;
   if(s>-3.5&&s<0)value=value*0.1+[223,184,114][c]*0.9;
   if(s>-0.8&&s<0.7)value=value*0.45+[174,231,221][c]*0.55;
   out[i+c]=Math.round(value);
  }out[i+3]=255;
 }
 return out;
}
