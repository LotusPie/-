export const HAND_MAX = 5;
export const DRAW_EVERY_MS = 3 * 60 * 1000;

export const CARD_LIST = [
  { id: "hide", name: "隱藏分數", cost: 3, needs: "" },
  { id: "swap", name: "交換分數", cost: 4, needs: "other" },
  { id: "peek", name: "偷看", cost: 2, needs: "other" },
  { id: "shield", name: "護身", cost: 3, needs: "" },
  { id: "block", name: "擋負分", cost: 3, needs: "" },
  { id: "double", name: "加倍", cost: 3, needs: "" },
  { id: "steal", name: "偷一分", cost: 2, needs: "other" },
  { id: "respin", name: "再轉一次", cost: 2, needs: "" },
  { id: "mode", name: "指定模式", cost: 3, needs: "mode" },
  { id: "extra", name: "多一首", cost: 3, needs: "" },
  { id: "next", name: "指定下一位", cost: 4, needs: "player" },
  { id: "pass", name: "這輪不當", cost: 3, needs: "" },
];

const CARD_IDS = new Set(CARD_LIST.map((card) => card.id));

export const DRAW_SPIN_MS = 30 * 60 * 1000;

const DRAW_WEIGHTS = [
  ["peek", 14],
  ["pass", 12],
  ["next", 12],
  ["respin", 10],
  ["block", 10],
  ["shield", 8],
  ["extra", 8],
  ["mode", 8],
  ["hide", 8],
  ["steal", 5],
  ["double", 3],
  ["swap", 2],
];

function drawTable() {
  const slices = [];
  const missing = [];
  for (const [id, weight] of DRAW_WEIGHTS) {
    const spec = CARD_LIST.find((card) => card.id === id);
    if (!spec) {
      missing.push(id);
      continue;
    }
    slices.push({ id, name: spec.name, weight });
  }
  return { slices, missing };
}

function ensureSpin(cards) {
  if (!cards.spinReady) cards.spinReady = {};
  if (!cards.spinsLeft) cards.spinsLeft = {};
  if (!cards.pendingDraw) cards.pendingDraw = {};
  if (!cards.wheelSpin) cards.wheelSpin = {};
  if (!cards.lastSpin) cards.lastSpin = {};
}

function spinsLeftOf(cards, playerId) {
  ensureSpin(cards);
  if (cards.spinsLeft[playerId] == null) cards.spinsLeft[playerId] = 3;
  return cards.spinsLeft[playerId];
}

function rollDrawCard() {
  const table = drawTable();
  if (!table.slices.length) return null;
  const total = table.slices.reduce((sum, slice) => sum + slice.weight, 0);
  let roll = Math.random() * total;
  let picked = table.slices[table.slices.length - 1];
  for (const slice of table.slices) {
    roll -= slice.weight;
    if (roll < 0) return slice;
  }
  return picked;
}

export function spinDraw(room, playerId, now = Date.now()) {
  const player = findPlayer(room, playerId);
  if (!player) return { error: "你不在這個房間。" };
  if (!room.cards) return { error: "現在不能抽牌。" };
  const hand = handOf(room, playerId);
  if (hand.length >= HAND_MAX) return { error: "手牌滿了，先用掉一張。" };
  const cards = room.cards;
  ensureSpin(cards);
  if (cards.pendingDraw[playerId]) return { error: "轉盤還在轉。" };
  const left = spinsLeftOf(cards, playerId);
  const readyAt = cards.spinReady[playerId] || 0;
  if (left <= 0 && now < readyAt) return { error: "還沒到可以轉的時間。" };
  const picked = rollDrawCard();
  if (!picked) return { error: "現在沒有可以抽的牌。" };
  cards.pendingDraw[playerId] = { card: picked.id, at: now };
  cards.wheelSpin[playerId] = (cards.wheelSpin[playerId] || 0) + 1;
  return { ok: true, card: picked.id };
}

export function takeDraw(room, playerId, now = Date.now()) {
  const player = findPlayer(room, playerId);
  if (!player) return { error: "你不在這個房間。" };
  if (!room.cards) return { error: "現在不能抽牌。" };
  const cards = room.cards;
  ensureSpin(cards);
  const pending = cards.pendingDraw[playerId];
  if (!pending) return { ok: true };
  const hand = handOf(room, playerId);
  if (hand.length >= HAND_MAX) return { error: "手牌滿了，先用掉一張。" };
  hand.push(pending.card);
  delete cards.pendingDraw[playerId];
  const left = spinsLeftOf(cards, playerId);
  if (left > 0) cards.spinsLeft[playerId] = left - 1;
  if ((cards.spinsLeft[playerId] || 0) <= 0) cards.spinReady[playerId] = now + DRAW_SPIN_MS;
  cards.lastSpin[playerId] = { card: pending.card, at: now };
  return { ok: true, card: pending.card };
}

function presentDraw(cards, viewerId) {
  const table = drawTable();
  const readyAt = cards?.spinReady?.[viewerId] || 0;
  const last = cards?.lastSpin?.[viewerId];
  const spinsLeft = cards?.spinsLeft?.[viewerId] == null ? 3 : cards.spinsLeft[viewerId];
  return {
    readyAt,
    spinsLeft,
    wheelSpin: cards?.wheelSpin?.[viewerId] || 0,
    wheelCard: cards?.pendingDraw?.[viewerId]?.card || null,
    lastId: last?.card || null,
    lastAt: last?.at || 0,
    slices: table.slices,
    missing: table.missing,
  };
}

export function initCards(now = Date.now()) {
  return {
    hands: {},
    bornAt: now,
    draws: 0,
    hidden: [],
    pass: [],
    peek: {},
    pendingMode: null,
    pendingExtra: false,
    pendingNext: null,
    spinReady: {},
    spinsLeft: {},
    pendingDraw: {},
    wheelSpin: {},
    lastSpin: {},
  };
}

function specOf(id) {
  return CARD_LIST.find((card) => card.id === id) || null;
}

function findPlayer(room, id) {
  return room.players.find((player) => player.id === id) || null;
}

function connectedPlayer(room, id) {
  const player = findPlayer(room, id);
  if (!player?.connected) return null;
  return player;
}

function handOf(room, playerId) {
  if (!room.cards.hands[playerId]) room.cards.hands[playerId] = [];
  return room.cards.hands[playerId];
}

function randomCardId() {
  const index = Math.floor(Math.random() * CARD_LIST.length);
  return CARD_LIST[index].id;
}

export function tickCardDraws(room, now = Date.now()) {
  const cards = room.cards;
  if (!cards) return false;
  const due = Math.floor((now - cards.bornAt) / DRAW_EVERY_MS);
  if (due <= cards.draws) return false;
  let changed = false;
  while (cards.draws < due) {
    cards.draws += 1;
  }
  return changed;
}

export function buyCard(room, playerId, cardId) {
  const player = findPlayer(room, playerId);
  if (!player) return { error: "你不在這個房間。" };
  const spec = specOf(cardId);
  if (!spec || !CARD_IDS.has(cardId)) return { error: "沒有這張牌。" };
  if (player.score < 0) return { error: "分數是負的，不能買。" };
  if (player.score < spec.cost) return { error: "分數不夠，不能買。" };
  const hand = handOf(room, playerId);
  if (hand.length >= HAND_MAX) return { error: "手上已經有五張牌。" };
  player.score -= spec.cost;
  hand.push(spec.id);
  return { ok: true };
}

function otherOnline(room, playerId, targetId) {
  if (!targetId || targetId === playerId) return { error: "請選另一位在線上的旅伴。" };
  const target = connectedPlayer(room, targetId);
  if (!target) return { error: "請選一位在線上的旅伴。" };
  return { player: target };
}

function liveSong(room) {
  const game = room.game;
  if (game?.kind !== "dj" || game.step !== "live" || !game.videoId) return null;
  return game;
}

function applyCard(room, playerId, cardId, msg) {
  const self = findPlayer(room, playerId);
  const game = room.game?.kind === "dj" ? room.game : null;

  if (cardId === "hide") {
    if (!room.cards.hidden.includes(playerId)) room.cards.hidden.push(playerId);
    return { ok: true };
  }

  if (cardId === "swap") {
    const picked = otherOnline(room, playerId, msg?.targetId);
    if (picked.error) return picked;
    const next = self.score;
    self.score = picked.player.score;
    picked.player.score = next;
    return { ok: true };
  }

  if (cardId === "peek") {
    const song = liveSong(room);
    if (!song) return { error: "現在沒有這一首。" };
    if (!msg?.targetId || msg.targetId === playerId) return { error: "請選另一位旅伴。" };
    const target = findPlayer(room, msg.targetId);
    if (!target) return { error: "請選一位在線上的旅伴。" };
    const value = song.ratings?.[msg.targetId];
    if (typeof value !== "number") return { error: "這位還沒評。" };
    room.cards.peek[playerId] = { targetId: target.id, value };
    return { ok: true };
  }

  if (cardId === "shield") {
    if (!game || game.djId !== playerId) return { error: "要在你當迪爵的這一輪用。" };
    game.shield = true;
    return { ok: true };
  }

  if (cardId === "block") {
    const song = liveSong(room);
    if (!song) return { error: "現在沒有這一首。" };
    song.blockVideoId = song.videoId;
    return { ok: true };
  }

  if (cardId === "double") {
    const song = liveSong(room);
    if (!song) return { error: "現在沒有這一首。" };
    if (song.djId === playerId) return { error: "迪爵這輪不評自己的歌。" };
    if (song.ratings?.[playerId] != null) return { error: "這首你評過了。" };
    if (!song.doubles) song.doubles = {};
    song.doubles[playerId] = song.videoId;
    return { ok: true };
  }

  if (cardId === "steal") {
    const picked = otherOnline(room, playerId, msg?.targetId);
    if (picked.error) return picked;
    if (picked.player.score <= 0) return { error: "沒有分可以拿。" };
    picked.player.score -= 1;
    self.score += 1;
    return { ok: true };
  }

  if (cardId === "respin") {
    if (!game || game.step !== "pick") return { error: "現在不能再轉。" };
    if (game.djId !== playerId) return { error: "這輪是另一位迪爵。" };
    game.extraSpins = (game.extraSpins || 0) + 1;
    return { ok: true };
  }

  if (cardId === "mode") {
    const songs = songsOf(msg?.songs);
    if (!songs) return { error: "沒有這個首數。" };
    if (game?.step === "pick") {
      game.forcedMode = songs;
      room.cards.pendingMode = null;
      if ((game.wheelSpin || 0) > 0) game.wheelSongs = songs;
    } else {
      room.cards.pendingMode = songs;
    }
    return { ok: true };
  }

  if (cardId === "extra") {
    if (game) game.extra = true;
    else room.cards.pendingExtra = true;
    return { ok: true };
  }

  if (cardId === "next") {
    const target = connectedPlayer(room, msg?.targetId);
    if (!target) return { error: "請選一位在線上的旅伴。" };
    if (game) game.nextDjId = target.id;
    else room.cards.pendingNext = target.id;
    return { ok: true };
  }

  if (cardId === "pass") {
    const myTurn = Boolean(game && game.djId === playerId && game.step !== "live");
    if (myTurn) return { passSeat: true };
    room.cards.pass.push(playerId);
    return { ok: true };
  }

  return { error: "沒有這張牌。" };
}

export function playCard(room, playerId, msg) {
  if (!findPlayer(room, playerId)) return { error: "你不在這個房間。" };
  const cardId = msg?.card;
  if (!specOf(cardId)) return { error: "沒有這張牌。" };
  const hand = room.cards.hands[playerId] || [];
  const index = hand.indexOf(cardId);
  if (index === -1) return { error: "你沒有這張牌。" };
  const result = applyCard(room, playerId, cardId, msg || {});
  if (result?.error) return result;
  hand.splice(index, 1);
  room.cards.hands[playerId] = hand;
  return result?.passSeat ? result : { ok: true };
}

function songsOf(value) {
  const n = typeof value === "number" ? value : Number(value);
  return n === 1 || n === 2 || n === 3 ? n : null;
}

export function songLimit(game) {
  const base = songsOf(game?.songs) || (game?.mode === "own" ? 1 : 3);
  return base + (game?.extra ? 1 : 0);
}

export function takeLobbyQueue(room, game) {
  if (!room.cards) return;
  if (room.cards.pendingExtra) {
    game.extra = true;
    room.cards.pendingExtra = false;
  }
  if (room.cards.pendingNext) {
    game.nextDjId = room.cards.pendingNext;
    room.cards.pendingNext = null;
  }
}

export function clearRoundCards(room) {
  if (!room.cards) return;
  room.cards.hidden = [];
  room.cards.peek = {};
}

export function clearSongCards(room, game) {
  if (game) {
    game.blockVideoId = null;
    game.doubles = {};
  }
  if (room.cards) room.cards.peek = {};
}

export function takeForcedMode(room, game, requested) {
  const forced = songsOf(game?.forcedMode ?? room.cards?.pendingMode);
  if (forced) {
    if (game) game.forcedMode = null;
    if (room.cards) room.cards.pendingMode = null;
    return forced;
  }
  return songsOf(requested);
}

export function skipPassed(room, next, online, pool) {
  const pass = room.cards?.pass;
  if (!pass?.length) return next;
  const skipped = new Set();
  let guard = 0;
  while (pass.includes(next) && guard <= online.length) {
    const index = pass.indexOf(next);
    if (index !== -1) pass.splice(index, 1);
    skipped.add(next);
    let found = null;
    const start = Math.max(0, online.indexOf(next));
    for (let step = 1; step <= online.length; step += 1) {
      const id = online[(start + step) % online.length];
      if (pool.includes(id) && !skipped.has(id)) {
        found = id;
        break;
      }
    }
    if (!found) break;
    next = found;
    guard += 1;
  }
  return next;
}

export function forgetPlayer(room, playerId) {
  const cards = room.cards;
  if (!cards) return;
  delete cards.hands[playerId];
  delete cards.peek[playerId];
  cards.hidden = cards.hidden.filter((id) => id !== playerId);
  cards.pass = cards.pass.filter((id) => id !== playerId);
  if (cards.pendingNext === playerId) cards.pendingNext = null;
  if (room.game?.nextDjId === playerId) room.game.nextDjId = null;
  if (room.game?.doubles) delete room.game.doubles[playerId];
  if (cards.spinReady) delete cards.spinReady[playerId];
  if (cards.bonusSpins) delete cards.bonusSpins[playerId];
  if (cards.lastSpin) delete cards.lastSpin[playerId];
  if (cards.spinsLeft) delete cards.spinsLeft[playerId];
  if (cards.pendingDraw) delete cards.pendingDraw[playerId];
  if (cards.wheelSpin) delete cards.wheelSpin[playerId];
}

export function presentCards(room, viewerId) {
  const cards = room.cards;
  const game = room.game?.kind === "dj" ? room.game : null;
  const peek = cards?.peek?.[viewerId];
  const peekPlayer = peek ? findPlayer(room, peek.targetId) : null;
  const doubled = Boolean(game?.doubles?.[viewerId] && game.doubles[viewerId] === game.videoId);
  return {
    hand: [...(cards?.hands?.[viewerId] || [])],
    shop: CARD_LIST.map(({ id, name, cost, needs }) => ({ id, name, cost, needs })),
    peek: peek
      ? {
          nickname: peekPlayer?.nickname || "旅伴",
          photo: typeof peekPlayer?.profile?.photo === "string" ? peekPlayer.profile.photo : "",
          emoji: typeof peekPlayer?.profile?.emoji === "string" ? peekPlayer.profile.emoji : "",
          value: peek.value,
        }
      : null,
    doubled,
    active: {
      shield: Boolean(game?.shield),
      blockNegative: Boolean(game?.blockVideoId && game.blockVideoId === game.videoId),
      forcedMode: game?.forcedMode || cards?.pendingMode || null,
      extra: Boolean(game?.extra || cards?.pendingExtra),
      nextDjId: game?.nextDjId || cards?.pendingNext || null,
      extraSpins: game?.extraSpins || 0,
    },
    draw: presentDraw(cards, viewerId),
  };
}

export function hiddenFrom(room, playerId, viewerId) {
  if (!playerId || playerId === viewerId) return false;
  return Boolean(room.cards?.hidden?.includes(playerId));
}
