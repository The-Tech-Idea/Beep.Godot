import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {GENERATED,inside,digest,scan,references,validateProduction,quarantineCopy} from '../tools/terrain-library/library.mjs';
import {includeAddonPath} from '../tools/terrain-library/distribution.mjs';
import {lakeFrame} from '../tools/terrain-library/water-motion.mjs';
const fixture=t=>{const root=fs.mkdtempSync(path.join(os.tmpdir(),'terrain-library-'));t.after(()=>fs.rmSync(root,{recursive:true,force:true}));return root;};
const put=(root,p,content)=>{const dest=inside(root,p);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,content);};
test('local lake motion loops exactly and leaves tile boundaries fixed',()=>{
 const source=Buffer.alloc(64*64*4);for(let i=0;i<source.length;i+=4){source[i]=20;source[i+1]=170;source[i+2]=210;source[i+3]=255;}
 const frames=Array.from({length:17},(_,k)=>lakeFrame(source,k));
 assert.deepEqual(frames[0],frames[16]);assert.notDeepEqual(frames[0],frames[5]);
 let changed=0;
 for(let y=0;y<64;y++)for(let x=0;x<64;x++){
  const i=(y*64+x)*4;
  if(frames[0][i]!==frames[5][i])changed++;
  if(x===0||y===0||x===63||y===63)for(const f of frames)assert.deepEqual(f.subarray(i,i+4),source.subarray(i,i+4));
 }
 assert.ok(changed>30&&changed<700,`Motion should remain local: ${changed}`);
});
test('paths cannot escape root or use Windows absolute paths',()=>{
 for(const p of ['../outside','C:/outside','a/../../outside','a\\b'])assert.throws(()=>inside(process.cwd(),p));
});
test('installer excludes artwork development and tests, not production',()=>{
 for(const p of ['generated/dev/cartoon/a.png','C:\\repo\\generated\\test\\x','legacy_art/terrain/a.png','.art_quarantine/x'])assert.equal(includeAddonPath(p),false);
 assert.equal(includeAddonPath('generated/production/cartoon/a.png'),true);
 assert.equal(includeAddonPath('ecs/terrain/test_helper.cs'),true);
});
test('reference scan handles resource, relative, escaped and dynamic paths',()=>{
 const png=`${GENERATED}/water/a.png`,known=new Set([png]);
 const r=references(`${GENERATED}/water/scene.tscn`,`"res://${png}" "a.png" "${png.replaceAll('/','\\/')}" "res://${GENERATED}/water/{theme}/asset.png"`,known);
 assert.equal(r.refs.length,1);assert.ok(r.dynamic.includes(`${GENERATED}/water/`));
});
test('duplicates and approved filenames are never deletion approval',t=>{
 const root=fixture(t);put(root,`${GENERATED}/old/approved.png`,'same');put(root,`${GENERATED}/old/copy.png`,'same');
 put(root,'scene.tscn',`path="res://${GENERATED}/old/approved.png"`);
 const report=scan(root);assert.equal(report.duplicates.length,1);
 assert.equal(report.assets[0].status,'unresolved');assert.equal(report.assets[0].references.length,1);
});
test('empty production is explicitly incomplete',t=>{
 const result=validateProduction(fixture(t));assert.equal(result.ok,true);assert.equal(result.productionPacks,0);assert.match(result.note,/not mean production is complete/);
});
test('production follows external resource dependencies and relative paths',t=>{
 const root=fixture(t),p=`${GENERATED}/production/cartoon/water`;
 put(root,`${p}/manifest.json`,JSON.stringify({status:'approved',approval:{reviewedBy:'fixture',reviewedAt:'2026-09-12'},assets:[]}));
 put(root,`${p}/water.tres`,'path="res://shared.tres"');
 put(root,'shared.tres','path="legacy_art/water.png"');put(root,'legacy_art/water.png','fixture');
 assert.equal(validateProduction(root).ok,false);
});
test('production rejects missing approval, non-production resources and orphan atlases',t=>{
 const root=fixture(t),p=`${GENERATED}/production/cartoon/water`;
 put(root,`${p}/manifest.json`,JSON.stringify({status:'candidate',assets:[{file:'../../../../dev/art.png'}]}));
 put(root,`${p}/godot/water.tres`,'path="res://legacy_art/terrain/water.png"');
 put(root,`${GENERATED}/production/pixel/other.png`,'test');
 const result=validateProduction(root);assert.equal(result.ok,false);assert.ok(result.errors.length>=4);
});
test('quarantine rejects unapproved, stale and protected selections',t=>{
 const root=fixture(t),p=`${GENERATED}/old/test.png`;put(root,p,'original');
 const selection={approved:true,approvedBy:'test reviewer',files:[{path:p,sha256:digest(Buffer.from('original')),reason:'fixture'}]};
 assert.throws(()=>quarantineCopy(root,{...selection,approved:false},'batch'));
 assert.throws(()=>quarantineCopy(root,{...selection,files:[{...selection.files[0],sha256:'stale'}]},'batch'));
 assert.throws(()=>quarantineCopy(root,{...selection,files:[{...selection.files[0],path:`${GENERATED}/production/a.png`}]},'batch'));
 assert.equal(fs.existsSync(inside(root,'.art_quarantine/batch')),false);
});
test('quarantine copies exact bytes, keeps original and refuses overwrite',t=>{
 const root=fixture(t),p=`${GENERATED}/old/test.png`;put(root,p,'original');
 const selection={approved:true,approvedBy:'test reviewer',files:[{path:p,sha256:digest(Buffer.from('original')),reason:'fixture'}]};
 const result=quarantineCopy(root,selection,'batch');assert.equal(result.originalsRemoved,false);
 assert.equal(fs.readFileSync(inside(root,p),'utf8'),'original');
 assert.equal(fs.readFileSync(inside(root,`.art_quarantine/batch/files/${p}`),'utf8'),'original');
 assert.throws(()=>quarantineCopy(root,selection,'batch'));
});
