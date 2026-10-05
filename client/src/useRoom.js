import { useCallback, useEffect, useRef, useState } from "react";

const STORAGE_KEY = "on-the-trip-session";

export function loadSession() {
  try {
    return JSON.parse(sessionStorage.getItem(STORAGE_KEY) || "null");
  } catch {
    return null;
  }
}

export function roomCodeFromLocation() {
  const raw = new URLSearchParams(window.location.search).get("room") || "";
  return raw.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 4);
}

function clearStoredSession() {
  sessionStorage.removeItem(STORAGE_KEY);
}

export function useRoom() {
  const [room, setRoom] = useState(null);
  const [youId, setYouId] = useState(null);
  const [error, setError] = useState("");
  const [status, setStatus] = useState("idle");
  const wsRef = useRef(null);
  const sessionRef = useRef(loadSession());
  const wantRef = useRef(null);
  const stoppedRef = useRef(false);
  const reconnectTimer = useRef(null);
  const connectRef = useRef(() => {});

  connectRef.current = () => {
    if (stoppedRef.current) return;
    const existing = wsRef.current;
    if (
      existing &&
      (existing.readyState === WebSocket.OPEN || existing.readyState === WebSocket.CONNECTING)
    ) {
      return;
    }
    setStatus("connecting");
    const proto = window.location.protocol === "https:" ? "wss" : "ws";
    const ws = new WebSocket(`${proto}://${window.location.host}/ws`);
    wsRef.current = ws;

    ws.addEventListener("open", () => {
      if (wsRef.current !== ws) return;
      setStatus("open");
      const pending = wantRef.current;
      wantRef.current = null;
      if (pending) ws.send(JSON.stringify(pending));
    });

    ws.addEventListener("message", (event) => {
      let msg;
      try {
        msg = JSON.parse(event.data);
      } catch {
        setError("收到讀不懂的訊息。");
        return;
      }
      if (msg.type === "state") {
        const me = msg.room.players.find((player) => player.id === msg.youId);
        const session = {
          playerId: msg.youId,
          nickname: me?.nickname || sessionRef.current?.nickname || "",
          code: msg.room.code,
        };
        sessionRef.current = session;
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
        const url = new URL(window.location.href);
        if (url.searchParams.get("room") !== msg.room.code) {
          url.searchParams.set("room", msg.room.code);
          window.history.replaceState(null, "", `${url.pathname}${url.search}`);
        }
        setRoom(msg.room);
        setYouId(msg.youId);
        setError("");
        return;
      }
      if (msg.type === "left") {
        stoppedRef.current = true;
        sessionRef.current = null;
        clearStoredSession();
        setRoom(null);
        setYouId(null);
        const url = new URL(window.location.href);
        url.searchParams.delete("room");
        window.history.replaceState(null, "", `${url.pathname}${url.search}`);
        return;
      }
      if (msg.type === "error") {
        const message = msg.message || "出了一點狀況。";
        setError(message);
        if (message.includes("找不到這個房間")) {
          stoppedRef.current = true;
          sessionRef.current = null;
          clearStoredSession();
          setRoom(null);
          setYouId(null);
        }
      }
    });

    ws.addEventListener("close", () => {
      if (wsRef.current === ws) {
        wsRef.current = null;
        setStatus("idle");
      }
      if (stoppedRef.current) return;
      const session = sessionRef.current;
      if (!session?.code || !session?.playerId) return;
      wantRef.current = {
        type: "join",
        code: session.code,
        playerId: session.playerId,
        nickname: session.nickname,
      };
      clearTimeout(reconnectTimer.current);
      reconnectTimer.current = setTimeout(() => connectRef.current(), 900);
    });
  };

  const send = useCallback((msg) => {
    stoppedRef.current = false;
    const ws = wsRef.current;
    if (ws && ws.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify(msg));
      return;
    }
    wantRef.current = msg;
    if (ws && ws.readyState === WebSocket.CONNECTING) return;
    connectRef.current();
  }, []);

  useEffect(() => {
    const session = sessionRef.current;
    const query = roomCodeFromLocation();
    if (session?.playerId && session.code && (!query || query === session.code)) {
      wantRef.current = {
        type: "join",
        code: session.code,
        playerId: session.playerId,
        nickname: session.nickname,
      };
      connectRef.current();
    }
    return () => {
      stoppedRef.current = true;
      clearTimeout(reconnectTimer.current);
      wsRef.current?.close();
    };
  }, []);

  return {
    room,
    youId,
    error,
    setError,
    status,
    create(nickname) {
      setError("");
      send({ type: "create", nickname });
    },
    join(nickname, code) {
      setError("");
      send({ type: "join", nickname, code });
    },
    leave() {
      stoppedRef.current = true;
      clearTimeout(reconnectTimer.current);
      const ws = wsRef.current;
      if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: "leave" }));
      } else {
        sessionRef.current = null;
        clearStoredSession();
        setRoom(null);
        setYouId(null);
      }
    },
    act(action) {
      send({ type: "action", ...action });
    },
  };
}
