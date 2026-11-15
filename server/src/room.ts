import { Room, type Client, ServerError } from "colyseus";
import { GameState, Player } from "./state.js";
import { authenticate, roomRecord } from "./firebase.js";
import { dist, freeAt, generator, door, exit, monsterSpawn, nextStep } from "./world.js";
import { randomInt } from "node:crypto";

type Input = { x:number; y:number; sprint:boolean };
const emptyInput: Input = {x:0,y:0,sprint:false};
const CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
function randomCode() { return Array.from({length:6}, () => CODE_CHARS[randomInt(CODE_CHARS.length)]).join(""); }

export class DarkRoom extends Room<{state: InstanceType<typeof GameState>}> {
  maxClients = 2;
  state = new GameState();
  private inputs = new Map<string, Input>();
  private uids = new Map<string, string>();
  private monsterTick = 0;
  private roam = {x:monsterSpawn.x, y:monsterSpawn.y};
  private collisionCooldown = 0;

  async onCreate() {
    // Presence is shared by Colyseus on this process; production multi-instance requires Redis.
    const used = await this.presence.smembers("$dark-together-codes");
    let code = randomCode();
    while (used.includes(code)) code = randomCode();
    await this.presence.sadd("$dark-together-codes", code);
    this.roomId = code;
    this.maxMessagesPerSecond = 35;
    this.setTimestep((dt: number) => this.update(Math.min(dt / 1000, 0.10)), 50);
    await this.publish();
  }

  async onAuth(_client: Client, options: any) {
    if (this.state.phase !== "lobby" || this.state.players.size >= 2)
      throw new ServerError(403, "Room unavailable");
    const uid = await authenticate(options?.firebaseToken);
    if ([...this.uids.values()].includes(uid)) throw new ServerError(403,"Already joined");
    return {uid};
  }

  async onJoin(client: Client, options: any, auth: {uid:string}) {
    const p = new Player();
    p.x = this.state.players.size===0 ? 2 : 3;
    p.y = 2;
    p.name = typeof options?.displayName === 'string' ? options.displayName.slice(0, 18) : `Player ${this.state.players.size+1}`;
    this.uids.set(client.sessionId, auth.uid);
    this.state.players.set(client.sessionId, p);
    if (!this.state.host) this.state.host = client.sessionId;
    this.state.notice = "Both players must press READY";
    await this.publish();
  }

  // Message handlers in Colyseus 0.18. Player input contains direction, NOT position.
  messages = {
    input: (client: Client, msg: any) => {
      if (this.state.phase !== 'playing') return;
      const x = Number(msg?.x), y = Number(msg?.y);
      if (!Number.isFinite(x) || !Number.isFinite(y)) return;
      const len = Math.hypot(x,y);
      this.inputs.set(client.sessionId, {x:len>1?x/len:Math.max(-1,Math.min(1,x)),
        y:len>1?y/len:Math.max(-1,Math.min(1,y)), sprint:msg?.sprint===true});
    },
    ready: (client: Client) => {
      if (this.state.phase!=='lobby') return;
      const p = this.state.players.get(client.sessionId);
      if(p) p.ready = !p.ready;
    },
    start: (client: Client) => {
      if (client.sessionId!==this.state.host || this.state.players.size!==2 || this.state.phase!=='lobby') return;
      if([...this.state.players.values()].some(p=>!p.ready)) return;
      this.state.phase='playing'; this.state.notice='Find generator [G], then open the gate [D]';
      void this.publish();
    },
    light: (client: Client) => {
      const p = this.state.players.get(client.sessionId);
      if(p && this.state.phase==='playing') p.light = !p.light;
    },
    interact: (client: Client) => {
      if(this.state.phase!=='playing') return;
      const p=this.state.players.get(client.sessionId); if(!p) return;
      if(dist(p,generator)<1.6 && !this.state.generatorOn) {
        this.state.generatorOn=true; this.state.notice='Generator restored! Find the gate [D]'; void this.publish();return;
      }
      if(dist(p,door)<1.6 && this.state.generatorOn && !this.state.doorOpen) {
        this.state.doorOpen=true; this.state.notice='Gate unlocked. Both players reach [X]';void this.publish();return;
      }
      if(this.state.doorOpen && dist(p,exit)<1.8 &&
          [...this.state.players.values()].length===2 &&
          [...this.state.players.values()].every(other=>dist(other,exit)<2.0)) {
        this.state.phase='won'; this.state.notice='YOU ESCAPED TOGETHER!';void this.publish();return;
      }
      this.state.notice=this.state.generatorOn ? 'Approach gate [D], then exit [X] together' : 'Find the generator [G]';
    }
  };

  private update(dt:number) {
    if(this.state.phase!=='playing') return;
    for(const [id,p] of this.state.players) {
      const input=this.inputs.get(id)??emptyInput;
      const speed=input.sprint?3.7:2.7;
      const nx=p.x+input.x*speed*dt, ny=p.y+input.y*speed*dt;
      if(freeAt(nx,p.y,this.state.doorOpen)) p.x=nx;
      if(freeAt(p.x,ny,this.state.doorOpen)) p.y=ny;
      if(Math.hypot(input.x,input.y)>0.01) {p.dx=input.x;p.dy=input.y;}
    }
    this.monsterTick += dt; this.collisionCooldown=Math.max(0,this.collisionCooldown-dt);
    if(this.monsterTick >= 0.22) {
      const stepDt=this.monsterTick;this.monsterTick=0;
      this.advanceMonster(stepDt);
    }
  }

  private advanceMonster(dt:number) {
    const m = {x:this.state.monsterX,y:this.state.monsterY};
    const players = [...this.state.players.values()];
    if(!players.length) return;
    const nearest=players.reduce((a,b)=>dist(a,m)<dist(b,m)?a:b);
    let target: {x:number;y:number}=this.roam;
    if(dist(m,nearest)<6.5) target=nearest;
    else if(dist(m,this.roam)<0.6) {
      const wander=[{x:18,y:3},{x:19,y:5},{x:14,y:4},{x:18,y:10}];
      this.roam=wander[randomInt(wander.length)];target=this.roam;
    }
    const node=nextStep(m,target,this.state.doorOpen);
    const dx=node.x-m.x,dy=node.y-m.y,len=Math.hypot(dx,dy);
    const speed=target===nearest?1.9:1.2;
    if(len>0.04) {
      const amount=Math.min(speed*dt,len);
      const nx=m.x+dx/len*amount,ny=m.y+dy/len*amount;
      if(freeAt(nx,ny,this.state.doorOpen,0.16)) {
        this.state.monsterX=nx;this.state.monsterY=ny;
      }
    }
    if(this.collisionCooldown===0) for(const player of players) {
      if(dist(player, {x:this.state.monsterX,y:this.state.monsterY})<0.65) {
        // Non-graphic capture mechanic: return to entrance.
        player.x=2;player.y=2; this.state.monsterX=monsterSpawn.x;this.state.monsterY=monsterSpawn.y;
        this.state.notice='The shadow found you. Return to entrance!';
        this.collisionCooldown=3;break;
      }
    }
  }

  async onLeave(client: Client) {
    this.state.players.delete(client.sessionId);this.inputs.delete(client.sessionId);this.uids.delete(client.sessionId);
    if(this.state.host===client.sessionId) this.state.host=[...this.state.players.keys()][0]??'';
    if(this.state.phase==='playing') { this.state.phase='lobby'; this.state.notice='Teammate disconnected; waiting for a new room'; }
    for(const p of this.state.players.values()) p.ready=false;
    await this.publish();
  }
  async onDispose() {
    await this.presence.srem("$dark-together-codes",this.roomId);
    await roomRecord(this.roomId,null);
  }
  private publish() {
    return roomRecord(this.roomId, {
      status: this.state.phase, hostId: this.uids.get(this.state.host)??null,
      playerCount: this.state.players.size,
      players: Object.fromEntries([...this.uids].map(([sessionId,uid]) => [sessionId, {uid}])),
      createdBy: 'dark-together-server'
    });
  }
}
