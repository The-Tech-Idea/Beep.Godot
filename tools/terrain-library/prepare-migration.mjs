import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {GENERATED,scan,walk,inside,digest,writeJson} from './library.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const packs=[
 {prefix:'mountains/low_poly_sandstone/authored_prefabs/modular_front_2_5d/',evidence:'docs/MOUNTAIN_PREFAB_1.md'},
 {prefix:'mountains/low_poly_sandstone/authored_prefabs/modular_themes/',evidence:'docs/MOUNTAIN_PREFAB_1.md'},
 {prefix:'mountains/natural_plateau/authored_prefabs/mountain_prefab_2/',evidence:'docs/MOUNTAIN_PREFAB_2.md'},
 {prefix:'hills/soft_grass/authored_prefabs/hill_prefab_1/',evidence:'docs/HILL_PREFAB_1.md'}
];
const audit=scan(root),files=walk(root,GENERATED),review=[];
for(const pack of packs){
 const selected=files.filter(f=>f.startsWith(`${GENERATED}/${pack.prefix}`));
 const entries=selected.map(file=>{
  const buffer=fs.readFileSync(inside(root,file));
  return {from:file,to:'legacy_art/terrain/'+file.slice(GENERATED.length+1),bytes:buffer.length,sha256:digest(buffer),
    incomingReferences:audit.edges.filter(e=>e.to===file).map(e=>e.from)};
 });
 review.push({family:pack.prefix,documentedUse:pack.evidence,approval:'review_required',
  note:'Documented prefab family. Every member still needs approval review; membership alone is not approval.',
  files:entries,dynamicReferenceSites:audit.dynamicSites.filter(s=>s.prefix.startsWith(`${GENERATED}/${pack.prefix}`)||`${GENERATED}/${pack.prefix}`.startsWith(s.prefix))});
}
writeJson(root,`${GENERATED}/dev/library/staging/legacy_migration_review.json`,{schemaVersion:1,approved:false,operation:'review_only',originalsChanged:false,
 summary:{files:review.reduce((n,p)=>n+p.files.length,0),bytes:review.reduce((n,p)=>n+p.files.reduce((a,f)=>a+f.bytes,0),0)},
 requiredChecks:['Review every file, including previews and source drafts.','Update dynamic and literal references in a separate reviewed migration.',
 'Preserve resource identity without leaving duplicate imported UIDs.','Load affected scenes before asking to remove superseded originals.'],packs:review});
console.log(JSON.stringify({status:'review_only',families:review.length,files:review.reduce((n,p)=>n+p.files.length,0),originalsChanged:false}));
