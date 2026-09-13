import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {GENERATED,inside} from './library.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const riverMode=process.argv.includes('--river');
const base=`${GENERATED}/dev/cartoon/water/${riverMode?'river_staging':'staging'}`;
const output=`${GENERATED}/test/cartoon/water/output${riverMode?'/river':''}`;
const fixture=inside(root,`${output}/godot_fixture`),target=inside(fixture,base);
if(!process.env.TERRAIN_GODOT)throw Error('Set TERRAIN_GODOT to your Godot console executable.');
fs.mkdirSync(target,{recursive:true});
fs.cpSync(inside(root,`${base}/runtime`),path.join(target,'runtime'),{recursive:true});
fs.copyFileSync(inside(root,`${base}/manifest.json`),path.join(target,'manifest.json'));
fs.copyFileSync(inside(root,`tools/terrain-library/package-${riverMode?'river':'foundation'}.gd`),path.join(fixture,'package-foundation.gd'));
fs.writeFileSync(path.join(fixture,'project.godot'),'config_version=5\n[application]\nconfig/name="Terrain Foundation Validation"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n');
const env={...process.env,APPDATA:path.join(fixture,'userdata')};
for(const [name,args] of [['import',['--headless','--editor','--path',fixture,'--import','--quit']],['package',['--headless','--path',fixture,'--script','res://package-foundation.gd']]]){
 const run=spawnSync(process.env.TERRAIN_GODOT,args,{encoding:'utf8',env,timeout:120000});
 const log=(run.stdout??'')+(run.stderr??'');fs.writeFileSync(inside(root,`${output}/godot_${name}.log`),log);
 if(run.status!==0||/SCRIPT ERROR|Parse Error|Assertion failed/.test(log))throw Error(`Godot ${name} failed; inspect log.`);
 if(name==='package'&&!log.includes(riverMode?'RIVER PASS':'FOUNDATION PASS'))throw Error('Missing success marker');
}
const report=JSON.parse(fs.readFileSync(path.join(fixture,'godot_validation.json'),'utf8'));
if(report.status!=='passed'||(riverMode?report.profiles!==12:report.packs.length!==4))throw Error('Invalid Godot report');
fs.cpSync(path.join(target,'godot'),inside(root,`${base}/godot`),{recursive:true});
fs.copyFileSync(path.join(fixture,'godot_validation.json'),inside(root,`${output}/godot_validation.json`));
console.log(riverMode?'Godot river import, reload, 12 directional animations and explicitly placed example passed.':'Godot import, reload, animation metadata and autoterrain examples passed; candidate resources copied to staging.');
