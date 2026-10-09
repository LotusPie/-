import { useEffect, useRef, useState } from "react";
import { nameOf } from "./play.jsx";
import { expectedPlayhead, planFollow } from "./watch-sync.js";

const RATINGS = [-2, -1, 1, 2];

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

const WHEEL_MS = 2500;

const SONG_SLICES = [
  { songs: 1, label: "一首歌", mid: 60 },
  { songs: 2, label: "二首歌", mid: 180 },
  { songs: 3, label: "三首歌", mid: 300 },
];

function songLabel(count) {
  return SONG_SLICES.find((slice) => slice.songs === count)?.label || "";
}

function ModeWheel({ wheelSongs, wheelSpin }) {
  const turns = useRef(0);
  const [angle, setAngle] = useState(0);
  useEffect(() => {
    if (!wheelSpin) {
      turns.current = 0;
      setAngle(0);
      return;
    }
    const mid = SONG_SLICES.find((slice) => slice.songs === wheelSongs)?.mid ?? 60;
    turns.current += 5;
    setAngle(turns.current * 360 - mid);
  }, [wheelSpin, wheelSongs]);
  return (
    <div className="wheel-wrap">
      <div className="wheel-pointer" aria-hidden="true" />
      <div className="wheel" style={{ transform: `rotate(${angle}deg)` }}>
        {SONG_SLICES.map((slice) => {
          const rad = (slice.mid * Math.PI) / 180;
          const left = 50 + Math.sin(rad) * 30;
          const top = 50 - Math.cos(rad) * 30;
          return (
            <span
              key={slice.songs}
              className="wheel-label"
              style={{
                left: `${left}%`,
                top: `${top}%`,
                transform: `translate(-50%, -50%) rotate(${-angle}deg)`,
              }}
            >
              {slice.label}
            </span>
          );
        })}
        <div className="wheel-hub" />
      </div>
    </div>
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
            : "這支影片不能在這裡播。換一個連結。"}
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
                : "這支影片不能在這裡播。換一個連結。"}
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
      {locked ? <p className="hint">評過了。換歌可以再評。</p> : <p className="hint">評分是 -2、-1、+1、+2。</p>}
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
    if (!mine || game.step !== "pick" || !game.wheelSpin || !game.wheelSongs) return undefined;
    if ((game.extraSpins || 0) > 0) return undefined;
    const songs = game.wheelSongs;
    const reduce =
      typeof window.matchMedia === "function" &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const timer = setTimeout(() => actRef.current({ name: "djMode", songs }), reduce ? 0 : WHEEL_MS);
    return () => clearTimeout(timer);
  }, [mine, game.step, game.wheelSpin, game.wheelSongs, game.extraSpins]);

  return (
    <div className="dj-layout dj-game">
      <div className="stack dj-play">
      <p className="dj-kicker">如果我是迪爵 · 第 {game.round} 輪</p>
      <p className="dj-who">
        本輪迪爵
        <strong>{djName}</strong>
      </p>

      {game.step === "pick" && (
        <section className="stack">
          <ModeWheel wheelSongs={game.wheelSongs} wheelSpin={game.wheelSpin || 0} />
          <p className="turn-line">{game.wheelSongs ? songLabel(game.wheelSongs) : "一首歌、二首歌、三首歌，機會一樣。"}</p>
          {mine ? (
            <button
              className="primary xl"
              type="button"
              disabled={(game.wheelSpin || 0) > 0 && !(game.extraSpins > 0)}
              onClick={() => {
                const songs = 1 + Math.floor(Math.random() * 3);
                act({ name: "djSpin", songs });
              }}
            >
              {(game.wheelSpin || 0) > 0 && game.extraSpins > 0 ? "再轉一次" : (game.wheelSpin || 0) > 0 ? "轉盤轉著" : "轉一下"}
            </button>
          ) : (
            <p className="turn-line">等 {djName} 轉</p>
          )}
        </section>
      )}

      {game.step === "enter" && (
        <section className="stack">
          <p className="turn-line">{songLabel(game.songs) || (game.mode === "playlist" ? "接著播" : "一首歌")}</p>
          {mine ? (
            <form
              className="stack"
              onSubmit={(event) => {
                event.preventDefault();
                act({ name: "djCue", url });
              }}
            >
              <label>
                YouTube 連結
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
                  ? `貼你要播的第一首。只有這支手機會接著播推薦，大家一起看同一支，播完 ${game.songLimit || game.songs || 3} 首就停。`
                  : game.extra
                    ? "只播這一支。大家一起看這支影片。播完可以再貼一支。"
                    : "只播這一支。大家一起看這支影片。播完這一輪就結束。"}
              </p>
              <button className="primary xl" type="submit">
                {game.mode === "playlist" ? `開始，接著播 ${game.songLimit || game.songs || 3} 首` : game.songCount > 0 ? "再播這一首" : "這首開始播"}
              </button>
            </form>
          ) : (
            <p className="hint">{djName} 正在貼連結。等一下大家一起看這支影片。</p>
          )}
        </section>
      )}

      {game.step === "live" && (
        <Live room={room} youId={youId} act={act} mine={mine} />
      )}
      </div>

      <button className="texty dj-back" type="button" onClick={() => act({ name: "lobby" })}>
        回房間
      </button>
    </div>
  );
}

function ExtraCue({ act }) {
  const [url, setUrl] = useState("");
  return (
    <form
      className="stack"
      onSubmit={(event) => {
        event.preventDefault();
        act({ name: "djCue", url });
      }}
    >
      <label>
        再貼一支
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
      <button className="secondary" type="submit">
        再播這一首
      </button>
    </form>
  );
}

function Live({ room, youId, act, mine }) {
  const game = room.game;
  const playlist = game.mode === "playlist";
  const limit = game.songLimit || (playlist ? 3 : 2);
  const showCount = playlist || (game.mode === "own" && game.extra);
  const canCueExtra = mine && game.mode === "own" && game.extra && (game.songCount || 1) < 2;
  return (
    <section className="stack">
      {showCount ? <p className="song-count">第 {game.songCount || 1} / {limit} 首</p> : null}
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
      {canCueExtra ? <ExtraCue act={act} /> : null}
      {mine && (
        <button className="secondary xl" type="button" onClick={() => act({ name: "djFinish" })}>
          {playlist ? "提前結束" : "本輪結束"}
        </button>
      )}
    </section>
  );
}
