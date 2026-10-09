import { useEffect, useState } from "react";

export function nameOf(room, id) {
  return room.players.find((player) => player.id === id)?.nickname || "旅伴";
}

function formatDelta(delta) {
  if (delta > 0) return `+${delta}`;
  return String(delta);
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
          朋友一起在路上玩。抽一張話題、做一個小挑戰，或記一筆好玩的分數。沒有帳號，房間只有拿到代碼的人進得來。
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
            <span className="nick">
              {player.nickname}
              {player.id === youId && <em>你</em>}
              {player.id === room.hostId && <em className="host">房主</em>}
              {!player.connected && <em>離線</em>}
            </span>
            <strong
              className={`points${bumped[player.id] ? " bump" : ""}${player.score < 0 ? " neg" : ""}`}
            >
              {player.score}
            </strong>
          </li>
        );
      })}
    </ul>
  );
}

export function Ranking({ ranking, youId }) {
  return (
    <ol className="ranking">
      {ranking.map((row) => (
        <li key={row.id} className={row.id === youId ? "me" : ""}>
          <span className="rank">{rankWord(row.rank)}</span>
          <span className="who">
            {row.nickname}
            {row.id === youId ? "（你）" : ""}
            {!row.connected ? " · 離線" : ""}
          </span>
          <strong>{row.score}</strong>
        </li>
      ))}
    </ol>
  );
}

export function Lobby({ room, youId, onStart, onLeave }) {
  const [leaving, setLeaving] = useState(false);
  const hasScore = room.players.some((player) => player.score !== 0);
  return (
    <div className="stack">
      <section className="panel">
        <h2>車上主遊戲</h2>
        <p className="hint">聲音只從本輪迪爵的手機出來。其他人看現在這首、評分。駕駛或睡著，誰都可以這輪跳過。</p>
      </section>
      <button className="game-choice dj" type="button" onClick={() => onStart("dj")}>
        <span className="kicker">乘客玩</span>
        <strong>如果我是迪爵</strong>
        <span>自行選歌播一首。貼歌單讓演算法接著播 20 分鐘。</span>
      </button>
      <h2 className="minor-title">也可以玩</h2>
      <button className="game-choice minor prompt" type="button" onClick={() => onStart("prompt")}>
        <span className="kicker">一起說</span>
        <strong>開話題</strong>
        <span>抽一張旅行提問，輪流回答。說完的人 +1。</span>
      </button>
      <button className="game-choice minor vibe" type="button" onClick={() => onStart("vibe")}>
        <span className="kicker">輕輕一下</span>
        <strong>帶動氣氛</strong>
        <span>一個做得來的小挑戰。不想做可以讓給下一個人。完成 +2。</span>
      </button>
      <button className="game-choice minor score" type="button" onClick={() => onStart("score")}>
        <span className="kicker">好玩的分數</span>
        <strong>積分獎懲</strong>
        <span>投票、小獎勵、請一杯想像中的飲料。這款本身不會把分數扣到負的。</span>
      </button>
      <section className="panel">
        <h2>旅途積分</h2>
        {hasScore ? (
          <Ranking ranking={room.ranking} youId={youId} />
        ) : (
          <p className="hint">分數會在遊戲裡慢慢長出來。現在大家都是 0。</p>
        )}
      </section>
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

function SideActions({ act, skipLabel }) {
  const [confirm, setConfirm] = useState(null);
  if (confirm === "skip") {
    return (
      <div className="confirm">
        <p>這張先收起來，換下一張？還沒完成的人不會拿到這張的分數。</p>
        <div className="row">
          <button className="secondary" type="button" onClick={() => setConfirm(null)}>
            留下
          </button>
          <button className="primary" type="button" onClick={() => act({ name: "draw", force: true })}>
            換一張
          </button>
        </div>
      </div>
    );
  }
  if (confirm === "home") {
    return (
      <div className="confirm">
        <p>先回到房間？分數會留著。</p>
        <div className="row">
          <button className="secondary" type="button" onClick={() => setConfirm(null)}>
            留下
          </button>
          <button className="primary" type="button" onClick={() => act({ name: "lobby" })}>
            回房間
          </button>
        </div>
      </div>
    );
  }
  return (
    <div className="row">
      <button className="texty" type="button" onClick={() => setConfirm("skip")}>
        {skipLabel}
      </button>
      <button className="texty" type="button" onClick={() => setConfirm("home")}>
        回房間
      </button>
    </div>
  );
}

export function PromptGame({ room, youId, act }) {
  const game = room.game;
  const [note, setNote] = useState("");
  useEffect(() => {
    setNote("");
  }, [game.round]);
  const currentId = game.turnOrder[game.turnIndex];
  const mine = currentId === youId;

  return (
    <div className="stack">
      <article className="play-card prompt" key={game.card?.id || game.round}>
        <div className="card-meta">
          <span className="stamp">開話題</span>
          <span>第 {game.round} 張</span>
        </div>
        <p className="prompt-text">{game.card?.text}</p>
      </article>

      {game.done ? (
        <section className="panel stack">
          <h2>這一輪先記在這裡</h2>
          <ul className="notes">
            {game.turnOrder.map((id) => (
              <li key={id}>
                <strong>{nameOf(room, id)}</strong>
                <span>{game.notes[id] ? game.notes[id] : "說完了，沒有留下文字。"}</span>
              </li>
            ))}
          </ul>
          <div className="row">
            <button className="primary" type="button" onClick={() => act({ name: "draw" })}>
              再抽一張
            </button>
            <button className="secondary" type="button" onClick={() => act({ name: "lobby" })}>
              回房間
            </button>
          </div>
        </section>
      ) : (
        <>
          <section className="panel stack">
            <p className="turn-line" aria-live="polite">
              {mine ? "輪到你了" : `等 ${nameOf(room, currentId)} 說`}
            </p>
            <p className="hint">說完 +1。想留一句話給大家，也可以不留。</p>
            {mine && (
              <form
                className="stack"
                onSubmit={(event) => {
                  event.preventDefault();
                  act({ name: "speak", note });
                }}
              >
                <label>
                  留給旅途的一句（可不填）
                  <input
                    value={note}
                    maxLength={40}
                    placeholder="最多 40 字"
                    onChange={(event) => setNote(event.target.value)}
                  />
                </label>
                <button className="primary" type="submit">
                  我說完了
                </button>
              </form>
            )}
            <ul className="turns">
              {game.turnOrder.map((id, index) => (
                <li
                  key={id}
                  className={index === game.turnIndex ? "now" : game.spoken[id] ? "did" : ""}
                >
                  <span>
                    {nameOf(room, id)}
                    {game.spoken[id] ? " · 說完了" : index === game.turnIndex ? " · 現在" : ""}
                  </span>
                  {game.notes[id] ? <span className="said">{game.notes[id]}</span> : null}
                </li>
              ))}
            </ul>
          </section>
          <SideActions act={act} skipLabel="換一張" />
        </>
      )}
    </div>
  );
}

export function VibeGame({ room, youId, act }) {
  const game = room.game;
  const mine = game.assigneeId === youId;
  const clapped = game.reactions.includes(youId);
  return (
    <div className="stack">
      <article className="play-card vibe" key={game.card.id}>
        <div className="card-meta">
          <span className="stamp">{game.card.tag || "帶動氣氛"}</span>
          <span>第 {game.round} 張</span>
        </div>
        <p className="prompt-text">{game.card.text}</p>
      </article>
      <section className="panel stack">
        <p className="turn-line" aria-live="polite">
          {game.done ? "做到了" : mine ? "這張交給你" : `交給 ${nameOf(room, game.assigneeId)}`}
        </p>
        <p className="hint">完成 +2。不想做就讓給下一個人，沒有人會被勉強。</p>
        <p className="hint">掌聲 {game.reactions.length}</p>
        {game.done ? (
          <div className="row">
            <button className="primary" type="button" onClick={() => act({ name: "draw" })}>
              再抽一張
            </button>
            <button className="secondary" type="button" onClick={() => act({ name: "lobby" })}>
              回房間
            </button>
          </div>
        ) : (
          <>
            {mine ? (
              <button className="primary" type="button" onClick={() => act({ name: "complete" })}>
                我做到了
              </button>
            ) : (
              <button className="secondary" type="button" onClick={() => act({ name: "react" })} disabled={clapped}>
                {clapped ? "已鼓掌" : "鼓掌"}
              </button>
            )}
            <button className="texty" type="button" onClick={() => act({ name: "pass" })}>
              讓給下一個人
            </button>
          </>
        )}
      </section>
      {!game.done && <SideActions act={act} skipLabel="換一張" />}
    </div>
  );
}

function Tally({ tally }) {
  const max = Math.max(...tally.map((item) => item.count), 1);
  return (
    <ul className="tally">
      {tally.map((item) => (
        <li key={item.id}>
          <span>{item.label}</span>
          <span className="track" aria-hidden="true">
            <span style={{ width: `${(item.count / max) * 100}%` }} />
          </span>
          <strong>{item.count}</strong>
        </li>
      ))}
    </ul>
  );
}

function ScorePlay({ room, youId, game, act }) {
  const card = game.card;
  const online = room.players.filter((player) => player.connected);
  const myChoice = typeof game.votes[youId] === "string" ? game.votes[youId] : null;
  const voted = online.filter((player) => game.votes[player.id] != null).length;

  if (card.type === "vote" || card.type === "choice") {
    const voteTargets =
      card.type === "vote" ? online.filter((player) => online.length === 1 || player.id !== youId) : [];
    return (
      <section className="panel stack">
        <p className="hint">
          {card.type === "vote"
            ? "投給一位旅伴。全員投完會自己揭曉，也可以提早揭曉。"
            : "選一個最接近你的。沒有標準答案。"}
        </p>
        <p className="hint">
          {voted}/{online.length} 已投
        </p>
        {card.type === "vote"
          ? voteTargets.map((player) => (
              <button
                key={player.id}
                type="button"
                className={myChoice === player.id ? "choice on" : "choice"}
                aria-pressed={myChoice === player.id}
                onClick={() => act({ name: "vote", choice: player.id })}
              >
                {player.nickname}
                {myChoice === player.id ? " · 已選" : ""}
              </button>
            ))
          : card.options.map((option) => (
              <button
                key={option.id}
                type="button"
                className={myChoice === option.id ? "choice on" : "choice"}
                aria-pressed={myChoice === option.id}
                onClick={() => act({ name: "vote", choice: option.id })}
              >
                {option.label}
                {myChoice === option.id ? " · 已選" : ""}
              </button>
            ))}
        {voted > 0 && voted < online.length && (
          <button className="secondary" type="button" onClick={() => act({ name: "settle" })}>
            現在揭曉
          </button>
        )}
      </section>
    );
  }

  const mine = game.assigneeId === youId;
  return (
    <section className="panel stack">
      <p className="turn-line" aria-live="polite">
        {mine ? "這張是你的" : `交給 ${nameOf(room, game.assigneeId)}`}
      </p>
      <p className="hint">
        {card.type === "honor" ? "收下就 +3。" : "這是輕輕的小懲：請大家一下，分數不會變成負的。"}
      </p>
      {mine && (
        <button className="primary" type="button" onClick={() => act({ name: "confirm" })}>
          {card.type === "honor" ? "收下獎勵" : "完成這張"}
        </button>
      )}
      <button className="texty" type="button" onClick={() => act({ name: "pass" })}>
        讓給下一個人
      </button>
    </section>
  );
}

export function ScoreGame({ room, youId, act }) {
  const game = room.game;
  const result = game.result;
  return (
    <div className="stack">
      {!result && (
        <section className="panel">
          <h2>積分榜</h2>
          <Ranking ranking={room.ranking} youId={youId} />
        </section>
      )}
      <article className="play-card score" key={`${game.card.id}-${game.round}`}>
        <div className="card-meta">
          <span className="stamp">{game.card.kicker || "積分獎懲"}</span>
          <span>第 {game.round} 輪</span>
        </div>
        <p className="prompt-text">{game.card.text}</p>
      </article>
      {result ? (
        <section className="panel stack">
          <h2>這一輪排名</h2>
          <p className="turn-line">{result.summary}</p>
          <ul className="awards">
            {result.awards.map((award) => (
              <li key={`${award.playerId}-${award.label}`}>
                <span>{nameOf(room, award.playerId)}</span>
                <strong className={award.delta > 0 ? "up" : award.delta < 0 ? "down" : ""}>
                  {formatDelta(award.delta)}
                </strong>
                <span className="award-label">{award.label}</span>
              </li>
            ))}
          </ul>
          {result.tally?.length > 0 && <Tally tally={result.tally} />}
          <Ranking ranking={result.ranking} youId={youId} />
          <div className="row">
            <button className="primary" type="button" onClick={() => act({ name: "draw" })}>
              下一題
            </button>
            <button className="secondary" type="button" onClick={() => act({ name: "lobby" })}>
              結束這一輪
            </button>
          </div>
        </section>
      ) : (
        <>
          <ScorePlay room={room} youId={youId} game={game} act={act} />
          <SideActions act={act} skipLabel="換一題" />
        </>
      )}
    </div>
  );
}
