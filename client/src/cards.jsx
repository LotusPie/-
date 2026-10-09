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

export function CardDesk({ room, youId, act }) {
  const cards = room.cards;
  const [pending, setPending] = useState(null);
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
      <h2>手牌</h2>
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
        {shop.map((card) => (
          <button key={card.id} className="secondary" type="button" onClick={() => act({ name: "cardBuy", card: card.id })}>
            {card.name}
            <span className="card-cost">{card.cost} 分</span>
          </button>
        ))}
      </div>
    </section>
  );
}
