const clamp=n=>Math.max(0,Math.min(1,n));
const wrap=n=>((n%1)+1)%1;
const ripples=[[13,12,6],[43,9,5],[29,28,7],[51,39,5],[12,48,6],[36,53,6]];

export function lakeFrame(texture,frame,size=64){
  const mean=[0,0,0];
  for(let i=0;i<texture.length;i+=4)for(let c=0;c<3;c++)mean[c]+=texture[i+c]/(size*size);
  const out=Buffer.alloc(texture.length),phase=wrap(frame/16);
  for(let y=0;y<size;y++)for(let x=0;x<size;x++){
    const i=(y*size+x)*4;
    // Stationary substrate; only sparse local highlights change, never UV coordinates.
    let light=0;
    for(let r=0;r<ripples.length;r++){
      const [cx,cy,length]=ripples[r],age=wrap(phase+r/ripples.length);
      const nx=(x-cx)/(length*(0.8+0.2*age));
      if(Math.abs(nx)>=1)continue;
      const centerY=cy+age*2.5+0.7*nx*nx;
      const line=clamp(1-Math.abs(y-centerY)/1.1);
      light+=line*(1-nx*nx)*Math.sin(Math.PI*age)**2*0.45;
    }
    light*=clamp(Math.min(x,y,size-1-x,size-1-y)/5);
    for(let c=0;c<3;c++){
      const base=mean[c]+(texture[i+c]-mean[c])*0.18;
      out[i+c]=Math.round(base+([181,239,247][c]-base)*clamp(light));
    }
    out[i+3]=255;
  }
  return out;
}
