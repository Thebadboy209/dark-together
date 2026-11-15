import { defineServer, defineRoom } from "colyseus";
import { DarkRoom } from "./room.js";

const server = defineServer({ rooms: { dark_together: defineRoom(DarkRoom) } });
const port = Number(process.env.PORT) || 2567;
server.listen(port);
console.log(`DARK TOGETHER game server starting on :${port}`);
