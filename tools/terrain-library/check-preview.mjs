import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {createRequire} from 'node:module';
import {GENERATED,inside,writeJson} from './library.mjs';
const require=createRequire(import.meta.url);let playwright;
try{playwright=require('playwright')}catch{playwright=require(path.join(process.env.TERRAIN_NODE_MODULES,'playwright'))}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const out=`${GENERATED}/test/cartoon/water/output`;
fs.mkdirSync(inside(root,out),{recursive:true});
const browser=await playwright.chromium.launch({headless:true,...(process.env.TERRAIN_BROWSER?{executablePath:process.env.TERRAIN_BROWSER}:{})});
try{
 const page=await browser.newPage({viewport:{width:1280,height:1000}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto(pathToFileURL(inside(root,`${GENERATED}/dev/cartoon/water/staging/index.html`)).href);
 await page.waitForFunction(()=>window.foundationReview?.ready);
 const first=await page.evaluate(()=>foundationReview.frame());await page.waitForTimeout(220);
 if(first===await page.evaluate(()=>foundationReview.frame()))throw Error('Playback is not advancing');
 const results=[];
 for(let i=0;i<4;i++){
  await page.evaluate(i=>{foundationReview.select(i);foundationReview.show(0)},i);
  const a=await page.evaluate(()=>document.getElementById('map').toDataURL());
  await page.evaluate(()=>foundationReview.show(5));
  const b=await page.evaluate(()=>document.getElementById('map').toDataURL());
  if((i===0&&a!==b)||(i>0&&a===b))throw Error('Unexpected frame behaviour');
  results.push({transition:i,changes:a!==b});
 }
 await page.evaluate(()=>{foundationReview.select(1);foundationReview.show(0)});
 await page.screenshot({path:inside(root,`${out}/foundation_desktop.png`),fullPage:true});
 await page.setViewportSize({width:390,height:844});
 if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Mobile overflow');
 await page.screenshot({path:inside(root,`${out}/foundation_mobile.png`),fullPage:true});
 const pixels=await page.evaluate(()=>{const c=document.getElementById('map'),d=c.getContext('2d').getImageData(0,0,c.width,c.height).data;let n=0;for(let i=0;i<d.length;i+=4)if(d[i+3]===255)n++;return n;});
 if(pixels!==1152*768||errors.length)throw Error('Canvas incomplete or browser errors');
 writeJson(root,`${out}/browser_validation.json`,{status:'passed',results,mobileOverflow:false,opaquePixels:pixels,errors,visualApproval:false});
 console.log('Browser pass: four sets, live water playback, static dirt, opaque canvas and responsive layout.');
}finally{await browser.close()}
