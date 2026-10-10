import jiege from "./assets/professions/jiege.png";
import yaju from "./assets/professions/yaju.png";
import nailong from "./assets/professions/nailong.png";
import niulai from "./assets/professions/niulai.png";
import tung from "./assets/professions/tung.png";
import tralala from "./assets/professions/tralala.png";
import bombardiro from "./assets/professions/bombardiro.jpg";
import ballerina from "./assets/professions/ballerina.png";

export const PROFESSIONS = [
  { id: "jiege", name: "傑哥", effect: "進房間時，手上多一張護身。", src: jiege },
  { id: "yaju", name: "野獸先輩", effect: "你的轉盤上，再轉一次的機會變成兩倍。", src: yaju },
  { id: "nailong", name: "奶龍", effect: "抽牌冷卻比平常的三十分鐘少十分鐘。", src: nailong },
  { id: "niulai", name: "牛來", effect: "迷因懲罰由他加權", src: niulai },
  { id: "tung", name: "Tung Tung Tung Sahur", effect: "你的轉盤上，偷一分的機會變成兩倍。", src: tung },
  { id: "tralala", name: "Tralalero Tralala", effect: "普通牌手牌上限是 6 張。", src: tralala },
  { id: "bombardiro", name: "Bombardiro Crocodilo", effect: "你的轉盤上，指定模式的機會變成兩倍。", src: bombardiro },
  { id: "ballerina", name: "Ballerina Cappuccina", effect: "進房間時多一次免費抽牌，一開始可以抽四次。", src: ballerina },
];

const ART = Object.fromEntries(PROFESSIONS.map((job) => [job.id, job.src]));

export function professionArt(id) {
  return ART[id] || "";
}

export function ProfessionMark({ profession, large = false }) {
  if (!profession?.name) return null;
  const src = professionArt(profession.id);
  return (
    <span className={large ? "profession-badge is-large" : "profession-badge"}>
      {src ? <img src={src} alt="" /> : null}
      <span>{profession.name}</span>
    </span>
  );
}
