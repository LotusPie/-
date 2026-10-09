import { useState } from "react";
import { nameOf } from "./play.jsx";

function signed(value) {
  return value > 0 ? `+${value}` : String(value);
}

function modeName(mode) {
  if (mode === "playlist") return "貼歌單";
  if (mode === "own") return "自行選歌";
  return "";
}

const CARD_COPY = {
  hide: {
    short: "先把你的分數藏起來",
    detail: "用了之後，這一輪其他人看你的分數是問號，你自己還看得到。下一位迪爵上台就恢復。",
  },
  swap: {
    short: "跟一個人對調分數",
    detail: "選一個在線的人，立刻對調兩人現在的房間積分，負分也一起換。",
  },
  peek: {
    short: "看某人這首打幾分",
    detail: "選一個已經評過這一首的人，只有你看得到他打的數字。他還沒評，會告訴你。",
  },
  shield: {
    short: "迪爵不受負分",
    detail: "你當這輪迪爵時，別人打的負分不會加到你身上。打分的人自己仍 +1。位子換人就結束。",
  },
  block: {
    short: "這一首不能打負分",
    detail: "這一首不能打 -1 或 -2。換到下一首，或這一輪結束，就解除。",
  },
  double: {
    short: "你的評分算兩次",
    detail: "你下一筆打在這一首的數字，會加到迪爵身上兩次。你自己仍只 +1。",
  },
  steal: {
    short: "從別人拿 1 分",
    detail: "選一個人。對方分數大於 0，他 -1、你 +1。對方是 0 或負分，這張牌不消耗，並說沒有分可以拿。",
  },
  respin: {
    short: "轉盤可以再轉",
    detail: "只有本輪迪爵、還在選模式時能用。轉過一次之後，還可以再轉一次。",
  },
  mode: {
    short: "指定下一次怎麼播",
    detail: "你選自行選歌或貼歌單。下一次轉盤會落在這個模式。若還在選模式，這一轉就用它。",
  },
  extra: {
    short: "這一輪多播一首",
    detail: "貼歌單這輪改成播四首，要換到第五首才結束。自行選歌則可以再貼一支。",
  },
  next: {
    short: "點名下一位迪爵",
    detail: "選一個在線的人。這一輪結束後，若他還在線上，就換他當迪爵。",
  },
  pass: {
    short: "把迪爵讓給下一個人",
    detail: "若現在輪到你、歌還沒開始，位子立刻讓給下一個人。若還沒輪到你，下次輪到你時跳過。這是用一張牌。",
  },
};

export function CardDesk({ room, youId, act }) {
  const cards = room.cards;
  const [pending, setPending] = useState(null);
  const [detailId, setDetailId] = useState(null);
  if (!cards) return null;
  const shop = cards.shop || [];
  const hand = cards.hand || [];
  const specOf = (id) => shop.find((card) => card.id === id);
  const active = cards.active || {};
  const notes = [];
  if (active.shield) notes.push("護身：負分不會加到這輪迪爵");
  if (active.blockNegative) notes.push("擋負分：這一首不能打負分");
  if (active.forcedMode) notes.push(`指定模式：下一轉是${modeName(active.forcedMode)}`);
  if (active.extra) notes.push("多一首：這一輪多播一首");
  if (active.nextDjId) notes.push(`指定下一位：${nameOf(room, active.nextDjId)}`);
  if (cards.doubled) notes.push("你的下一筆評分會算兩次");

  const online = room.players.filter((player) => player.connected);
  const others = online.filter((player) => player.id !== youId);
  const pendingSpec = pending ? specOf(pending) : null;

  function play(cardId, extra = {}) {
    act({ name: "cardPlay", card: cardId, ...extra });
    setPending(null);
  }

  function choose(cardId) {
    const spec = specOf(cardId);
    if (!spec?.needs) {
      play(cardId);
      return;
    }
    setPending((current) => (current === cardId ? null : cardId));
  }

  return (
    <section className="card-desk panel">
      <h2>目前的手牌</h2>
      {hand.length ? (
        <div className="card-hand">
          {hand.map((id, index) => {
            const spec = specOf(id);
            return (
              <button
                key={`${id}-${index}`}
                className={pending === id ? "choice on" : "choice"}
                type="button"
                onClick={() => choose(id)}
              >
                {spec?.name || id}
              </button>
            );
          })}
        </div>
      ) : (
        <p className="hint">還沒有牌。每 3 分鐘抽一張，手牌滿三張就跳過。也可以用分數買。</p>
      )}
      {pendingSpec?.needs === "mode" && (
        <div className="card-picks">
          <button className="secondary" type="button" onClick={() => play(pending, { mode: "own" })}>
            自行選歌
          </button>
          <button className="secondary" type="button" onClick={() => play(pending, { mode: "playlist" })}>
            貼歌單
          </button>
        </div>
      )}
      {pendingSpec && pendingSpec.needs !== "mode" && (
        <div className="card-picks">
          {(pendingSpec.needs === "player" ? online : others).map((player) => (
            <button key={player.id} className="secondary" type="button" onClick={() => play(pending, { targetId: player.id })}>
              {player.nickname}
              {player.id === youId ? "（你）" : ""}
            </button>
          ))}
          {pendingSpec.needs === "other" && others.length === 0 ? <p className="hint">現在沒有別的旅伴。</p> : null}
        </div>
      )}
      {cards.peek ? (
        <p className="card-note">
          你看到{cards.peek.nickname}這首打了 {signed(cards.peek.value)}
        </p>
      ) : null}
      {notes.length ? (
        <ul className="card-notes">
          {notes.map((line) => (
            <li key={line}>{line}</li>
          ))}
        </ul>
      ) : null}
      <h2>牌店</h2>
      <div className="card-shop">
        {shop.map((card) => {
          const copy = CARD_COPY[card.id] || { short: card.name, detail: card.name };
          const open = detailId === card.id;
          return (
            <article className="shop-card" key={card.id}>
              <p className="shop-name">{card.name}</p>
              <p className="shop-blurb">{copy.short}</p>
              <div className="shop-actions">
                <button className="texty" type="button" onClick={() => setDetailId(open ? null : card.id)}>
                  {open ? "收起" : "看詳細"}
                </button>
                <button className="secondary" type="button" onClick={() => act({ name: "cardBuy", card: card.id })}>
                  買下
                  <span className="card-cost">{card.cost} 分</span>
                </button>
              </div>
              {open ? <p className="card-detail">{copy.detail}</p> : null}
            </article>
          );
        })}
      </div>
    </section>
  );
}
