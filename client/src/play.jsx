import { useEffect, useState } from "react";
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
  invited,
}) {
  return (
    <div className="home stack">
      <section className="ticket">
        <div className="ticket-top">
          <span className="stamp">私人牌局</span>
        </div>
        <p className="lede">
          朋友一起在路上玩。輪流當迪爵，大家看同一支影片。沒有帳號，房間只有拿到代碼的人進得來。
        </p>
      </section>

      {invited && (
        <p className="invite">
          朋友把房間 <strong>{invited}</strong> 留給你了。取個暱稱就能進來。
        </p>
      )}
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

export function PageSwitch({ page, onChange, dev = false }) {
  const items = [
    ["play", "如果我是迪爵"],
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
