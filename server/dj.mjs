const SONG_MAX = 36;
const ARTIST_MAX = 24;

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

function others(room) {
  return active(room).filter((player) => player.id !== room.game.djId);
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
    song: null,
    submissions: [],
    passedSubmit: [],
    ratings: {},
    ratingApplied: false,
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

function openJudgeOrRate(room) {
  const game = room.game;
  const pending = game.submissions.some((item) => !item.verdict && findPlayer(room, item.playerId));
  game.step = pending ? "judge" : "rate";
}

function maybeCloseCollect(room) {
  const game = room.game;
  if (game.step !== "collect") return;
  const waiting = others(room).some(
    (player) =>
      !game.passedSubmit.includes(player.id) &&
      !game.submissions.some((item) => item.playerId === player.id),
  );
  if (!waiting) openJudgeOrRate(room);
}

function currentSubmission(room) {
  return room.game.submissions.find((item) => !item.verdict && findPlayer(room, item.playerId)) || null;
}

function finishRound(room) {
  const game = room.game;
  if (game.ratingApplied) return advanceDj(room, "served");
  let sum = 0;
  for (const value of Object.values(game.ratings)) {
    if (typeof value === "number") sum += value;
  }
  addExact(findPlayer(room, game.djId), sum);
  game.ratingApplied = true;
  game.ratingSum = sum;
  return advanceDj(room, "served");
}

function requireDj(room) {
  if (room.game?.kind !== "dj") return { error: "現在不是如果我是迪爵。" };
  return null;
}

export function handleDjAction(room, playerId, msg) {
  const blocked = requireDj(room);
  if (blocked) return blocked;
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
    const title = clip(msg.title, SONG_MAX);
    if (!title) return { error: "先寫歌名。" };
    game.song = { title, artist: clip(msg.artist, ARTIST_MAX) };
    game.step = game.mode === "playlist" && others(room).length ? "collect" : "rate";
    return { ok: true };
  }

  if (name === "djSubmit") {
    if (game.step !== "collect") return { error: "現在還不能交歌。" };
    if (playerId === game.djId) return { error: "迪爵這輪不用交歌。" };
    if (
      game.passedSubmit.includes(playerId) ||
      game.submissions.some((item) => item.playerId === playerId)
    ) {
      return { error: "你已經交過了。" };
    }
    const title = clip(msg.title, SONG_MAX);
    if (!title) return { error: "先寫歌名。" };
    game.submissions.push({
      id: crypto.randomUUID(),
      playerId,
      title,
      artist: clip(msg.artist, ARTIST_MAX),
      verdict: null,
    });
    maybeCloseCollect(room);
    return { ok: true };
  }

  if (name === "djPassSubmit") {
    if (game.step !== "collect") return { error: "現在還不能跳過。" };
    if (playerId === game.djId) return { error: "迪爵這輪不用交歌。" };
    if (
      game.passedSubmit.includes(playerId) ||
      game.submissions.some((item) => item.playerId === playerId)
    ) {
      return { error: "你已經交過了。" };
    }
    game.passedSubmit.push(playerId);
    maybeCloseCollect(room);
    return { ok: true };
  }

  if (name === "djClose") {
    if (game.step !== "collect") return { error: "現在還不能收歌單。" };
    openJudgeOrRate(room);
    return { ok: true };
  }

  if (name === "djJudge") {
    if (game.step !== "judge") return { error: "現在還不能判斷。" };
    if (playerId !== game.djId) return { error: "這輪由迪爵判斷。" };
    const submission = currentSubmission(room);
    if (!submission || submission.id !== msg.submissionId) return { error: "這首已經看過了。" };
    if (msg.verdict !== "like" && msg.verdict !== "unlike") return { error: "請選像我或不像。" };
    submission.verdict = msg.verdict;
    if (msg.verdict === "like") addExact(findPlayer(room, submission.playerId), 2);
    if (currentSubmission(room)) return { ok: true };
    game.step = "rate";
    return { ok: true };
  }

  if (name === "djRate") {
    if (game.step !== "rate") return { error: "現在還不能評。" };
    if (playerId === game.djId) return { error: "迪爵這輪不評自己的歌。" };
    const value = msg.value;
    const allowed = value === "none" || value === -2 || value === -1 || value === 0 || value === 1 || value === 2;
    if (!allowed) return { error: "沒有這個分數。" };
    game.ratings[playerId] = value;
    const allIn = others(room).every((player) => game.ratings[player.id] != null);
    if (allIn) return finishRound(room);
    return { ok: true };
  }

  if (name === "djFinish") {
    if (game.step !== "rate") return { error: "這輪還沒到結束。" };
    return finishRound(room);
  }

  return { error: "還沒有這個動作。" };
}

export function advanceDisconnectedDj(room) {
  const game = room.game;
  if (!game || game.kind !== "dj" || !game.djId) return;
  if (findPlayer(room, game.djId)?.connected) return;
  if (!active(room).length) return;
  if (game.step === "rate" && !game.ratingApplied) {
    finishRound(room);
    return;
  }
  advanceDj(room, "skip");
}
