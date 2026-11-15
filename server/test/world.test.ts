import test from 'node:test';
import assert from 'node:assert/strict';
import {W,H,rows,solid,freeAt,findTile,door,nextStep} from '../src/world.js';

test('map has consistent row widths and sealed outer walls', () => {
  assert.ok(W>=20 && H>=12);
  assert.ok(rows.every(row => row.length===W));
  assert.ok(rows[0].split('').every(c=>c==='#'));
  assert.ok(rows[H-1].split('').every(c=>c==='#'));
});
test('map contains the objectives', () => {
  for(const key of ['G','D','X','M']) assert.ok(findTile(key));
});
test('door opens only after server activates it', () => {
  assert.ok(solid(door.x,door.y,false));
  assert.equal(solid(door.x,door.y,true),false);
  assert.ok(freeAt(2,2,false));
});
test('pathfinding chooses a traversable neighboring tile', () => {
  const node=nextStep({x:2,y:2},findTile('G'),false);
  assert.ok(!solid(node.x,node.y,false));
  assert.equal(Math.abs(node.x-2)+Math.abs(node.y-2),1);
});
