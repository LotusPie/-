import { promptDeck, vibeDeck, scoreDeck } from "./decks.mjs";

const MAX_PLAYERS = 12;
const NICK_MAX = 12;
const NOTE_MAX = 40;

const DECKS = { prompt: promptDeck, vibe: vibeDeck, score: scoreDeck };

function shuffle(list) {
  const arr = [...list];
  for (let i = arr.length - 1; i > 0; i -= 1) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

export function normalizeNickname(raw) {
  const name = String(raw ?? "")
    .replace(/[\u0000-\u001f]/g, "")
    .trim()
    .replace(/\s+/g, " ");
  const length = [...name].length;
  if (!length) return { error: "請先取一個暱稱。" };
  if (length > NICK_MAX) return { error: "暱稱最多 12 個字。" };
  return { nickname: name };
}

export function normalizeCode(raw) {
  const code = String(raw ?? "")
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "");
  if (code.length !== 4) return { error: "房間代碼是 4 個字。" };
  return { code };
}

function freshPiles() {
  return {
    prompt: shuffle(promptDeck.map((card) => card.id)),
    vibe: shuffle(vibeDeck.map((card) => card.id)),
    score: shuffle(scoreDeck.map((card) => card.id)),
  };
}

export function createPlayerRoom(nickname) {
  const parsed = normalizeNickname(nickname);
  if (parsed.error) return { error: parsed.error };
  const player = {
    id: crypto.randomUUID(),
    nickname: parsed.nickname,
    score: 0,
    connected: true,
    ws: null,
  };
  const room = {
    code: "",
    hostId: player.id,
    players: [player],
    phase: "lobby",
    game: null,
    piles: freshPiles(),
    lastDrawn: {},
    cursors: { vibe: 0, honor: 0, treat: 0 },
    updatedAt: Date.now(),
  };
  return { room, youId: player.id };
}

export function debugSetNext(room, key, id) {
  const rest = room.piles[key].filter((item) => item !== id);
  room.piles[key] = [id, ...rest];
}

function active(room) {
  return room.players.filter((player) => player.connected);
}

function isOnline(room, id) {
  return room.players.some((player) => player.id === id && player.connected);
}

function findPlayer(room, id) {
  return room.players.find((player) => player.id === id) || null;
}

export function rankingOf(players) {
  const sorted = [...players].sort(
    (a, b) => b.score - a.score || a.nickname.localeCompare(b.nickname, "zh-Hant"),
  );
  let lastScore = null;
  let lastRank = 0;
  return sorted.map((player, index) => {
    const rank = player.score === lastScore ? lastRank : index + 1;
    lastScore = player.score;
    lastRank = rank;
    return {
      rank,
      id: player.id,
      nickname: player.nickname,
      score: player.score,
      connected: player.connected,
    };
  });
}

function addScore(player, delta) {
  const next = Math.max(0, player.score + delta);
  const applied = next - player.score;
  player.score = next;
  return applied;
}

function clip(text, max) {
  return [...String(text ?? "").replace(/[\u0000-\u001f]/g, "").trim()].slice(0, max).join("");
}

function drawCard(room, key) {
  const deck = DECKS[key];
  if (!room.piles[key].length) {
    room.piles[key] = shuffle(deck.map((card) => card.id));
    if (room.piles[key].length > 1 && room.piles[key][0] === room.lastDrawn[key]) {
      room.piles[key].push(room.piles[key].shift());
    }
  }
  const id = room.piles[key].shift();
  room.lastDrawn[key] = id;
  const card = deck.find((item) => item.id === id);
  if (!card) throw new Error(`missing card ${id}`);
  return {
    id: card.id,
    text: card.text,
    tag: card.tag || "",
    type: card.type || "",
    kicker: card.kicker || "",
    honor: card.honor || "",
    options: card.options?.map((option) => ({ id: option.id, label: option.label })),
  };
}

function takeAssignee(room, cursorKey) {
  const players = active(room);
  if (!players.length) return null;
  const index = room.cursors[cursorKey] % players.length;
  room.cursors[cursorKey] += 1;
  return players[index].id;
}

function nicknameOf(room, id) {
  return findPlayer(room, id)?.nickname || "旅伴";
}

function finishScore(room, summary, awards, tally = []) {
  room.game.result = {
    summary,
    awards,
    tally,
    ranking: rankingOf(room.players),
  };
}

function voteEntries(room) {
  return Object.entries(room.game.votes).filter(([id]) => findPlayer(room, id));
}

function settleVote(room) {
  const game = room.game;
  const entries = voteEntries(room);
  if (!entries.length) return { error: "至少要有一個人投票。" };
  const counts = new Map();
  for (const [, choice] of entries) counts.set(choice, (counts.get(choice) || 0) + 1);
  let max = 0;
  for (const count of counts.values()) max = Math.max(max, count);
  const winnerIds = [...counts.entries()].filter(([, count]) => count === max).map(([id]) => id);
  const delta = winnerIds.length === 1 ? 3 : 2;
  const awards = winnerIds.map((id) => {
    const player = findPlayer(room, id);
    const applied = player ? addScore(player, delta) : 0;
    return { playerId: id, delta: applied, label: game.card.honor || "本輪旅伴" };
  });
  const names = winnerIds.map((id) => nicknameOf(room, id)).join("、");
  const summary = winnerIds.length === 1 ? `最多人投給${names}。` : `${names} 平手。`;
  const tally = [...counts.entries()]
    .map(([id, count]) => ({ id, label: nicknameOf(room, id), count }))
    .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label, "zh-Hant"));
  finishScore(room, summary, awards, tally);
  return { ok: true };
}

function settleChoice(room) {
  const game = room.game;
  const entries = voteEntries(room);
  if (!entries.length) return { error: "至少要有一個人選一個答案。" };
  const counts = new Map();
  for (const [, choice] of entries) counts.set(choice, (counts.get(choice) || 0) + 1);
  let max = 0;
  for (const count of counts.values()) max = Math.max(max, count);
  const awards = [];
  if (max >= 2) {
    const winning = new Set(
      [...counts.entries()].filter(([, count]) => count === max).map(([id]) => id),
    );
    for (const [playerId, choice] of entries) {
      if (!winning.has(choice)) continue;
      const player = findPlayer(room, playerId);
      awards.push({
        playerId,
        delta: player ? addScore(player, 2) : 0,
        label: "心意相近",
      });
    }
  } else {
    for (const [playerId] of entries) {
      const player = findPlayer(room, playerId);
      awards.push({
        playerId,
        delta: player ? addScore(player, 1) : 0,
        label: "各有所愛",
      });
    }
  }
  const optionLabel = (id) => game.card.options?.find((option) => option.id === id)?.label || id;
  const tally = [...counts.entries()]
    .map(([id, count]) => ({ id, label: optionLabel(id), count }))
    .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label, "zh-Hant"));
  const top = tally
    .filter((item) => item.count === max)
    .map((item) => item.label)
    .join("、");
  const summary = max >= 2 ? `大家比較偏向「${top}」。` : "每個人選的都不一樣，也好。";
  finishScore(room, summary, awards, tally);
  return { ok: true };
}

function autoSettle(room) {
  const game = room.game;
  if (!game || game.kind !== "score" || game.result || !game.card) return;
  if (game.card.type !== "vote" && game.card.type !== "choice") return;
  const voters = active(room);
  if (!voters.length) return;
  if (!voters.every((player) => game.votes[player.id])) return;
  if (game.card.type === "vote") settleVote(room);
  else settleChoice(room);
}

export function reconcile(room) {
  const game = room.game;
  if (!game) return;
  if (game.kind === "prompt" && game.card && !game.done) {
    const online = new Set(active(room).map((player) => player.id));
    while (game.turnIndex < game.turnOrder.length && !online.has(game.turnOrder[game.turnIndex])) {
      game.turnIndex += 1;
    }
    if (game.turnIndex >= game.turnOrder.length) game.done = true;
  }
  if (game.kind === "vibe" && game.card && !game.done && !isOnline(room, game.assigneeId)) {
    const next = active(room)[0];
    if (next) game.assigneeId = next.id;
  }
  if (
    game.kind === "score" &&
    game.card &&
    !game.result &&
    (game.card.type === "honor" || game.card.type === "treat") &&
    !isOnline(room, game.assigneeId)
  ) {
    const next = active(room)[0];
    if (next) game.assigneeId = next.id;
  }
  autoSettle(room);
}

export function joinRoom(room, { nickname, playerId } = {}) {
  room.updatedAt = Date.now();
  if (playerId) {
    const existing = findPlayer(room, playerId);
    if (existing) {
      existing.connected = true;
      if (nickname) {
        const parsed = normalizeNickname(nickname);
        if (!parsed.error) existing.nickname = parsed.nickname;
      }
      reconcile(room);
      return { youId: existing.id };
    }
  }
  if (room.players.length >= MAX_PLAYERS) {
    return { error: "這個房間滿了（最多 12 位旅伴）。" };
  }
  const parsed = normalizeNickname(nickname);
  if (parsed.error) return { error: parsed.error };
  const player = {
    id: crypto.randomUUID(),
    nickname: parsed.nickname,
    score: 0,
    connected: true,
    ws: null,
  };
  room.players.push(player);
  return { youId: player.id };
}

export function disconnectPlayer(room, playerId) {
  const player = findPlayer(room, playerId);
  if (!player) return;
  player.connected = false;
  player.ws = null;
  room.updatedAt = Date.now();
  reconcile(room);
}

function removeFromPrompt(game, playerId) {
  if (!game || game.kind !== "prompt" || !game.card || game.done) return;
  const index = game.turnOrder.indexOf(playerId);
  if (index === -1) return;
  game.turnOrder.splice(index, 1);
  if (index < game.turnIndex) game.turnIndex -= 1;
  if (game.turnIndex >= game.turnOrder.length) game.done = true;
}

export function removePlayer(room, playerId) {
  removeFromPrompt(room.game, playerId);
  const leaving = room.players.find((player) => player.id === playerId);
  if (leaving?.ws) {
    leaving.ws.playerId = null;
    leaving.ws.roomCode = null;
    leaving.ws = null;
  }
  room.players = room.players.filter((player) => player.id !== playerId);
  if (room.hostId === playerId && room.players[0]) room.hostId = room.players[0].id;
  if (room.game?.votes) delete room.game.votes[playerId];
  room.updatedAt = Date.now();
  reconcile(room);
}

function blankGame(kind) {
  if (kind === "prompt") {
    return {
      kind,
      round: 0,
      card: null,
      turnOrder: [],
      turnIndex: 0,
      notes: {},
      spoken: {},
      done: false,
    };
  }
  if (kind === "vibe") {
    return { kind, round: 0, card: null, assigneeId: null, done: false, reactions: [] };
  }
  return { kind, round: 0, card: null, assigneeId: null, votes: {}, result: null };
}

function start(room, kind) {
  if (!["prompt", "vibe", "score"].includes(kind)) return { error: "沒有這個遊戲。" };
  if (!active(room).length) return { error: "現在沒有人在線上。" };
  room.phase = "playing";
  room.game = blankGame(kind);
  return draw(room, true);
}

function draw(room, force) {
  const previous = room.game;
  if (!previous) return { error: "先選一個遊戲。" };
  if (previous.kind === "prompt" && previous.card && !previous.done && !force) {
    return { error: "這一輪還沒說完。" };
  }
  if (previous.kind === "vibe" && previous.card && !previous.done && !force) {
    return { error: "這個挑戰還沒完成。" };
  }
  if (previous.kind === "score" && previous.card && !previous.result && !force) {
    return { error: "這一輪還沒揭曉。" };
  }
  const round = (previous.round || 0) + 1;
  if (previous.kind === "prompt") {
    const card = drawCard(room, "prompt");
    const turnOrder = active(room).map((player) => player.id);
    room.game = {
      kind: "prompt",
      round,
      card,
      turnOrder,
      turnIndex: 0,
      notes: {},
      spoken: {},
      done: turnOrder.length === 0,
    };
    return { ok: true };
  }
  if (previous.kind === "vibe") {
    room.game = {
      kind: "vibe",
      round,
      card: drawCard(room, "vibe"),
      assigneeId: takeAssignee(room, "vibe"),
      done: false,
      reactions: [],
    };
    return { ok: true };
  }
  const card = drawCard(room, "score");
  const assigneeId =
    card.type === "honor"
      ? takeAssignee(room, "honor")
      : card.type === "treat"
        ? takeAssignee(room, "treat")
        : null;
  room.game = { kind: "score", round, card, assigneeId, votes: {}, result: null };
  return { ok: true };
}

function speak(room, playerId, note) {
  const game = room.game;
  if (game.kind !== "prompt" || !game.card) return { error: "現在不是開話題。" };
  if (game.done) return { error: "這一輪已經說完了。" };
  if (game.turnOrder[game.turnIndex] !== playerId) return { error: "還沒輪到你。" };
  game.notes[playerId] = clip(note || "", NOTE_MAX);
  game.spoken[playerId] = true;
  const player = findPlayer(room, playerId);
  if (player) addScore(player, 1);
  game.turnIndex += 1;
  reconcile(room);
  return { ok: true };
}

function complete(room, playerId) {
  const game = room.game;
  if (game.kind !== "vibe" || !game.card) return { error: "現在不是帶動氣氛。" };
  if (game.done) return { error: "這個挑戰已經完成了。" };
  if (game.assigneeId !== playerId) return { error: "這張挑戰是交給另一位旅伴的。" };
  const player = findPlayer(room, playerId);
  if (player) addScore(player, 2);
  game.done = true;
  return { ok: true };
}

function react(room, playerId) {
  const game = room.game;
  if (game.kind !== "vibe" || !game.card) return { error: "現在沒有可以鼓掌的挑戰。" };
  if (playerId === game.assigneeId) return { error: "把掌聲留給旅伴吧。" };
  if (game.reactions.includes(playerId)) return { error: "你已經鼓過掌了。" };
  game.reactions.push(playerId);
  return { ok: true };
}

function vote(room, playerId, choice) {
  const game = room.game;
  if (game.kind !== "score" || !game.card) return { error: "現在不是積分獎懲。" };
  if (game.result) return { error: "這一輪已經揭曉了。" };
  if (game.card.type !== "vote" && game.card.type !== "choice") {
    return { error: "這張卡不用選。" };
  }
  if (game.card.type === "vote") {
    const online = active(room);
    const target = findPlayer(room, choice);
    if (!target || !target.connected) return { error: "請投給在線上的旅伴。" };
    if (target.id === playerId && online.length > 1) return { error: "這輪把票投給別人吧。" };
  } else if (!game.card.options?.some((option) => option.id === choice)) {
    return { error: "沒有這個選項。" };
  }
  game.votes[playerId] = choice;
  autoSettle(room);
  return { ok: true };
}

function confirmCard(room, playerId) {
  const game = room.game;
  if (game.kind !== "score" || !game.card) return { error: "現在不是積分獎懲。" };
  if (game.result) return { error: "這一輪已經揭曉了。" };
  if (game.card.type !== "honor" && game.card.type !== "treat") {
    return { error: "這張卡不用確認。" };
  }
  if (game.assigneeId !== playerId) return { error: "這張是交給另一位旅伴的。" };
  const player = findPlayer(room, playerId);
  if (!player) return { error: "找不到這位旅伴。" };
  if (game.card.type === "honor") {
    const applied = addScore(player, 3);
    finishScore(room, `${player.nickname} 收下了獎勵。`, [
      { playerId, delta: applied, label: "獎勵" },
    ]);
    return { ok: true };
  }
  const others = active(room).filter((item) => item.id !== playerId);
  if (!others.length) {
    const applied = addScore(player, 1);
    finishScore(room, "一個人旅行也好好招待自己。", [
      { playerId, delta: applied, label: "招待自己" },
    ]);
    return { ok: true };
  }
  const selfDelta = addScore(player, -1);
  const awards = [
    {
      playerId,
      delta: selfDelta,
      label: selfDelta === 0 ? "請客（分數已經是 0，不再往下扣）" : "請客",
    },
  ];
  for (const other of others) {
    awards.push({ playerId: other.id, delta: addScore(other, 1), label: "被請到了" });
  }
  finishScore(room, `${player.nickname} 完成了這張小懲。`, awards);
  return { ok: true };
}

function settle(room) {
  const game = room.game;
  if (game.kind !== "score" || !game.card) return { error: "現在不是積分獎懲。" };
  if (game.result) return { error: "這一輪已經揭曉了。" };
  if (game.card.type === "vote") return settleVote(room);
  if (game.card.type === "choice") return settleChoice(room);
  return { error: "這張卡會在本人確認後揭曉。" };
}

function passAssignee(room) {
  const game = room.game;
  const canPass =
    (game.kind === "vibe" && game.card && !game.done) ||
    (game.kind === "score" &&
      game.card &&
      !game.result &&
      (game.card.type === "honor" || game.card.type === "treat"));
  if (!canPass) return { error: "這張不能換人。" };
  const players = active(room);
  if (players.length <= 1) return { error: "現在只有你，這張就留給你吧。" };
  const index = players.findIndex((player) => player.id === game.assigneeId);
  const next = players[(index + 1) % players.length];
  game.assigneeId = next.id;
  if (game.kind === "vibe") game.reactions = [];
  return { ok: true };
}

function lobby(room) {
  room.phase = "lobby";
  room.game = null;
  return { ok: true };
}

export function applyAction(room, playerId, msg) {
  const player = findPlayer(room, playerId);
  if (!player) return { error: "你不在這個房間。" };
  room.updatedAt = Date.now();
  const name = msg?.name;
  if (name === "start") return start(room, msg.game);
  if (name === "lobby") return lobby(room);
  if (!room.game) return { error: "現在還在房間裡，先選一個遊戲。" };
  if (name === "draw") return draw(room, Boolean(msg.force));
  if (name === "speak") return speak(room, playerId, msg.note);
  if (name === "complete") return complete(room, playerId);
  if (name === "react") return react(room, playerId);
  if (name === "vote") return vote(room, playerId, msg.choice);
  if (name === "confirm") return confirmCard(room, playerId);
  if (name === "settle") return settle(room);
  if (name === "pass") return passAssignee(room);
  return { error: "還沒有這個動作。" };
}

export function serialize(room, viewerId) {
  const game = room.game;
  let votes = {};
  if (game?.kind === "score") {
    if (game.result) {
      votes = { ...game.votes };
    } else {
      for (const id of Object.keys(game.votes || {})) {
        votes[id] = id === viewerId ? game.votes[id] : true;
      }
    }
  }
  return {
    code: room.code,
    hostId: room.hostId,
    phase: room.phase,
    players: room.players.map((player) => ({
      id: player.id,
      nickname: player.nickname,
      score: player.score,
      connected: player.connected,
    })),
    ranking: rankingOf(room.players),
    game: game
      ? {
          kind: game.kind,
          round: game.round,
          card: game.card,
          turnOrder: game.turnOrder || [],
          turnIndex: game.turnIndex ?? 0,
          notes: game.notes || {},
          spoken: game.spoken || {},
          done: Boolean(game.done),
          assigneeId: game.assigneeId || null,
          reactions: game.reactions || [],
          votes,
          result: game.result || null,
        }
      : null,
  };
}
