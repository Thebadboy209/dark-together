import { readFileSync } from "node:fs";
import { resolve } from "node:path";

export const rows: string[] = JSON.parse(readFileSync(resolve(process.cwd(), "data/hospital_map.json"), "utf8")).rows;
export const W = rows[0].length;
export const H = rows.length;
export const findTile = (c: string): { x: number; y: number } => {
  for (let y = 0; y < H; y++) { const x = rows[y].indexOf(c); if (x >= 0) return { x, y }; }
  throw new Error(`Missing tile ${c}`);
};
export const generator = findTile("G");
export const door = findTile("D");
export const exit = findTile("X");
export const monsterSpawn = findTile("M");
export const dist = (a: {x:number; y:number}, b: {x:number; y:number}) => Math.hypot(a.x - b.x, a.y - b.y);
export function solid(x: number, y: number, doorOpen: boolean): boolean {
  const col = Math.round(x), row = Math.round(y);
  if (row < 0 || row >= H || col < 0 || col >= W) return true;
  const tile = rows[row][col];
  return tile === "#" || (tile === "D" && !doorOpen);
}
export function freeAt(x: number, y: number, doorOpen: boolean, r = 0.24): boolean {
  return !solid(x-r,y-r,doorOpen) && !solid(x+r,y-r,doorOpen)
      && !solid(x-r,y+r,doorOpen) && !solid(x+r,y+r,doorOpen);
}
// Breadth-first search one tile at a time. Used only by one small monster per room.
export function nextStep(from: {x:number;y:number}, target: {x:number;y:number}, doorOpen: boolean) {
  const sx=Math.round(from.x), sy=Math.round(from.y), tx=Math.round(target.x), ty=Math.round(target.y);
  const queue=[{x:sx,y:sy}]; const visited=new Set([`${sx},${sy}`]);
  const previous=new Map<string,string>(); const goal=`${tx},${ty}`;
  for(let i=0;i<queue.length;i++) {
    const c=queue[i], key=`${c.x},${c.y}`;
    if (key===goal) break;
    for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
      const x=c.x+dx,y=c.y+dy,k=`${x},${y}`;
      if(visited.has(k)||solid(x,y,doorOpen)) continue;
      visited.add(k);previous.set(k,key);queue.push({x,y});
    }
  }
  if(!visited.has(goal)) return {x:sx,y:sy};
  let cur=goal;
  while(previous.has(cur) && previous.get(cur)!==`${sx},${sy}`) cur=previous.get(cur)!;
  const [x,y]=cur.split(',').map(Number);
  return {x,y};
}
