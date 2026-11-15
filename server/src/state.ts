import { schema, t } from "@colyseus/schema";
import { monsterSpawn } from "./world.js";

export const Player = schema({
  x: t.number().default(2),
  y: t.number().default(2),
  dx: t.number().default(0),
  dy: t.number().default(-1),
  ready: t.boolean().default(false),
  connected: t.boolean().default(true),
  light: t.boolean().default(true),
  name: t.string().default("Player")
}, "DTPlayer");

export const GameState = schema({
  players: t.map(Player),
  phase: t.string().default("lobby"),
  host: t.string().default(""),
  generatorOn: t.boolean().default(false),
  doorOpen: t.boolean().default(false),
  monsterX: t.number().default(monsterSpawn.x),
  monsterY: t.number().default(monsterSpawn.y),
  notice: t.string().default("Waiting for 2 players")
}, "DTGameState");
