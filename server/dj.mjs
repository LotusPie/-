const SONG_MAX = 80;
const ARTIST_MAX = 40;
const ROUND_MS = 20 * 60 * 1000;

const PLAY_STATES = new Set(["unstarted", "playing", "paused", "buffering", "ended", "cued", "error"]);

const HOSTS = new Set([
  "youtube.com",
  "m.youtube.com",
  "music.youtube.com",
  "youtu.be",
  "youtube-nocookie.com",
]);

function clip(text, max) {
  return [...String(text ?? "").replace(/[\u0000-\u001f]/g, "").trim()].slice(0, max).join("");
}

function active(room) {
  return room.players.filter((player) => player.connected);
}

function findPlayer(room, id) {
  return room.players.find((player) => player.id === id) || null;
}

function addExact(player, delta) {
  if (!player || !delta) return 0;
  player.score += delta;
  return delta;
}

function videoIdOk(id) {
  return /^[A-Za-z0-9_-]{11}$/.test(id || "") ? id : null;
}

function currentTimeOk(value) {
  if (value == null || value === "") return null;
  const n = typeof value === "number" ? value : Number(value);
  if (!Number.isFinite(n) || n < 0 || n > 24 * 60 * 60) return null;
  return Math.round(n * 10) / 10;
}

export function parseYoutubeVideoId(input) {
  const text = String(input ?? "").trim();
  if (!text || text.length > 500) return null;
  const bare = videoIdOk(text);
  if (bare) return bare;
  let url;
  try {
    url = new URL(text);
  } catch {
    return null;
  }
  if (url.protocol !== "https:" && url.protocol !== "http:") return null;
  const host = url.hostname.toLowerCase().replace(/^www\./, "");
  if (!HOSTS.has(host)) return null;
  if (host === "youtu.be") {
    return videoIdOk(url.pathname.split("/").filter(Boolean)[0]);
  }
  const fromQuery = videoIdOk(url.searchParams.get("v"));
  if (fromQuery) return fromQuery;
  const parts = url.pathname.split("/").filter(Boolean);
  if (parts[0] === "embed" || parts[0] === "shorts" || parts[0] === "live" || parts[0] === "v") {
    return videoIdOk(parts[1]);
  }
  return null;
}

function freshRound(game, djId, round) {
  return {
    kind: "dj",
    round,
    step: "pick",
    mode: null,
    djId,
    skipped: game?.skipped || [],
    served: game?.served || [],
    seedId: null,
    videoId: null,
    currentTime: 0,
    song: null,
    playState: null,
    ratings: {},
    endsAt: null,
  };
}

export function beginDj(room) {
  const first = active(room)[0];
  if (!first) return { error: "現在沒有人在線上。" };
  room.phase = "playing";
  room.game = freshRound(null, first.id, 1);
  return { ok: true };
}

function advanceDj(room, mark) {
  const game = room.game;
  const online = active(room).map((player) => player.id);
  if (!online.length) return { error: "現在沒有人在線上。" };
  if (mark === "skip" && game.djId && !game.skipped.includes(game.djId)) {
    game.skipped.push(game.djId);
  }
  if (mark === "served" && game.djId && !game.served.includes(game.djId)) {
    game.served.push(game.djId);
  }
  let pool = online.filter((id) => !game.skipped.includes(id) && !game.served.includes(id));
  if (!pool.length) {
    game.skipped = [];
    game.served = [];
    pool = online;
  }
  const start = Math.max(0, online.indexOf(game.djId));
  let next = pool[0];
  for (let step = 1; step <= online.length; step += 1) {
    const id = online[(start + step) % online.length];
    if (pool.includes(id)) {
      next = id;
      break;
    }
  }
  room.game = freshRound(game, next, game.round + 1);
  return { ok: true };
}

function requireDj(room) {
  if (room.game?.kind !== "dj") return { error: "現在不是如果我是迪爵。" };
  return null;
}

export function expireDj(room, now = Date.now()) {
  const game = room.game;
  if (!game || game.kind !== "dj" || game.step !== "live" || game.mode !== "playlist") return false;
  if (!game.endsAt || now < game.endsAt) return false;
  advanceDj(room, "served");
  return true;
}

function applyRating(room, playerId, value) {
  const game = room.game;
  if (playerId === game.djId) return { error: "迪爵這輪不評自己的歌。" };
  const allowed = value === "none" || value === -2 || value === -1 || value === 0 || value === 1 || value === 2;
  if (!allowed) return { error: "沒有這個分數。" };
  if (game.ratings[playerId] != null) return { error: "這首你評過了。" };
  game.ratings[playerId] = value;
  if (typeof value === "number") {
    addExact(findPlayer(room, playerId), 1);
    addExact(findPlayer(room, game.djId), value);
  }
  return { ok: true };
}

export function handleDjAction(room, playerId, msg) {
  const blocked = requireDj(room);
  if (blocked) return blocked;
  if (expireDj(room)) return { ok: true };
  const game = room.game;
  const name = msg.name;

  if (name === "djSkip") {
    return advanceDj(room, "skip");
  }

  if (name === "djBack") {
    if (game.step !== "enter") return { error: "現在不能重選。" };
    if (playerId !== game.djId) return { error: "這輪是另一位迪爵。" };
    game.step = "pick";
    game.mode = null;
    return { ok: true };
  }

  if (name === "djMode") {
    if (game.step !== "pick") return { error: "現在還不能選模式。" };
    if (playerId !== game.djId) return { error: "這輪是另一位迪爵。" };
    if (msg.mode !== "own" && msg.mode !== "playlist") return { error: "沒有這個模式。" };
    game.mode = msg.mode;
    game.step = "enter";
    return { ok: true };
  }

  if (name === "djCue") {
    if (game.step !== "enter") return { error: "現在還不能播。" };
    if (playerId !== game.djId) return { error: "這輪是另一位迪爵。" };
    const videoId = parseYoutubeVideoId(msg.url);
    if (!videoId) return { error: "貼一個 YouTube 或 YouTube Music 的歌曲連結。" };
    game.seedId = videoId;
    game.videoId = videoId;
    game.currentTime = 0;
    game.song = { title: "", artist: "" };
    game.playState = "unstarted";
    game.ratings = {};
    game.step = "live";
    game.endsAt = game.mode === "playlist" ? Date.now() + ROUND_MS : null;
    return { ok: true };
  }

  if (name === "djSync") {
    if (game.step !== "live") return { ok: true };
    if (playerId !== game.djId) return { error: "只有本輪迪爵的裝置會對時間。" };
    const reported = videoIdOk(msg.videoId) || game.videoId;
    const nextId = game.mode === "playlist" ? reported : game.seedId;
    const time = currentTimeOk(msg.currentTime);
    if (nextId && nextId !== game.videoId) {
      game.videoId = nextId;
      game.ratings = {};
      game.song = { title: "", artist: "" };
      game.currentTime = time != null ? time : 0;
    } else if (time != null) {
      game.currentTime = time;
    }
    const title = clip(msg.title, SONG_MAX);
    const artist = clip(msg.artist, ARTIST_MAX);
    if (!game.song) game.song = { title: "", artist: "" };
    if (title) game.song.title = title;
    if (artist) game.song.artist = artist;
    if (PLAY_STATES.has(msg.playState)) game.playState = msg.playState;
    return { ok: true };
  }

  if (name === "djRate") {
    if (game.step !== "live") return { error: "現在還不能評。" };
    return applyRating(room, playerId, msg.value);
  }

  if (name === "djEnded") {
    if (game.step !== "live" || game.mode !== "own") return { ok: true };
    if (playerId !== game.djId) return { error: "這輪由迪爵結束。" };
    return advanceDj(room, "served");
  }

  if (name === "djFinish") {
    if (game.step !== "live") return { error: "這輪還沒到結束。" };
    if (playerId !== game.djId) return { error: "這輪由迪爵結束。" };
    return advanceDj(room, "served");
  }

  if (name === "djTimeUp") {
    if (game.step !== "live" || game.mode !== "playlist") return { ok: true };
    if (Date.now() + 2000 < game.endsAt) return { error: "時間還沒到。" };
    return advanceDj(room, "served");
  }

  return { error: "還沒有這個動作。" };
}

export function advanceDisconnectedDj(room) {
  const game = room.game;
  if (!game || game.kind !== "dj" || !game.djId) return;
  if (findPlayer(room, game.djId)?.connected) return;
  if (!active(room).length) return;
  advanceDj(room, "skip");
}
