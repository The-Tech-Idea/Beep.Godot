(() => {
'use strict';
const COUNT=16,PERIOD=1.2;
const wrap=n=>((n%1)+1)%1;
const clamp=n=>Math.max(0,Math.min(1,n));
const blue=(d,i)=>d[i+2]>d[i]+18&&d[i+1]>d[i]+10&&d[i+2]>90&&d[i+2]>d[i+1]*0.88;
const readyAssets=[];
let playing=true,phase=0,last=0;
function build(config,image){
 const [cx,cy,w,h]=config.crop;
 const c=document.createElement('canvas');c.width=w;c.height=h;
 const ctx=c.getContext('2d');ctx.drawImage(image,cx,cy,w,h,0,0,w,h);
 const source=ctx.getImageData(0,0,w,h),d=source.data,mask=new Uint8Array(w*h);
 const surfaces=config.surfaces.map(([x,y,r,b])=>[x-cx,y-cy,r-cx,b-cy]);
 const falls=config.falls.map(([x,y,r,b])=>[x-cx,y-cy,r-cx,b-cy]);
 const splashes=config.splashes.map(([x,y,r,s])=>[x-cx,y-cy,r,s]);
 const pools=config.pools.map(([x,y,r,s])=>[x-cx,y-cy,r,s]);
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const n=y*w+x,i=n*4;
  const foamArea=falls.some(([l,t,r,b])=>x>l&&x<r&&y>t&&y<b+10)||splashes.some(([sx,sy,r])=>Math.abs(x-sx)<r&&Math.abs(y-sy)<35);
  const white=foamArea&&d[i]>210&&d[i+1]>225&&d[i+2]>230&&Math.max(d[i],d[i+1],d[i+2])-Math.min(d[i],d[i+1],d[i+2])<35;
  mask[n]=blue(d,i)||white?1:0;
 }
 function render(k){
  const p=k/COUNT,out=new ImageData(new Uint8ClampedArray(d),w,h);
  const blend=(x,y,color,a)=>{
   if(x<0||y<0||x>=w||y>=h||!mask[y*w+x]||a<=0)return;
   const i=(y*w+x)*4;a=clamp(a);
   for(let ch=0;ch<3;ch++)out.data[i+ch]=out.data[i+ch]*(1-a)+color[ch]*a;
  };
  function foam(x0,y0,rx,ry,a,color){
   for(let y=Math.floor(y0-ry-1);y<=Math.ceil(y0+ry+1);y++)for(let x=Math.floor(x0-rx-1);x<=Math.ceil(x0+rx+1);x++){
    const coverage=clamp((1-Math.hypot((x-x0)/rx,(y-y0)/ry))*3);
    blend(x,y,color,coverage*a);
   }
  }
  // Advect highlights downstream; the original terrain is never resampled.
  surfaces.forEach(([l,t,r,b],zone)=>{
   const count=Math.max(12,Math.round((r-l)*(b-t)/750));
   for(let s=0;s<count;s++){
    const age=wrap(p+s*0.61803398875+zone*0.17),x0=l+wrap(s*0.754877666)*Math.max(1,r-l),y0=t+age*(b-t);
    const length=4+s%6,a=Math.min(1,age*8,(1-age)*8)*0.65;
    for(let y=Math.floor(y0-2);y<=Math.ceil(y0+2);y++)for(let x=Math.floor(x0-length);x<=Math.ceil(x0+length);x++){
     blend(x,y,[224,253,255],clamp(1-Math.abs(y-y0)/1.6)*clamp(1-Math.abs(x-x0)/length)*a);
    }
   }
  });
  falls.forEach(([l,t,r,b],zone)=>{
   const count=Math.max(12,Math.round((r-l)/3.5));
   for(let s=0;s<count;s++){
    const age=wrap(p+s*0.61803398875+zone*0.21),x0=l+3+wrap(s*0.754877666)*Math.max(1,r-l-6),head=t+age*(b-t);
    const length=Math.min((b-t)*0.45,12+s*7%22),width=0.7+s%3*0.35,a=Math.min(1,age*12,(1-age)*12);
    for(let y=Math.max(Math.floor(t),Math.floor(head-length));y<=Math.min(b,Math.ceil(head));y++){
     const taper=clamp(1-(head-y)/length);
     for(let x=Math.floor(x0-width*2);x<=Math.ceil(x0+width*2);x++){
      blend(x,y,[210,249,255],Math.exp(-(((x-x0)/width)**2))*taper*a*0.48);
     }
    }
   }
  });
  splashes.forEach(([sx,sy,r,scale],zone)=>{
   const vertical=27*scale;
   for(let y=Math.floor(sy-vertical);y<sy+vertical;y++)for(let x=Math.floor(sx-r);x<sx+r;x++){
    const coverage=clamp(1-(((x-sx)/r)**2+((y-sy)/vertical)**2));
    blend(x,y,[54,188,220],clamp(coverage*4));
   }
   const count=Math.max(14,Math.round(r/5));
   for(let s=0;s<count;s++){
    const age=wrap(p+s*0.38196601125+zone*0.23),startX=sx-r*0.65+wrap(s*0.754877666)*r*1.3;
    const direction=startX<sx?-1:1,life=Math.min(1,age*10,(1-age)*7);
    foam(startX+direction*age*10*scale,sy-4*age*(1-age)*(15+s%8)*scale,
     (6+(1-age)*5)*scale,(5+(1-age)*7)*scale,life,[235,253,255]);
    const dx=startX+direction*age*(10+s%7)*scale,dy=sy-4*age*(1-age)*(24+s%13)*scale+age*6*scale;
    foam(dx,dy,(2.5+(1-age)*1.3)*scale,(3.5+(1-age)*1.5)*scale,life,[250,255,255]);
   }
   for(let s=0;s<count+8;s++){
    const age=wrap(p+s*0.61803398875+zone*0.13),startX=sx-r*0.8+wrap(s*0.754877666)*r*1.6;
    foam(startX+(startX-sx)*age*0.24,sy+age*15*scale,(8+age*7)*scale,(3+age*2)*scale,
     Math.min(1,age*10,(1-age)*8),[240,254,255]);
   }
  });
  pools.forEach(([sx,sy,radius,depth],zone)=>{
   for(let s=0;s<3;s++){
    const age=wrap(p+s/3+zone*0.19),rx=radius*(0.15+age*0.85),ry=2+age*depth,a=Math.min(1,age*8)*(1-age)*0.65;
    for(let y=Math.floor(sy);y<Math.min(h,sy+depth+3);y++)for(let x=Math.max(0,Math.floor(sx-radius));x<Math.min(w,sx+radius);x++){
     const radial=Math.hypot((x-sx)/rx,(y-sy)/ry),edge=clamp(1-Math.abs(radial-1)*ry/1.25);
     if((x+s*13)%31<22)blend(x,y,[212,250,255],edge*a);
    }
   }
  });
  return out;
 }
 const frames=Array.from({length:COUNT},(_,k)=>render(k));
 const endpoint=render(COUNT);let staticChanges=0,loopDifference=0,changedPixels=0;
 for(let i=0;i<d.length;i++){
  if(endpoint.data[i]!==frames[0].data[i])loopDifference++;
  if(!mask[Math.floor(i/4)])for(const f of frames)if(f.data[i]!==d[i])staticChanges++;
 }
 const regions={surfaces:[],falls:[],splashes:[]};
 const countRegion=([l,t,r,b])=>{
  let n=0;
  for(let y=Math.max(0,Math.floor(t));y<Math.min(h,b);y++)for(let x=Math.max(0,Math.floor(l));x<Math.min(w,r);x++){
   const i=(y*w+x)*4;if([0,1,2].some(ch=>frames[0].data[i+ch]!==frames[5].data[i+ch]))n++;
  }
  return n;
 };
 for(let n=0;n<w*h;n++)if([0,1,2].some(ch=>frames[0].data[n*4+ch]!==frames[5].data[n*4+ch]))changedPixels++;
 regions.surfaces=surfaces.map(countRegion);regions.falls=falls.map(countRegion);
 regions.splashes=splashes.map(([x,y,r,s])=>countRegion([x-r,y-27*s,x+r,y+27*s]));
 const exportSheet=()=>{
  const sheet=document.createElement('canvas');sheet.width=w*4;sheet.height=h*4;
  const sc=sheet.getContext('2d');frames.forEach((f,k)=>sc.putImageData(f,k%4*w,Math.floor(k/4)*h));
  return sheet.toDataURL('image/png');
 };
 return {config,frames,source,w,h,exportSheet,stats:{id:config.id,staticChanges,loopDifference,changedPixels,regions}};
}
async function boot(){
 const image=new Image();image.src=window.WATER_SOURCE;
 await image.decode();
 for(const config of window.WATER_ASSETS){
  const asset=build(config,image);readyAssets.push(asset);
  const section=document.createElement('section');
  const heading=document.createElement('h2');heading.textContent=config.title;
  const canvas=document.createElement('canvas');canvas.width=asset.w;canvas.height=asset.h;
  canvas.style.aspectRatio=asset.w+'/'+asset.h;
  asset.ctx=canvas.getContext('2d');
  const link=document.createElement('a');link.href=config.id+'_16frames.png';link.textContent='Sprite sheet';
  const resource=document.createElement('a');resource.href=config.id+'.tres';resource.textContent='Godot SpriteFrames';
  const nav=document.createElement('nav');nav.append(link,resource);
  section.append(heading,canvas,nav);document.getElementById('gallery').append(section);
  await new Promise(r=>setTimeout(r,0));
 }
 window.waterGallery={ready:true,assets:readyAssets,stats:readyAssets.map(a=>a.stats),show:k=>show(k),setPlaying:value=>{playing=value;updateButton();}};
 document.getElementById('status').textContent='8 animations / 16 frames each';
 document.getElementById('play').disabled=false;document.getElementById('step').disabled=false;
 requestAnimationFrame(tick);
}
function show(k){for(const a of readyAssets)a.ctx.putImageData(a.frames[k],0,0);document.getElementById('frame').value='Frame '+(k+1)+' / '+COUNT;}
function updateButton(){const b=document.getElementById('play');b.innerHTML=playing?'&#10074;&#10074;':'&#9654;';b.title=playing?'Pause':'Play';b.setAttribute('aria-label',b.title);}
document.getElementById('play').onclick=()=>{playing=!playing;updateButton();};
document.getElementById('step').onclick=()=>{playing=false;updateButton();phase=(Math.floor(phase*COUNT)+1)%COUNT/COUNT;show(Math.floor(phase*COUNT));};
document.getElementById('speed').oninput=e=>document.getElementById('rate').value=e.target.value+'x';
function tick(t){if(last&&playing)phase=wrap(phase+Math.min((t-last)/1000,0.1)*Number(document.getElementById('speed').value)/PERIOD);last=t;show(Math.floor(phase*COUNT)%COUNT);requestAnimationFrame(tick);}
boot().catch(e=>{document.getElementById('status').textContent=e.message;window.animationError=e.message;});
})();
