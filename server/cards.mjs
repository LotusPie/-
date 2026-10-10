export const HAND_MAX = 5;
export const DRAW_EVERY_MS = 3 * 60 * 1000;
export const HIDE_MS = 2 * 60 * 60 * 1000;

export const CARD_LIST = [
  { id: "hide", name: "隱藏兩小時", cost: 3, needs: "" },
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
    hideUntil: {},
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
    offers: [],
    declined: {},
    offerSeq: 0,
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
  const before = player.score;
  player.score -= spec.cost;
  hand.push(spec.id);
  if (player.score !== before) offerHide(room, playerId);
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

  if (cardId === "hide") return stackHide(room, playerId);

  if (cardId === "swap") {
    const picked = otherOnline(room, playerId, msg?.targetId);
    if (picked.error) return picked;
    const beforeSelf = self.score;
    const beforeOther = picked.player.score;
    self.score = beforeOther;
    picked.player.score = beforeSelf;
    if (self.score !== beforeSelf) offerHide(room, playerId);
    if (picked.player.score !== beforeOther) offerHide(room, picked.player.id);
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
    const beforeSelf = self.score;
    const beforeOther = picked.player.score;
    picked.player.score -= 1;
    self.score += 1;
    if (self.score !== beforeSelf) offerHide(room, playerId);
    if (picked.player.score !== beforeOther) offerHide(room, picked.player.id);
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

  if (cardId === "next") return queueNextDj(room, msg?.targetId);

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
  if (cards.hideUntil) delete cards.hideUntil[playerId];
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
  if (cards.offers) cards.offers = cards.offers.filter((offer) => offer.playerId !== playerId);
  if (cards.declined) delete cards.declined[playerId];
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

export function stackHide(room, playerId, now = Date.now()) {
  if (!findPlayer(room, playerId)) return { error: "請選一位旅伴。" };
  if (!room.cards) return { error: "現在不能用這張牌。" };
  if (!room.cards.hideUntil) room.cards.hideUntil = {};
  const current = room.cards.hideUntil[playerId];
  const base = typeof current === "number" && current > now ? current : now;
  room.cards.hideUntil[playerId] = base + HIDE_MS;
  return { ok: true };
}

export function setHideExpiry(room, playerId, until) {
  if (!findPlayer(room, playerId)) return { error: "請選一位旅伴。" };
  if (!room.cards) return { error: "現在不能用這張牌。" };
  if (!room.cards.hideUntil) room.cards.hideUntil = {};
  room.cards.hideUntil[playerId] = until;
  return { ok: true };
}

export function queueNextDj(room, targetId) {
  const target = connectedPlayer(room, targetId);
  if (!target) return { error: "請選一位在線上的旅伴。" };
  const game = room.game?.kind === "dj" ? room.game : null;
  if (game) game.nextDjId = target.id;
  else room.cards.pendingNext = target.id;
  return { ok: true };
}

export function giveCard(room, playerId, cardId) {
  if (!findPlayer(room, playerId)) return { error: "請選一位旅伴。" };
  if (!specOf(cardId)) return { error: "沒有這張牌。" };
  const hand = handOf(room, playerId);
  if (hand.length >= HAND_MAX) return { error: "手上已經有五張牌。" };
  hand.push(cardId);
  return { ok: true };
}

export function clearHand(room, playerId) {
  if (!findPlayer(room, playerId)) return { error: "請選一位旅伴。" };
  if (!room.cards) return { error: "現在不能用這張牌。" };
  room.cards.hands[playerId] = [];
  return { ok: true };
}

export function setFreeDraws(room, playerId, count = 3) {
  if (!findPlayer(room, playerId)) return { error: "請選一位旅伴。" };
  if (!room.cards) return { error: "現在不能抽牌。" };
  ensureSpin(room.cards);
  room.cards.spinsLeft[playerId] = count;
  room.cards.spinReady[playerId] = 0;
  return { ok: true };
}

export function setDrawCooldown(room, playerId, ms, now = Date.now()) {
  if (!findPlayer(room, playerId)) return { error: "請選一位旅伴。" };
  if (!room.cards) return { error: "現在不能抽牌。" };
  ensureSpin(room.cards);
  room.cards.spinsLeft[playerId] = 0;
  room.cards.spinReady[playerId] = now + ms;
  return { ok: true };
}

export function scoreHideUntil(room, playerId, now = Date.now()) {
  const until = room.cards?.hideUntil?.[playerId];
  if (typeof until !== "number" || until <= now) return null;
  return until;
}

export function hiddenFrom(room, playerId, viewerId, now = Date.now()) {
  if (!playerId || playerId === viewerId) return false;
  return scoreHideUntil(room, playerId, now) != null;
}

export function tickScoreHides(room, now = Date.now()) {
  const map = room.cards?.hideUntil;
  if (!map) return false;
  let changed = false;
  for (const [id, until] of Object.entries(map)) {
    if (typeof until !== "number" || until <= now) {
      delete map[id];
      changed = true;
    }
  }
  return changed;
}

function ensureOffers(room) {
  if (!room.cards.offers) room.cards.offers = [];
  if (!room.cards.declined) room.cards.declined = {};
}

function holds(room, playerId, cardId) {
  return (room.cards?.hands?.[playerId] || []).includes(cardId);
}

function songStopLabel(songs) {
  if (songs === 1) return "一首歌";
  if (songs === 2) return "二首歌";
  if (songs === 3) return "三首歌";
  if (songs === 0) return "此輪跳過";
  return "";
}

function stillUseful(room, offer) {
  if (!holds(room, offer.playerId, offer.card)) return false;
  const game = room.game?.kind === "dj" ? room.game : null;
  if (offer.card === "shield") {
    return Boolean(game && game.djId === offer.playerId && !game.shield && game.pendingRatings?.[offer.raterId] != null);
  }
  if (offer.card === "block") {
    return Boolean(
      game &&
        game.pendingRatings?.[offer.raterId] != null &&
        !(game.blockVideoId && game.blockVideoId === game.videoId),
    );
  }
  if (offer.card === "hide") return !scoreHideUntil(room, offer.playerId);
  if (offer.card === "peek") {
    return Boolean(
      game &&
        game.step === "live" &&
        offer.key === `peek:${game.videoId}:${offer.play?.targetId}` &&
        typeof game.ratings?.[offer.play?.targetId] === "number",
    );
  }
  if (offer.card === "respin") {
    return Boolean(
      game &&
        game.step === "pick" &&
        game.djId === offer.playerId &&
        !(game.extraSpins > 0) &&
        offer.key === `respin:${game.round}:${game.wheelSpin}`,
    );
  }
  if (offer.card === "pass") {
    return Boolean(
      game &&
        game.djId === offer.playerId &&
        game.step !== "live" &&
        offer.key === `pass:${game.round}:${game.djId}`,
    );
  }
  if (offer.card === "extra") return Boolean(game && !game.extra && offer.key === `extra:${game.round}`);
  if (offer.card === "double") {
    return Boolean(
      game &&
        game.step === "live" &&
        offer.key === `double:${game.videoId}` &&
        game.djId !== offer.playerId &&
        game.ratings?.[offer.playerId] == null &&
        game.pendingRatings?.[offer.playerId] == null &&
        game.doubles?.[offer.playerId] !== game.videoId,
    );
  }
  return false;
}

export function pruneOffers(room) {
  if (!room.cards?.offers?.length) return [];
  const removed = [];
  room.cards.offers = room.cards.offers.filter((offer) => {
    if (stillUseful(room, offer)) return true;
    removed.push(offer);
    return false;
  });
  return removed;
}

export function enqueueOffer(room, offer) {
  if (!room.cards || !offer?.playerId || !offer.card || !offer.key) return false;
  ensureOffers(room);
  if (offer.card === "hide") {
    room.cards.offers = room.cards.offers.filter((item) => !(item.playerId === offer.playerId && item.card === "hide"));
  }
  const declined = room.cards.declined[offer.playerId] || [];
  if (declined.includes(offer.key)) return false;
  if (room.cards.offers.some((item) => item.playerId === offer.playerId && item.key === offer.key)) return false;
  room.cards.offerSeq = (room.cards.offerSeq || 0) + 1;
  room.cards.offers.push({
    id: String(room.cards.offerSeq),
    playerId: offer.playerId,
    card: offer.card,
    key: offer.key,
    text: offer.text,
    play: offer.play || {},
    gateId: offer.gateId || null,
    raterId: offer.raterId || null,
  });
  if (!stillUseful(room, room.cards.offers[room.cards.offers.length - 1])) {
    room.cards.offers.pop();
    return false;
  }
  return true;
}

export function offerHide(room, playerId) {
  const player = findPlayer(room, playerId);
  if (!player) return;
  enqueueOffer(room, {
    key: `hide:${player.score}`,
    playerId,
    card: "hide",
    text: `你的分數變成了 ${player.score}，要使用隱藏兩小時嗎？`,
  });
}

export function offerPeek(room, raterId) {
  const game = room.game;
  if (!game || game.kind !== "dj" || typeof game.ratings?.[raterId] !== "number") return;
  for (const player of room.players) {
    if (player.id === raterId) continue;
    enqueueOffer(room, {
      key: `peek:${game.videoId}:${raterId}`,
      playerId: player.id,
      card: "peek",
      text: "有人打了分，要偷看嗎？",
      play: { targetId: raterId },
    });
  }
}

export function offerRespin(room) {
  const game = room.game;
  if (!game || game.kind !== "dj") return;
  const label = songStopLabel(game.wheelSongs);
  enqueueOffer(room, {
    key: `respin:${game.round}:${game.wheelSpin}`,
    playerId: game.djId,
    card: "respin",
    text: `轉到了${label}，要再轉一次嗎？`,
  });
}

export function offerPass(room) {
  const game = room.game;
  if (!game || game.kind !== "dj") return;
  enqueueOffer(room, {
    key: `pass:${game.round}:${game.djId}`,
    playerId: game.djId,
    card: "pass",
    text: "輪到你了，要使用這輪不當嗎？",
  });
}

export function offerExtra(room) {
  const game = room.game;
  if (!game || game.kind !== "dj") return;
  const label = songStopLabel(game.songs) || "這幾首";
  for (const player of room.players) {
    enqueueOffer(room, {
      key: `extra:${game.round}`,
      playerId: player.id,
      card: "extra",
      text: `這一輪是${label}，要多播一首嗎？`,
    });
  }
}

export function offerDouble(room) {
  const game = room.game;
  if (!game || game.kind !== "dj") return;
  for (const player of room.players) {
    if (player.id === game.djId) continue;
    enqueueOffer(room, {
      key: `double:${game.videoId}`,
      playerId: player.id,
      card: "double",
      text: "這一首要使用加倍嗎？",
    });
  }
}

export function ratingGates(room, raterId, value) {
  const game = room.game;
  if (!game || (value !== -1 && value !== -2)) return [];
  const gates = [];
  if (!(game.blockVideoId && game.blockVideoId === game.videoId)) {
    for (const player of room.players) {
      if (player.id === raterId || !holds(room, player.id, "block")) continue;
      gates.push({
        key: `block:${game.videoId}:${raterId}`,
        playerId: player.id,
        card: "block",
        text: `有人給了 ${value}，要使用擋負分嗎？`,
        gateId: `rate:${raterId}`,
        raterId,
      });
    }
  }
  if (!game.shield && holds(room, game.djId, "shield")) {
    gates.push({
      key: `shield:${game.videoId}:${raterId}`,
      playerId: game.djId,
      card: "shield",
      text: `有人給了 ${value}，要使用護身嗎？`,
      gateId: `rate:${raterId}`,
      raterId,
    });
  }
  return gates;
}

export function declinePrompt(room, playerId) {
  const offers = room.cards?.offers || [];
  const index = offers.findIndex((offer) => offer.playerId === playerId);
  if (index < 0) return null;
  const [offer] = offers.splice(index, 1);
  if (!room.cards.declined[playerId]) room.cards.declined[playerId] = [];
  room.cards.declined[playerId].push(offer.key);
  return offer;
}

export function takeUsedPrompt(room, playerId, cardId) {
  const offers = room.cards?.offers || [];
  const index = offers.findIndex((offer) => offer.playerId === playerId && offer.card === cardId);
  if (index < 0) return null;
  const [offer] = offers.splice(index, 1);
  if (!room.cards.declined[playerId]) room.cards.declined[playerId] = [];
  room.cards.declined[playerId].push(offer.key);
  return offer;
}

export function promptFor(room, viewerId) {
  const offer = (room.cards?.offers || []).find((item) => item.playerId === viewerId);
  if (!offer) return null;
  return {
    card: offer.card,
    text: offer.text,
    targetId: offer.play?.targetId || null,
  };
}
