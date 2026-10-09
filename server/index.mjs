import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { WebSocketServer } from "ws";
import {
  createPlayerRoom,
  joinRoom,
  applyAction,
  serialize,
  disconnectPlayer,
  removePlayer,
  normalizeCode,
} from "./logic.mjs";
import { expireDj } from "./dj.mjs";
import { tickCardDraws } from "./cards.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const distDir = path.resolve(__dirname, "../client/dist");
const PORT = Number(process.env.PORT || 3001);
const rooms = new Map();

const ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

function makeCode() {
  for (let attempt = 0; attempt < 30; attempt += 1) {
    let code = "";
    for (let i = 0; i < 4; i += 1) {
      code += ALPHABET[Math.floor(Math.random() * ALPHABET.length)];
    }
    if (!rooms.has(code)) return code;
  }
  throw new Error("could not allocate room code");
}

function send(ws, msg) {
  if (ws.readyState === ws.OPEN) ws.send(JSON.stringify(msg));
}

function broadcast(room) {
  for (const player of room.players) {
    if (player.ws && player.ws.readyState === player.ws.OPEN) {
      send(player.ws, { type: "state", youId: player.id, room: serialize(room, player.id) });
    }
  }
}

function attach(ws, room, playerId) {
  const player = room.players.find((item) => item.id === playerId);
  if (!player) return;
  if (player.ws && player.ws !== ws) {
    const previous = player.ws;
    previous.playerId = null;
    previous.roomCode = null;
    previous.close();
  }
  player.ws = ws;
  player.connected = true;
  ws.playerId = playerId;
  ws.roomCode = room.code;
}

function detach(ws) {
  const code = ws.roomCode;
  const playerId = ws.playerId;
  if (!code || !playerId) return;
  const room = rooms.get(code);
  ws.playerId = null;
  ws.roomCode = null;
  if (!room) return;
  const player = room.players.find((item) => item.id === playerId);
  if (!player || player.ws !== ws) return;
  disconnectPlayer(room, playerId);
  broadcast(room);
}

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".svg": "image/svg+xml",
  ".woff2": "font/woff2",
  ".woff": "font/woff",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".webp": "image/webp",
  ".ico": "image/x-icon",
  ".map": "application/json; charset=utf-8",
};

function helpHtml() {
  return `<!doctype html>
<html lang="zh-Hant">
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>旅途小遊戲</title>
<body style="font-family:sans-serif;background:#f3eadf;color:#211c18;padding:32px;line-height:1.6;max-width:36rem">
<h1>旅途小遊戲</h1>
<p>開發時請開 <a href="http://localhost:5173">http://localhost:5173</a>。先在專案目錄執行 <code>npm run dev</code>。</p>
<p>若要由這個連接埠直接玩，請先 <code>npm run build</code>，再 <code>npm start</code>。</p>
</body></html>`;
}

function serveStatic(req, res) {
  const url = new URL(req.url || "/", "http://localhost");
  if (url.pathname === "/api/health") {
    res.writeHead(200, {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
    });
    res.end(JSON.stringify({ ok: true }));
    return;
  }

  const indexPath = path.join(distDir, "index.html");
  if (!fs.existsSync(indexPath)) {
    res.writeHead(200, { "content-type": "text/html; charset=utf-8" });
    res.end(helpHtml());
    return;
  }

  let pathname = decodeURIComponent(url.pathname);
  const resolved = path.resolve(distDir, `.${pathname}`);
  const relative = path.relative(distDir, resolved);
  if (relative.startsWith("..") || path.isAbsolute(relative)) {
    res.writeHead(400);
    res.end();
    return;
  }

  let filePath = resolved;
  const exists = fs.existsSync(filePath) && fs.statSync(filePath).isFile();
  if (!exists) {
    if (path.extname(pathname)) {
      res.writeHead(404);
      res.end();
      return;
    }
    filePath = indexPath;
  }

  if (req.method === "HEAD") {
    res.writeHead(200, { "content-type": MIME[path.extname(filePath)] || "application/octet-stream" });
    res.end();
    return;
  }

  res.writeHead(200, { "content-type": MIME[path.extname(filePath)] || "application/octet-stream" });
  const stream = fs.createReadStream(filePath);
  stream.on("error", () => {
    if (!res.headersSent) res.writeHead(404);
    res.end();
  });
  stream.pipe(res);
}

const server = http.createServer((req, res) => {
  if (req.method !== "GET" && req.method !== "HEAD") {
    res.writeHead(405, { allow: "GET, HEAD" });
    res.end();
    return;
  }
  serveStatic(req, res);
});

const wss = new WebSocketServer({ server, path: "/ws" });

wss.on("connection", (ws) => {
  ws.on("message", (buf) => {
    if (buf.length > 340 * 1024) {
      send(ws, { type: "error", message: "訊息太長了。" });
      return;
    }
    let msg;
    try {
      msg = JSON.parse(buf.toString());
    } catch {
      send(ws, { type: "error", message: "讀不懂這個訊息。" });
      return;
    }
    if (buf.length > 8000 && !(msg?.type === "action" && msg?.name === "profileSave")) {
      send(ws, { type: "error", message: "訊息太長了。" });
      return;
    }
    if (!msg || typeof msg !== "object") return;
    handle(ws, msg);
  });
  ws.on("close", () => detach(ws));
});

function handle(ws, msg) {
  if (msg.type === "create") {
    const created = createPlayerRoom(msg.nickname);
    if (created.error) {
      send(ws, { type: "error", message: created.error });
      return;
    }
    const room = created.room;
    room.code = makeCode();
    rooms.set(room.code, room);
    attach(ws, room, created.youId);
    broadcast(room);
    return;
  }

  if (msg.type === "join") {
    const normalized = normalizeCode(msg.code);
    if (normalized.error) {
      send(ws, { type: "error", message: normalized.error });
      return;
    }
    const room = rooms.get(normalized.code);
    if (!room) {
      send(ws, {
        type: "error",
        message: "找不到這個房間。代碼可能打錯了，或這間已經散了。",
      });
      return;
    }
    const joined = joinRoom(room, { nickname: msg.nickname, playerId: msg.playerId });
    if (joined.error) {
      send(ws, { type: "error", message: joined.error });
      return;
    }
    attach(ws, room, joined.youId);
    broadcast(room);
    return;
  }

  if (msg.type === "leave") {
    const room = rooms.get(ws.roomCode);
    const playerId = ws.playerId;
    if (room && playerId) {
      removePlayer(room, playerId);
      if (room.players.length === 0) rooms.delete(room.code);
      else broadcast(room);
    } else {
      ws.playerId = null;
      ws.roomCode = null;
    }
    send(ws, { type: "left" });
    return;
  }

  if (msg.type === "action") {
    const room = rooms.get(ws.roomCode);
    if (!room || !ws.playerId) {
      send(ws, { type: "error", message: "你還沒進入房間。" });
      return;
    }
    const result = applyAction(room, ws.playerId, msg);
    if (result?.error) {
      send(ws, { type: "error", message: result.error });
      return;
    }
    broadcast(room);
    return;
  }

  send(ws, { type: "error", message: "還沒有這個動作。" });
}

setInterval(() => {
  const now = Date.now();
  for (const [code, room] of rooms) {
    const online = room.players.some((player) => player.connected);
    const idle = now - room.updatedAt;
    if ((!online && idle > 30 * 60 * 1000) || idle > 6 * 60 * 60 * 1000) {
      rooms.delete(code);
    }
  }
}, 60 * 1000).unref();

setInterval(() => {
  for (const room of rooms.values()) {
    const expired = expireDj(room);
    const drawn = tickCardDraws(room);
    if (expired || drawn) broadcast(room);
  }
}, 1000).unref();

server.listen(PORT, "0.0.0.0", () => {
  console.log(`on the trip listening on http://0.0.0.0:${PORT}`);
});
