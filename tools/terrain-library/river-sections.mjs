import {riverLight} from './river-motion.mjs';

export const SECTION_KINDS = ['narrow', 'low_bank', 'middle', 'high_bank'];
export const FLOW_DIRECTIONS = ['north_south', 'south_north', 'west_east', 'east_west'];
export function sectionSequence(width) {
  if (!Number.isInteger(width) || width < 1) throw Error('Channel width must be a positive cell count');
  return width === 1 ? ['narrow'] : ['low_bank', ...Array(width - 2).fill('middle'), 'high_bank'];
}
export function sectionPixel(kind, direction, x, y, frame, base) {
  if (!SECTION_KINDS.includes(kind) || !FLOW_DIRECTIONS.includes(direction)) throw Error('Unknown river section');
  const vertical = direction === 'north_south' || direction === 'south_north';
  const n = vertical ? x : y;
  const along = vertical ? y : x;
  const s = direction === 'south_north' || direction === 'east_west' ? 64 - along : along;
  const low = kind === 'narrow' || kind === 'low_bank' ? n - 10.94 : 100;
  const high = kind === 'narrow' || kind === 'high_bank' ? 53.06 - n : 100;
  const distance = Math.min(low, high);
  if (distance < -1) return [0, 0, 255, 255];
  if (distance < 1) return [189, 167, 121, 255];
  const light = riverLight(s, n - 32, frame) * Math.min(1, distance / 4);
  return [...base.map((value, channel) => Math.round(value + ([181,239,247][channel] - value) * light)), 255];
}
