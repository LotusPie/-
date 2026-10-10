import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { WebSocket } from "ws";
import { promptDeck, vibeDeck, scoreDeck } from "../server/decks.mjs";
import { expireDj, parseYoutubeVideoId, REEL_ITEMS } from "../server/dj.mjs";
import { HIDE_MS, tickCardDraws, tickScoreHides } from "../server/cards.mjs";
import { handleDev } from "../server/dev.mjs";
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

function lockReel(room, id, index = 0) {
  assert.equal(room.game.step, "reel");
  assert.equal(applyAction(room, id, { name: "djReel", index }).ok, true);
  assert.equal(room.game.reel, index);
  assert.equal(applyAction(room, id, { name: "djReelLock" }).ok, true);
  assert.equal(room.game.step, "enter");
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
    "client/src/profile.jsx",
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
  testProfile();
  testDev();
}

function testDev() {
  const { room, ids } = freshRoom(["阿凱", "小魚"]);
  const [a, b] = ids;
  assert.equal(handleDev(room, a, { name: "addFake" }).ok, true);
  const fake = room.players.find((player) => player.fake);
  assert.equal(fake.nickname, "假旅伴");
  assert.equal(serialize(room, b).players.find((player) => player.id === fake.id).fake, true);
  assert.match(handleDev(room, a, { name: "removeFake", targetId: a }).error, /假旅伴/);
  assert.equal(handleDev(room, a, { name: "setScore", targetId: b, score: 12 }).ok, true);
  assert.equal(scoreOf(room, b), 12);
  assert.equal(handleDev(room, a, { name: "nextDj", targetId: fake.id }).ok, true);
  assert.equal(room.cards.pendingNext, fake.id);
  assert.equal(handleDev(room, a, { name: "hide", targetId: b }).ok, true);
  const base = room.cards.hideUntil[b];
  assert.equal(handleDev(room, a, { name: "hideStack", targetId: b }).ok, true);
  assert.equal(room.cards.hideUntil[b], base + HIDE_MS);
  assert.equal(serialize(room, a).ranking.find((row) => row.id === b), undefined);
  assert.equal(handleDev(room, a, { name: "hideSoon", targetId: b }).ok, true);
  assert.ok(room.cards.hideUntil[b] <= Date.now() + 10000);
  assert.ok(room.cards.hideUntil[b] > Date.now() + 8000);
  room.cards.hideUntil[b] = Date.now() - 1;
  assert.equal(tickScoreHides(room), true);
  assert.ok(serialize(room, a).ranking.some((row) => row.id === b));

  applyAction(room, a, { name: "start", game: "dj" });
  assert.equal(room.game.nextDjId, fake.id);
  assert.equal(handleDev(room, a, { name: "wheel", songs: 0 }).ok, true);
  assert.equal(room.game.wheelSongs, 0);
  assert.equal(room.game.step, "pick");
  assert.equal(applyAction(room, a, { name: "djMode", songs: 0 }).ok, true);
  assert.equal(room.game.djId, fake.id);
  assert.equal(room.game.step, "pick");
  assert.equal(room.game.reel, null);

  assert.equal(handleDev(room, fake.id, { name: "wheel", songs: 2 }).ok, true);
  assert.equal(applyAction(room, fake.id, { name: "djMode", songs: 2 }).ok, true);
  assert.equal(room.game.step, "reel");
  assert.equal(handleDev(room, fake.id, { name: "reel", index: 6 }).ok, true);
  assert.equal(room.game.reel, 6);
  assert.equal(applyAction(room, fake.id, { name: "djReelLock" }).ok, true);
  assert.equal(room.game.step, "enter");
  assert.equal(serialize(room, a).game.reel, 6);

  assert.equal(handleDev(room, a, { name: "giveCard", targetId: a, card: "peek" }).ok, true);
  assert.deepEqual(room.cards.hands[a], ["peek"]);
  assert.equal(handleDev(room, a, { name: "clearHand", targetId: a }).ok, true);
  assert.deepEqual(room.cards.hands[a], []);
  room.cards.spinsLeft[a] = 0;
  room.cards.spinReady[a] = Date.now() + 60_000;
  assert.equal(handleDev(room, a, { name: "freeDraws", targetId: a }).ok, true);
  assert.equal(room.cards.spinsLeft[a], 3);
  assert.equal(room.cards.spinReady[a], 0);
  assert.equal(handleDev(room, a, { name: "drawSoon", targetId: a }).ok, true);
  assert.equal(room.cards.spinsLeft[a], 0);
  assert.ok(room.cards.spinReady[a] > Date.now() + 8000);
  assert.equal(handleDev(room, a, { name: "drawReady", targetId: a }).ok, true);
  assert.ok(room.cards.spinReady[a] <= Date.now());
  assert.equal(handleDev(room, a, { name: "removeFake", targetId: fake.id }).ok, true);
  assert.equal(room.players.some((player) => player.fake), false);
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
  assert.equal(room.game.step, "reel");
  assert.equal(room.game.mode, "own");
  assert.equal(room.game.songs, 1);
  assert.match(applyAction(room, b, { name: "djCue", url: watch }).error, /還不能播/);
  assert.match(applyAction(room, a, { name: "djReel", index: 1 }).error, /另一位迪爵/);
  assert.match(applyAction(room, b, { name: "djReel", index: 1.5 }).error, /條件/);
  assert.match(applyAction(room, b, { name: "djReel", index: 12 }).error, /條件/);
  lockReel(room, b, 4);
  assert.equal(serialize(room, a).game.reel, 4);
  assert.equal(REEL_ITEMS[4], "AI 做的歌");
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
  lockReel(room, c, 0);
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
  lockReel(timed.room, timed.ids[0]);
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
  lockReel(mix.room, mix.ids[0]);
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
  lockReel(two.room, two.ids[0]);
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

  const skipped = freshRoom(["阿凱", "小魚"]);
  applyAction(skipped.room, skipped.ids[0], { name: "start", game: "dj" });
  assert.equal(applyAction(skipped.room, skipped.ids[0], { name: "djSpin", songs: 0 }).ok, true);
  assert.equal(skipped.room.game.wheelSongs, 0);
  assert.equal(serialize(skipped.room, skipped.ids[1]).game.wheelSongs, 0);
  assert.equal(skipped.room.game.step, "pick");
  assert.match(applyAction(skipped.room, skipped.ids[1], { name: "djMode", songs: 0 }).error, /另一位迪爵/);
  assert.equal(applyAction(skipped.room, skipped.ids[0], { name: "djMode", songs: 0 }).ok, true);
  assert.equal(skipped.room.game.step, "pick");
  assert.equal(skipped.room.game.djId, skipped.ids[1]);
  assert.ok(skipped.room.game.served.includes(skipped.ids[0]));
  assert.equal(skipped.room.game.wheelSongs, null);
  assert.equal(skipped.room.game.reel, null);

  const low = freshRoom(["阿凱", "小魚"]);
  applyAction(low.room, low.ids[0], { name: "start", game: "dj" });
  applyAction(low.room, low.ids[0], { name: "djMode", songs: 1 });
  lockReel(low.room, low.ids[0]);
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
  const until = hide.room.cards.hideUntil[hide.ids[0]];
  assert.ok(until > Date.now() + HIDE_MS - 2000);
  assert.ok(until <= Date.now() + HIDE_MS + 50);
  const masked = serialize(hide.room, hide.ids[1]);
  const hiddenRow = masked.players.find((player) => player.id === hide.ids[0]);
  assert.equal(hiddenRow.scoreHidden, true);
  assert.equal(hiddenRow.score, null);
  assert.equal(hiddenRow.scoreHideUntil, until);
  const selfRow = serialize(hide.room, hide.ids[0]).players.find((player) => player.id === hide.ids[0]);
  assert.equal(selfRow.score, 8);
  assert.equal(selfRow.scoreHidden, false);
  assert.equal(selfRow.scoreHideUntil, until);
  assert.equal(masked.ranking.find((row) => row.id === hide.ids[0]), undefined);
  assert.deepEqual(
    masked.ranking.map((row) => [row.id, row.rank]),
    [[hide.ids[1], 1]],
  );
  assert.deepEqual(
    serialize(hide.room, hide.ids[0]).ranking.map((row) => [row.id, row.rank]),
    [[hide.ids[1], 1]],
  );
  const board = freshRoom(["阿凱", "小魚", "阿德"]);
  board.room.players[0].score = 9;
  board.room.players[1].score = 5;
  board.room.players[2].score = 1;
  board.room.cards.hands[board.ids[1]] = ["hide"];
  assert.equal(applyAction(board.room, board.ids[1], { name: "cardPlay", card: "hide" }).ok, true);
  const visible = serialize(board.room, board.ids[2]).ranking.map((row) => [row.nickname, row.rank, row.score]);
  assert.deepEqual(visible, [
    ["阿凱", 1, 9],
    ["阿德", 2, 1],
  ]);
  assert.deepEqual(
    serialize(board.room, board.ids[1]).ranking.map((row) => row.nickname),
    ["阿凱", "阿德"],
  );
  applyAction(hide.room, hide.ids[0], { name: "start", game: "dj" });
  applyAction(hide.room, hide.ids[0], { name: "djSkip" });
  assert.equal(hide.room.cards.hideUntil[hide.ids[0]], until);
  assert.equal(serialize(hide.room, hide.ids[1]).players.find((player) => player.id === hide.ids[0]).score, null);
  const base = Date.now() + 90 * 60 * 1000;
  hide.room.cards.hideUntil[hide.ids[0]] = base;
  hide.room.cards.hands[hide.ids[0]] = ["hide"];
  assert.equal(applyAction(hide.room, hide.ids[0], { name: "cardPlay", card: "hide" }).ok, true);
  assert.equal(hide.room.cards.hideUntil[hide.ids[0]], base + HIDE_MS);
  hide.room.cards.hideUntil[hide.ids[0]] = Date.now() - 1;
  assert.equal(tickScoreHides(hide.room), true);
  const shown = serialize(hide.room, hide.ids[1]).players.find((player) => player.id === hide.ids[0]);
  assert.equal(shown.scoreHidden, false);
  assert.equal(shown.score, 8);
  assert.equal(shown.scoreHideUntil, null);
  assert.deepEqual(
    serialize(hide.room, hide.ids[0]).ranking.map((row) => [row.id, row.rank]),
    [
      [hide.ids[0], 1],
      [hide.ids[1], 2],
    ],
  );
  board.room.cards.hideUntil[board.ids[1]] = Date.now() - 1;
  assert.equal(tickScoreHides(board.room), true);
  assert.deepEqual(
    serialize(board.room, board.ids[1]).ranking.map((row) => [row.nickname, row.rank]),
    [
      ["阿凱", 1],
      ["小魚", 2],
      ["阿德", 3],
    ],
  );

  const shield = freshRoom(["阿凱", "小魚"]);
  applyAction(shield.room, shield.ids[0], { name: "start", game: "dj" });
  shield.room.cards.hands[shield.ids[0]] = ["shield"];
  assert.equal(applyAction(shield.room, shield.ids[0], { name: "cardPlay", card: "shield" }).ok, true);
  applyAction(shield.room, shield.ids[0], { name: "djMode", songs: 1 });
  lockReel(shield.room, shield.ids[0]);
  applyAction(shield.room, shield.ids[0], { name: "djCue", url: watch });
  assert.equal(applyAction(shield.room, shield.ids[1], { name: "djRate", value: -2 }).ok, true);
  assert.equal(scoreOf(shield.room, shield.ids[0]), 0);
  assert.equal(scoreOf(shield.room, shield.ids[1]), 1);
  assert.equal(shield.room.game.ratings[shield.ids[1]], -2);

  const block = freshRoom(["阿凱", "小魚"]);
  applyAction(block.room, block.ids[0], { name: "start", game: "dj" });
  applyAction(block.room, block.ids[0], { name: "djMode", songs: 1 });
  lockReel(block.room, block.ids[0]);
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
  lockReel(extra.room, extra.ids[0]);
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
  draw.room.cards.hands[draw.ids[0]] = ["hide", "peek", "steal", "shield", "block"];
  draw.room.cards.bornAt = Date.now() - 3 * 60 * 1000;
  assert.equal(tickCardDraws(draw.room), false);
  assert.equal(draw.room.cards.hands[draw.ids[0]].length, 5);
  assert.equal(draw.room.cards.draws, 1);
}


  const spun = freshRoom(["阿凱", "小魚"]);
  const spinId = spun.ids[0];
  assert.equal(serialize(spun.room, spinId).cards.draw.spinsLeft, 3);
  const random = Math.random;
  try {
    Math.random = () => 0.4;
    assert.equal(applyAction(spun.room, spinId, { name: "cardSpin" }).ok, true);
    assert.equal(spun.room.cards.hands[spinId]?.length || 0, 0);
    assert.equal(serialize(spun.room, spinId).cards.draw.wheelCard, "respin");
    assert.equal(serialize(spun.room, spun.ids[1]).cards.draw.wheelCard, null);
    assert.equal(applyAction(spun.room, spinId, { name: "cardSpinTake" }).ok, true);
    assert.deepEqual(spun.room.cards.hands[spinId], ["respin"]);
    assert.equal(serialize(spun.room, spinId).cards.draw.spinsLeft, 2);
    assert.equal(spun.room.cards.spinReady[spinId] || 0, 0);
    Math.random = () => 0;
    assert.equal(applyAction(spun.room, spinId, { name: "cardSpin" }).ok, true);
    assert.equal(applyAction(spun.room, spinId, { name: "cardSpinTake" }).ok, true);
    assert.equal(applyAction(spun.room, spinId, { name: "cardSpin" }).ok, true);
    assert.equal(applyAction(spun.room, spinId, { name: "cardSpinTake" }).ok, true);
    assert.equal(serialize(spun.room, spinId).cards.draw.spinsLeft, 0);
    const readyAt = spun.room.cards.spinReady[spinId];
    assert.ok(readyAt > Date.now() + 29 * 60 * 1000);
    spun.room.cards.hands[spinId].pop();
    assert.match(applyAction(spun.room, spinId, { name: "cardSpin" }).error, /還沒到/);
    spun.room.cards.spinReady[spinId] = Date.now() - 1000;
    assert.equal(applyAction(spun.room, spinId, { name: "cardSpin" }).ok, true);
    assert.equal(applyAction(spun.room, spinId, { name: "cardSpinTake" }).ok, true);
    assert.equal(serialize(spun.room, spinId).cards.draw.spinsLeft, 0);
    assert.ok(spun.room.cards.spinReady[spinId] > Date.now() + 29 * 60 * 1000);
    spun.room.cards.hands[spinId].pop();
    assert.match(applyAction(spun.room, spinId, { name: "cardSpin" }).error, /還沒到/);
  } finally {
    Math.random = random;
  }

  const three = freshRoom(["阿凱"]);
  three.room.players[0].score = 20;
  three.room.cards.hands[three.ids[0]] = ["hide", "peek", "steal"];
  assert.equal(applyAction(three.room, three.ids[0], { name: "cardSpin" }).ok, true);
  assert.equal(serialize(three.room, three.ids[0]).cards.draw.spinsLeft, 3);
  assert.equal(applyAction(three.room, three.ids[0], { name: "cardBuy", card: "shield" }).ok, true);
  assert.equal(three.room.cards.hands[three.ids[0]].length, 4);

  const four = freshRoom(["阿凱"]);
  four.room.players[0].score = 20;
  four.room.cards.hands[four.ids[0]] = ["hide", "peek", "steal", "shield"];
  assert.equal(applyAction(four.room, four.ids[0], { name: "cardSpin" }).ok, true);
  assert.equal(serialize(four.room, four.ids[0]).cards.draw.spinsLeft, 3);
  assert.equal(applyAction(four.room, four.ids[0], { name: "cardBuy", card: "block" }).ok, true);
  assert.equal(four.room.cards.hands[four.ids[0]].length, 5);

  const fullDraw = freshRoom(["阿凱"]);
  fullDraw.room.players[0].score = 20;
  fullDraw.room.cards.hands[fullDraw.ids[0]] = ["hide", "peek", "steal", "shield", "block"];
  assert.match(applyAction(fullDraw.room, fullDraw.ids[0], { name: "cardSpin" }).error, /手牌滿了/);
  assert.equal(serialize(fullDraw.room, fullDraw.ids[0]).cards.draw.spinsLeft, 3);
  assert.match(applyAction(fullDraw.room, fullDraw.ids[0], { name: "cardBuy", card: "pass" }).error, /五張/);
  assert.equal(fullDraw.room.cards.hands[fullDraw.ids[0]].length, 5);
  assert.equal(fullDraw.room.players[0].score, 20);

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
    env: { ...process.env, PORT: String(port), DEV_TOOLS: "" },
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
    host.send({ type: "dev", name: "addFake" });
    const rejectedDev = await waitUntil(host.states, (msg) => msg.type === "error" && /開發工具/.test(msg.message || ""));
    assert.match(rejectedDev.message, /開發工具沒有開/);

    host.send({ type: "action", name: "start", game: "prompt" });
    const rejectedGame = await waitUntil(host.states, (msg) => msg.type === "error" && /沒有這個遊戲/.test(msg.message || ""));
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
    await waitUntil(firstDj.states, (msg) => msg.room?.game?.step === "reel");
    firstDj.send({ type: "action", name: "djReel", index: 2 });
    await waitUntil(firstListener.states, (msg) => msg.room?.game?.reel === 2 && msg.room.game.step === "reel");
    firstDj.send({ type: "action", name: "djReelLock" });
    await waitUntil(firstDj.states, (msg) => msg.room?.game?.step === "enter" && msg.room.game.reel === 2);
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
    await waitUntil(playlistDj.states, (msg) => msg.room?.game?.step === "reel" && msg.room.game.djId === playlistDjId);
    playlistDj.send({ type: "action", name: "djReel", index: 11 });
    await waitUntil(playlistListener.states, (msg) => msg.room?.game?.reel === 11);
    playlistDj.send({ type: "action", name: "djReelLock" });
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

function testProfile() {
  const { room, ids } = freshRoom(["阿凱", "小魚"]);
  const [a, b] = ids;
  const before = serialize(room, b);
  const blank = before.players.find((player) => player.id === a).profile;
  assert.equal(blank.emoji, "");
  assert.equal(blank.photo, "");
  assert.equal(blank.line, "");
  assert.equal(blank.from, "");
  assert.equal(blank.music, "");
  assert.equal(before.page, undefined);

  assert.equal(
    applyAction(room, a, {
      name: "profileSave",
      emoji: "🎸",
      line: "這趟想聽老歌",
      nickname: "阿凱",
    }).ok,
    true,
  );
  let mine = serialize(room, a).players.find((player) => player.id === a);
  assert.equal(mine.nickname, "阿凱");
  assert.equal(mine.profile.emoji, "🎸");
  assert.equal(mine.profile.line, "這趟想聽老歌");
  assert.equal(mine.profile.from, "");
  assert.equal(mine.profile.music, "");

  assert.equal(
    applyAction(room, a, {
      name: "profileSave",
      emoji: "🌙",
      nickname: "   ",
      line: "歌".repeat(50),
      from: "台南",
      music: "城市民謠",
    }).ok,
    true,
  );
  mine = room.players.find((player) => player.id === a);
  assert.equal(mine.nickname, "阿凱");
  assert.equal([...mine.profile.line].length, 40);
  assert.equal(mine.profile.from, "台南");
  assert.equal(mine.profile.music, "城市民謠");
  assert.equal(mine.profile.emoji, "🌙");

  assert.match(applyAction(room, a, { name: "profileSave", nickname: "一二三四五六七八九十一二三" }).error, /12/);
  assert.equal(room.players.find((player) => player.id === a).nickname, "阿凱");

  const tiny =
    "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=";
  assert.equal(applyAction(room, a, { name: "profileSave", photo: tiny, emoji: "🎧" }).ok, true);
  const seen = serialize(room, b);
  assert.equal(seen.ranking.find((item) => item.id === a).profile.photo, tiny);
  assert.equal(seen.players.find((player) => player.id === a).profile.emoji, "🎧");

  assert.match(applyAction(room, a, { name: "profileSave", photo: "data:image/gif;base64,AAAA" }).error, /沒辦法用/);
  assert.equal(room.players.find((player) => player.id === a).profile.photo, tiny);

  const raw = Buffer.alloc(200 * 1024 + 8, 7);
  const big = `data:image/jpeg;base64,${raw.toString("base64")}`;
  assert.match(applyAction(room, a, { name: "profileSave", photo: big }).error, /200KB/);
  assert.equal(room.players.find((player) => player.id === a).profile.photo, tiny);

  assert.equal(applyAction(room, a, { name: "profileSave", photo: "", emoji: "不是" }).ok, true);
  assert.equal(room.players.find((player) => player.id === a).profile.photo, "");
  assert.equal(room.players.find((player) => player.id === a).profile.emoji, "🎵");

  const profile = fs.readFileSync(path.join(root, "client/src/profile.jsx"), "utf8");
  const play = fs.readFileSync(path.join(root, "client/src/play.jsx"), "utf8");
  const app = fs.readFileSync(path.join(root, "client/src/App.jsx"), "utf8");
  const server = fs.readFileSync(path.join(root, "server/index.mjs"), "utf8");
  for (const emoji of ["🎵", "🎧", "🎸", "🎤", "🚗", "🧳", "🌙", "⭐", "🍵", "🌊"]) {
    assert.equal(profile.includes(emoji), true, emoji);
  }
  assert.equal(profile.includes("來自"), true);
  assert.equal(profile.includes("喜歡的音樂"), true);
  assert.equal(profile.includes("這個人還沒留下一句話。"), true);
  assert.equal(profile.includes("移除照片"), true);
  assert.equal(play.includes('["me", "個人"]'), true);
  assert.equal(play.includes("who-btn"), true);
  assert.equal(play.includes("setViewingId"), true);
  assert.equal(app.includes('value === "me"'), true);
  assert.equal(app.includes('next !== "me"'), true);
  const choose = app.slice(app.indexOf("function choosePage"), app.indexOf("useEffect", app.indexOf("function choosePage")));
  assert.equal(choose.includes("act("), false);
  assert.equal(choose.includes("setPage(next)"), true);
  assert.equal(server.includes('msg?.name === "profileSave"'), true);
  const cardsUi = fs.readFileSync(path.join(root, "client/src/cards.jsx"), "utf8");
  const djUi = fs.readFileSync(path.join(root, "client/src/dj.jsx"), "utf8");
  assert.equal(profile.includes("function PersonFace"), true);
  assert.equal(play.includes("PersonFace"), true);
  assert.equal(cardsUi.includes("PersonFace"), true);
  assert.equal(cardsUi.includes("hand.length >= 5"), true);
  assert.equal(djUi.includes("這支手機在播"), true);
  assert.equal(djUi.includes("評分的人"), true);
  assert.equal(server.includes("buf.length > 8000"), true);
}

function cueLive(room, djId, songs = 1) {
  const watch = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
  assert.equal(applyAction(room, djId, { name: "djMode", songs }).ok, true);
  lockReel(room, djId);
  assert.equal(applyAction(room, djId, { name: "djCue", url: watch }).ok, true);
}

function testPeekIdentity() {
  const peek = freshRoom(["阿凱", "小魚", "阿德"]);
  const [dj, rater, other] = peek.ids;
  applyAction(peek.room, dj, { name: "start", game: "dj" });
  peek.room.cards.hands[dj] = ["peek"];
  cueLive(peek.room, dj);
  assert.equal(applyAction(peek.room, rater, { name: "djRate", value: -2 }).ok, true);
  const hidden = serialize(peek.room, dj);
  assert.equal(hidden.game.ratings[rater], undefined);
  assert.equal(JSON.stringify(hidden.game.ratingFeed).includes("小魚"), false);
  assert.equal(JSON.stringify(hidden.game.ratingFeed).includes(rater), false);
  assert.deepEqual(hidden.game.ratingFeed, [{ value: -2, nickname: null, profile: null }]);
  assert.equal(hidden.prompt.text, "有人打了分，要偷看嗎？");
  assert.equal(JSON.stringify(hidden.prompt).includes("小魚"), false);
  const bystander = serialize(peek.room, other);
  assert.equal(bystander.game.ratings[rater], undefined);
  assert.equal(bystander.game.ratingFeed[0].nickname, null);
  assert.equal(bystander.game.ratingFeed[0].value, true);
  assert.equal(applyAction(peek.room, dj, { name: "cardPlay", card: "peek", targetId: hidden.prompt.targetId }).ok, true);
  const shown = serialize(peek.room, dj);
  assert.equal(shown.game.ratingFeed[0].nickname, "小魚");
  assert.equal(shown.game.ratingFeed[0].value, -2);
  assert.equal(shown.cards.peek.nickname, "小魚");
  assert.equal(shown.cards.peek.value, -2);
  assert.equal(serialize(peek.room, other).game.ratingFeed[0].nickname, null);
  assert.equal(serialize(peek.room, rater).game.ratings[rater], -2);

  const both = freshRoom(["阿凱", "小魚", "阿德"]);
  applyAction(both.room, both.ids[0], { name: "start", game: "dj" });
  cueLive(both.room, both.ids[0]);
  applyAction(both.room, both.ids[1], { name: "djRate", value: 1 });
  applyAction(both.room, both.ids[2], { name: "djRate", value: 2 });
  both.room.cards.hands[both.ids[0]] = ["peek"];
  const before = serialize(both.room, both.ids[0]);
  assert.deepEqual(
    before.game.ratingFeed.map((row) => row.nickname),
    [null, null],
  );
  assert.deepEqual(
    before.game.ratingFeed.map((row) => row.value),
    [1, 2],
  );
  assert.equal(applyAction(both.room, both.ids[0], { name: "cardPlay", card: "peek", targetId: both.ids[1] }).ok, true);
  const after = serialize(both.room, both.ids[0]).game.ratingFeed;
  assert.equal(after[0].nickname, "小魚");
  assert.equal(after[0].value, 1);
  assert.equal(after[1].nickname, null);
  assert.equal(after[1].value, 2);
}

function testPrompts() {
  const watch = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

  const shield = freshRoom(["阿凱", "小魚", "阿德"]);
  applyAction(shield.room, shield.ids[0], { name: "start", game: "dj" });
  shield.room.cards.hands[shield.ids[0]] = ["shield", "swap", "steal", "mode", "next"];
  cueLive(shield.room, shield.ids[0]);
  assert.equal(applyAction(shield.room, shield.ids[1], { name: "djRate", value: -2 }).ok, true);
  assert.equal(scoreOf(shield.room, shield.ids[0]), 0);
  assert.equal(scoreOf(shield.room, shield.ids[1]), 0);
  assert.equal(shield.room.game.ratings[shield.ids[1]], undefined);
  assert.equal(shield.room.game.step, "live");
  const asked = serialize(shield.room, shield.ids[0]);
  assert.equal(asked.prompt.text, "有人給了 -2，要使用護身嗎？");
  assert.equal(asked.prompt.card, "shield");
  assert.equal(asked.prompt.text.includes("小魚"), false);
  assert.equal(asked.game.ratings[shield.ids[1]], undefined);
  assert.deepEqual(asked.game.ratingFeed, [{ value: -2, nickname: null, profile: null }]);
  assert.equal(serialize(shield.room, shield.ids[1]).prompt, null);
  assert.equal(serialize(shield.room, shield.ids[2]).game.ratings[shield.ids[1]], undefined);
  assert.equal(serialize(shield.room, shield.ids[2]).game.ratingFeed.length, 0);
  assert.equal(serialize(shield.room, shield.ids[1]).game.ratings[shield.ids[1]], -2);
  assert.equal(applyAction(shield.room, shield.ids[0], { name: "cardDecline" }).ok, true);
  assert.deepEqual(shield.room.cards.hands[shield.ids[0]], ["shield", "swap", "steal", "mode", "next"]);
  assert.equal(scoreOf(shield.room, shield.ids[0]), -2);
  assert.equal(scoreOf(shield.room, shield.ids[1]), 1);
  assert.equal(shield.room.game.ratings[shield.ids[1]], -2);
  assert.equal(serialize(shield.room, shield.ids[0]).prompt, null);

  const used = freshRoom(["阿凱", "小魚"]);
  applyAction(used.room, used.ids[0], { name: "start", game: "dj" });
  used.room.cards.hands[used.ids[0]] = ["shield"];
  cueLive(used.room, used.ids[0]);
  applyAction(used.room, used.ids[1], { name: "djRate", value: -2 });
  assert.equal(applyAction(used.room, used.ids[0], { name: "cardPlay", card: "shield" }).ok, true);
  assert.deepEqual(used.room.cards.hands[used.ids[0]], []);
  assert.equal(scoreOf(used.room, used.ids[0]), 0);
  assert.equal(scoreOf(used.room, used.ids[1]), 1);
  assert.equal(used.room.game.ratings[used.ids[1]], -2);
  assert.equal(used.room.game.shield, true);

  const sunny = freshRoom(["阿凱", "小魚"]);
  applyAction(sunny.room, sunny.ids[0], { name: "start", game: "dj" });
  sunny.room.cards.hands[sunny.ids[0]] = ["shield"];
  cueLive(sunny.room, sunny.ids[0]);
  assert.equal(applyAction(sunny.room, sunny.ids[1], { name: "djRate", value: 1 }).ok, true);
  assert.equal(scoreOf(sunny.room, sunny.ids[0]), 1);
  assert.equal(serialize(sunny.room, sunny.ids[0]).prompt, null);
  assert.equal(sunny.room.game.pendingRatings?.[sunny.ids[1]], undefined);

  const gated = freshRoom(["阿凱", "小魚", "阿德"]);
  applyAction(gated.room, gated.ids[0], { name: "start", game: "dj" });
  gated.room.cards.hands[gated.ids[0]] = ["shield"];
  gated.room.cards.hands[gated.ids[2]] = ["block"];
  cueLive(gated.room, gated.ids[0]);
  applyAction(gated.room, gated.ids[1], { name: "djRate", value: -1 });
  assert.equal(serialize(gated.room, gated.ids[2]).prompt.card, "block");
  assert.equal(serialize(gated.room, gated.ids[2]).prompt.text, "有人給了 -1，要使用擋負分嗎？");
  assert.equal(serialize(gated.room, gated.ids[0]).prompt, null);
  assert.equal(applyAction(gated.room, gated.ids[2], { name: "cardDecline" }).ok, true);
  assert.deepEqual(gated.room.cards.hands[gated.ids[2]], ["block"]);
  assert.equal(serialize(gated.room, gated.ids[0]).prompt.card, "shield");
  assert.equal(applyAction(gated.room, gated.ids[0], { name: "cardDecline" }).ok, true);
  assert.equal(scoreOf(gated.room, gated.ids[0]), -1);
  assert.equal(gated.room.game.ratings[gated.ids[1]], -1);

  const blocked = freshRoom(["阿凱", "小魚", "阿德"]);
  applyAction(blocked.room, blocked.ids[0], { name: "start", game: "dj" });
  blocked.room.cards.hands[blocked.ids[0]] = ["shield"];
  blocked.room.cards.hands[blocked.ids[2]] = ["block"];
  cueLive(blocked.room, blocked.ids[0]);
  applyAction(blocked.room, blocked.ids[1], { name: "djRate", value: -2 });
  assert.equal(applyAction(blocked.room, blocked.ids[2], { name: "cardPlay", card: "block" }).ok, true);
  assert.deepEqual(blocked.room.cards.hands[blocked.ids[2]], []);
  assert.equal(serialize(blocked.room, blocked.ids[0]).prompt, null);
  assert.equal(scoreOf(blocked.room, blocked.ids[0]), 0);
  assert.equal(scoreOf(blocked.room, blocked.ids[1]), 0);
  assert.equal(blocked.room.game.ratings[blocked.ids[1]], undefined);
  assert.equal(applyAction(blocked.room, blocked.ids[1], { name: "djRate", value: 2 }).ok, true);
  assert.equal(scoreOf(blocked.room, blocked.ids[0]), 2);

  const hide = freshRoom(["阿凱", "小魚"]);
  applyAction(hide.room, hide.ids[0], { name: "start", game: "dj" });
  hide.room.cards.hands[hide.ids[1]] = ["hide"];
  cueLive(hide.room, hide.ids[0]);
  applyAction(hide.room, hide.ids[1], { name: "djRate", value: 2 });
  const hideAsk = serialize(hide.room, hide.ids[1]);
  assert.equal(hideAsk.prompt.card, "hide");
  assert.equal(hideAsk.prompt.text, "你的分數變成了 1，要使用隱藏兩小時嗎？");
  assert.equal(hideAsk.ranking.some((row) => row.id === hide.ids[1]), true);
  assert.equal(applyAction(hide.room, hide.ids[1], { name: "cardDecline" }).ok, true);
  assert.deepEqual(hide.room.cards.hands[hide.ids[1]], ["hide"]);
  assert.equal(serialize(hide.room, hide.ids[0]).ranking.some((row) => row.id === hide.ids[1]), true);

  const hideUse = freshRoom(["阿凱", "小魚"]);
  applyAction(hideUse.room, hideUse.ids[0], { name: "start", game: "dj" });
  hideUse.room.cards.hands[hideUse.ids[1]] = ["hide"];
  cueLive(hideUse.room, hideUse.ids[0]);
  applyAction(hideUse.room, hideUse.ids[1], { name: "djRate", value: 1 });
  assert.equal(applyAction(hideUse.room, hideUse.ids[1], { name: "cardPlay", card: "hide" }).ok, true);
  assert.deepEqual(hideUse.room.cards.hands[hideUse.ids[1]], []);
  assert.equal(serialize(hideUse.room, hideUse.ids[1]).ranking.some((row) => row.id === hideUse.ids[1]), false);

  const hidden = freshRoom(["阿凱", "小魚"]);
  applyAction(hidden.room, hidden.ids[0], { name: "start", game: "dj" });
  hidden.room.cards.hands[hidden.ids[1]] = ["hide"];
  hidden.room.cards.hideUntil[hidden.ids[1]] = Date.now() + 60_000;
  cueLive(hidden.room, hidden.ids[0]);
  applyAction(hidden.room, hidden.ids[1], { name: "djRate", value: 1 });
  assert.equal(serialize(hidden.room, hidden.ids[1]).prompt, null);

  const queued = freshRoom(["阿凱", "小魚"]);
  applyAction(queued.room, queued.ids[0], { name: "start", game: "dj" });
  queued.room.cards.hands[queued.ids[0]] = ["hide", "peek"];
  cueLive(queued.room, queued.ids[0]);
  applyAction(queued.room, queued.ids[1], { name: "djRate", value: 2 });
  assert.equal(serialize(queued.room, queued.ids[0]).prompt.card, "hide");
  assert.equal(applyAction(queued.room, queued.ids[0], { name: "cardDecline" }).ok, true);
  const peek = serialize(queued.room, queued.ids[0]).prompt;
  assert.equal(peek.card, "peek");
  assert.equal(peek.targetId, queued.ids[1]);
  assert.equal(peek.text, "有人打了分，要偷看嗎？");
  assert.equal(peek.text.includes("小魚"), false);
  assert.equal(applyAction(queued.room, queued.ids[0], { name: "cardPlay", card: "peek", targetId: peek.targetId }).ok, true);
  assert.equal(serialize(queued.room, queued.ids[0]).cards.peek.value, 2);
  assert.deepEqual(queued.room.cards.hands[queued.ids[0]], ["hide"]);

  const passed = freshRoom(["阿凱", "小魚"]);
  passed.room.cards.hands[passed.ids[0]] = ["pass"];
  applyAction(passed.room, passed.ids[0], { name: "start", game: "dj" });
  assert.equal(serialize(passed.room, passed.ids[0]).prompt.text, "輪到你了，要使用這輪不當嗎？");
  assert.equal(serialize(passed.room, passed.ids[1]).prompt, null);
  assert.equal(applyAction(passed.room, passed.ids[0], { name: "cardDecline" }).ok, true);
  assert.deepEqual(passed.room.cards.hands[passed.ids[0]], ["pass"]);
  assert.equal(passed.room.game.djId, passed.ids[0]);

  const passUse = freshRoom(["阿凱", "小魚"]);
  passUse.room.cards.hands[passUse.ids[0]] = ["pass"];
  applyAction(passUse.room, passUse.ids[0], { name: "start", game: "dj" });
  assert.equal(applyAction(passUse.room, passUse.ids[0], { name: "cardPlay", card: "pass" }).ok, true);
  assert.equal(passUse.room.game.djId, passUse.ids[1]);
  assert.deepEqual(passUse.room.cards.hands[passUse.ids[0]], []);

  const spin = freshRoom(["阿凱", "小魚"]);
  applyAction(spin.room, spin.ids[0], { name: "start", game: "dj" });
  spin.room.cards.hands[spin.ids[0]] = ["respin"];
  assert.equal(serialize(spin.room, spin.ids[0]).prompt, null);
  assert.equal(applyAction(spin.room, spin.ids[0], { name: "djSpin", songs: 1 }).ok, true);
  assert.equal(serialize(spin.room, spin.ids[0]).prompt.text, "轉到了一首歌，要再轉一次嗎？");
  assert.equal(serialize(spin.room, spin.ids[1]).prompt, null);
  assert.equal(applyAction(spin.room, spin.ids[0], { name: "cardPlay", card: "respin" }).ok, true);
  assert.equal(spin.room.game.extraSpins, 1);
  assert.equal(spin.room.game.step, "pick");

  const more = freshRoom(["阿凱", "小魚"]);
  applyAction(more.room, more.ids[0], { name: "start", game: "dj" });
  more.room.cards.hands[more.ids[1]] = ["extra"];
  assert.equal(applyAction(more.room, more.ids[0], { name: "djMode", songs: 2 }).ok, true);
  assert.equal(serialize(more.room, more.ids[1]).prompt.text, "這一輪是二首歌，要多播一首嗎？");
  assert.equal(serialize(more.room, more.ids[0]).prompt, null);
  assert.equal(applyAction(more.room, more.ids[1], { name: "cardDecline" }).ok, true);
  assert.deepEqual(more.room.cards.hands[more.ids[1]], ["extra"]);
  lockReel(more.room, more.ids[0]);
  applyAction(more.room, more.ids[0], { name: "djCue", url: watch });
  assert.equal(serialize(more.room, more.ids[1]).prompt, null);
  applyAction(more.room, more.ids[0], { name: "djFinish" });
  more.room.cards.hands[more.ids[1]] = [];
  more.room.cards.hands[more.ids[0]] = ["double"];
  applyAction(more.room, more.ids[1], { name: "djMode", songs: 1 });
  lockReel(more.room, more.ids[1]);
  applyAction(more.room, more.ids[1], { name: "djCue", url: watch });
  assert.equal(serialize(more.room, more.ids[0]).prompt.text, "這一首要使用加倍嗎？");
  assert.equal(serialize(more.room, more.ids[1]).prompt, null);
  assert.equal(applyAction(more.room, more.ids[0], { name: "cardPlay", card: "double" }).ok, true);
  applyAction(more.room, more.ids[0], { name: "djRate", value: 1 });
  assert.equal(scoreOf(more.room, more.ids[1]), 2);
  assert.equal(scoreOf(more.room, more.ids[0]), 1);

  const play = fs.readFileSync(path.join(root, "client/src/play.jsx"), "utf8");
  const app = fs.readFileSync(path.join(root, "client/src/App.jsx"), "utf8");
  const djUi = fs.readFileSync(path.join(root, "client/src/dj.jsx"), "utf8");
  const cardsUi = fs.readFileSync(path.join(root, "client/src/cards.jsx"), "utf8");
  assert.equal(play.includes("這次不用"), true);
  assert.equal(play.includes('name: "cardDecline"'), true);
  assert.equal(play.includes('name: "cardPlay"'), true);
  assert.equal(app.includes("<CardPrompt"), true);
  assert.equal(djUi.includes("評分的人"), true);
  assert.equal(djUi.includes("`+${value}`"), true);
  assert.equal(djUi.includes("有人"), true);
  assert.equal(djUi.includes("ratingFeed"), true);
  assert.equal(djUi.includes("一首歌、二首歌、三首歌、此輪跳過，機會一樣。"), false);
  assert.equal(djUi.includes("wheelLanded"), true);
  assert.equal(app.includes("旅途小遊戲"), false);
  assert.equal(app.includes("on the trip"), true);
  assert.equal(cardsUi.includes("使用"), true);
}

try {
  testDecksAndCopy();
  testWatchSync();
  testLogic();
  testPeekIdentity();
  testPrompts();
  await testServer();
  console.log("smoke ok");
} catch (error) {
  console.error(error);
  process.exitCode = 1;
}
