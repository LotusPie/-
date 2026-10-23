import { useEffect, useState } from "react";
import { CardFace } from "./card-faces.jsx";
import { PersonFace, ProfileView } from "./profile.jsx";

export function playerOf(room, id) {
  return room?.players?.find((player) => player.id === id) || null;
}

export function nameOf(room, id) {
  return playerOf(room, id)?.nickname || "旅伴";
}

export function remainText(ms) {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = total % 60;
  if (hours > 0) return hours + " 小時 " + minutes + " 分";
  if (minutes > 0) return minutes + " 分 " + seconds + " 秒";
  return seconds + " 秒";
}

export function CooldownText({ until }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!until) return undefined;
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [until]);
  if (!until) return null;
  const remain = until - now;
  if (remain <= 0) return null;
  return <p className="draw-cooldown">冷卻 {remainText(remain)}</p>;
}

function rankWord(rank) {
  if (rank === 1) return "第一";
  if (rank === 2) return "第二";
  if (rank === 3) return "第三";
  return String(rank);
}

export function Home({
  nickname,
  setNickname,
  code,
  setCode,
  onCreate,
  onJoin,
  error,
  pending,
}) {
  return (
    <div className="home stack">
      {error && (
        <p className="banner" role="alert">
          {error}
        </p>
      )}

      <form
        className="panel stack"
        onSubmit={(event) => {
          event.preventDefault();
          onCreate(nickname);
        }}
      >
        <label>
          你的暱稱
          <input
            value={nickname}
            onChange={(event) => setNickname(event.target.value)}
            maxLength={12}
            autoComplete="nickname"
            placeholder="例如：阿凱"
            required
          />
        </label>
        <button className="primary" type="submit" disabled={pending}>
          建立房間
        </button>
      </form>

      <form
        className="panel stack"
        onSubmit={(event) => {
          event.preventDefault();
          onJoin(nickname, code);
        }}
      >
        <label>
          房間代碼
          <input
            className="code-input"
            value={code}
            onChange={(event) =>
              setCode(event.target.value.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 4))
            }
            maxLength={4}
            autoCapitalize="characters"
            autoCorrect="off"
            spellCheck={false}
            placeholder="4 個字"
            required
          />
        </label>
        <button className="secondary" type="submit" disabled={pending}>
          加入房間
        </button>
      </form>
    </div>
  );
}

export function Roster({ room, youId, turnId, bumped }) {
  return (
    <ul className="roster" aria-label="旅伴">
      {room.players.map((player) => {
        const classes = [
          player.id === youId ? "me" : "",
          player.id === turnId ? "turn" : "",
          player.connected ? "" : "off",
        ]
          .filter(Boolean)
          .join(" ");
        return (
          <li key={player.id} className={classes}>
            <PersonFace nickname={player.nickname} profile={player.profile} />
            <span className="nick">
              <span className="who-name">{player.nickname}</span>
              {player.id === youId && <em>你</em>}
              {player.id === room.hostId && <em className="host">房主</em>}
              {!player.connected && <em>離線</em>}
            </span>
            <strong
              className={`points${bumped[player.id] ? " bump" : ""}${!player.scoreHidden && player.score < 0 ? " neg" : ""}`}
            >
              {player.scoreHidden ? "?" : player.score}
            </strong>
          </li>
        );
      })}
    </ul>
  );
}

export function Ranking({ ranking, youId, onOpen }) {
  return (
    <ol className="ranking">
      {ranking.map((row) => (
        <li key={row.id} className={row.id === youId ? "me" : ""}>
          <span className="rank">{rankWord(row.rank)}</span>
          <button type="button" className="who who-btn" onClick={() => onOpen?.(row.id)}>
            <PersonFace nickname={row.nickname} profile={row.profile} />
            <span className="who-name">
              {row.nickname}
              {row.id === youId ? "（你）" : ""}
              {!row.connected ? " · 離線" : ""}
            </span>
          </button>
          <span className="score-slot">
            <strong>{row.scoreHidden ? "?" : row.score}</strong>
            {row.scoreHideUntil ? <CooldownText until={row.scoreHideUntil} /> : null}
          </span>
        </li>
      ))}
    </ol>
  );
}

const INTROS = {
  cards: {
    title: "我的卡牌",
    lines: [
      "普通牌手牌最多 5 張",
      "進房可免費轉 3 次，之後每 30 分鐘一次",
      "轉盤有 10% 是兌換券",
      "功能牌不限張數，每張 10 點，或用兌換券換",
      "功能牌管 10 分鐘怎麼說話",
      "抓到了要另外兩人附議才扣分",
    ],
  },
  rank: {
    title: "積分排名",
    lines: ["聽歌可以打 -2 到 +2，不打也行", "幫別人打分自己 +1", "隱藏兩小時的人不在榜上，其他人從第一名重排"],
  },
  me: {
    title: "個人",
    lines: ["頭像、一句話、基本資料都選填", "進房後才選八個角色", "同一個角色用猜拳決定"],
  },
};

export function IntroPage({ kind }) {
  const intro = INTROS[kind];
  if (!intro) return null;
  return (
    <section className="panel stack intro-page">
      <h2>{intro.title}</h2>
      <ul className="intro-list">
        {intro.lines.map((line) => (
          <li key={line}>{line}</li>
        ))}
      </ul>
    </section>
  );
}

export function PageSwitch({ page, onChange, dev = false, inRoom = false }) {
  const items = [
    ["play", inRoom ? "如果我是迪爵" : "首頁"],
    ["cards", "我的卡牌"],
    ["rank", "積分排名"],
    ["me", "個人"],
  ];
  if (dev) items.push(["dev", "開發"]);
  return (
    <nav className={dev ? "page-switch has-dev" : "page-switch"} aria-label="切換頁面">
      {items.map(([id, label]) => (
        <button
          key={id}
          type="button"
          aria-current={page === id ? "page" : undefined}
          onClick={() => onChange(id)}
        >
          {label}
        </button>
      ))}
    </nav>
  );
}

export function CardPrompt({ room, act }) {
  const prompt = room?.prompt;
  if (!prompt) return null;
  const name =
    room.cards?.shop?.find((card) => card.id === prompt.card)?.name ||
    room.cards?.functionShop?.find((card) => card.id === prompt.card)?.name ||
    "";
  return (
    <div className="card-sheet" role="dialog" aria-label={name || "卡牌"}>
      <CardFace id={prompt.card} name={name} />
      <p className="card-sheet-text">{prompt.text}</p>
      {prompt.kind === "jiege" ? (
        <div className="row">
          <button className="secondary" type="button" onClick={() => act({ name: "roleAnswer", refuse: false })}>
            就這樣
          </button>
          <button className="primary" type="button" onClick={() => act({ name: "roleAnswer", refuse: true })}>
            不要
          </button>
        </div>
      ) : (
        <div className="row">
          <button className="secondary" type="button" onClick={() => act({ name: "cardDecline" })}>
            這次不用
          </button>
          <button
            className="primary"
            type="button"
            onClick={() => act({ name: "cardPlay", card: prompt.card, targetId: prompt.targetId || undefined })}
          >
            使用
          </button>
        </div>
      )}
    </div>
  );
}

export function ScorePage({ room, youId }) {
  const [viewingId, setViewingId] = useState("");
  if (!room) {
    return (
      <section className="panel score-page">
        <h2>積分排名</h2>
        <p className="hint">進房間之後，這裡會列出大家的分數。</p>
      </section>
    );
  }
  const hasScore = room.players.some((player) => player.score !== 0);
  const viewed = room.players.find((player) => player.id === viewingId) || null;
  return (
    <section className="panel score-page stack">
      <h2>積分排名</h2>
      {hasScore ? null : <p className="hint">分數會在遊戲裡慢慢長出來。現在大家都是 0。</p>}
      <Ranking ranking={room.ranking || []} youId={youId} onOpen={setViewingId} />
      {viewed ? (
        <ProfileView nickname={viewed.nickname} profile={viewed.profile} onClose={() => setViewingId("")} />
      ) : null}
    </section>
  );
}

export function Lobby({ onStart, onLeave }) {
  const [leaving, setLeaving] = useState(false);
  return (
    <div className="stack lobby">
      <section className="panel">
        <h2>車上主遊戲</h2>
        <p className="hint">大家一起看同一支影片，其他人評分。</p>
      </section>
      <button className="game-choice dj" type="button" onClick={() => onStart("dj")}>
        <span className="kicker">乘客玩</span>
        <strong>如果我是迪爵</strong>
        <span>轉盤決定播一首、兩首、三首，或此輪跳過。</span>
      </button>
      {leaving ? (
        <div className="confirm">
          <p>離開後，你的名字會從名單拿掉。想再玩就用代碼進來。</p>
          <div className="row">
            <button className="secondary" type="button" onClick={() => setLeaving(false)}>
              留下
            </button>
            <button className="primary" type="button" onClick={onLeave}>
              離開房間
            </button>
          </div>
        </div>
      ) : (
        <button className="texty" type="button" onClick={() => setLeaving(true)}>
          離開房間
        </button>
      )}
    </div>
  );
}
