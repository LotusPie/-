import { useEffect, useState } from "react";
import { Ranking, nameOf } from "./play.jsx";

function SongFields({ title, setTitle, artist, setArtist }) {
  return (
    <>
      <label>
        歌名
        <input
          className="song-input"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          maxLength={36}
          placeholder="這首叫什麼"
          required
          enterKeyHint="next"
        />
      </label>
      <label>
        歌手（可不填）
        <input
          className="song-input"
          value={artist}
          onChange={(event) => setArtist(event.target.value)}
          maxLength={24}
          placeholder="誰唱的"
          enterKeyHint="done"
        />
      </label>
    </>
  );
}

function NowPlaying({ song, huge = false }) {
  if (!song) return null;
  return (
    <div className={huge ? "song-hero" : "song-aside"}>
      {huge ? null : <p className="kicker">正在播</p>}
      <p className={huge ? "song-title" : "song-aside-title"}>{song.title}</p>
      {song.artist ? <p className="song-artist">{song.artist}</p> : null}
    </div>
  );
}

function SkipButton({ act, primary = false }) {
  return (
    <button className={primary ? "primary xl" : "skip-round"} type="button" onClick={() => act({ name: "djSkip" })}>
      這輪跳過
    </button>
  );
}

export function DjGame({ room, youId, act }) {
  const game = room.game;
  const mine = game.djId === youId;
  const djName = nameOf(room, game.djId);
  const [title, setTitle] = useState("");
  const [artist, setArtist] = useState("");

  useEffect(() => {
    setTitle("");
    setArtist("");
  }, [game.round, game.step]);

  return (
    <div className="stack dj-game">
      <p className="dj-kicker">
        如果我是迪爵 · 第 {game.round} 輪
      </p>
      <p className="dj-who">
        本輪迪爵
        <strong>{djName}</strong>
      </p>

      {game.step === "pick" && (
        <section className="stack">
          {mine ? (
            <>
              <button className="primary xl" type="button" onClick={() => act({ name: "djMode", mode: "own" })}>
                自行選歌
              </button>
              <button className="secondary xl" type="button" onClick={() => act({ name: "djMode", mode: "playlist" })}>
                貼歌單
              </button>
            </>
          ) : (
            <p className="turn-line">等 {djName} 選一種播法</p>
          )}
          <SkipButton act={act} primary={!mine} />
        </section>
      )}

      {game.step === "enter" && (
        <section className="stack">
          <p className="turn-line">{game.mode === "playlist" ? "貼歌單" : "自行選歌"}</p>
          {mine ? (
            <form
              className="stack"
              onSubmit={(event) => {
                event.preventDefault();
                act({ name: "djCue", title, artist });
              }}
            >
              <SongFields title={title} setTitle={setTitle} artist={artist} setArtist={setArtist} />
              <button className="primary xl" type="submit">
                這首開始播
              </button>
            </form>
          ) : (
            <p className="hint">{djName} 正在寫歌名。音響先等一下。</p>
          )}
          {mine && (
            <button className="texty" type="button" onClick={() => act({ name: "djBack" })}>
              重選模式
            </button>
          )}
          <SkipButton act={act} />
        </section>
      )}

      {game.step === "collect" && (
        <Collect room={room} youId={youId} act={act} title={title} setTitle={setTitle} artist={artist} setArtist={setArtist} />
      )}

      {game.step === "judge" && <Judge room={room} youId={youId} act={act} />}

      {game.step === "rate" && <Rate room={room} youId={youId} act={act} />}

      <section className="panel">
        <h2>積分榜</h2>
        <Ranking ranking={room.ranking} youId={youId} />
      </section>
      <button className="texty" type="button" onClick={() => act({ name: "lobby" })}>
        回房間
      </button>
    </div>
  );
}

function Collect({ room, youId, act, title, setTitle, artist, setArtist }) {
  const game = room.game;
  const mine = game.djId === youId;
  const submitted = game.submissions.find((item) => item.playerId === youId);
  const passed = game.passedSubmit.includes(youId);
  return (
    <section className="stack">
      <NowPlaying song={game.song} huge />
      {mine ? (
        <>
          <p className="hint">大家會交一首像你歌單的歌。睡著沒交的，收齊時就算跳過。</p>
          <ul className="notes">
            {game.submissions.map((item) => (
              <li key={item.id}>
                <strong>{nameOf(room, item.playerId)}</strong>
                <span>
                  {item.title}
                  {item.artist ? ` · ${item.artist}` : ""}
                </span>
              </li>
            ))}
            {game.passedSubmit.map((id) => (
              <li key={id}>
                <strong>{nameOf(room, id)}</strong>
                <span>跳過不交</span>
              </li>
            ))}
          </ul>
          <button className="primary xl" type="button" onClick={() => act({ name: "djClose" })}>
            收齊歌單
          </button>
        </>
      ) : submitted ? (
        <p className="turn-line">你交了 {submitted.title}</p>
      ) : passed ? (
        <p className="turn-line">你這首不交</p>
      ) : (
        <form
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();
            act({ name: "djSubmit", title, artist });
          }}
        >
          <p className="hint">交一首你覺得會出現在 {nameOf(room, game.djId)} 歌單裡的歌。</p>
          <SongFields title={title} setTitle={setTitle} artist={artist} setArtist={setArtist} />
          <button className="primary xl" type="submit">
            交出去
          </button>
          <button className="secondary xl" type="button" onClick={() => act({ name: "djPassSubmit" })}>
            跳過不交
          </button>
        </form>
      )}
      <SkipButton act={act} />
    </section>
  );
}

function Judge({ room, youId, act }) {
  const game = room.game;
  const mine = game.djId === youId;
  const submission = game.submissions.find((item) => !item.verdict);
  if (!submission) return null;
  return (
    <section className="stack">
      <NowPlaying song={game.song} />
      <p className="song-title">{submission.title}</p>
      <p className="song-artist">
        {nameOf(room, submission.playerId)}
        {submission.artist ? ` · ${submission.artist}` : ""}
      </p>
      {mine ? (
        <>
          <button
            className="primary xl"
            type="button"
            onClick={() => act({ name: "djJudge", submissionId: submission.id, verdict: "like" })}
          >
            像我
          </button>
          <button
            className="secondary xl"
            type="button"
            onClick={() => act({ name: "djJudge", submissionId: submission.id, verdict: "unlike" })}
          >
            不像
          </button>
          <p className="hint">像我，對方 +2。不像，不加分。</p>
        </>
      ) : (
        <p className="turn-line">等 {nameOf(room, game.djId)} 說像不像</p>
      )}
    </section>
  );
}

const RATINGS = [-2, -1, 0, 1, 2];

function Rate({ room, youId, act }) {
  const game = room.game;
  const mine = game.djId === youId;
  const mineValue = game.ratings[youId];
  const others = room.players.filter((player) => player.connected && player.id !== game.djId);
  const rated = others.filter((player) => game.ratings[player.id] != null).length;
  return (
    <section className="stack">
      <NowPlaying song={game.song} huge />
      <p className="hint">
        已評 {rated}/{others.length}。0 也算評過。不評不加不減。
      </p>
      {mine ? (
        <p className="turn-line">等大家用自己的口味評這首</p>
      ) : (
        <>
          <div className="rate-row" role="group" aria-label="給這首打分">
            {RATINGS.map((value) => (
              <button
                key={value}
                type="button"
                className={mineValue === value ? "choice on" : "choice"}
                aria-pressed={mineValue === value}
                onClick={() => act({ name: "djRate", value })}
              >
                {value > 0 ? `+${value}` : value}
              </button>
            ))}
          </div>
          <button
            className={mineValue === "none" ? "primary xl" : "secondary xl"}
            type="button"
            onClick={() => act({ name: "djRate", value: "none" })}
          >
            不評
          </button>
        </>
      )}
      <button className={mine ? "primary xl" : "skip-round"} type="button" onClick={() => act({ name: "djFinish" })}>
        本輪結束
      </button>
    </section>
  );
}
