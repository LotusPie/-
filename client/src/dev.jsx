import { useEffect, useState } from "react";
import { REEL_ITEMS } from "./dj.jsx";
import { nameOf, remainText } from "./play.jsx";

const WHEEL_STOPS = [
  { songs: 1, label: "一首歌" },
  { songs: 2, label: "二首歌" },
  { songs: 3, label: "三首歌" },
  { songs: 0, label: "此輪跳過" },
];

function songLabel(count) {
  return WHEEL_STOPS.find((stop) => stop.songs === count)?.label || "";
}

function hideLine(player, now) {
  if (!player.scoreHideUntil || player.scoreHideUntil <= now) return `${player.nickname}：沒有在藏`;
  return `${player.nickname}：還有 ${remainText(player.scoreHideUntil - now)}`;
}

function drawLine(player, now) {
  const left = player.drawsLeft == null ? 3 : player.drawsLeft;
  if (left > 0) return `${player.nickname}：還可以抽 ${left} 次`;
  const wait = (player.drawReadyAt || 0) - now;
  if (wait > 0) return `${player.nickname}：冷卻 ${remainText(wait)}`;
  return `${player.nickname}：冷卻 0 秒`;
}

export function DevPanel({ room, youId, dev }) {
  const [targetId, setTargetId] = useState(youId || "");
  const [score, setScore] = useState("0");
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  const players = room?.players || [];
  const target = players.some((player) => player.id === targetId) ? targetId : youId || players[0]?.id || "";
  const shop = room?.cards?.shop || [];
  const fakes = players.filter((player) => player.fake);
  const game = room?.game?.kind === "dj" ? room.game : null;
  const wheelKnown = game && (game.wheelSongs === 0 || game.wheelSongs);
  const wheelText = !game ? "還沒開始" : wheelKnown ? songLabel(game.wheelSongs) : songLabel(game.songs) || "還沒轉";
  const reelText = game?.reel == null ? "還沒轉" : REEL_ITEMS[game.reel] || "還沒轉";

  function run(action) {
    dev({ ...action, targetId: action.targetId || target });
  }

  return (
    <section className="panel dev-panel">
      <h2>開發</h2>
      {!room ? <p className="hint">進房間之後，這裡的按鈕會改這一房。其他手機看得到結果，但看不到這個頁面。</p> : null}
      {room ? (
        <>
          <p className="hint">改的是這一房。大家會一起看到結果。</p>
          <div className="dev-picks" role="group" aria-label="選一位旅伴">
            {players.map((player) => (
              <button
                key={player.id}
                type="button"
                className={player.id === target ? "primary" : "secondary"}
                aria-pressed={player.id === target}
                onClick={() => setTargetId(player.id)}
              >
                {player.nickname}
                {player.id === youId ? "（你）" : ""}
                {player.fake ? " · 假" : ""}
              </button>
            ))}
          </div>

          <section className="stack">
            <h2>房間</h2>
            <button className="secondary" type="button" onClick={() => dev({ name: "addFake" })}>
              加一位假旅伴
            </button>
            {fakes.map((player) => (
              <button key={player.id} className="secondary" type="button" onClick={() => dev({ name: "removeFake", targetId: player.id })}>
                移走{player.nickname}
              </button>
            ))}
            <button className="secondary" type="button" onClick={() => run({ name: "nextDj" })}>
              下一位迪爵是{nameOf(room, target)}
            </button>
          </section>

          <section className="stack">
            <h2>分數</h2>
            <form
              className="dev-score"
              onSubmit={(event) => {
                event.preventDefault();
                if (!/^-?\d+$/.test(score.trim())) return;
                dev({ name: "setScore", targetId: target, score: Number(score) });
              }}
            >
              <label>
                {nameOf(room, target)}的分數
                <input
                  inputMode="numeric"
                  value={score}
                  onChange={(event) => setScore(event.target.value)}
                />
              </label>
              <button className="primary" type="submit">
                設定
              </button>
            </form>
          </section>

          <section className="stack">
            <h2>隱藏兩小時</h2>
            <button className="secondary" type="button" onClick={() => run({ name: "hide" })}>
              現在藏起{nameOf(room, target)}
            </button>
            <button className="secondary" type="button" onClick={() => run({ name: "hideStack" })}>
              再加兩小時
            </button>
            <button className="secondary" type="button" onClick={() => run({ name: "hideSoon" })}>
              十秒後恢復
            </button>
          </section>

          <section className="stack">
            <h2>轉盤</h2>
            <div className="dev-picks">
              {WHEEL_STOPS.map((stop) => (
                <button key={stop.songs} className="secondary" type="button" onClick={() => dev({ name: "wheel", songs: stop.songs })}>
                  {stop.label}
                </button>
              ))}
            </div>
          </section>

          <section className="stack">
            <h2>拉霸</h2>
            <div className="dev-reel">
              {REEL_ITEMS.map((label, index) => (
                <button key={label} className="secondary" type="button" onClick={() => dev({ name: "reel", index })}>
                  {label}
                </button>
              ))}
            </div>
          </section>

          <section className="stack">
            <h2>卡牌</h2>
            <div className="dev-reel">
              {shop.map((card) => (
                <button key={card.id} className="secondary" type="button" onClick={() => run({ name: "giveCard", card: card.id })}>
                  給{card.name}
                </button>
              ))}
            </div>
            <button className="secondary" type="button" onClick={() => run({ name: "clearHand" })}>
              清掉手牌
            </button>
            <button className="secondary" type="button" onClick={() => run({ name: "freeDraws" })}>
              免費抽三次
            </button>
            <button className="secondary" type="button" onClick={() => run({ name: "drawReady" })}>
              冷卻歸零
            </button>
            <button className="secondary" type="button" onClick={() => run({ name: "drawSoon" })}>
              冷卻十秒
            </button>
          </section>

          <details className="dev-state">
            <summary>現在的狀態</summary>
            <dl>
              <dt>這一輪</dt>
              <dd>{game ? nameOf(room, game.djId) : "還沒開始"}</dd>
              <dt>轉盤</dt>
              <dd>{wheelText}</dd>
              <dt>拉霸</dt>
              <dd>{reelText}</dd>
              <dt>隱藏</dt>
              {players.map((player) => (
                <dd key={player.id}>{hideLine(player, now)}</dd>
              ))}
              <dt>抽牌</dt>
              {players.map((player) => (
                <dd key={`${player.id}-draw`}>{drawLine(player, now)}</dd>
              ))}
            </dl>
          </details>
        </>
      ) : null}
    </section>
  );
}
