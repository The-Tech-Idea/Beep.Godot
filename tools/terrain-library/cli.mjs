import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {GENERATED,STYLES,PACKS,inside,scan,writeJson,validateProduction,quarantineCopy} from './library.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const [command,...args]=process.argv.slice(2);
const reportRoot=`${GENERATED}/test/library/output`;
function scaffold(){
  for(const style of STYLES)for(const pack of PACKS){
    for(const sub of ['experiments','staging'])fs.mkdirSync(inside(root,`${GENERATED}/dev/${style}/${pack}/${sub}`),{recursive:true});
    for(const sub of ['fixtures','output'])fs.mkdirSync(inside(root,`${GENERATED}/test/${style}/${pack}/${sub}`),{recursive:true});
    fs.writeFileSync(inside(root,`${GENERATED}/test/${style}/${pack}/output/.gdignore`),'');
    const plan=`${GENERATED}/dev/${style}/${pack}/pack.json`;
    if(!fs.existsSync(inside(root,plan)))writeJson(root,plan,{schemaVersion:1,id:`${style}.${pack}.v1`,style,pack,status:'planned',gameCell:64,masterCell:style==='pixel'?32:128,assets:[],approval:null});
  }
  for(const style of STYLES)fs.mkdirSync(inside(root,`${GENERATED}/production/${style}`),{recursive:true});
  fs.mkdirSync(inside(root,'legacy_art/terrain'),{recursive:true});
  fs.mkdirSync(inside(root,'.art_quarantine'),{recursive:true});
  fs.writeFileSync(inside(root,'.art_quarantine/.gdignore'),'');
  fs.mkdirSync(inside(root,reportRoot),{recursive:true});
  fs.writeFileSync(inside(root,`${reportRoot}/.gdignore`),'');
  console.log('Created non-production work areas. No source assets moved or approved.');
}
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
function audit(){
  const inventory=scan(root);writeJson(root,`${reportRoot}/inventory.json`,inventory);
  writeJson(root,`${reportRoot}/review.json`,{schemaVersion:1,approved:false,files:[],duplicateGroups:inventory.duplicates,
    instruction:'Duplicates are review groups, not deletions. Add exact paths, sha256 and reason only after reference/approval review.'});
  const output=inside(root,`${reportRoot}/catalog.html`);
  const rows=inventory.assets.map(a=>{
    const url=path.relative(path.dirname(output),inside(root,a.path)).split(path.sep).map(encodeURIComponent).join('/');
    return `<article data-search="${escape(a.path.toLowerCase())}"><a href="${url}"><img loading="lazy" src="${url}" alt=""></a><strong>${escape(path.basename(a.path))}</strong><p>${escape(a.status)} | ${a.dimensions?.join(' x ')} | ${a.references.length} literal refs | ${a.dynamicReferenceSites.length} possible dynamic refs</p><small>${escape(a.path)}</small></article>`;
  }).join('\n');
  fs.writeFileSync(output,`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Terrain asset audit</title><style>body{background:#f2f5f4;color:#202624;font:14px system-ui;margin:20px}input{padding:10px;width:min(600px,90%)}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:16px;margin-top:20px}article{border:1px solid #bdc8c3;padding:12px;border-radius:4px;min-width:0}img{width:100%;height:180px;object-fit:contain;background:#24322d}small,strong{overflow-wrap:anywhere}p{font-size:12px}</style><h1>Terrain asset audit</h1><p>${inventory.summary.images} images. Unresolved means retain. Reference counts are evidence, not approval.</p><input id="q" placeholder="Filter by folder or name" aria-label="Filter assets"><main>${rows}</main><script>document.getElementById('q').oninput=e=>{const q=e.target.value.toLowerCase();for(const a of document.querySelectorAll('article'))a.hidden=!a.dataset.search.includes(q)};</script></html>`);
  console.log(JSON.stringify(inventory.summary));console.log(`${reportRoot}/catalog.html`);
}
try{
  if(command==='init')scaffold();
  else if(command==='audit')audit();
  else if(command==='validate'){const result=validateProduction(root);console.log(JSON.stringify(result,null,2));if(!result.ok)process.exitCode=1;}
  else if(command==='quarantine-copy')console.log(quarantineCopy(root,JSON.parse(fs.readFileSync(inside(root,args[0]),'utf8')),args[1]));
  else throw Error('Commands: init | audit | validate | quarantine-copy <reviewed-selection.json> <batch>. No deletion command is provided.');
}catch(error){console.error(error.message);process.exitCode=1;}
