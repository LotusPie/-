export const DRIFT_SEC = 1.5;
export const PAUSE_DRIFT_SEC = 0.4;
export const SEEK_COOLDOWN_MS = 1500;
export const AUTOPLAY_GRACE_MS = 1200;

const PLAYING = 1;
const BUFFERING = 3;

export function expectedPlayhead(clock, now) {
  const base = Number(clock?.currentTime);
  const start = Number.isFinite(base) && base > 0 ? base : 0;
  if (clock?.playState !== "playing") return start;
  const at = Number(clock.receivedAt);
  if (!Number.isFinite(at)) return start;
  return start + Math.max(0, (now - at) / 1000);
}

export function planFollow(input) {
  const now = input.now;
  const clock = input.clock || {};
  const unlocked = Boolean(input.unlocked);
  const localState = input.localState;
  const localVideoId = input.localVideoId || "";
  const target = expectedPlayhead(clock, now);
  const wantPlay = clock.playState === "playing" || clock.playState === "buffering";
  const markUnlocked = localState === PLAYING;
  const base = {
    type: "wait",
    videoId: clock.videoId || "",
    seconds: target,
    seek: false,
    play: false,
    needsGesture: false,
    markUnlocked,
  };

  if (!clock.videoId || clock.playState === "error") return base;

  const knownId = localVideoId || clock.videoId;
  const videoChanged = knownId !== clock.videoId;
  const drift = Math.abs((Number(input.localTime) || 0) - target);
  const seekCool = now - (input.lastSeekAt || 0) < SEEK_COOLDOWN_MS;

  if (videoChanged) {
    if (unlocked && wantPlay) {
      return { ...base, type: "load", seek: true, play: true, needsGesture: false };
    }
    return {
      ...base,
      type: "cue",
      seek: true,
      needsGesture: Boolean(wantPlay && !unlocked),
    };
  }

  if (!wantPlay) {
    return {
      ...base,
      type: "pause",
      seek: !seekCool && drift > PAUSE_DRIFT_SEC && localState !== BUFFERING,
    };
  }

  if (!unlocked && localState !== PLAYING && localState !== BUFFERING) {
    const tried = input.autoplayTriedAt || 0;
    if (!tried || now - tried < AUTOPLAY_GRACE_MS) return base;
    return {
      ...base,
      type: "gesture",
      seek: !seekCool && drift > DRIFT_SEC && localState !== BUFFERING,
      needsGesture: true,
    };
  }

  return {
    ...base,
    type: "play",
    seek: !seekCool && drift > DRIFT_SEC && localState !== BUFFERING,
    play: localState !== PLAYING && localState !== BUFFERING,
    needsGesture: false,
  };
}
