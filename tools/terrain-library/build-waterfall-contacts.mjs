import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {writeJson,digest} from './library.mjs';
const require=createRequire(import.meta.url);
let sharp;
try {sharp=require('sharp');} catch {sharp=require(path.join(process.env.TERRAIN_NODE_MODULES,'sharp'));}
const root=process.cwd();
const folder='addons/beep_game_builder_cs/generated/dev/cartoon/elevation/waterfall_contacts_v1/';
const specification=JSON.parse(fs.readFileSync(folder+'specification.json','utf8'));
const calibration=JSON.parse(fs.readFileSync(folder+'calibration.json','utf8'));
const source=fs.readFileSync(folder+specification.source);
if(digest(source)!==specification.sourceSha256) throw Error('Source changed; recalibration required');
const metadata=await sharp(source).metadata();
if(metadata.width!==calibration.sourceSize[0]||metadata.height!==calibration.sourceSize[1]) throw Error('Source size changed');
fs.mkdirSync(folder+'runtime',{recursive:true});
fs.mkdirSync(folder+'staging',{recursive:true});
const pieces=[];
for(const piece of calibration.pieces) {
  const contract=specification.pieceContracts.find(p=>p.id===piece.id);
  if(!contract) throw Error('Unknown contact');
  const [left,top,width,height]=piece.crop;
  const scale=contract.risePixels?contract.risePixels/(piece.sourceBaseY-piece.sourceAnchor[1]):piece.landWidthPixels/width;
  const origin=[contract.pivot[0]+(left-piece.sourceAnchor[0])*scale,contract.pivot[1]+(top-piece.sourceAnchor[1])*scale];
  if(origin.some(v=>v<-.01)||origin[0]+width*scale>contract.runtimeBounds[0]+.01||origin[1]+height*scale>contract.runtimeBounds[1]+.01) throw Error('Contact artwork exceeds declared bounds: '+piece.id);
  const keyed=await sharp(source).extract({left,top,width,height}).ensureAlpha().raw().toBuffer();
  for(let i=0;i<keyed.length;i+=4) if(keyed[i+1]>200&&keyed[i]<65&&keyed[i+2]<75) keyed.fill(0,i,i+4);
  const masterSize=contract.runtimeBounds.map(v=>v*2);
  const scaled=await sharp(keyed,{raw:{width,height,channels:4}}).resize(Math.round(width*scale*2),Math.round(height*scale*2)).png().toBuffer();
  const master=await sharp({create:{width:masterSize[0],height:masterSize[1],channels:4,background:'#00000000'}}).composite([{input:scaled,left:Math.round(origin[0]*2),top:Math.round(origin[1]*2)}]).png().toBuffer();
  const runtime=await sharp(master).resize(...contract.runtimeBounds).raw().toBuffer();
  let clipped=0,opaque=0;
  for(let y=0;y<contract.runtimeBounds[1];y++) for(let x=0;x<contract.runtimeBounds[0];x++) {
    const i=(y*contract.runtimeBounds[0]+x)*4;
    const protectedPixel=contract.waterSide==='right'?x+.5>contract.pivot[0]:x+.5<contract.pivot[0];
    if(protectedPixel&&runtime[i+3]) {clipped++;runtime.fill(0,i,i+4);}
    if(runtime[i+3]) opaque++;
  }
  if(opaque<10) throw Error('Empty contact export');
  const runtimeFile='runtime/'+piece.id+'.png';
  await sharp(runtime,{raw:{width:contract.runtimeBounds[0],height:contract.runtimeBounds[1],channels:4}}).png().toFile(folder+runtimeFile);
  await sharp(master).toFile(folder+'staging/'+piece.id+'_master.png');
  pieces.push({...contract,sourceCrop:piece.crop,sourceAnchor:piece.sourceAnchor,uniformScale:scale,unroundedOrigin:origin,runtime:runtimeFile,edgeFilterPixelsClipped:clipped,visiblePixels:opaque,approvalEvidence:null});
}
writeJson(root,folder+'manifest.json',{status:'calibration_candidate',source:specification.source,sourceSha256:digest(source),pieces,productionReady:false,approvalEvidence:null,pending:['source_anchor_review','alpha_edge_visual_review','Godot_contact_integration','isometric_artwork','visual_approval']});
console.log('Calibrated six contact candidates; protected water half-planes remain transparent.');
