import jiege from "./assets/professions/jiege.png";
import yaju from "./assets/professions/yaju.png";
import nailong from "./assets/professions/nailong.png";
import niulai from "./assets/professions/niulai.png";
import tung from "./assets/professions/tung.png";
import tralala from "./assets/professions/tralala.png";
import bombardiro from "./assets/professions/bombardiro.jpg";
import ballerina from "./assets/professions/ballerina.png";

export const PROFESSIONS = [
  { id: "jiege", name: "傑哥", effect: "功能牌打到你時，可以說一次不要，改打到出牌的人。", src: jiege },
  { id: "yaju", name: "野獸先輩", effect: "你當迪爵時，轉盤沒有此輪跳過。", src: yaju },
  { id: "nailong", name: "奶龍", effect: "抓到了要另外三位旅伴按下。", src: nailong },
  { id: "niulai", name: "牛來", effect: "打給你的分數會在 -2、-1、0、+1、+2 裡重抽。", src: niulai },
  { id: "tung", name: "Tung Tung Tung Sahur", effect: "可以提早結束一位旅伴身上的一張功能牌。", src: tung },
  { id: "tralala", name: "Tralalero Tralala", effect: "每一輪迪爵可以重轉一次拉霸條件。", src: tralala },
  { id: "bombardiro", name: "Bombardiro Crocodilo", effect: "這一趟可以清掉所有功能牌的倒數，只能用一次。", src: bombardiro },
  { id: "ballerina", name: "Ballerina Cappuccina", effect: "每一輪迪爵可以再轉一次首數轉盤。", src: ballerina },
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

const HANDS = [
  ["scissors", "剪刀"],
  ["rock", "石頭"],
  ["paper", "布"],
];

export function RoleBoard({ room, youId, act }) {
  const roles = room?.roles || [];
  const duel = room?.duel;
  const result = room?.roleResult;
  const me = room?.players?.find((player) => player.id === youId);
  const mine = me?.profile?.profession?.id || "";
  const inDuel = duel && (duel.challengerId === youId || duel.holderId === youId);
  return (
    <section className="panel stack profession-pick">
      <h2>選一個職業</h2>
      <p className="hint">進房間之後先搶。每個職業只能有一個人。點空的就拿走，點別人的就猜拳。有職業的人不能再搶，但可以被挑戰。</p>
      {result ? (
        <p className="hint">
          {result.challengerName}出{result.challengerHand}，{result.holderName}出{result.holderHand}。
          {result.tie ? "平手，再來一次。" : `${result.winnerName}拿到${result.roleName}。`}
        </p>
      ) : null}
      {duel ? (
        <div className="duel-box">
          <p>
            {duel.challengerName}向{duel.holderName}挑戰{duel.roleName}。猜拳：剪刀、石頭、布。
          </p>
          <p className="hint">
            {duel.challengerReady ? `${duel.challengerName}出好了` : `${duel.challengerName}還沒出`}
            {" · "}
            {duel.holderReady ? `${duel.holderName}出好了` : `${duel.holderName}還沒出`}
          </p>
          {inDuel ? (
            duel.youChose ? (
              <p>你出了{duel.youChose}，等對方。</p>
            ) : (
              <div className="row">
                {HANDS.map(([id, label]) => (
                  <button key={id} className="secondary" type="button" onClick={() => act({ name: "roleThrow", hand: id })}>
                    {label}
                  </button>
                ))}
              </div>
            )
          ) : (
            <p className="hint">現在有人在猜拳，其他人先等。</p>
          )}
        </div>
      ) : null}
      <div className="profession-grid" aria-label="職業">
        {roles.map((role) => {
          const src = professionArt(role.id);
          const taken = Boolean(role.holderId);
          const own = role.holderId === youId;
          return (
            <button
              key={role.id}
              type="button"
              className={own ? "is-on" : ""}
              disabled={Boolean(duel) || own || Boolean(mine)}
              onClick={() => act({ name: "roleTake", role: role.id })}
            >
              {src ? <img src={src} alt="" /> : null}
              <strong>{role.name}</strong>
              <span>{role.effect}</span>
              <em>{role.holderName || "還沒人"}</em>
            </button>
          );
        })}
      </div>
    </section>
  );
}
