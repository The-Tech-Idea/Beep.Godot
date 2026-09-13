import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {coverageReport} from './coverage.mjs';
import {MASKS} from './topology.mjs';
import {writeJson} from './library.mjs';

export function mergeCandidates(ledger,candidates,evidence=[]) {
  const next=structuredClone(ledger);
  let added=0,retained=0;
  const ids=new Set(next.bindings.map(b=>b.requirementId));
  const incoming=new Set();
  for(const binding of candidates) {
    if(binding.status!=='candidate'||binding.approvalEvidence!==null) throw Error('Reconciliation cannot promote artwork');
    if(incoming.has(binding.requirementId)) throw Error('Duplicate incoming requirement');
    incoming.add(binding.requirementId);
    // Existing decisions belong to their reviewer; do not overwrite them.
    if(ids.has(binding.requirementId)) {retained++;continue;}
    next.bindings.push(binding);ids.add(binding.requirementId);added++;
  }
  next.evidence??=[];
  for(const entry of evidence) if(!next.evidence.some(e=>e.path===entry.path)) next.evidence.push(entry);
  return {ledger:next,added,retained};
}

export function waterElevationCandidates(root) {
  const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
  const water='addons/beep_game_builder_cs/generated/dev/cartoon/water/';
  const ground='addons/beep_game_builder_cs/generated/dev/cartoon/ground_masks/staging/';
  const cliff='addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_front_v1/';
  const contact='addons/beep_game_builder_cs/generated/dev/cartoon/elevation/staging/grass_granite_contact_v1/';
  const sections=read(water+'river_sections_v1/manifest.json');
  const adapters=read(water+'river_adapters_v1/manifest.json');
  const junctions=read(water+'river_junctions_v1/manifest.json');
  const ports=read(water+'river_lake_connectors_v1/manifest.json');
  const sea=read(water+'shared_sea_v1/manifest.json');
  const bindings=[];
  const add=(id,source,runtime,region,resource,provenance,scope)=>bindings.push({requirementId:id,status:'candidate',source,
    runtimeFile:runtime,runtimeRegion:{x:region[0],y:region[1],width:region[2],height:region[3]},
    godotResource:resource,provenance,validation:{status:'partial',report:null},approvalEvidence:null,
    coverageScope:scope,conversionRequired:['Complete family acceptance and visual approval remain pending; this binding is not production readiness.']});
  const flow={N:'south_north',E:'west_east',S:'north_south',W:'east_west'};
  const initials={north:'N',east:'E',south:'S',west:'W'};
  const label=ports=>['N','E','S','W'].filter(d=>ports.some(p=>initials[p]===d)).join('');
  for(const projection of ['square','isometric']) {
    const h=projection==='square'?64:32;
    for(const material of ['grass','dirt']) {
      const index=material==='grass'?MASKS.indexOf(255):47;
      const folder=ground+projection+'/surface_candidate_v1/';
      add(`cartoon.${projection}.ground.${material}.fill.authored.0.static`,ground+`sources/plain_${material}_surface_v1.png`,
        folder+'grass_dirt_masks.png',[index%8*64,Math.floor(index/8)*h,64,h],folder+'grass_dirt_masks.tres',ground+'surface_candidates_v1.json',
        'Plain shared-surface fill; decoration overlays and other material transitions are separate requirements.');
    }
    for(const [direction,flowId] of Object.entries(flow)) {
      for(const piece of ['left_bank','right_bank','repeatable_interior']) {
        const leftLow=direction==='N'||direction==='E';
        const kind=piece==='repeatable_interior'?'middle':((piece==='left_bank')===leftLow?'low_bank':'high_bank');
        const profile=sections.profiles.find(p=>p.direction===flowId&&p.kind===kind);
        if(!profile) throw Error('Missing river section profile');
        const folder=water+'river_sections_v1/'+projection+'/';
        add(`cartoon.${projection}.river.grass.${piece}.${direction}.0.river`,sections.source,folder+'river_sections_16.png',
          [profile.atlas[0]*64,profile.atlas[1]*h,64,h],folder+'river_sections.tres',water+'river_sections_v1/manifest.json',
          'Direction denotes outflow heading; left/right are relative to travel. Straight sections only, not curved banks.');
      }
      for(const [piece,kind] of [['widening','narrow_expand'],['narrowing','narrow_contract']]) {
        const profile=adapters.profiles.find(p=>p.direction===flowId&&p.kind===kind);
        if(!profile) throw Error('Missing width adapter profile');
        const xs=profile.cells.map(c=>c.atlas[0]),ys=profile.cells.map(c=>c.atlas[1]);
        const folder=water+'river_adapters_v1/'+projection+'/';
        add(`cartoon.${projection}.river.grass.${piece}.${direction}.0.river`,adapters.source,folder+'river_adapters_16.png',
          [Math.min(...xs)*64,Math.min(...ys)*h,128,2*h],folder+'river_adapters.tres',water+'river_adapters_v1/manifest.json',
          `Direction denotes outflow heading. Native pattern ${profile.patternIndex}; narrow 1/2-cell transition only, not arbitrary width jumps.`);
      }
    }
    for(const profile of junctions.profiles) {
      const pieces=[profile.inflows.length===1?'branch':'confluence'];
      if(profile.inflows.length+profile.outflows.length===4) pieces.push('crossing');
      for(const piece of pieces) {
        const config=`in_${label(profile.inflows)}_out_${label(profile.outflows)}`;
        const folder=water+'river_junctions_v1/'+projection+'/';
        add(`cartoon.${projection}.river.grass.${piece}.${config}.0.river`,junctions.source,folder+'river_junctions_16.png',
          [profile.atlas[0]*64,profile.atlas[1]*h,64,h],folder+'river_junctions.tres',water+'river_junctions_v1/manifest.json',
          `Narrow single-cell profile with explicit routes ${profile.routes.join(', ')}. Wide and two-to-two junctions remain missing.`);
      }
    }
    for(const profile of ports.profiles) {
      const folder=water+'river_lake_connectors_v1/'+projection+'/';
      add(`cartoon.${projection}.lake.grass.river_${profile.role}.${initials[profile.port]}.0.lake`,ports.source,folder+'river_lake_16.png',
        [profile.atlas[0]*64,0,64,h],folder+'river_lake.tres',water+'river_lake_connectors_v1/manifest.json',
        'Direction is the external river port. Narrow river/lake connector only; not a sea mouth or wide inlet.');
    }
    for(const [index,profile] of sea.profiles.entries()) {
      const folder=water+'shared_sea_v1/'+projection+'/';
      add(`cartoon.${projection}.sea.grass.estuary.${initials[profile.port]}.0.sea`,sea.source,folder+'inlets_control_16.png',
        [index*64,0,64,h],folder+'mouth_widths_'+profile.port+'.tscn',water+'shared_sea_v1/manifest.json',
        'Direction is the external incoming river port. Includes native width patterns 1/2/3/5 and repeatable middles; requires the supplied control shader and shared surface plane. Visual approval, other banks and depth transitions remain pending.');
    }
    for(const [index,mask] of MASKS.entries()) {
      const folder=water+'shared_sea_v1/'+projection+'/';
      add(`cartoon.${projection}.water_boundaries.shallow_water_to_deep_water.binary_mask.mask_${mask}.0.static_mask`,sea.source,folder+'depth_control_16.png',
        [index%8*64,Math.floor(index/8)*h,64,h],folder+'depth_review.tscn',water+'shared_sea_v1/manifest.json',
        'Sea-only depth mask with shared animated surface. Requires the supplied depth material, surface plane and placement guard. Open-sea cells only; coastal/river contacts, lake depth and visual approval remain pending.');
    }
  }
  for(const [piece,region] of [['front_face',[0,16,320,64]],['plateau_rim',[0,0,320,24]]])
    add(`cartoon.square.cliffs.grass_granite.${piece}.S.64.static`,cliff+'sources/front_wall_source.png',cliff+'runtime/front_wall_320.png',
      region,cliff+'river_waterfall_review.tscn',cliff+'manifest.json','South-facing source-contiguous strip with shared grass mask. Arbitrary repetition, corners and other heights remain missing.');
  add('cartoon.square.cliffs.grass_granite.bottom_contact.S.64.static',contact+'sources/contact_master_green_640.png',contact+'runtime/contact_320.png',
    [0,0,320,40],cliff+'river_waterfall_review.tscn',contact+'manifest.json','Separate overlay at the 64px cliff ground line; contributes no visual rise. Other directions and contacts remain missing.');
  const evidence=[{path:water+'waterfall_sections_v1/manifest.json',status:'technical_candidate',note:'Water-only modules are not geology-specific complete waterfall structures; retain without multiplying into eight material bindings.'}];
  evidence.push({path:water+'shared_sea_v1/manifest.json',status:'technical_candidate',
    requirementIds:bindings.filter(b=>b.requirementId.includes('.shallow_water_to_deep_water.')).map(b=>b.requirementId),
    note:'Shared-sea depth alternatives cover both projections. Existing square foundation bindings are retained, not overwritten; manifest.depth.scenes points to the new usable candidates. Visual approval and coastal/river depth contacts remain pending.'});
  evidence.push({path:water+'lake_depth_v1/manifest.json',status:'technical_candidate',
    requirementIds:bindings.filter(b=>b.requirementId.includes('.shallow_water_to_deep_water.')).map(b=>b.requirementId),
    note:'Lake-specific shallow/deep alternative in both projections, including grass-bank contacts. Original local-ripple frames are preserved; native cell lookups reuse geometric depth masks only. Existing canonical bindings remain unchanged. River/depth contacts, other banks, visual approval and production integration remain pending.'});
  evidence.push({path:water+'lake_river_depth_v1/manifest.json',status:'technical_candidate',
    requirementIds:bindings.filter(b=>b.requirementId.includes('.lake.grass.river_')).map(b=>b.requirementId),
    note:'Depth-aware one-cell river/lake inlet and outlet alternatives in both projections. Original current/ripple frames and river-facing pixels are retained. Eight native patterns per projection require explicit compatible flow and bank neighborhoods. Wide contacts, other banks, sea depth, visual approval and production integration remain pending.'});
  evidence.push({path:water+'sea_river_depth_v1/manifest.json',status:'technical_candidate',
    requirementIds:bindings.filter(b=>b.requirementId.includes('.sea.grass.estuary.')).map(b=>b.requirementId),
    note:'Depth-aware river-to-sea mouths reuse existing edge/middle profiles and native width patterns in both projections. Widths 1/2/3/5 pass all four port cases; upstream frames are retained. Other banks, visual approval and production integration remain pending.'});
  evidence.push({path:water+'lake_mouth_sections_v1/manifest.json',status:'technical_candidate',
    requirementIds:bindings.filter(b=>b.requirementId.includes('.lake.grass.river_')).map(b=>b.requirementId),
    note:'Modular inlet/outlet lake alternatives use low-bank, repeatable-middle and high-bank pieces in both projections. Widths 1/2/3/5 use explicit directed river profiles and native patterns. Painted shallow mouth masks continue visually toward the river without editing logical river depth. Other banks, visual approval and production integration remain pending.'});
  return {bindings,evidence};
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
  const spec=JSON.parse(fs.readFileSync(path.join(root,'tools/terrain-library/specification.json'),'utf8'));
  const previous=JSON.parse(fs.readFileSync(path.join(root,spec.coverage.bindingsFile),'utf8'));
  const candidates=waterElevationCandidates(root);
  const merged=mergeCandidates(previous,candidates.bindings,candidates.evidence);
  const report=coverageReport(root,spec,merged.ledger);
  if(report.records.some(r=>r.issues.length)) throw Error('Invalid binding; no ledger changes written');
  writeJson(root,spec.coverage.bindingsFile,merged.ledger);
  writeJson(root,spec.coverage.reportFile,report);
  console.log(JSON.stringify({added:merged.added,retained:merged.retained,...report.summary}));
}
