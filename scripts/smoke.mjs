import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { WebSocket } from "ws";
import { promptDeck, vibeDeck, scoreDeck } from "../server/decks.mjs";
import { expireDj, parseYoutubeVideoId } from "../server/dj.mjs";
import { tickCardDraws } from "../server/cards.mjs";
import { expectedPlayhead, planFollow } from "../client/src/watch-sync.js";
import {
  applyAction,
  createPlayerRoom,
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
    "client/src/cards.jsx",
    "client/src/watch-sync.js",
    "client/src/useRoom.js",
    "server/dj.mjs",
    "server/cards.mjs",
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

  assert.match(applyAction(room, b, { name: "start", game: "prompt" }).error, /沒有這個遊戲/);
  assert.match(applyAction(room, a, { name: "start", game: "vibe" }).error, /沒有這個遊戲/);
  assert.match(applyAction(room, c, { name: "start", game: "score" }).error, /沒有這個遊戲/);
  assert.equal(room.phase, "lobby");
  assert.equal(room.game, null);

  const host = freshRoom(["阿凱", "小魚"]);
  removePlayer(host.room, host.ids[0]);
  assert.equal(host.room.hostId, host.ids[1]);

  applyAction(room, a, { name: "lobby" });
  assert.equal(room.phase, "lobby");
  assert.equal(room.game, null);
  assert.equal(scoreOf(room, a), 0);
  testDj();
  testCards();
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
  assert.match(applyAction(room, b, { name: "djMode", songs: 1 }).error, /另一位迪爵/);
  assert.match(applyAction(room, b, { name: "djSpin", songs: 3 }).error, /另一位迪爵/);
  assert.equal(applyAction(room, a, { name: "djSpin", songs: 1 }).ok, true);
  assert.equal(room.game.step, "pick");
  assert.equal(room.game.wheelSongs, 1);
  assert.equal(room.game.wheelSpin, 1);
  assert.match(applyAction(room, a, { name: "djSubmit", title: "七里香" }).error, /還沒有這個動作/);

  assert.equal(applyAction(room, c, { name: "djSkip" }).ok, true);
  assert.equal(room.game.djId, b);
  assert.ok(room.game.skipped.includes(a));

  assert.equal(applyAction(room, b, { name: "djMode", songs: 1 }).ok, true);
  assert.match(applyAction(room, b, { name: "djBack" }).error, /不能重選/);
  assert.equal(room.game.step, "enter");
  assert.equal(room.game.mode, "own");
  assert.equal(room.game.songs, 1);
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
  assert.match(applyAction(room, a, { name: "djRate", value: 0 }).error, /沒有這個分數/);
  assert.equal(scoreOf(room, a), 0);
  assert.equal(scoreOf(room, b), 0);
  assert.equal(room.game.ratings[a], undefined);
  assert.equal(applyAction(room, a, { name: "djRate", value: 1 }).ok, true);
  assert.equal(scoreOf(room, a), 1);
  assert.equal(scoreOf(room, b), 1);
  assert.equal(room.game.step, "live");
  assert.match(applyAction(room, a, { name: "djRate", value: 2 }).error, /評過了/);
  assert.match(applyAction(room, c, { name: "djRate", value: "none" }).error, /沒有這個分數/);
  assert.equal(room.game.ratings[c], undefined);
  assert.equal(scoreOf(room, c), 0);
  assert.equal(scoreOf(room, b), 1);
  assert.equal(applyAction(room, b, { name: "djEnded" }).ok, true);
  assert.equal(room.game.step, "pick");
  assert.equal(room.game.djId, c);

  assert.equal(applyAction(room, c, { name: "djMode", songs: 3 }).ok, true);
  assert.equal(applyAction(room, c, { name: "djCue", url: music }).ok, true);
  assert.equal(room.game.step, "live");
  assert.equal(room.game.videoId, "dQw4w9WgXcQ");
  assert.equal(room.game.endsAt, null);
  assert.equal(room.game.songCount, 1);
  assert.equal(applyAction(room, b, { name: "djRate", value: -2 }).ok, true);
  assert.equal(scoreOf(room, b), 2);
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
  assert.equal(room.game.songCount, 2);
  assert.equal(room.game.ratings[b], undefined);
  assert.equal(applyAction(room, b, { name: "djRate", value: 1 }).ok, true);
  assert.equal(scoreOf(room, b), 3);
  assert.equal(scoreOf(room, c), -1);
  assert.match(applyAction(room, a, { name: "djFinish" }).error, /迪爵結束/);
  assert.equal(applyAction(room, c, { name: "djFinish" }).ok, true);
  assert.equal(room.game.step, "pick");
  assert.notEqual(room.game.djId, c);

  const timed = freshRoom(["阿凱", "小魚"]);
  applyAction(timed.room, timed.ids[0], { name: "start", game: "dj" });
  applyAction(timed.room, timed.ids[0], { name: "djMode", songs: 3 });
  applyAction(timed.room, timed.ids[0], { name: "djCue", url: watch });
  assert.equal(timed.room.game.endsAt, null);
  assert.equal(timed.room.game.songCount, 1);
  assert.equal(applyAction(timed.room, timed.ids[0], { name: "djTimeUp" }).ok, true);
  assert.equal(timed.room.game.step, "live");
  assert.equal(timed.room.game.djId, timed.ids[0]);
  assert.equal(expireDj(timed.room), false);

  const mix = freshRoom(["阿凱", "小魚"]);
  applyAction(mix.room, mix.ids[0], { name: "start", game: "dj" });
  applyAction(mix.room, mix.ids[0], { name: "djMode", songs: 3 });
  applyAction(mix.room, mix.ids[0], { name: "djCue", url: music });
  assert.equal(
    applyAction(mix.room, mix.ids[0], { name: "djSync", videoId: "jNQXAC9IVRw", title: "第二首", playState: "playing" }).ok,
    true,
  );
  assert.equal(mix.room.game.songCount, 2);
  assert.equal(mix.room.game.step, "live");
  assert.equal(
    applyAction(mix.room, mix.ids[0], { name: "djSync", videoId: "M7lc1UVf-VE", title: "第三首", playState: "playing" }).ok,
    true,
  );
  assert.equal(mix.room.game.songCount, 3);
  assert.equal(mix.room.game.videoId, "M7lc1UVf-VE");
  assert.equal(mix.room.game.step, "live");
  assert.equal(
    applyAction(mix.room, mix.ids[0], { name: "djSync", videoId: "abcdefghijk", title: "第四首", playState: "playing" }).ok,
    true,
  );
  assert.equal(mix.room.game.step, "pick");
  assert.equal(mix.room.game.djId, mix.ids[1]);
  assert.equal(mix.room.game.videoId, null);

  const two = freshRoom(["阿凱", "小魚"]);
  applyAction(two.room, two.ids[0], { name: "start", game: "dj" });
  two.room.cards.hands[two.ids[0]] = ["mode"];
  assert.equal(applyAction(two.room, two.ids[0], { name: "cardPlay", card: "mode", songs: 2 }).ok, true);
  assert.equal(applyAction(two.room, two.ids[0], { name: "djSpin", songs: 1 }).ok, true);
  assert.equal(two.room.game.wheelSongs, 2);
  assert.equal(applyAction(two.room, two.ids[0], { name: "djMode", songs: 2 }).ok, true);
  assert.equal(two.room.game.mode, "playlist");
  assert.equal(two.room.game.songs, 2);
  applyAction(two.room, two.ids[0], { name: "djCue", url: music });
  assert.equal(serialize(two.room, two.ids[1]).game.songLimit, 2);
  assert.equal(
    applyAction(two.room, two.ids[0], { name: "djSync", videoId: "jNQXAC9IVRw", title: "第二首", playState: "playing" }).ok,
    true,
  );
  assert.equal(two.room.game.songCount, 2);
  assert.equal(two.room.game.step, "live");
  assert.equal(
    applyAction(two.room, two.ids[0], { name: "djSync", videoId: "abcdefghijk", title: "第三首", playState: "playing" }).ok,
    true,
  );
  assert.equal(two.room.game.step, "pick");

  const again = freshRoom(["阿凱"]);
  applyAction(again.room, again.ids[0], { name: "start", game: "dj" });
  again.room.cards.hands[again.ids[0]] = ["respin"];
  assert.equal(applyAction(again.room, again.ids[0], { name: "cardPlay", card: "respin" }).ok, true);
  assert.equal(again.room.game.extraSpins, 1);
  assert.equal(applyAction(again.room, again.ids[0], { name: "djSpin", songs: 1 }).ok, true);
  assert.equal(again.room.game.extraSpins, 1);
  assert.equal(applyAction(again.room, again.ids[0], { name: "djSpin", songs: 3 }).ok, true);
  assert.equal(again.room.game.wheelSongs, 3);
  assert.equal(again.room.game.extraSpins, 0);
  assert.match(applyAction(again.room, again.ids[0], { name: "djSpin", songs: 4 }).error, /首數|轉過了/);

  const low = freshRoom(["阿凱", "小魚"]);
  applyAction(low.room, low.ids[0], { name: "start", game: "dj" });
  applyAction(low.room, low.ids[0], { name: "djMode", songs: 1 });
  applyAction(low.room, low.ids[0], { name: "djCue", url: watch });
  applyAction(low.room, low.ids[1], { name: "djRate", value: -2 });
  assert.equal(scoreOf(low.room, low.ids[0]), -2);
  assert.equal(scoreOf(low.room, low.ids[1]), 1);
}

function testCards() {
  const watch = "https://www.youtube.com/watch?v=M7lc1UVf-VE";
  const poor = freshRoom(["阿凱", "小魚"]);
  assert.match(applyAction(poor.room, poor.ids[0], { name: "cardBuy", card: "peek" }).error, /不能買/);
  poor.room.players[0].score = -2;
  assert.match(applyAction(poor.room, poor.ids[0], { name: "cardBuy", card: "peek" }).error, /不能買/);
  assert.equal(poor.room.cards.hands[poor.ids[0]]?.length || 0, 0);

  const swap = freshRoom(["阿凱", "小魚"]);
  const [swapA, swapB] = swap.ids;
  swap.room.players.find((player) => player.id === swapA).score = 4;
  swap.room.players.find((player) => player.id === swapB).score = -5;
  swap.room.cards.hands[swapA] = ["swap"];
  assert.deepEqual(serialize(swap.room, swapB).cards.hand, []);
  assert.deepEqual(serialize(swap.room, swapA).cards.hand, ["swap"]);
  assert.equal(applyAction(swap.room, swapA, { name: "cardPlay", card: "swap", targetId: swapB }).ok, true);
  assert.equal(scoreOf(swap.room, swapA), -5);
  assert.equal(scoreOf(swap.room, swapB), 4);
  assert.deepEqual(swap.room.cards.hands[swapA], []);

  const hide = freshRoom(["阿凱", "小魚"]);
  hide.room.players[0].score = 8;
  hide.room.cards.hands[hide.ids[0]] = ["hide"];
  assert.equal(applyAction(hide.room, hide.ids[0], { name: "cardPlay", card: "hide" }).ok, true);
  const masked = serialize(hide.room, hide.ids[1]);
  const hiddenRow = masked.players.find((player) => player.id === hide.ids[0]);
  assert.equal(hiddenRow.scoreHidden, true);
  assert.equal(hiddenRow.score, null);
  const selfRow = serialize(hide.room, hide.ids[0]).players.find((player) => player.id === hide.ids[0]);
  assert.equal(selfRow.score, 8);
  assert.equal(selfRow.scoreHidden, false);
  assert.equal(masked.ranking.find((row) => row.id === hide.ids[0]).score, null);
  assert.equal(masked.ranking.find((row) => row.id === hide.ids[0]).scoreHidden, true);

  const shield = freshRoom(["阿凱", "小魚"]);
  applyAction(shield.room, shield.ids[0], { name: "start", game: "dj" });
  shield.room.cards.hands[shield.ids[0]] = ["shield"];
  assert.equal(applyAction(shield.room, shield.ids[0], { name: "cardPlay", card: "shield" }).ok, true);
  applyAction(shield.room, shield.ids[0], { name: "djMode", songs: 1 });
  applyAction(shield.room, shield.ids[0], { name: "djCue", url: watch });
  assert.equal(applyAction(shield.room, shield.ids[1], { name: "djRate", value: -2 }).ok, true);
  assert.equal(scoreOf(shield.room, shield.ids[0]), 0);
  assert.equal(scoreOf(shield.room, shield.ids[1]), 1);
  assert.equal(shield.room.game.ratings[shield.ids[1]], -2);

  const block = freshRoom(["阿凱", "小魚"]);
  applyAction(block.room, block.ids[0], { name: "start", game: "dj" });
  applyAction(block.room, block.ids[0], { name: "djMode", songs: 1 });
  applyAction(block.room, block.ids[0], { name: "djCue", url: watch });
  block.room.cards.hands[block.ids[1]] = ["block"];
  assert.equal(applyAction(block.room, block.ids[1], { name: "cardPlay", card: "block" }).ok, true);
  assert.match(applyAction(block.room, block.ids[1], { name: "djRate", value: -1 }).error, /負分/);
  assert.equal(scoreOf(block.room, block.ids[0]), 0);
  assert.equal(scoreOf(block.room, block.ids[1]), 0);
  assert.equal(block.room.game.ratings[block.ids[1]], undefined);
  assert.deepEqual(block.room.cards.hands[block.ids[1]], []);

  const steal = freshRoom(["阿凱", "小魚"]);
  steal.room.cards.hands[steal.ids[0]] = ["steal"];
  assert.match(
    applyAction(steal.room, steal.ids[0], { name: "cardPlay", card: "steal", targetId: steal.ids[1] }).error,
    /沒有分可以拿/,
  );
  assert.deepEqual(steal.room.cards.hands[steal.ids[0]], ["steal"]);
  assert.equal(scoreOf(steal.room, steal.ids[0]), 0);
  assert.equal(scoreOf(steal.room, steal.ids[1]), 0);

  const extra = freshRoom(["阿凱", "小魚"]);
  applyAction(extra.room, extra.ids[0], { name: "start", game: "dj" });
  extra.room.cards.hands[extra.ids[0]] = ["extra"];
  assert.equal(applyAction(extra.room, extra.ids[0], { name: "cardPlay", card: "extra" }).ok, true);
  applyAction(extra.room, extra.ids[0], { name: "djMode", songs: 3 });
  applyAction(extra.room, extra.ids[0], { name: "djCue", url: "https://music.youtube.com/watch?v=dQw4w9WgXcQ" });
  assert.equal(serialize(extra.room, extra.ids[1]).game.songLimit, 4);
  const videos = ["jNQXAC9IVRw", "M7lc1UVf-VE", "abcdefghijk", "bbbbbbbbbbb"];
  for (let i = 0; i < 3; i += 1) {
    assert.equal(
      applyAction(extra.room, extra.ids[0], {
        name: "djSync",
        videoId: videos[i],
        title: `歌${i + 2}`,
        playState: "playing",
      }).ok,
      true,
    );
    assert.equal(extra.room.game.step, "live");
  }
  assert.equal(extra.room.game.songCount, 4);
  assert.equal(
    applyAction(extra.room, extra.ids[0], { name: "djSync", videoId: videos[3], title: "歌5", playState: "playing" }).ok,
    true,
  );
  assert.equal(extra.room.game.step, "pick");
  assert.equal(extra.room.game.djId, extra.ids[1]);

  const draw = freshRoom(["阿凱"]);
  draw.room.cards.hands[draw.ids[0]] = ["hide", "peek", "steal"];
  draw.room.cards.bornAt = Date.now() - 3 * 60 * 1000;
  assert.equal(tickCardDraws(draw.room), false);
  assert.equal(draw.room.cards.hands[draw.ids[0]].length, 3);
  assert.equal(draw.room.cards.draws, 1);
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
    const rejectedGame = await waitUntil(host.states, (msg) => msg.type === "error");
    assert.match(rejectedGame.message, /沒有這個遊戲/);

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
    firstDj.send({ type: "action", name: "djMode", songs: 1 });
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
    playlistDj.send({ type: "action", name: "djMode", songs: 3 });
    await waitUntil(playlistDj.states, (msg) => msg.room?.game?.step === "enter" && msg.room.game.djId === playlistDjId);
    playlistDj.send({ type: "action", name: "djCue", url: "https://music.youtube.com/watch?v=dQw4w9WgXcQ" });
    await waitUntil(
      playlistListener.states,
      (msg) =>
        msg.room?.game?.step === "live" &&
        msg.room.game.videoId === "dQw4w9WgXcQ" &&
        msg.room.game.endsAt == null &&
        msg.room.game.songCount === 1,
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
  assert.equal(play.includes("旅途積分"), false);
  assert.equal(play.includes("積分排名"), true);
  assert.equal(dj.includes("積分榜"), false);
  const app = fs.readFileSync(path.join(root, "client/src/App.jsx"), "utf8");
  assert.equal(app.includes("PageSwitch"), true);
  assert.equal(app.includes("is-parked"), true);
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
