import { useEffect, useRef, useState } from "react";
import { Ranking, nameOf } from "./play.jsx";
import { expectedPlayhead, planFollow } from "./watch-sync.js";

const RATINGS = [-2, -1, 0, 1, 2];

let youtubeApiPromise;

function loadYoutubeApi() {
  if (window.YT?.Player) return Promise.resolve(window.YT);
  if (youtubeApiPromise) return youtubeApiPromise;
  youtubeApiPromise = new Promise((resolve) => {
    const previous = window.onYouTubeIframeAPIReady;
    window.onYouTubeIframeAPIReady = () => {
      if (typeof previous === "function") previous();
      resolve(window.YT);
    };
    const script = document.createElement("script");
    script.src = "https://www.youtube.com/iframe_api";
    script.async = true;
    document.head.appendChild(script);
  });
  return youtubeApiPromise;
}

function formatClock(ms) {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  return `${minutes}:${String(seconds).padStart(2, "0")}`;
}

function SkipButton({ act, primary = false }) {
  return (
    <button className={primary ? "primary xl" : "skip-round"} type="button" onClick={() => act({ name: "djSkip" })}>
      這輪跳過
    </button>
  );
}

function NowPlaying({ song }) {
  const title = song?.title || "正在讀取歌名";
  return (
    <div className="song-hero">
      <p className="kicker">現在這首</p>
      <p className="song-title">{title}</p>
      {song?.artist ? <p className="song-artist">{song.artist}</p> : null}
    </div>
  );
}

function Countdown({ endsAt }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 250);
    return () => clearInterval(timer);
  }, []);
  const left = Math.max(0, (endsAt || 0) - now);
  return (
    <p className="countdown" aria-label={`剩下 ${formatClock(left)}`}>
      {formatClock(left)}
    </p>
  );
}

function createYoutubeFrame(videoId, { mix = false } = {}) {
  const params = new URLSearchParams({
    enablejsapi: "1",
    origin: window.location.origin,
    playsinline: "1",
    rel: "0",
    autoplay: "1",
  });
  if (mix) params.set("list", `RD${videoId}`);
  const iframe = document.createElement("iframe");
  iframe.src = `https://www.youtube.com/embed/${videoId}?${params.toString()}`;
  iframe.title = "YouTube";
  iframe.allow = "autoplay; encrypted-media; picture-in-picture; fullscreen";
  iframe.referrerPolicy = "strict-origin-when-cross-origin";
  iframe.setAttribute("allowfullscreen", "");
  return iframe;
}

function readPlayerState(target) {
  try {
    return target.getPlayerState?.();
  } catch {
    return -1;
  }
}

function readPlayerVideoId(target) {
  try {
    return target.getVideoData?.()?.video_id || "";
  } catch {
    return "";
  }
}

function readPlayerTime(target) {
  try {
    const time = target.getCurrentTime?.();
    return Number.isFinite(time) ? time : 0;
  } catch {
    return 0;
  }
}

function playerIsAudible(target) {
  try {
    if (target.isMuted?.()) return false;
    const volume = target.getVolume?.();
    if (Number.isFinite(volume) && volume === 0) return false;
  } catch {
    return true;
  }
  return true;
}

function unmute(target) {
  try {
    target.unMute?.();
    target.setVolume?.(100);
  } catch {
    /* already gone */
  }
}

function YoutubeDeck({ seedId, mix, onSync, onEnded }) {
  const hostRef = useRef(null);
  const playerRef = useRef(null);
  const onSyncRef = useRef(onSync);
  const onEndedRef = useRef(onEnded);
  const lastKey = useRef("");
  const lastSentAt = useRef(0);
  const lastSentTime = useRef(0);
  const lastPlayState = useRef("");
  const endedSent = useRef(false);
  const [localState, setLocalState] = useState("unstarted");
  const [failed, setFailed] = useState("");
  onSyncRef.current = onSync;
  onEndedRef.current = onEnded;

  useEffect(() => {
    let dead = false;
    let poll;
    const mount = hostRef.current;
    if (!mount) return undefined;
    lastKey.current = "";
    lastSentAt.current = 0;
    lastSentTime.current = 0;
    lastPlayState.current = "";
    endedSent.current = false;
    const iframe = createYoutubeFrame(seedId, { mix });
    mount.replaceChildren(iframe);

    loadYoutubeApi().then((YT) => {
      if (dead) return;
      const player = new YT.Player(iframe, {
        events: {
          onReady: (event) => {
            playerRef.current = event.target;
            try {
              event.target.playVideo();
            } catch {
              /* 需要再按一次播放 */
            }
            push(event.target);
          },
          onStateChange: (event) => {
            push(event.target);
            if (!mix && event.data === YT.PlayerState.ENDED && !endedSent.current) {
              endedSent.current = true;
              onEndedRef.current();
            }
          },
          onError: (event) => {
            const code = String(event?.data ?? "");
            setFailed(code);
            setLocalState("error");
            onSyncRef.current({ videoId: seedId, title: "", artist: "", playState: "error" });
          },
        },
      });
      playerRef.current = player;
      poll = setInterval(() => {
        if (playerRef.current) push(playerRef.current);
      }, 500);
    });
    return () => {
      dead = true;
      clearInterval(poll);
      const current = playerRef.current;
      playerRef.current = null;
      try {
        current?.stopVideo?.();
      } catch {
        /* already gone */
      }
      try {
        current?.destroy?.();
      } catch {
        /* already gone */
      }
    };
  }, [seedId, mix]);

  function push(target) {
    let data = {};
    try {
      data = target.getVideoData?.() || {};
    } catch {
      data = {};
    }
    let playState = "unstarted";
    try {
      const code = target.getPlayerState?.();
      playState =
        {
          [-1]: "unstarted",
          0: "ended",
          1: "playing",
          2: "paused",
          3: "buffering",
          5: "cued",
        }[code] || "unstarted";
    } catch {
      playState = "unstarted";
    }
    setLocalState(playState);
    let currentTime = 0;
    try {
      const time = target.getCurrentTime?.();
      if (Number.isFinite(time)) currentTime = Math.round(time * 10) / 10;
    } catch {
      currentTime = 0;
    }
    const payload = {
      videoId: data.video_id || seedId,
      title: data.title || "",
      artist: data.author || "",
      playState,
      currentTime,
    };
    const stateKey = JSON.stringify({
      videoId: payload.videoId,
      title: payload.title,
      artist: payload.artist,
      playState,
    });
    const now = Date.now();
    const elapsed = lastPlayState.current === "playing" ? (now - lastSentAt.current) / 1000 : 0;
    const jumped = Math.abs(currentTime - lastSentTime.current - elapsed) > 1;
    const heartbeat = now - lastSentAt.current > 2000;
    if (stateKey === lastKey.current && !jumped && !heartbeat) return;
    lastKey.current = stateKey;
    lastSentAt.current = now;
    lastSentTime.current = currentTime;
    lastPlayState.current = playState;
    onSyncRef.current(payload);
  }

  function toggle() {
    const target = playerRef.current;
    if (!target?.getPlayerState) return;
    const code = target.getPlayerState();
    if (code === window.YT?.PlayerState?.PLAYING || code === window.YT?.PlayerState?.BUFFERING) {
      target.pauseVideo();
    } else {
      target.playVideo();
    }
  }

  const playing = localState === "playing" || localState === "buffering";
  return (
    <div className="stack">
      <div className="yt-frame">
        <div ref={hostRef} />
      </div>
      {failed ? (
        <p className="hint" data-yt-error={failed}>
          {/^\d+\.\d+\.\d+\.\d+$/.test(window.location.hostname)
            ? "YouTube 不接受 IP 網址。請改開 localhost，或用有名字的網址。"
            : "這支影片不能在這裡播。換一個連結，或這輪跳過。"}
        </p>
      ) : null}
      <button className="primary xl" type="button" onClick={toggle}>
        {playing ? "暫停" : "播放"}
      </button>
    </div>
  );
}

function FollowDeck({ videoId, currentTime, playState }) {
  const hostRef = useRef(null);
  const playerRef = useRef(null);
  const unlockedRef = useRef(false);
  const readyRef = useRef(false);
  const lastSeekRef = useRef(0);
  const requestedRef = useRef("");
  const autoplayTriedAt = useRef(0);
  const applyRef = useRef(() => {});
  const initialIdRef = useRef(videoId);
  const optedOutRef = useRef(false);
  const [optedOut, setOptedOut] = useState(false);
  const [needsGesture, setNeedsGesture] = useState(false);
  const [failed, setFailed] = useState("");
  const clockRef = useRef({
    videoId,
    currentTime: Number(currentTime) || 0,
    playState,
    receivedAt: Date.now(),
  });
  const sampleKey = `${videoId}|${playState}|${currentTime}`;
  const sampleRef = useRef("");
  if (sampleRef.current !== sampleKey) {
    sampleRef.current = sampleKey;
    clockRef.current = {
      videoId,
      currentTime: Number(currentTime) || 0,
      playState,
      receivedAt: Date.now(),
    };
  }

  function applyPlan() {
    const player = playerRef.current;
    if (!player || !readyRef.current) return;
    if (optedOutRef.current) {
      setNeedsGesture(false);
      try {
        player.mute?.();
        const state = readPlayerState(player);
        if (state === 1 || state === 3) player.pauseVideo();
      } catch {
        /* player is going away */
      }
      return;
    }
    const plan = planFollow({
      localVideoId: readPlayerVideoId(player),
      localTime: readPlayerTime(player),
      localState: readPlayerState(player),
      clock: clockRef.current,
      now: Date.now(),
      unlocked: unlockedRef.current,
      lastSeekAt: lastSeekRef.current,
      autoplayTriedAt: autoplayTriedAt.current,
    });
    const audible = playerIsAudible(player);
    if (plan.markUnlocked && audible) unlockedRef.current = true;
    const gesture = unlockedRef.current ? false : plan.needsGesture || (readPlayerState(player) === 1 && !audible);
    setNeedsGesture(gesture);
    if (unlockedRef.current) unmute(player);
    try {
      if (plan.type === "load" || plan.type === "cue") {
        const request = `${plan.type}:${plan.videoId}`;
        if (requestedRef.current === request && Date.now() - lastSeekRef.current < 2000) return;
        requestedRef.current = request;
        lastSeekRef.current = Date.now();
        if (plan.type === "load") player.loadVideoById(plan.videoId, plan.seconds);
        else player.cueVideoById(plan.videoId, plan.seconds);
        return;
      }
      if (plan.seek) {
        player.seekTo(plan.seconds, true);
        lastSeekRef.current = Date.now();
      }
      if (plan.type === "pause") {
        const state = readPlayerState(player);
        if (state === 1 || state === 3) player.pauseVideo();
      } else if (plan.play) {
        player.playVideo();
      }
    } catch {
      /* player is going away */
    }
  }
  applyRef.current = applyPlan;

  useEffect(() => {
    let dead = false;
    let poll;
    const mount = hostRef.current;
    const openingId = initialIdRef.current;
    if (!mount || !openingId) return undefined;
    const iframe = createYoutubeFrame(openingId);
    mount.replaceChildren(iframe);
    loadYoutubeApi().then((YT) => {
      if (dead) return;
      const player = new YT.Player(iframe, {
        events: {
          onReady: (event) => {
            playerRef.current = event.target;
            readyRef.current = true;
            if (optedOutRef.current) {
              try {
                event.target.mute();
                event.target.pauseVideo();
              } catch {
                /* already stopped */
              }
              return;
            }
            autoplayTriedAt.current = Date.now();
            try {
              event.target.playVideo();
            } catch {
              /* 需要按開始一起看 */
            }
            applyRef.current();
          },
          onStateChange: () => applyRef.current(),
          onError: (event) => setFailed(String(event?.data ?? "")),
        },
      });
      playerRef.current = player;
      poll = setInterval(() => applyRef.current(), 1000);
    });
    return () => {
      dead = true;
      clearInterval(poll);
      readyRef.current = false;
      const current = playerRef.current;
      playerRef.current = null;
      try {
        current?.destroy?.();
      } catch {
        /* already gone */
      }
    };
  }, []);

  useEffect(() => {
    applyRef.current();
  }, [sampleKey]);

  function leaveSync() {
    optedOutRef.current = true;
    setOptedOut(true);
    setNeedsGesture(false);
    const player = playerRef.current;
    if (!player) return;
    try {
      player.mute?.();
      player.pauseVideo?.();
    } catch {
      /* player is going away */
    }
  }

  function rejoin() {
    optedOutRef.current = false;
    setOptedOut(false);
    startTogether();
  }

  function startTogether() {
    const player = playerRef.current;
    unlockedRef.current = true;
    setNeedsGesture(false);
    if (!player) return;
    unmute(player);
    const clock = clockRef.current;
    const seconds = expectedPlayhead(clock, Date.now());
    try {
      const reported = readPlayerVideoId(player);
      if (reported && reported !== clock.videoId) {
        requestedRef.current = `load:${clock.videoId}`;
        lastSeekRef.current = Date.now();
        player.loadVideoById(clock.videoId, seconds);
      } else {
        if (Math.abs(readPlayerTime(player) - seconds) > 0.4) {
          player.seekTo(seconds, true);
          lastSeekRef.current = Date.now();
        }
        player.playVideo();
      }
    } catch {
      /* player is going away */
    }
  }

  return (
    <div className="stack">
      <div className={optedOut ? "yt-frame yt-away" : "yt-frame"}>
        <div ref={hostRef} />
      </div>
      {optedOut ? (
        <>
          <p className="hint">這支手機已退出同步。其他人繼續播。</p>
          <button className="secondary" type="button" onClick={rejoin}>
            再一起看
          </button>
        </>
      ) : (
        <>
          {failed ? (
            <p className="hint" data-yt-error={failed}>
              {/^\d+\.\d+\.\d+\.\d+$/.test(window.location.hostname)
                ? "YouTube 不接受 IP 網址。請改開 localhost，或用有名字的網址。"
                : "這支影片不能在這裡播。換一個連結，或這輪跳過。"}
            </p>
          ) : null}
          {needsGesture ? (
            <button className="primary xl together" type="button" onClick={startTogether}>
              開始一起看
            </button>
          ) : null}
          <button className="secondary" type="button" onClick={leaveSync}>
            退出同步收聽
          </button>
        </>
      )}
    </div>
  );
}

function RateControls({ room, youId, act }) {
  const game = room.game;
  const mineValue = game.ratings[youId];
  const locked = mineValue != null;
  return (
    <>
      <p className="rule-line">幫別人評分，自己 +1</p>
      <div className="rate-row" role="group" aria-label="給這首打分">
        {RATINGS.map((value) => (
          <button
            key={value}
            type="button"
            className={mineValue === value ? "choice on" : "choice"}
            aria-pressed={mineValue === value}
            disabled={locked}
            onClick={() => act({ name: "djRate", value })}
          >
            {value > 0 ? `+${value}` : value}
          </button>
        ))}
      </div>
      <button
        className={mineValue === "none" ? "primary xl" : "secondary xl"}
        type="button"
        disabled={locked && mineValue !== "none"}
        onClick={() => act({ name: "djRate", value: "none" })}
      >
        不評
      </button>
      {locked ? (
        <p className="hint">{mineValue === "none" ? "這首不評。" : "評過了。換歌可以再評。"}</p>
      ) : (
        <p className="hint">0 也算評過。不評不加不減。</p>
      )}
    </>
  );
}

export function DjGame({ room, youId, act }) {
  const game = room.game;
  const mine = game.djId === youId;
  const djName = nameOf(room, game.djId);
  const [url, setUrl] = useState("");
  const actRef = useRef(act);
  actRef.current = act;

  useEffect(() => {
    setUrl("");
  }, [game.round, game.step]);

  useEffect(() => {
    if (game.step === "enter" || game.step === "live") loadYoutubeApi();
  }, [game.step]);

  useEffect(() => {
    if (!mine || game.mode !== "playlist" || game.step !== "live" || !game.endsAt) return;
    const left = game.endsAt - Date.now();
    const timer = setTimeout(() => actRef.current({ name: "djTimeUp" }), Math.max(0, left) + 250);
    return () => clearTimeout(timer);
  }, [mine, game.mode, game.step, game.endsAt, game.round]);

  return (
    <div className="stack dj-game">
      <p className="dj-kicker">如果我是迪爵 · 第 {game.round} 輪</p>
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
                act({ name: "djCue", url });
              }}
            >
              <label>
                {game.mode === "playlist" ? "第一首的連結" : "YouTube 連結"}
                <input
                  className="song-input"
                  type="url"
                  inputMode="url"
                  value={url}
                  onChange={(event) => setUrl(event.target.value)}
                  maxLength={500}
                  placeholder="https://music.youtube.com/watch?v="
                  required
                  enterKeyHint="done"
                />
              </label>
              <p className="hint">
                {game.mode === "playlist"
                  ? "貼你真的會開的那一首。只有這支手機會接著播推薦，大家一起看同一支，20 分鐘後停。"
                  : "只播這一支。大家一起看這支影片。"}
              </p>
              <button className="primary xl" type="submit">
                {game.mode === "playlist" ? "開始，演算法播 20 分鐘" : "這首開始播"}
              </button>
            </form>
          ) : (
            <p className="hint">{djName} 正在貼連結。等一下大家一起看這支影片。</p>
          )}
          {mine && (
            <button className="texty" type="button" onClick={() => act({ name: "djBack" })}>
              重選模式
            </button>
          )}
          <SkipButton act={act} />
        </section>
      )}

      {game.step === "live" && (
        <Live room={room} youId={youId} act={act} mine={mine} />
      )}

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

function Live({ room, youId, act, mine }) {
  const game = room.game;
  const playlist = game.mode === "playlist";
  return (
    <section className="stack">
      {playlist && game.endsAt ? <Countdown endsAt={game.endsAt} /> : null}
      <NowPlaying song={game.song} />
      <p className="turn-line">大家一起看這支影片</p>
      {mine ? (
        <YoutubeDeck
          seedId={game.seedId}
          mix={playlist}
          onSync={(payload) => act({ name: "djSync", ...payload })}
          onEnded={() => act({ name: "djEnded" })}
        />
      ) : (
        <FollowDeck
          videoId={game.videoId}
          currentTime={game.currentTime ?? 0}
          playState={game.playState || "unstarted"}
        />
      )}
      {mine ? (
        <p className="turn-line">等大家用自己的口味評這首</p>
      ) : (
        <RateControls room={room} youId={youId} act={act} />
      )}
      {mine ? <p className="rule-line">幫別人評分，自己 +1</p> : null}
      {mine && (
        <button className="secondary xl" type="button" onClick={() => act({ name: "djFinish" })}>
          {playlist ? "提前結束" : "本輪結束"}
        </button>
      )}
      <SkipButton act={act} />
    </section>
  );
}
