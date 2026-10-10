export const BASE_HAND_MAX = 5;
export const BASE_DRAW_MS = 30 * 60 * 1000;
export const BASE_FREE_DRAWS = 3;

export const PROFESSIONS = [
  {
    id: "jiege",
    name: "傑哥",
    effect: "進房間時，手上多一張護身。",
    grant: "shield",
  },
  {
    id: "yaju",
    name: "野獸先輩",
    effect: "你的轉盤上，再轉一次的機會變成兩倍。",
    boost: "respin",
  },
  {
    id: "nailong",
    name: "奶龍",
    effect: "抽牌冷卻比平常的三十分鐘少十分鐘。",
    cooldownCut: 10 * 60 * 1000,
  },
  {
    id: "niulai",
    name: "牛來",
    effect: "迷因懲罰由他加權",
    memeWeight: true,
  },
  {
    id: "tung",
    name: "Tung Tung Tung Sahur",
    effect: "你的轉盤上，偷一分的機會變成兩倍。",
    boost: "steal",
  },
  {
    id: "tralala",
    name: "Tralalero Tralala",
    effect: "普通牌手牌上限是 6 張。",
    handMax: 6,
  },
  {
    id: "bombardiro",
    name: "Bombardiro Crocodilo",
    effect: "你的轉盤上，指定模式的機會變成兩倍。",
    boost: "mode",
  },
  {
    id: "ballerina",
    name: "Ballerina Cappuccina",
    effect: "進房間時多一次免費抽牌，一開始可以抽四次。",
    freeDraws: 4,
  },
];

export function professionOf(id) {
  return PROFESSIONS.find((item) => item.id === id) || null;
}

export function publicProfession(player) {
  const spec = professionOf(player?.profession);
  if (!spec) return null;
  return {
    id: spec.id,
    name: spec.name,
    effect: spec.effect,
    memeWeight: Boolean(spec.memeWeight),
  };
}

export function handMaxOf(player) {
  return professionOf(player?.profession)?.handMax || BASE_HAND_MAX;
}

export function drawCooldownMs(player) {
  const cut = professionOf(player?.profession)?.cooldownCut || 0;
  return BASE_DRAW_MS - cut;
}

export function startingSpins(player) {
  return professionOf(player?.profession)?.freeDraws || BASE_FREE_DRAWS;
}

export function wheelBoost(player) {
  return professionOf(player?.profession)?.boost || "";
}
