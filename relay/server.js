// The online relay. Deliberately dumb: it pairs two sockets into a room, forwards every
// message from one seat to the other verbatim, and remembers the messages a client
// flagged "durable" so a peer that drops can catch up. It never reads a game field --
// the two things it does understand (seats and the durable log) are the two things that
// cannot be agreed client-side, and everything else can change without touching this file.
import { WebSocketServer } from 'ws';
import { randomBytes } from 'node:crypto';

const PORT = Number(process.env.PORT ?? 8787);
const ROOM_TTL_MS = 30 * 60 * 1000;
const HEARTBEAT_MS = 20 * 1000;
// No 0/O/1/I: codes are read aloud and typed by hand.
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

const rooms = new Map();

function newCode() {
  for (;;) {
    let code = '';
    for (let i = 0; i < 4; i++) code += ALPHABET[Math.floor(Math.random() * ALPHABET.length)];
    if (!rooms.has(code)) return code;
  }
}

function newRoom() {
  const room = { code: newCode(), seats: [null, null], tokens: [token(), token()], log: [], epoch: 0, lastActive: Date.now() };
  rooms.set(room.code, room);
  return room;
}

function token() {
  return randomBytes(12).toString('hex');
}

function send(ws, msg) {
  if (ws && ws.readyState === ws.OPEN) ws.send(typeof msg === 'string' ? msg : JSON.stringify(msg));
}

function seatIn(room, seat, ws) {
  const index = seat - 1;
  const previous = room.seats[index];
  if (previous && previous !== ws) previous.close(4000, 'replaced');
  room.seats[index] = ws;
  ws.room = room;
  ws.seat = seat;
  room.lastActive = Date.now();

  const other = room.seats[1 - index];
  send(ws, { type: 'joined', room: room.code, seat, token: room.tokens[index], peerPresent: !!other });
  for (const entry of room.log) send(ws, entry);
  send(ws, { type: 'synced' });
  send(other, { type: 'peer', present: true });
  console.log(`[${room.code}] seat ${seat} connected (${room.log.length} durable replayed)`);
}

function handleControl(ws, msg) {
  if (msg.type === 'create') {
    seatIn(newRoom(), 1, ws);
    return true;
  }
  if (msg.type === 'join') {
    const room = rooms.get(String(msg.room ?? '').toUpperCase());
    if (!room) return send(ws, { type: 'error', reason: 'no-such-room' }), true;
    if (room.seats[1]) return send(ws, { type: 'error', reason: 'room-full' }), true;
    seatIn(room, 2, ws);
    return true;
  }
  if (msg.type === 'resume') {
    const room = rooms.get(String(msg.room ?? '').toUpperCase());
    const seat = Number(msg.seat);
    if (!room) return send(ws, { type: 'error', reason: 'no-such-room' }), true;
    if (seat !== 1 && seat !== 2) return send(ws, { type: 'error', reason: 'bad-seat' }), true;
    if (room.tokens[seat - 1] !== msg.token) return send(ws, { type: 'error', reason: 'bad-token' }), true;
    seatIn(room, seat, ws);
    return true;
  }
  if (msg.type === 'ping') {
    send(ws, { type: 'pong', t: msg.t });
    return true;
  }
  return false;
}

function relay(ws, raw, msg) {
  const room = ws.room;
  if (!room) return send(ws, { type: 'error', reason: 'not-in-room' });
  room.lastActive = Date.now();
  if (msg.durable) {
    // A rematch starts a new epoch; the old match's log would only confuse a late joiner.
    if (typeof msg.epoch === 'number' && msg.epoch > room.epoch) {
      room.epoch = msg.epoch;
      room.log.length = 0;
    }
    room.log.push(raw);
  }
  send(room.seats[2 - ws.seat], raw);
}

const wss = new WebSocketServer({ port: PORT });
wss.on('connection', (ws) => {
  ws.alive = true;
  ws.on('pong', () => { ws.alive = true; });
  ws.on('message', (data) => {
    const raw = data.toString();
    let msg;
    try { msg = JSON.parse(raw); } catch { return send(ws, { type: 'error', reason: 'bad-json' }); }
    if (!handleControl(ws, msg)) relay(ws, raw, msg);
  });
  ws.on('close', () => {
    const room = ws.room;
    if (!room) return;
    const index = ws.seat - 1;
    if (room.seats[index] === ws) {
      room.seats[index] = null;
      send(room.seats[1 - index], { type: 'peer', present: false });
      console.log(`[${room.code}] seat ${ws.seat} disconnected`);
    }
  });
});

setInterval(() => {
  for (const ws of wss.clients) {
    if (!ws.alive) { ws.terminate(); continue; }
    ws.alive = false;
    ws.ping();
  }
  const now = Date.now();
  for (const [code, room] of rooms) {
    if (!room.seats[0] && !room.seats[1] && now - room.lastActive > ROOM_TTL_MS) rooms.delete(code);
  }
}, HEARTBEAT_MS);

console.log(`retrace relay listening on ws://localhost:${PORT}`);
