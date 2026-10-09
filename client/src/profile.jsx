import { useState } from "react";

const AVATARS = ["🎵", "🎧", "🎸", "🎤", "🚗", "🧳", "🌙", "⭐", "🍵", "🌊"];
const PHOTO_MAX = 200 * 1024;

function takeChars(value, max) {
  return [...String(value ?? "")].slice(0, max).join("");
}

function decodedBytes(dataUrl) {
  const body = String(dataUrl).split(",")[1] || "";
  const padding = body.endsWith("==") ? 2 : body.endsWith("=") ? 1 : 0;
  return Math.floor((body.length * 3) / 4) - padding;
}

function loadImage(file) {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const image = new Image();
    image.onload = () => {
      URL.revokeObjectURL(url);
      resolve(image);
    };
    image.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error("這張照片沒辦法用。"));
    };
    image.src = url;
  });
}

async function fitPhoto(file) {
  if (!file || !String(file.type || "").startsWith("image/")) {
    throw new Error("這張照片沒辦法用。");
  }
  const image = await loadImage(file);
  let edge = 256;
  for (let attempt = 0; attempt < 6; attempt += 1) {
    const scale = Math.min(1, edge / Math.max(image.width, image.height, 1));
    const width = Math.max(1, Math.round(image.width * scale));
    const height = Math.max(1, Math.round(image.height * scale));
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("這張照片沒辦法用。");
    ctx.drawImage(image, 0, 0, width, height);
    let quality = 0.72;
    let url = canvas.toDataURL("image/jpeg", quality);
    while (decodedBytes(url) > PHOTO_MAX && quality > 0.45) {
      quality -= 0.08;
      url = canvas.toDataURL("image/jpeg", quality);
    }
    if (decodedBytes(url) <= PHOTO_MAX) return url;
    edge = Math.round(edge * 0.75);
  }
  throw new Error("照片請小於 200KB。");
}

export function PersonFace({ nickname = "旅伴", profile, large = false }) {
  const photo = typeof profile?.photo === "string" ? profile.photo : "";
  const emoji = AVATARS.includes(profile?.emoji) ? profile.emoji : "";
  const letter = [...(nickname || "旅")][0] || "旅";
  return (
    <span className={large ? "person-face is-large" : "person-face"} aria-hidden="true">
      {photo ? <img src={photo} alt="" /> : <span>{emoji || letter}</span>}
    </span>
  );
}

export function ProfileView({ nickname, profile, onClose }) {
  return (
    <article className="profile-card">
      <PersonFace large nickname={nickname} profile={profile} />
      <h3>{nickname}</h3>
      <p className={profile?.line ? "profile-line" : "profile-line empty"}>
        {profile?.line || "這個人還沒留下一句話。"}
      </p>
      {profile?.from ? (
        <p className="profile-meta">
          <span>來自</span>
          {profile.from}
        </p>
      ) : null}
      {profile?.music ? (
        <p className="profile-meta">
          <span>喜歡的音樂</span>
          {profile.music}
        </p>
      ) : null}
      {onClose ? (
        <button className="secondary" type="button" onClick={onClose}>
          關閉
        </button>
      ) : null}
    </article>
  );
}

export function ProfilePage({ room, youId, act }) {
  const you = room?.players?.find((player) => player.id === youId) || null;
  const saved = you?.profile || {};
  const [emoji, setEmoji] = useState(() => (AVATARS.includes(saved.emoji) ? saved.emoji : AVATARS[0]));
  const [photo, setPhoto] = useState(() => saved.photo || "");
  const [line, setLine] = useState(() => saved.line || "");
  const [nickname, setNickname] = useState(() => you?.nickname || "");
  const [from, setFrom] = useState(() => saved.from || "");
  const [music, setMusic] = useState(() => saved.music || "");
  const [photoError, setPhotoError] = useState("");

  if (!room || !you) {
    return (
      <section className="panel profile-page stack">
        <h2>個人</h2>
        <p className="hint">進房間之後，可以編輯頭像和一句話。</p>
      </section>
    );
  }

  async function onPhoto(event) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    try {
      const next = await fitPhoto(file);
      setPhoto(next);
      setPhotoError("");
    } catch (error) {
      setPhotoError(error?.message || "這張照片沒辦法用。");
    }
  }

  function onSave(event) {
    event.preventDefault();
    if (photoError) return;
    act({
      name: "profileSave",
      emoji,
      photo,
      line,
      nickname,
      from,
      music,
    });
  }

  return (
    <section className="panel profile-page stack">
      <h2>個人</h2>
      <form className="stack" onSubmit={onSave}>
        <div className="profile-face" aria-hidden="true">
          {photo ? <img src={photo} alt="" /> : <span>{emoji}</span>}
        </div>
        <div className="avatar-choices" role="group" aria-label="頭像">
          {AVATARS.map((item) => (
            <button
              key={item}
              type="button"
              aria-pressed={emoji === item}
              onClick={() => setEmoji(item)}
            >
              {item}
            </button>
          ))}
        </div>
        <div className="row">
          <label className="photo-pick">
            從手機選照片
            <input type="file" accept="image/*" onChange={onPhoto} />
          </label>
          {photo ? (
            <button className="texty" type="button" onClick={() => setPhoto("")}>
              移除照片
            </button>
          ) : null}
        </div>
        {photoError ? (
          <p className="banner" role="alert">
            {photoError}
          </p>
        ) : (
          <p className="hint">照片會留在這個房間裡，大約 200KB 以內。不選也可以。</p>
        )}
        <label>
          一句話
          <input
            value={line}
            onChange={(event) => setLine(takeChars(event.target.value, 40))}
            maxLength={40}
            placeholder="例如：這趟想聽老歌"
          />
        </label>
        <label>
          暱稱
          <input
            value={nickname}
            onChange={(event) => setNickname(takeChars(event.target.value, 12))}
            maxLength={12}
            autoComplete="nickname"
            placeholder={you.nickname}
          />
        </label>
        <p className="hint">空白就沿用現在的暱稱。</p>
        <label>
          來自
          <input
            value={from}
            onChange={(event) => setFrom(takeChars(event.target.value, 24))}
            maxLength={24}
            placeholder="例如：台南"
          />
        </label>
        <label>
          喜歡的音樂
          <input
            value={music}
            onChange={(event) => setMusic(takeChars(event.target.value, 40))}
            maxLength={40}
            placeholder="例如：城市民謠"
          />
        </label>
        <p className="hint">來自和喜歡的音樂可以空白。</p>
        <button className="primary" type="submit">
          儲存
        </button>
      </form>
    </section>
  );
}
