export const PROFESSIONS = [
  { id: "jiege", name: "傑哥", effect: "功能牌打到你時，可以說一次不要，改打到出牌的人。" },
  { id: "yaju", name: "野獸先輩", effect: "你當迪爵時，轉盤沒有此輪跳過。" },
  { id: "nailong", name: "奶龍", effect: "抓到了要另外三位旅伴按下。" },
  { id: "niulai", name: "牛來", effect: "打給你的分數會在 -2、-1、0、+1、+2 裡重抽。" },
  { id: "tung", name: "Tung Tung Tung Sahur", effect: "可以提早結束一位旅伴身上的一張功能牌。" },
  { id: "tralala", name: "Tralalero Tralala", effect: "每一輪迪爵可以重轉一次拉霸條件。" },
  { id: "bombardiro", name: "Bombardiro Crocodilo", effect: "這一趟可以清掉所有功能牌的倒數，只能用一次。" },
  { id: "ballerina", name: "Ballerina Cappuccina", effect: "每一輪迪爵可以再轉一次首數轉盤。" },
];

const HANDS = {
  scissors: "剪刀",
  rock: "石頭",
  paper: "布",
};

const NIULAI_SCORES = [-2, -1, 0, 1, 2];

export function professionOf(id) {
  return PROFESSIONS.find((item) => item.id === id) || null;
}

export function isProfession(player, id) {
  return player?.profession === id;
}

export function publicProfession(player) {
  const spec = professionOf(player?.profession);
  if (!spec) return null;
  return { id: spec.id, name: spec.name, effect: spec.effect };
}

export function niulaiScore() {
  return NIULAI_SCORES[Math.floor(Math.random() * NIULAI_SCORES.length)];
}

function findPlayer(room, id) {
  return room.players.find((player) => player.id === id) || null;
}

function handName(hand) {
  return HANDS[hand] || "";
}

function beats(a, b) {
  return (
    (a === "scissors" && b === "paper") ||
    (a === "rock" && b === "scissors") ||
    (a === "paper" && b === "rock")
  );
}

export function dropDuel(room, playerId) {
  const duel = room.duel;
  if (!duel) return;
  if (duel.challengerId === playerId || duel.holderId === playerId) room.duel = null;
}

export function takeRole(room, playerId, roleId) {
  const job = professionOf(roleId);
  if (!job) return { error: "沒有這個職業。" };
  const player = findPlayer(room, playerId);
  if (!player) return { error: "你不在這個房間。" };
  if (room.duel) return { error: "現在有人在猜拳。" };
  const holder = room.players.find((item) => item.profession === job.id) || null;
  if (!holder) {
    if (player.profession) return { error: "你已經有職業了。" };
    player.profession = job.id;
    room.roleResult = null;
    return { ok: true };
  }
  if (holder.id === player.id) return { error: "這是你的職業。" };
  if (player.profession) return { error: "你已經有職業了。" };
  room.duel = {
    roleId: job.id,
    challengerId: player.id,
    holderId: holder.id,
    picks: {},
  };
  room.roleResult = null;
  return { ok: true };
}

export function throwHand(room, playerId, hand) {
  const duel = room.duel;
  if (!duel) return { error: "現在沒有猜拳。" };
  if (playerId !== duel.challengerId && playerId !== duel.holderId) return { error: "這局猜拳不是你。" };
  if (!HANDS[hand]) return { error: "請出剪刀、石頭或布。" };
  if (duel.picks[playerId]) return { error: "你已經出了。" };
  duel.picks[playerId] = hand;
  room.roleResult = null;
  const challengerHand = duel.picks[duel.challengerId];
  const holderHand = duel.picks[duel.holderId];
  if (!challengerHand || !holderHand) return { ok: true };
  const challenger = findPlayer(room, duel.challengerId);
  const holder = findPlayer(room, duel.holderId);
  const tie = challengerHand === holderHand;
  const winnerId = tie ? null : beats(challengerHand, holderHand) ? duel.challengerId : duel.holderId;
  const reveal = {
    roleId: duel.roleId,
    roleName: professionOf(duel.roleId)?.name || "",
    challengerId: duel.challengerId,
    holderId: duel.holderId,
    challengerName: challenger?.nickname || "旅伴",
    holderName: holder?.nickname || "旅伴",
    challengerHand: handName(challengerHand),
    holderHand: handName(holderHand),
    tie,
    winnerId,
    winnerName: "",
  };
  if (tie) {
    duel.picks = {};
    room.roleResult = reveal;
    return { ok: true };
  }
  const winner = findPlayer(room, winnerId);
  const loserId = winnerId === duel.challengerId ? duel.holderId : duel.challengerId;
  const loser = findPlayer(room, loserId);
  if (loser) {
    if (loser.profession === "jiege") loser.jiegeUsed = false;
    delete loser.profession;
  }
  if (winner) winner.profession = duel.roleId;
  reveal.winnerName = winner?.nickname || "旅伴";
  room.duel = null;
  room.roleResult = reveal;
  return { ok: true, lostJiege: loser?.id && reveal.roleId === "jiege" ? loser.id : null };
}

function presentDuel(room, viewerId) {
  const duel = room.duel;
  if (!duel) return null;
  const challenger = findPlayer(room, duel.challengerId);
  const holder = findPlayer(room, duel.holderId);
  const mine = duel.picks[viewerId];
  return {
    roleId: duel.roleId,
    roleName: professionOf(duel.roleId)?.name || "",
    challengerId: duel.challengerId,
    holderId: duel.holderId,
    challengerName: challenger?.nickname || "旅伴",
    holderName: holder?.nickname || "旅伴",
    youChose: mine ? handName(mine) : "",
    challengerReady: Boolean(duel.picks[duel.challengerId]),
    holderReady: Boolean(duel.picks[duel.holderId]),
  };
}

export function presentRoleState(room, viewerId) {
  return {
    roles: PROFESSIONS.map((job) => {
      const holder = room.players.find((player) => player.profession === job.id) || null;
      return {
        id: job.id,
        name: job.name,
        effect: job.effect,
        holderId: holder?.id || null,
        holderName: holder?.nickname || "",
      };
    }),
    duel: presentDuel(room, viewerId),
    roleResult: room.roleResult || null,
  };
}
