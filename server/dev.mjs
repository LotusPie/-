import {
  clearHand,
  giveCard,
  queueNextDj,
  setDrawCooldown,
  setFreeDraws,
  setHideExpiry,
  stackHide,
} from "./cards.mjs";
import { forceReel, forceWheelStop } from "./dj.mjs";
import { addFakePlayer, removeFakePlayer, setPlayerScore } from "./logic.mjs";

const HIDE_SOON_MS = 10 * 1000;
const DRAW_SOON_MS = 10 * 1000;

export function handleDev(room, playerId, msg) {
  if (!room?.players?.some((player) => player.id === playerId)) return { error: "你還沒進入房間。" };
  room.updatedAt = Date.now();
  const name = msg?.name;
  const targetId = msg?.targetId || playerId;

  if (name === "addFake") return addFakePlayer(room);
  if (name === "removeFake") return removeFakePlayer(room, msg?.targetId);
  if (name === "nextDj") return queueNextDj(room, msg?.targetId);
  if (name === "setScore") return setPlayerScore(room, targetId, msg?.score);
  if (name === "hide" || name === "hideStack") return stackHide(room, targetId);
  if (name === "hideSoon") return setHideExpiry(room, targetId, Date.now() + HIDE_SOON_MS);
  if (name === "wheel") return forceWheelStop(room, msg?.songs);
  if (name === "reel") return forceReel(room, msg?.index);
  if (name === "giveCard") return giveCard(room, targetId, msg?.card);
  if (name === "clearHand") return clearHand(room, targetId);
  if (name === "freeDraws") return setFreeDraws(room, targetId, 3);
  if (name === "drawReady") return setDrawCooldown(room, targetId, 0);
  if (name === "drawSoon") return setDrawCooldown(room, targetId, DRAW_SOON_MS);
  return { error: "還沒有這個動作。" };
}
