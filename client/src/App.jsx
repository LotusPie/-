import { useEffect, useRef, useState } from "react";
import { loadSession, roomCodeFromLocation, useRoom } from "./useRoom.js";
import { DjGame } from "./dj.jsx";
import { CardDesk } from "./cards.jsx";
import { Home, Lobby, PageSwitch, Roster, ScorePage } from "./play.jsx";

async function writeText(text) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    const area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "");
    area.style.position = "fixed";
    area.style.left = "-9999px";
    document.body.appendChild(area);
    area.select();
    const ok = document.execCommand("copy");
    area.remove();
    return ok;
  }
}

const PAGE_KEY = "on-the-trip-page";

function readLocalPage() {
  try {
    const value = sessionStorage.getItem(PAGE_KEY);
    if (value === "play" || value === "cards" || value === "rank") return value;
  } catch {
    // this browser is not keeping local page state
  }
  return "play";
}

function roomLink(code) {
  const url = new URL(window.location.href);
  url.searchParams.set("room", code);
  url.hash = "";
  return url.toString();
}

export default function App() {
  const { room, youId, error, setError, status, create, join, leave, act } = useRoom();
  const [nickname, setNickname] = useState(() => loadSession()?.nickname || "");
  const [linkCode] = useState(() => roomCodeFromLocation());
  const [code, setCode] = useState(linkCode);
  const [toast, setToast] = useState("");
  const [page, setPage] = useState(readLocalPage);
  const [bumped, setBumped] = useState({});
  const prevScores = useRef({});
  const invited = !room && linkCode && code === linkCode ? linkCode : "";

  useEffect(() => {
    document.title = room ? `房間 ${room.code} · 旅途小遊戲` : "旅途小遊戲";
  }, [room]);

  useEffect(() => {
    try {
      sessionStorage.setItem(PAGE_KEY, page);
    } catch {
      // the choice still stays in this page until reload
    }
  }, [page]);

  function choosePage(next) {
    if (next !== "play" && next !== "cards" && next !== "rank") return;
    setPage(next);
  }

  useEffect(() => {
    if (!room) return undefined;
    const next = {};
    let changed = false;
    for (const player of room.players) {
      const previous = prevScores.current[player.id];
      if (previous != null && previous !== player.score) {
        next[player.id] = true;
        changed = true;
      }
      prevScores.current[player.id] = player.score;
    }
    if (!changed) return undefined;
    setBumped(next);
    const timer = setTimeout(() => setBumped({}), 700);
    return () => clearTimeout(timer);
  }, [room]);

  useEffect(() => {
    if (!toast) return undefined;
    const timer = setTimeout(() => setToast(""), 2200);
    return () => clearTimeout(timer);
  }, [toast]);

  async function copyCode() {
    const ok = await writeText(room.code);
    setToast(ok ? "代碼複製好了" : "複製沒成功，請自己選取代碼");
  }

  async function shareLink() {
    const url = roomLink(room.code);
    const text = `來一起玩旅途小遊戲，房間代碼 ${room.code}`;
    if (navigator.share) {
      try {
        await navigator.share({ title: "旅途小遊戲", text, url });
        return;
      } catch (event) {
        if (event?.name === "AbortError") return;
      }
    }
    const ok = await writeText(url);
    setToast(ok ? "連結複製好了" : "複製沒成功，請自己選取網址");
  }

  const turnId = room?.game?.kind === "dj" ? room.game.djId : null;
  const returning = !room && status === "connecting" && Boolean(loadSession()?.playerId);
  const pending = status === "connecting";
  const onPlay = page === "play";
  const appClass = ["app", room ? "in-room" : "", page === "rank" ? "show-rank" : "", page === "cards" ? "show-cards" : ""]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={appClass}>
      <header className="topbar">
        <p className="brand-en">on the trip</p>
        <div className="title-row">
          <h1>旅途小遊戲</h1>
          {room && (
            <p className="code" aria-label={`房間代碼 ${room.code}`}>
              {room.code}
            </p>
          )}
        </div>
      </header>

      {room && <Roster room={room} youId={youId} turnId={turnId} bumped={bumped} />}
      {room && status !== "open" && <p className="banner">正在重新連上房間…</p>}
      {room && error && (
        <p className="banner" role="alert">
          {error}
          <button className="texty" type="button" onClick={() => setError("")}>
            知道了
          </button>
        </p>
      )}

      <div
        className={onPlay ? "play-stage" : "play-stage is-parked"}
        aria-hidden={onPlay ? undefined : true}
        inert={onPlay ? undefined : "true"}
      >
        {!room && returning && <p className="panel">正在回到房間…</p>}
        {!room && !returning && (
          <Home
            nickname={nickname}
            setNickname={setNickname}
            code={code}
            setCode={setCode}
            onCreate={create}
            onJoin={join}
            error={error}
            pending={pending}
            invited={invited}
          />
        )}
        {room?.phase === "lobby" && (
          <Lobby onStart={(game) => act({ name: "start", game })} onLeave={leave} />
        )}
        {room?.game?.kind === "dj" && <DjGame room={room} youId={youId} act={act} />}
      </div>
      {page === "rank" && <ScorePage room={room} youId={youId} />}
      {page === "cards" &&
        (room ? (
          <CardDesk room={room} youId={youId} act={act} />
        ) : (
          <section className="panel card-page stack">
            <h2>我的卡牌</h2>
            <p className="hint">進房間之後，這裡會顯示目前的手牌和牌店。</p>
          </section>
        ))}
      <footer>
        <p>私人房間 · 沒有帳號 · 不會公開列出</p>
        {room && (
          <div className="row footer-actions">
            <button className="tiny" type="button" onClick={copyCode}>
              複製代碼
            </button>
            <button className="tiny" type="button" onClick={shareLink}>
              分享連結
            </button>
          </div>
        )}
      </footer>
      <PageSwitch page={page} onChange={choosePage} />
      {toast && <p className="toast">{toast}</p>}
    </div>
  );
}
