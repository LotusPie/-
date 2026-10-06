import { useEffect, useRef, useState } from "react";
import { loadSession, roomCodeFromLocation, useRoom } from "./useRoom.js";
import { DjGame } from "./dj.jsx";
import { Home, Lobby, PromptGame, Roster, ScoreGame, VibeGame } from "./play.jsx";

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
  const [bumped, setBumped] = useState({});
  const prevScores = useRef({});
  const invited = !room && linkCode && code === linkCode ? linkCode : "";

  useEffect(() => {
    document.title = room ? `房間 ${room.code} · 旅途小遊戲` : "旅途小遊戲";
  }, [room]);

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

  const turnId =
    room?.game?.kind === "dj"
      ? room.game.djId
      : room?.game?.kind === "prompt" && !room.game.done
        ? room.game.turnOrder[room.game.turnIndex]
        : room?.game?.assigneeId || null;
  const returning = !room && status === "connecting" && Boolean(loadSession()?.playerId);
  const pending = status === "connecting";

  return (
    <div className={room ? "app in-room" : "app"}>
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
        {room && (
          <div className="row">
            <button className="tiny" type="button" onClick={copyCode}>
              複製代碼
            </button>
            <button className="tiny" type="button" onClick={shareLink}>
              分享連結
            </button>
          </div>
        )}
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
        <Lobby
          room={room}
          youId={youId}
          onStart={(game) => act({ name: "start", game })}
          onLeave={leave}
        />
      )}
      {room?.game?.kind === "dj" && <DjGame room={room} youId={youId} act={act} />}
      {room?.game?.kind === "prompt" && <PromptGame room={room} youId={youId} act={act} />}
      {room?.game?.kind === "vibe" && <VibeGame room={room} youId={youId} act={act} />}
      {room?.game?.kind === "score" && <ScoreGame room={room} youId={youId} act={act} />}

      <footer>私人房間 · 沒有帳號 · 不會公開列出</footer>
      {toast && <p className="toast">{toast}</p>}
    </div>
  );
}
