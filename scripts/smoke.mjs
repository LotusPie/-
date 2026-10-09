import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { WebSocket } from "ws";
import { promptDeck, vibeDeck, scoreDeck } from "../server/decks.mjs";
import { expireDj, parseYoutubeVideoId } from "../server/dj.mjs";
import { expectedPlayhead, planFollow } from "../client/src/watch-sync.js";
import {
  applyAction,
  createPlayerRoom,
  debugSetNext,
  joinRoom,
  removePlayer,
  serialize,
} from "../server/logic.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function scoreOf(room, id) {
  return room.players.find((player) => player.id === id).score;
}

function freshRoom(names) {
  const created = createPlayerRoom(names[0]);
  assert.equal(created.error, undefined);
  created.room.code = "TEST";
  const ids = [created.youId];
  for (const name of names.slice(1)) {
    const joined = joinRoom(created.room, { nickname: name });
    assert.equal(joined.error, undefined);
    ids.push(joined.youId);
  }
  return { room: created.room, ids };
}

function testDecksAndCopy() {
  assert.ok(promptDeck.length >= 30, "prompt deck");
  assert.ok(vibeDeck.length >= 20, "vibe deck");
  assert.ok(scoreDeck.length >= 8, "score deck");
  for (const deck of [promptDeck, vibeDeck, scoreDeck]) {
    const ids = deck.map((card) => card.id);
    assert.equal(new Set(ids).size, ids.length);
    for (const card of deck) assert.ok(card.text && card.text.length > 6);
  }
  for (const card of vibeDeck) assert.ok(card.tag);
  for (const card of scoreDeck) assert.ok(["vote", "choice", "honor", "treat"].includes(card.type));
  assert.ok(promptDeck.some((card) => card.text.includes("這趟")));
  assert.ok(promptDeck.some((card) => card.text.includes("哪裡")));

  const banned = ["游戏", "房间", "挑战", "积分", "链接", "离线", "计分", "设置", "软件", "网络", "视频", "默认", "开始"];
  const files = [
    "README.md",
    "server/decks.mjs",
    "server/logic.mjs",
    "server/index.mjs",
    "client/index.html",
    "client/src/App.jsx",
    "client/src/play.jsx",
    "client/src/dj.jsx",
    "client/src/watch-sync.js",
    "client/src/useRoom.js",
    "server/dj.mjs",
  ];
  for (const file of files) {
    const text = fs.readFileSync(path.join(root, file), "utf8");
    for (const word of banned) {
      assert.equal(text.includes(word), false, `${file} contains simplified ${word}`);
    }
  }
}

function testLogic() {
  assert.equal(createPlayerRoom("   ").error, "請先取一個暱稱。");
  assert.ok(createPlayerRoom("一二三四五六七八九十一二三").error);

  const { room, ids } = freshRoom(["阿凱", "小魚", "阿德"]);
  const [a, b, c] = ids;
  assert.equal(room.players.length, 3);

  const full = freshRoom(["甲"]);
  for (let i = 0; i < 11; i += 1) joinRoom(full.room, { nickname: `旅伴${i}` });
  assert.ok(joinRoom(full.room, { nickname: "太多" }).error);

  debugSetNext(room, "prompt", "prompt-01");
  assert.equal(applyAction(room, b, { name: "start", game: "prompt" }).ok, true);
  assert.equal(room.game.card.id, "prompt-01");
  assert.equal(room.game.turnOrder[0], a);
  assert.match(applyAction(room, b, { name: "speak", note: "太早" }).error, /還沒輪到你/);
  const longNote = "一二三四五六七八九十".repeat(5);
  assert.equal(applyAction(room, a, { name: "speak", note: longNote }).ok, true);
  assert.equal([...room.game.notes[a]].length, 40);
  assert.equal(scoreOf(room, a), 1);
  assert.equal(applyAction(room, b, { name: "speak", note: "老街再走一天" }).ok, true);
  assert.equal(applyAction(room, c, { name: "speak", note: "" }).ok, true);
  assert.equal(room.game.done, true);
  assert.equal(scoreOf(room, b), 1);
  assert.equal(scoreOf(room, c), 1);
  const shared = serialize(room, b);
  assert.equal(shared.game.notes[b], "老街再走一天");
  assert.equal(shared.piles, undefined);

  debugSetNext(room, "vibe", "vibe-02");
  assert.equal(applyAction(room, a, { name: "start", game: "vibe" }).ok, true);
  assert.equal(room.game.card.id, "vibe-02");
  assert.equal(room.game.assigneeId, a);
  assert.match(applyAction(room, a, { name: "react" }).error, /掌聲/);
  assert.equal(applyAction(room, b, { name: "react" }).ok, true);
  assert.equal(applyAction(room, a, { name: "pass" }).ok, true);
  assert.equal(room.game.assigneeId, b);
  assert.equal(room.game.reactions.length, 0);
  const before = scoreOf(room, b);
  assert.equal(applyAction(room, b, { name: "complete" }).ok, true);
  assert.equal(scoreOf(room, b), before + 2);
  assert.equal(room.game.done, true);

  debugSetNext(room, "score", "score-01");
  assert.equal(applyAction(room, c, { name: "start", game: "score" }).ok, true);
  assert.equal(room.game.card.type, "vote");
  assert.match(applyAction(room, b, { name: "vote", choice: b }).error, /別人/);
  assert.equal(applyAction(room, a, { name: "vote", choice: b }).ok, true);
  const masked = serialize(room, c);
  assert.equal(masked.game.votes[a], true);
  assert.equal(serialize(room, a).game.votes[a], b);
  assert.equal(room.game.result, null);
  assert.equal(applyAction(room, b, { name: "vote", choice: a }).ok, true);
  assert.equal(applyAction(room, c, { name: "vote", choice: b }).ok, true);
  assert.ok(room.game.result);
  assert.equal(room.game.result.awards.find((award) => award.playerId === b).delta, 3);
  assert.match(room.game.result.summary, /小魚/);
  const revealed = serialize(room, a);
  assert.equal(revealed.game.votes[c], b);
  assert.ok(revealed.game.result.ranking.length === 3);

  const tie = freshRoom(["阿凱", "小魚"]);
  debugSetNext(tie.room, "score", "score-06");
  applyAction(tie.room, tie.ids[0], { name: "start", game: "score" });
  applyAction(tie.room, tie.ids[0], { name: "vote", choice: tie.ids[1] });
  applyAction(tie.room, tie.ids[1], { name: "vote", choice: tie.ids[0] });
  assert.equal(tie.room.game.result.awards.length, 2);
  assert.ok(tie.room.game.result.awards.every((award) => award.delta === 2));

  const choice = freshRoom(["阿凱", "小魚", "阿德"]);
  debugSetNext(choice.room, "score", "score-11");
  applyAction(choice.room, choice.ids[0], { name: "start", game: "score" });
  applyAction(choice.room, choice.ids[0], { name: "vote", choice: "a" });
  applyAction(choice.room, choice.ids[1], { name: "vote", choice: "a" });
  applyAction(choice.room, choice.ids[2], { name: "vote", choice: "b" });
  assert.equal(scoreOf(choice.room, choice.ids[0]), 2);
  assert.equal(scoreOf(choice.room, choice.ids[1]), 2);
  assert.equal(scoreOf(choice.room, choice.ids[2]), 0);
  assert.match(choice.room.game.result.summary, /海邊或河岸/);

  const honor = freshRoom(["阿凱", "小魚"]);
  debugSetNext(honor.room, "score", "score-15");
  applyAction(honor.room, honor.ids[0], { name: "start", game: "score" });
  assert.equal(honor.room.game.assigneeId, honor.ids[0]);
  assert.equal(applyAction(honor.room, honor.ids[0], { name: "confirm" }).ok, true);
  assert.equal(scoreOf(honor.room, honor.ids[0]), 3);

  const treat = freshRoom(["阿凱", "小魚"]);
  debugSetNext(treat.room, "score", "score-17");
  applyAction(treat.room, treat.ids[0], { name: "start", game: "score" });
  assert.equal(scoreOf(treat.room, treat.ids[0]), 0);
  assert.equal(applyAction(treat.room, treat.ids[0], { name: "confirm" }).ok, true);
  assert.equal(scoreOf(treat.room, treat.ids[0]), 0);
  assert.equal(scoreOf(treat.room, treat.ids[1]), 1);
  assert.match(treat.room.game.result.awards[0].label, /不再往下扣/);

  const recycle = freshRoom(["阿凱"]);
  for (let i = 0; i < promptDeck.length + 2; i += 1) {
    const drawn = applyAction(recycle.room, recycle.ids[0], { name: "draw", force: true });
    if (!recycle.room.game) {
      applyAction(recycle.room, recycle.ids[0], { name: "start", game: "prompt" });
    } else if (drawn.error) {
      throw new Error(drawn.error);
    }
    assert.ok(recycle.room.game.card.text.length > 6);
  }

  const host = freshRoom(["阿凱", "小魚"]);
  removePlayer(host.room, host.ids[0]);
  assert.equal(host.room.hostId, host.ids[1]);

  applyAction(room, a, { name: "lobby" });
  assert.equal(room.phase, "lobby");
  assert.equal(room.game, null);
  assert.ok(scoreOf(room, a) > 0);
  testDj();
}

function testDj() {
  const music = "https://music.youtube.com/watch?v=dQw4w9WgXcQ&si=abc";
  const watch = "https://www.youtube.com/watch?v=M7lc1UVf-VE&t=12";
  assert.equal(parseYoutubeVideoId(music), "dQw4w9WgXcQ");
  assert.equal(parseYoutubeVideoId(watch), "M7lc1UVf-VE");
  assert.equal(parseYoutubeVideoId("https://youtu.be/M7lc1UVf-VE"), "M7lc1UVf-VE");
  assert.equal(parseYoutubeVideoId("https://www.youtube.com/shorts/M7lc1UVf-VE"), "M7lc1UVf-VE");
  assert.equal(parseYoutubeVideoId("https://open.spotify.com/track/abc"), null);
  assert.equal(parseYoutubeVideoId("not a url"), null);

  const { room, ids } = freshRoom(["阿凱", "小魚", "阿德"]);
  const [a, b, c] = ids;
  assert.equal(applyAction(room, b, { name: "start", game: "dj" }).ok, true);
  assert.equal(room.game.kind, "dj");
  assert.equal(room.game.djId, a);
  assert.equal(room.game.step, "pick");
  assert.match(applyAction(room, b, { name: "djMode", mode: "own" }).error, /另一位迪爵/);
  assert.match(applyAction(room, a, { name: "djSubmit", title: "七里香" }).error, /還沒有這個動作/);

  assert.equal(applyAction(room, c, { name: "djSkip" }).ok, true);
  assert.equal(room.game.djId, b);
  assert.ok(room.game.skipped.includes(a));

  assert.equal(applyAction(room, b, { name: "djMode", mode: "own" }).ok, true);
  assert.match(applyAction(room, b, { name: "djCue", url: "https://example.com/a" }).error, /連結/);
  assert.equal(applyAction(room, b, { name: "djCue", url: watch }).ok, true);
  assert.equal(room.game.step, "live");
  assert.equal(room.game.videoId, "M7lc1UVf-VE");
  assert.equal(room.game.endsAt, null);
  assert.equal(
    applyAction(room, b, {
      name: "djSync",
      videoId: "M7lc1UVf-VE",
      title: "晴天",
      artist: "周杰倫",
      playState: "playing",
      currentTime: 12.44,
    }).ok,
    true,
  );
  assert.equal(room.game.song.title, "晴天");
  assert.equal(room.game.currentTime, 12.4);
  assert.equal(serialize(room, a).game.currentTime, 12.4);
  assert.equal(
    applyAction(room, b, { name: "djSync", videoId: "jNQXAC9IVRw", playState: "playing", currentTime: 3 }).ok,
    true,
  );
  assert.equal(room.game.videoId, "M7lc1UVf-VE");
  assert.equal(room.game.currentTime, 3);
  assert.equal(
    applyAction(room, b, { name: "djSync", videoId: "M7lc1UVf-VE", playState: "paused", currentTime: -5 }).ok,
    true,
  );
  assert.equal(room.game.playState, "paused");
  assert.equal(room.game.currentTime, 3);
  assert.match(applyAction(room, a, { name: "djSync", videoId: "M7lc1UVf-VE", title: "假的", playState: "playing" }).error, /只有本輪迪爵/);
  assert.match(applyAction(room, b, { name: "djRate", value: 2 }).error, /不評自己/);
  assert.equal(applyAction(room, a, { name: "djRate", value: 0 }).ok, true);
  assert.equal(scoreOf(room, a), 1);
  assert.equal(scoreOf(room, b), 0);
  assert.equal(room.game.step, "live");
  assert.match(applyAction(room, a, { name: "djRate", value: 2 }).error, /評過了/);
  assert.equal(applyAction(room, c, { name: "djRate", value: "none" }).ok, true);
  assert.equal(scoreOf(room, c), 0);
  assert.equal(scoreOf(room, b), 0);
  assert.equal(applyAction(room, b, { name: "djEnded" }).ok, true);
  assert.equal(room.game.step, "pick");
  assert.equal(room.game.djId, c);

  assert.equal(applyAction(room, c, { name: "djMode", mode: "playlist" }).ok, true);
  const started = Date.now();
  assert.equal(applyAction(room, c, { name: "djCue", url: music }).ok, true);
  assert.equal(room.game.step, "live");
  assert.equal(room.game.videoId, "dQw4w9WgXcQ");
  assert.ok(room.game.endsAt >= started + 20 * 60 * 1000 - 2000);
  assert.equal(applyAction(room, b, { name: "djRate", value: -2 }).ok, true);
  assert.equal(scoreOf(room, b), 1);
  assert.equal(scoreOf(room, c), -2);
  assert.equal(room.game.step, "live");
  assert.equal(
    applyAction(room, c, {
      name: "djSync",
      videoId: "jNQXAC9IVRw",
      title: "下一首",
      artist: "別人",
      playState: "playing",
      currentTime: 8.5,
    }).ok,
    true,
  );
  assert.equal(room.game.videoId, "jNQXAC9IVRw");
  assert.equal(room.game.currentTime, 8.5);
  assert.equal(room.game.song.title, "下一首");
  assert.equal(room.game.ratings[b], undefined);
  assert.equal(applyAction(room, b, { name: "djRate", value: 1 }).ok, true);
  assert.equal(scoreOf(room, b), 2);
  assert.equal(scoreOf(room, c), -1);
  assert.match(applyAction(room, a, { name: "djFinish" }).error, /迪爵結束/);
  assert.equal(applyAction(room, c, { name: "djFinish" }).ok, true);
  assert.equal(room.game.step, "pick");
  assert.notEqual(room.game.djId, c);

  const timed = freshRoom(["阿凱", "小魚"]);
  applyAction(timed.room, timed.ids[0], { name: "start", game: "dj" });
  applyAction(timed.room, timed.ids[0], { name: "djMode", mode: "playlist" });
  applyAction(timed.room, timed.ids[0], { name: "djCue", url: watch });
  assert.match(applyAction(timed.room, timed.ids[0], { name: "djTimeUp" }).error, /時間還沒到/);
  timed.room.game.endsAt = Date.now() - 1000;
  assert.equal(expireDj(timed.room), true);
  assert.equal(timed.room.game.step, "pick");
  assert.equal(timed.room.game.djId, timed.ids[1]);

  const low = freshRoom(["阿凱", "小魚"]);
  applyAction(low.room, low.ids[0], { name: "start", game: "dj" });
  applyAction(low.room, low.ids[0], { name: "djMode", mode: "own" });
  applyAction(low.room, low.ids[0], { name: "djCue", url: watch });
  applyAction(low.room, low.ids[1], { name: "djRate", value: -2 });
  assert.equal(scoreOf(low.room, low.ids[0]), -2);
  assert.equal(scoreOf(low.room, low.ids[1]), 1);
  applyAction(low.room, low.ids[0], { name: "lobby" });
  applyAction(low.room, low.ids[0], { name: "start", game: "prompt" });
  if (low.room.game.turnOrder[0] === low.ids[0]) {
    applyAction(low.room, low.ids[0], { name: "speak", note: "還醒著" });
  } else {
    applyAction(low.room, low.ids[1], { name: "speak", note: "還醒著" });
    applyAction(low.room, low.ids[0], { name: "speak", note: "我也是" });
  }
  assert.equal(scoreOf(low.room, low.ids[0]), -1);
}

function openClient(port) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
    const states = [];
    ws.on("message", (buf) => {
      states.push(JSON.parse(buf.toString()));
    });
    ws.on("open", () => {
      resolve({
        ws,
        states,
        send(obj) {
          ws.send(JSON.stringify(obj));
        },
      });
    });
    ws.on("error", reject);
  });
}

async function waitUntil(states, pred) {
  const start = Date.now();
  while (Date.now() - start < 3000) {
    const latest = [...states].reverse().find((msg) => msg.type === "state");
    if (latest && pred(latest)) return latest;
    const error = [...states].reverse().find((msg) => msg.type === "error");
    if (error && pred(error)) return error;
    await new Promise((resolve) => setTimeout(resolve, 20));
  }
  throw new Error(`timeout ${JSON.stringify(states.slice(-4), null, 2)}`);
}

async function testServer() {
  const port = 17651;
  const child = spawn(process.execPath, ["server/index.mjs"], {
    cwd: root,
    env: { ...process.env, PORT: String(port) },
    stdio: ["ignore", "pipe", "pipe"],
  });
  let logs = "";
  child.stdout.on("data", (chunk) => {
    logs += chunk;
  });
  child.stderr.on("data", (chunk) => {
    logs += chunk;
  });
  const clients = [];
  try {
    let healthy = false;
    for (let i = 0; i < 40; i += 1) {
      try {
        const response = await fetch(`http://127.0.0.1:${port}/api/health`);
        if (response.ok) {
          assert.deepEqual(await response.json(), { ok: true });
          healthy = true;
          break;
        }
      } catch {
        // server still booting
      }
      await new Promise((resolve) => setTimeout(resolve, 50));
    }
    if (!healthy) throw new Error(`server did not start\n${logs}`);

    const host = await openClient(port);
    const guest = await openClient(port);
    clients.push(host, guest);
    host.send({ type: "create", nickname: "阿凱" });
    const created = await waitUntil(host.states, (msg) => msg.type === "state");
    const code = created.room.code;
    assert.equal(code.length, 4);
    guest.send({ type: "join", code, nickname: "小魚" });
    const joined = await waitUntil(guest.states, (msg) => msg.room?.players?.length === 2);
    assert.equal(joined.room.players.map((player) => player.nickname).join(","), "阿凱,小魚");
    const hostSawGuest = await waitUntil(host.states, (msg) => msg.room?.players?.length === 2);
    assert.equal(hostSawGuest.room.code, code);

    host.send({ type: "action", name: "start", game: "prompt" });
    const hostCard = await waitUntil(host.states, (msg) => msg.room?.game?.kind === "prompt");
    const guestCard = await waitUntil(guest.states, (msg) => msg.room?.game?.card?.id === hostCard.room.game.card.id);
    assert.equal(guestCard.room.game.card.text, hostCard.room.game.card.text);

    const speakerIsHost = hostCard.room.game.turnOrder[0] === hostCard.youId;
    const speaker = speakerIsHost ? host : guest;
    const watcher = speakerIsHost ? guest : host;
    speaker.send({ type: "action", name: "speak", note: "想在老街多留一天" });
    const watched = await waitUntil(
      watcher.states,
      (msg) => msg.room?.players?.some((player) => player.score === 1) && msg.room?.game?.notes,
    );
    const scorer = watched.room.players.find((player) => player.score === 1);
    assert.equal(watched.room.game.notes[scorer.id], "想在老街多留一天");
    const speakerView = await waitUntil(speaker.states, (msg) =>
      msg.room?.players?.some((player) => player.score === 1),
    );
    assert.equal(speakerView.room.game.card.id, watched.room.game.card.id);

    const other = speaker === host ? guest : host;
    if (!speakerView.room.game.done) {
      other.send({ type: "action", name: "speak", note: "我也想" });
      await waitUntil(host.states, (msg) => msg.room?.players?.every((player) => player.score >= 1));
      await waitUntil(guest.states, (msg) => msg.room?.players?.every((player) => player.score >= 1));
    }

    guest.send({ type: "action", name: "lobby" });
    await waitUntil(host.states, (msg) => msg.room?.phase === "lobby" && msg.room.players.some((player) => player.score >= 1));
    guest.send({ type: "action", name: "start", game: "vibe" });
    const vibe = await waitUntil(host.states, (msg) => msg.room?.game?.kind === "vibe");
    const guestVibe = await waitUntil(guest.states, (msg) => msg.room?.game?.card?.id === vibe.room.game.card.id);
    assert.equal(guestVibe.room.game.card.text, vibe.room.game.card.text);
    const assigneeId = vibe.room.game.assigneeId;
    const assignee = host.states.at(-1).youId === assigneeId ? host : guest;
    const before = vibe.room.players.find((player) => player.id === assigneeId).score;
    assignee.send({ type: "action", name: "complete" });
    const after = await waitUntil(
      (assignee === host ? guest : host).states,
      (msg) => msg.room?.players?.find((player) => player.id === assigneeId)?.score === before + 2,
    );
    assert.equal(after.room.game.done, true);

    const hostId = created.youId;
    const guestId = joined.youId;
    const clientOf = (id) => (id === hostId ? host : guest);
    host.send({ type: "action", name: "lobby" });
    await waitUntil(host.states, (msg) => msg.room?.phase === "lobby");
    host.send({ type: "action", name: "start", game: "dj" });
    const djStart = await waitUntil(host.states, (msg) => msg.room?.game?.kind === "dj" && msg.room.game.step === "pick");
    await waitUntil(guest.states, (msg) => msg.room?.game?.djId === djStart.room.game.djId);
    const firstDj = clientOf(djStart.room.game.djId);
    const firstListener = firstDj === host ? guest : host;
    const beforeOwn = djStart.room.players.find((player) => player.id === djStart.room.game.djId).score;
    const listenerId = firstListener === host ? hostId : guestId;
    const listenerBeforeOwn = djStart.room.players.find((player) => player.id === listenerId).score;
    firstDj.send({ type: "action", name: "djMode", mode: "own" });
    await waitUntil(firstDj.states, (msg) => msg.room?.game?.step === "enter");
    firstDj.send({ type: "action", name: "djCue", url: "https://www.youtube.com/watch?v=M7lc1UVf-VE" });
    await waitUntil(firstListener.states, (msg) => msg.room?.game?.videoId === "M7lc1UVf-VE" && msg.room.game.step === "live");
    firstDj.send({
      type: "action",
      name: "djSync",
      videoId: "M7lc1UVf-VE",
      title: "晴天",
      artist: "周杰倫",
      playState: "playing",
      currentTime: 12.4,
    });
    await waitUntil(
      firstListener.states,
      (msg) => msg.room?.game?.song?.title === "晴天" && msg.room?.game?.currentTime === 12.4,
    );
    firstListener.send({ type: "action", name: "djRate", value: -1 });
    await waitUntil(firstDj.states, (msg) => {
      const dj = msg.room?.players?.find((item) => item.id === djStart.room.game.djId);
      const rater = msg.room?.players?.find((item) => item.id === listenerId);
      return dj && rater && dj.score === beforeOwn - 1 && rater.score === listenerBeforeOwn + 1 && msg.room.game.step === "live";
    });
    await waitUntil(firstListener.states, (msg) => {
      const dj = msg.room?.players?.find((item) => item.id === djStart.room.game.djId);
      return dj && dj.score === beforeOwn - 1;
    });
    firstDj.send({ type: "action", name: "djFinish" });
    const picked = await waitUntil(firstListener.states, (msg) => msg.room?.game?.step === "pick");

    const playlistDjId = picked.room.game.djId;
    const playlistDj = clientOf(playlistDjId);
    const playlistListener = playlistDj === host ? guest : host;
    const playlistListenerId = playlistListener === host ? hostId : guestId;
    const listenerBefore = picked.room.players.find((player) => player.id === playlistListenerId).score;
    const djBefore = picked.room.players.find((player) => player.id === playlistDjId).score;
    playlistDj.send({ type: "action", name: "djMode", mode: "playlist" });
    await waitUntil(playlistDj.states, (msg) => msg.room?.game?.step === "enter" && msg.room.game.djId === playlistDjId);
    playlistDj.send({ type: "action", name: "djCue", url: "https://music.youtube.com/watch?v=dQw4w9WgXcQ" });
    await waitUntil(
      playlistListener.states,
      (msg) => msg.room?.game?.step === "live" && msg.room.game.videoId === "dQw4w9WgXcQ" && msg.room.game.endsAt,
    );
    playlistDj.send({
      type: "action",
      name: "djSync",
      videoId: "dQw4w9WgXcQ",
      title: "夜曲",
      artist: "周杰倫",
      playState: "playing",
    });
    await waitUntil(playlistListener.states, (msg) => msg.room?.game?.song?.title === "夜曲");
    playlistListener.send({ type: "action", name: "djRate", value: 2 });
    await waitUntil(playlistDj.states, (msg) => {
      const dj = msg.room?.players?.find((item) => item.id === playlistDjId);
      const rater = msg.room?.players?.find((item) => item.id === playlistListenerId);
      return dj && rater && dj.score === djBefore + 2 && rater.score === listenerBefore + 1;
    });
    await waitUntil(playlistListener.states, (msg) => {
      const rater = msg.room?.players?.find((item) => item.id === playlistListenerId);
      return rater && rater.score === listenerBefore + 1;
    });

    const bad = await openClient(port);
    clients.push(bad);
    bad.send({ type: "join", code: "ZZZZ", nickname: "路人" });
    const rejected = await waitUntil(bad.states, (msg) => msg.type === "error");
    assert.match(rejected.message, /找不到這個房間/);

    const page = await fetch(`http://127.0.0.1:${port}/`);
    const body = await page.text();
    const roomsPage = await fetch(`http://127.0.0.1:${port}/api/rooms`);
    const roomsBody = await roomsPage.text();
    assert.equal(body.includes(code), false);
    assert.equal(roomsBody.includes(code), false);
  } finally {
    for (const client of clients) client.ws.close();
    child.kill("SIGTERM");
  }
}

function testWatchSync() {
  const now = 10_000;
  const clock = {
    videoId: "M7lc1UVf-VE",
    currentTime: 20,
    playState: "playing",
    receivedAt: now - 1000,
  };
  assert.equal(expectedPlayhead(clock, now), 21);
  assert.equal(expectedPlayhead({ ...clock, playState: "paused" }, now), 20);
  assert.equal(expectedPlayhead({ ...clock, playState: "buffering" }, now), 20);

  const close = planFollow({
    localVideoId: clock.videoId,
    localTime: 21.2,
    localState: 1,
    clock,
    now,
    unlocked: true,
    lastSeekAt: 0,
    autoplayTriedAt: now - 5000,
  });
  assert.equal(close.type, "play");
  assert.equal(close.seek, false);
  assert.equal(close.needsGesture, false);

  const drifted = planFollow({
    localVideoId: clock.videoId,
    localTime: 10,
    localState: 1,
    clock,
    now,
    unlocked: true,
    lastSeekAt: 0,
    autoplayTriedAt: now - 5000,
  });
  assert.equal(drifted.seek, true);
  assert.equal(drifted.seconds, 21);

  const cooling = planFollow({
    localVideoId: clock.videoId,
    localTime: 10,
    localState: 1,
    clock,
    now,
    unlocked: true,
    lastSeekAt: now - 500,
    autoplayTriedAt: now - 5000,
  });
  assert.equal(cooling.seek, false);

  const paused = planFollow({
    localVideoId: clock.videoId,
    localTime: 10,
    localState: 1,
    clock: { ...clock, playState: "paused", currentTime: 30 },
    now,
    unlocked: true,
    lastSeekAt: 0,
  });
  assert.equal(paused.type, "pause");
  assert.equal(paused.seek, true);
  assert.equal(paused.seconds, 30);

  const switched = planFollow({
    localVideoId: "dQw4w9WgXcQ",
    localTime: 1,
    localState: 1,
    clock,
    now,
    unlocked: true,
    lastSeekAt: 0,
  });
  assert.equal(switched.type, "load");
  assert.equal(switched.videoId, clock.videoId);
  assert.equal(switched.needsGesture, false);

  const blocked = planFollow({
    localVideoId: "dQw4w9WgXcQ",
    localTime: 1,
    localState: -1,
    clock,
    now,
    unlocked: false,
    lastSeekAt: 0,
    autoplayTriedAt: now - 5000,
  });
  assert.equal(blocked.type, "cue");
  assert.equal(blocked.needsGesture, true);

  const gesture = planFollow({
    localVideoId: clock.videoId,
    localTime: 21,
    localState: -1,
    clock,
    now,
    unlocked: false,
    lastSeekAt: 0,
    autoplayTriedAt: now - 5000,
  });
  assert.equal(gesture.type, "gesture");
  assert.equal(gesture.needsGesture, true);

  const grace = planFollow({
    localVideoId: clock.videoId,
    localTime: 21,
    localState: -1,
    clock,
    now,
    unlocked: false,
    lastSeekAt: 0,
    autoplayTriedAt: now - 200,
  });
  assert.equal(grace.type, "wait");
  assert.equal(grace.needsGesture, false);

  const dj = fs.readFileSync(path.join(root, "client/src/dj.jsx"), "utf8");
  const play = fs.readFileSync(path.join(root, "client/src/play.jsx"), "utf8");
  assert.equal(dj.includes("聲音從這支手機出來"), false);
  assert.equal(dj.includes("只播這一支。大家一起看這支影片。"), true);
  assert.equal(dj.includes("開始一起看"), true);
  assert.equal(dj.includes("退出同步收聽"), true);
  assert.equal(dj.includes("其他人繼續播"), true);
  assert.equal(dj.includes('if (mix) params.set("list"'), true);
  assert.equal(play.includes("聲音只從本輪迪爵的手機出來"), false);
  assert.equal(play.includes("大家一起看同一支影片"), true);
}

try {
  testDecksAndCopy();
  testWatchSync();
  testLogic();
  await testServer();
  console.log("smoke ok");
} catch (error) {
  console.error(error);
  process.exitCode = 1;
}
