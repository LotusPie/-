import { clearRoundCards, clearSongCards, skipPassed, songLimit, takeForcedMode, takeLobbyQueue } from "./cards.mjs";

const SONG_MAX = 80;
const ARTIST_MAX = 40;

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

function freshRound(room, game, djId, round) {
  const next = {
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
    songCount: 0,
    songs: null,
    wheelSongs: null,
    wheelSpin: 0,
    extra: false,
    extraSpins: 0,
    shield: false,
    blockVideoId: null,
    doubles: {},
    forcedMode: null,
    nextDjId: null,
  };
  if (!game) takeLobbyQueue(room, next);
  return next;
}

export function beginDj(room) {
  const first = active(room)[0];
  if (!first) return { error: "現在沒有人在線上。" };
  room.phase = "playing";
  room.game = freshRound(room, null, first.id, 1);
  return { ok: true };
}

export function advanceDj(room, mark) {
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
  const named = game.nextDjId && online.includes(game.nextDjId) ? game.nextDjId : null;
  next = named || skipPassed(room, next, online, pool);
  clearRoundCards(room);
  room.game = freshRound(room, game, next, game.round + 1);
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
  const allowed = value === -2 || value === -1 || value === 1 || value === 2;
  if (!allowed) return { error: "沒有這個分數。" };
  if (game.ratings[playerId] != null) return { error: "這首你評過了。" };
  if ((value === -1 || value === -2) && game.blockVideoId && game.blockVideoId === game.videoId) {
    return { error: "這一首不能打負分。" };
  }
  game.ratings[playerId] = value;
  addExact(findPlayer(room, playerId), 1);
  let delta = value;
  if (game.doubles?.[playerId] === game.videoId) {
    delta = value * 2;
    delete game.doubles[playerId];
  }
  if (delta < 0 && game.shield) delta = 0;
  if (delta) addExact(findPlayer(room, game.djId), delta);
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
    return { error: "轉完就不能重選。" };
  }

  if (name === "djSpin") {
    if (game.step !== "pick") return { error: "現在還不能轉。" };
    if (playerId !== game.djId) return { error: "這輪是另一位迪爵。" };
    if ((game.wheelSpin || 0) > 0 && !(game.extraSpins > 0)) return { error: "轉過了。" };
    if ((game.wheelSpin || 0) > 0) game.extraSpins -= 1;
    const songs = takeForcedMode(room, game, msg.songs);
    if (!songs) return { error: "沒有這個首數。" };
    game.wheelSongs = songs;
    game.wheelSpin = (game.wheelSpin || 0) + 1;
    return { ok: true };
  }

  if (name === "djMode") {
    if (game.step !== "pick") return { error: "現在還不能選首數。" };
    if (playerId !== game.djId) return { error: "這輪是另一位迪爵。" };
    if ((game.wheelSpin || 0) > 0 && (game.extraSpins || 0) > 0) return { error: "還可以再轉一次。" };
    const songs = takeForcedMode(room, game, msg.songs);
    if (!songs) return { error: "沒有這個首數。" };
    game.songs = songs;
    game.mode = songs === 1 ? "own" : "playlist";
    game.step = "enter";
    return { ok: true };
  }

  if (name === "djCue") {
    const extraCue = game.step === "live" && game.mode === "own" && game.extra && game.songCount < 2;
    if (game.step !== "enter" && !extraCue) return { error: "現在還不能播。" };
    if (playerId !== game.djId) return { error: "這輪是另一位迪爵。" };
    const videoId = parseYoutubeVideoId(msg.url);
    if (!videoId) return { error: "貼一個 YouTube 或 YouTube Music 的歌曲連結。" };
    clearSongCards(room, game);
    game.seedId = videoId;
    game.videoId = videoId;
    game.currentTime = 0;
    game.song = { title: "", artist: "" };
    game.playState = "unstarted";
    game.ratings = {};
    game.step = "live";
    game.endsAt = null;
    game.songCount = game.songCount > 0 ? game.songCount + 1 : 1;
    return { ok: true };
  }

  if (name === "djSync") {
    if (game.step !== "live") return { ok: true };
    if (playerId !== game.djId) return { error: "只有本輪迪爵的裝置會對時間。" };
    const reported = videoIdOk(msg.videoId) || game.videoId;
    const nextId = game.mode === "playlist" ? reported : game.seedId;
    const time = currentTimeOk(msg.currentTime);
    if (nextId && nextId !== game.videoId) {
      if (game.mode === "playlist" && game.songCount >= songLimit(game)) {
        return advanceDj(room, "served");
      }
      clearSongCards(room, game);
      game.videoId = nextId;
      game.ratings = {};
      game.song = { title: "", artist: "" };
      game.currentTime = time != null ? time : 0;
      if (game.mode === "playlist") game.songCount += 1;
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
    if (game.extra && game.songCount < 2) {
      game.step = "enter";
      game.ratings = {};
      clearSongCards(room, game);
      return { ok: true };
    }
    return advanceDj(room, "served");
  }

  if (name === "djFinish") {
    if (game.step !== "live") return { error: "這輪還沒到結束。" };
    if (playerId !== game.djId) return { error: "這輪由迪爵結束。" };
    return advanceDj(room, "served");
  }

  if (name === "djTimeUp") return { ok: true };

  return { error: "還沒有這個動作。" };
}

export function advanceDisconnectedDj(room) {
  const game = room.game;
  if (!game || game.kind !== "dj" || !game.djId) return;
  if (findPlayer(room, game.djId)?.connected) return;
  if (!active(room).length) return;
  advanceDj(room, "skip");
}
