export const DIRECTIONS = [[0,-1],[1,0],[0,1],[-1,0],[1,-1],[1,1],[-1,1],[-1,-1]];
export function normalize(mask) {
  let m=mask&255;
  for(const [diagonal,a,b] of [[4,0,1],[5,1,2],[6,2,3],[7,3,0]])
    if(!(m&(1<<a))||!(m&(1<<b)))m&=~(1<<diagonal);
  return m;
}
export const MASKS=[...new Set(Array.from({length:256},(_,i)=>normalize(i)))].sort((a,b)=>a-b);
export function maskAt(map,x,y){
  return normalize(DIRECTIONS.reduce((m,[dx,dy],i)=>m|(map[y+dy]?.[x+dx]?1<<i:0),0));
}
// Quarter-cell signed distances give every tile the same border profile.
export function signedDistance(mask,x,y,size=64){
  const cx=(size-1)/2,dx=Math.abs(x-cx),dy=Math.abs(y-cx),r=size*0.34;
  const horizontal=x<cx?3:1,vertical=y<cx?0:2;
  const diagonal=y<cx?(x<cx?7:4):(x<cx?6:5);
  const h=!!(mask&(1<<horizontal)),v=!!(mask&(1<<vertical)),d=!!(mask&(1<<diagonal));
  if(h&&v&&d)return size;
  if(h&&v)return Math.hypot(cx-dx,cx-dy)-(cx-r);
  if(h)return r-dy;
  if(v)return r-dx;
  return r-Math.hypot(dx,dy);
}
