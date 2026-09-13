import {signedDistance} from './topology.mjs';
import {RIVER_PROFILES, flowCoordinates, riverLight} from './river-motion.mjs';

export const PORTS = ['north', 'east', 'south', 'west'];
export function makeJunction(inflows, outflows, routes) {
  const ports = [...inflows, ...outflows];
  if (!inflows.length || !outflows.length || ports.length < 3 || ports.length > 4 ||
      new Set(ports).size !== ports.length || ports.some(p => !PORTS.includes(p)))
    throw Error('Junction needs distinct explicit inflow and outflow ports');
  if (!Array.isArray(routes) || !routes.length || new Set(routes).size !== routes.length)
    throw Error('Explicit nonduplicate routes are required');
  const profiles = routes.map(id => {
    const profile = RIVER_PROFILES.find(p => p.id === id);
    const [from, to] = id.split('_');
    if (!profile || !inflows.includes(from) || !outflows.includes(to))
      throw Error('Route must connect a declared inflow to a declared outflow');
    return profile;
  });
  if (ports.some(p => !routes.some(r => r.split('_').includes(p))))
    throw Error('Every declared port needs an explicit route');
  return {id: routes.join('+'), inflows: [...inflows], outflows: [...outflows],
    routes: profiles, mask: ports.reduce((mask,p) => mask | (1 << PORTS.indexOf(p)),0)};
}

// Named one-to-many and many-to-one presets; no flow inference from terrain masks.
export const JUNCTIONS = [3,4].flatMap(count => {
  const groups = count === 4 ? [PORTS] : PORTS.map(missing => PORTS.filter(p => p !== missing));
  return groups.flatMap(group => group.flatMap(single => {
    const others = group.filter(p => p !== single);
    return [makeJunction([single], others, others.map(p => `${single}_${p}`)),
      makeJunction(others, [single], others.map(p => `${p}_${single}`))];
  }));
});

export function junctionPixel(profile, x, y, frame, base) {
  const distance = signedDistance(profile.mask,x-0.5,y-0.5)-0.7;
  if (distance < -1) return [0,0,255,255];
  if (distance < 1) return [189,167,121,255];
  let light = 0;
  for (const route of profile.routes) {
    const support = Math.max(0,Math.min(1,(signedDistance(route.mask,x-0.5,y-0.5)-0.7)/4));
    const [s,n] = flowCoordinates(route,x,y);
    light = Math.max(light,riverLight(s,n,frame)*support);
  }
  return [...base.map((v,c) => Math.round(v+([181,239,247][c]-v)*light)),255];
}
