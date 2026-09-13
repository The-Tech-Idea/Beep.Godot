import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

export const GENERATED = 'addons/beep_game_builder_cs/generated';
export const STYLES = ['cartoon', 'pixel'];
export const PACKS = ['ground', 'water', 'elevation', 'mountains_hills', 'access', 'landforms', 'accessories'];
const OMIT = new Set(['.git', '.godot', '.vs', 'node_modules', 'bin', 'obj', '.art_quarantine']);
const TEXT = new Set(['.cs', '.gd', '.gdshader', '.tscn', '.tres', '.json', '.html', '.js', '.mjs', '.cjs', '.py', '.ps1', '.md', '.import', '.godot', '.cfg']);
export const slash = s => s.replaceAll('\\', '/');
export const digest = buffer => crypto.createHash('sha256').update(buffer).digest('hex');
export function inside(root, relative) {
  if (path.isAbsolute(relative) || /^[a-z]:/i.test(relative) || relative.includes('\\')) throw Error(`Not a relative path: ${relative}`);
  const result = path.resolve(root, relative), rel = path.relative(path.resolve(root), result);
  if (rel === '..' || rel.startsWith(`..${path.sep}`)) throw Error(`Path escapes root: ${relative}`);
  // Never follow an existing junction/symlink for copying or writing library data.
  let current = path.resolve(root);
  for (const part of rel.split(path.sep).filter(Boolean)) {
    current = path.join(current, part);
    if (fs.existsSync(current) && fs.lstatSync(current).isSymbolicLink()) throw Error(`Linked path: ${current}`);
  }
  return result;
}
export function walk(root, relative = '') {
  const dir = inside(root, relative);
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, {withFileTypes:true}).flatMap(e => {
    if (e.isSymbolicLink() || OMIT.has(e.name.toLowerCase())) return [];
    const rel = relative ? `${relative}/${e.name}` : e.name;
    return e.isDirectory() ? walk(root, rel) : [rel];
  }).sort();
}
export function writeJson(root, relative, value) {
  const dest = inside(root, relative); fs.mkdirSync(path.dirname(dest), {recursive:true});
  fs.writeFileSync(dest, JSON.stringify(value, null, 2) + '\n');
}
export function pngSize(buffer) {
  return buffer.length >= 24 && buffer.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10]))
    ? [buffer.readUInt32BE(16), buffer.readUInt32BE(20)] : null;
}
export function references(file, text, known) {
  const normalized = slash(text.replaceAll('\\/', '/'));
  const refs = new Map();
  for (const match of normalized.matchAll(/(?:res:\/\/[^\s"'<>]+|["']([^"'\n]+)["'])/g)) {
    let token = match[1] ?? match[0];
    if (token.startsWith('data:') || token.length > 1500 || token.includes('${') || token.includes('{')) continue;
    token = token.split(/[?#]/)[0];
    let candidate;
    if (token.startsWith('res://')) candidate = token.slice(6);
    else if (token.startsWith('addons/') || token.startsWith('legacy_art/')) candidate = token;
    else candidate = path.posix.normalize(path.posix.join(path.posix.dirname(file), token));
    if (known.has(candidate)) refs.set(candidate, {path:candidate, kind:'literal'});
  }
  // A family prefix is evidence of possible dynamic usage, not proof of an exact dependency.
  const dynamic = [...normalized.matchAll(/(?:res:\/\/)?(?:addons\/beep_game_builder_cs\/generated|legacy_art)\/[\w/.-]*/g)]
    .map(m => m[0].replace(/^res:\/\//, '')).filter(p => !known.has(p));
  return {refs:[...refs.values()], dynamic:[...new Set(dynamic)]};
}
export function scan(root) {
  const all = walk(root), known = new Set(all);
  const library = all.filter(p => p.startsWith(`${GENERATED}/`) || p.startsWith('legacy_art/'));
  const images = library.filter(p => p.endsWith('.png') && !p.includes('/test/') && !p.includes('/output/'));
  const assets = images.map(p => {
    const b = fs.readFileSync(inside(root,p));
    return {path:p, bytes:b.length, sha256:digest(b), dimensions:pngSize(b),
      status:p.startsWith('legacy_art/') ? 'legacy_review' : p.includes('/production/') ? 'production_review' : p.includes('/dev/') ? 'active_development' : 'unresolved',
      approvalEvidence:/approved/i.test(path.posix.basename(p)) ? ['Filename contains approved; verify provenance before promotion or relocation.'] : [],
      references:[], dynamicReferenceSites:[]};
  });
  const index = new Map(assets.map(a => [a.path,a])), dynamicSites = [], edges = [];
  for (const file of all) {
    if (!TEXT.has(path.extname(file)) || file.endsWith('/inventory.json') || file.endsWith('/review.json') || file.endsWith('/catalog.html') || file.includes('/test/library/')) continue;
    const text = fs.readFileSync(inside(root,file),'utf8');
    const result = references(file,text,known);
    const kind = /\.(md|html)$/.test(file) || /provenance|catalog/i.test(file) ? 'documentation' : 'resource_or_code';
    for (const ref of result.refs) { edges.push({from:file,to:ref.path,kind}); index.get(ref.path)?.references.push({from:file,kind}); }
    for (const prefix of result.dynamic) {
      dynamicSites.push({from:file,prefix});
      for (const a of assets) if (a.path.startsWith(prefix)) a.dynamicReferenceSites.push(file);
    }
  }
  const hashes = new Map();
  for (const a of assets) { if (!hashes.has(a.sha256)) hashes.set(a.sha256,[]); hashes.get(a.sha256).push(a.path); }
  const duplicates = [...hashes.entries()].filter(([,paths])=>paths.length>1).map(([sha256,paths])=>({sha256,paths}));
  return {schemaVersion:1,generatedAt:new Date().toISOString(),readOnlyAudit:true,
    summary:{images:assets.length,bytes:assets.reduce((s,a)=>s+a.bytes,0),exactDuplicateGroups:duplicates.length},
    limitations:['Dynamic paths and external consumers require manual review. No unused-file claim is made.',
      'Approval is not inferred from references or filenames. Literal references are not a complete dependency graph.'],
    assets,duplicates,dynamicSites,edges};
}
export function validateProduction(root) {
  const files = walk(root,`${GENERATED}/production`), errors = [], manifests=[];
  const known=new Set(walk(root)), visited=new Set();
  const forbidden=p=>p.startsWith('legacy_art/') || p.startsWith(`${GENERATED}/`)&&!p.startsWith(`${GENERATED}/production/`);
  function checkDependencies(file){
    if(visited.has(file)||!TEXT.has(path.extname(file))||file.endsWith('.md')||file.endsWith('/manifest.json'))return;
    visited.add(file);
    const found=references(file,fs.readFileSync(inside(root,file),'utf8'),known);
    for(const ref of found.refs){
      if(forbidden(ref.path))errors.push(`${file}: forbidden dependency ${ref.path}`);
      else checkDependencies(ref.path);
    }
    for(const prefix of found.dynamic)if(forbidden(prefix))errors.push(`${file}: unresolved non-production dynamic prefix ${prefix}`);
  }
  for (const file of files) {
    if (!TEXT.has(path.extname(file))) continue;
    const text = fs.readFileSync(inside(root,file),'utf8');
    // Historical sources may be recorded in the manifest; runtime dependency paths must be local.
    if (file.endsWith('/manifest.json')) {
      const m=JSON.parse(text); manifests.push(file);
      if (m.status !== 'approved' || !m.approval?.reviewedBy || !m.approval?.reviewedAt) errors.push(`${file}: missing approval`);
      for (const a of m.assets ?? []) for (const key of ['file','resource']) if (a[key]) {
        const target=path.posix.normalize(path.posix.join(path.posix.dirname(file),a[key]));
        if (!target.startsWith(`${GENERATED}/production/`) || !fs.existsSync(inside(root,target))) errors.push(`${file}: invalid ${key}: ${a[key]}`);
      }
      continue;
    }
    if (file.endsWith('.md')) continue;
    if(!file.includes('/sources/'))checkDependencies(file);
    for (const m of text.matchAll(/res:\/\/([^"'\s<>]+)/g)) {
      const target=m[1];
      if(target.startsWith('.godot/'))continue;
      if (target.startsWith('legacy_art/') || target.startsWith(`${GENERATED}/`) && !target.startsWith(`${GENERATED}/production/`)) errors.push(`${file}: forbidden dependency ${target}`);
      else if (!target.includes('{') && !fs.existsSync(inside(root,target))) errors.push(`${file}: missing ${target}`);
    }
    if (/legacy_art\/|generated\/(dev|test)\/|(?:\.\.\/)+(?:dev|test|legacy_art)\//.test(slash(text))) errors.push(`${file}: non-production path`);
  }
  for (const p of files.filter(f => /\.(png|tres|tscn)$/.test(f))) {
    if (!manifests.some(m=>p.startsWith(path.posix.dirname(m)+'/'))) errors.push(`${p}: no pack manifest`);
  }
  return {ok:errors.length===0,productionPacks:manifests.length,errors,
    note:manifests.length ? 'Static dependency checks; also run Godot and visual acceptance.' : 'No approved packs yet; this does not mean production is complete.'};
}
export function quarantineCopy(root, selection, batch) {
  if (!/^[a-zA-Z0-9_-]+$/.test(batch)) throw Error('Invalid batch name');
  if (selection.approved !== true || !selection.approvedBy || !selection.files?.length) throw Error('Explicit reviewed selection required');
  const dest=`.art_quarantine/${batch}`;
  if (fs.existsSync(inside(root,dest))) throw Error('Batch already exists');
  const seen=new Set();
  for (const f of selection.files) {
    if (seen.has(f.path)) throw Error('Duplicate selection path'); seen.add(f.path);
    if (!f.path.startsWith(`${GENERATED}/`) || /\/(production|test)\//.test(f.path)) throw Error(`Protected or out-of-scope path: ${f.path}`);
    if (!f.reason || digest(fs.readFileSync(inside(root,f.path)))!==f.sha256) throw Error(`Changed file or missing reason: ${f.path}`);
  }
  for (const f of selection.files) {
    const target=inside(root,`${dest}/files/${f.path}`);fs.mkdirSync(path.dirname(target),{recursive:true});
    fs.copyFileSync(inside(root,f.path),target,fs.constants.COPYFILE_EXCL);
    if (digest(fs.readFileSync(target))!==f.sha256) throw Error('Archive verification failed');
  }
  writeJson(root,`${dest}/restore.json`,{schemaVersion:1,approvedBy:selection.approvedBy,originalsRemoved:false,files:selection.files});
  fs.writeFileSync(inside(root,'.art_quarantine/.gdignore'),'');
  return {batch:dest,copied:selection.files.length,originalsRemoved:false};
}
