const state = { profile: null, loading: false };

const els = {
  refreshBtn: document.getElementById("refreshBtn"),
  profileBtn: document.getElementById("profileBtn"),
  searchInput: document.getElementById("searchInput"),
  boardSelect: document.getElementById("boardSelect"),
  scoreSelect: document.getElementById("scoreSelect"),
  fetchedAt: document.getElementById("fetchedAt"),
  totalCount: document.getElementById("totalCount"),
  profileSummary: document.getElementById("profileSummary"),
  status: document.getElementById("status"),
  dealList: document.getElementById("dealList"),
  checklist: document.getElementById("checklist"),
  sourceList: document.getElementById("sourceList"),
  dialog: document.getElementById("profileDialog"),
  form: document.getElementById("profileForm"),
  profileName: document.getElementById("profileName"),
  profileCards: document.getElementById("profileCards"),
  profileInterests: document.getElementById("profileInterests"),
  profileBoost: document.getElementById("profileBoost"),
  profileMute: document.getElementById("profileMute"),
};

function splitCsv(value) {
  return String(value || "")
    .split(/[,，]/)
    .map((s) => s.trim())
    .filter(Boolean);
}

function joinCsv(list) {
  return (list || []).join(", ");
}

function escapeHtml(str) {
  return String(str)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function escapeAttr(str) {
  return escapeHtml(str).replaceAll("'", "&#39;");
}

async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options,
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }
  return res.json();
}

function renderProfileSummary(profile) {
  const cards = (profile.cards || []).slice(0, 2).join(" / ") || "未設定卡片";
  const interests = (profile.interests || []).slice(0, 3).join(" · ") || "未設定興趣";
  els.profileSummary.textContent = `${cards} · ${interests}`;
}

function fillProfileForm(profile) {
  els.profileName.value = profile.name || "";
  els.profileCards.value = joinCsv(profile.cards);
  els.profileInterests.value = joinCsv(profile.interests);
  els.profileBoost.value = joinCsv(profile.keywords_boost);
  els.profileMute.value = joinCsv(profile.keywords_mute);
}

function renderChecklist(items) {
  els.checklist.innerHTML = (items || [])
    .map((text) => `<li>${escapeHtml(text)}</li>`)
    .join("");
}

function renderSources(sources) {
  els.sourceList.innerHTML = (sources || [])
    .map(
      (s) => `
      <div class="source-item">
        <a href="${escapeAttr(s.url)}" target="_blank" rel="noopener noreferrer">${escapeHtml(s.name)}</a>
        <p>${escapeHtml(s.note || "")}</p>
      </div>`
    )
    .join("");
}

function renderDeals(items) {
  if (!items.length) {
    els.dealList.innerHTML = `<div class="empty">目前沒有符合條件的情報。試著降低最低相關分，或按重新抓取。</div>`;
    return;
  }

  els.dealList.innerHTML = items
    .map((item, index) => {
      const chips = (item.matched || [])
        .slice(0, 4)
        .map((m) => `<span class="chip">${escapeHtml(m)}</span>`)
        .join("");
      const reasons = (item.reasons || []).slice(0, 3).join(" · ");
      return `
        <article class="deal" style="animation-delay:${index * 30}ms">
          <div class="score" title="相關分數">${item.score ?? 0}</div>
          <div>
            <h3><a href="${escapeAttr(item.url)}" target="_blank" rel="noopener noreferrer">${escapeHtml(item.title)}</a></h3>
            <div class="meta">
              <span>${escapeHtml(item.board_name || item.board || "")}</span>
              <span>${escapeHtml(item.date || "")}</span>
              <span>推 ${escapeHtml(String(item.pushes || "-"))}</span>
              ${chips}
            </div>
            ${reasons ? `<div class="reasons">${escapeHtml(reasons)}</div>` : ""}
          </div>
          <a class="open" href="${escapeAttr(item.url)}" target="_blank" rel="noopener noreferrer">開原文</a>
        </article>`;
    })
    .join("");
}

async function loadDeals({ force = false } = {}) {
  if (state.loading) return;
  state.loading = true;
  els.refreshBtn.disabled = true;
  els.status.textContent = force ? "正在重新抓取各板情報…" : "載入情報中…";

  const params = new URLSearchParams({
    force: String(force),
    min_score: els.scoreSelect.value || "0",
  });
  if (els.searchInput.value.trim()) params.set("q", els.searchInput.value.trim());
  if (els.boardSelect.value) params.set("board", els.boardSelect.value);

  try {
    const data = await api(`/api/deals?${params.toString()}`);
    state.profile = data.profile;
    renderProfileSummary(data.profile);
    fillProfileForm(data.profile);
    els.fetchedAt.textContent = data.fetched_at || "—";
    els.totalCount.textContent = String(data.total ?? 0);
    renderChecklist(data.checklist);
    renderDeals(data.items || []);
    els.status.textContent = force
      ? `已更新 ${data.total} 筆（10 分鐘內重複開啟會走快取）`
      : `已載入 ${data.total} 筆`;
  } catch (err) {
    console.error(err);
    els.status.textContent = `載入失敗：${err.message}`;
    els.dealList.innerHTML = `<div class="empty">抓取失敗。請確認網路可連到 PTT，或稍後再試。</div>`;
  } finally {
    state.loading = false;
    els.refreshBtn.disabled = false;
  }
}

async function loadSources() {
  try {
    const data = await api("/api/sources");
    renderSources(data.curated || []);
  } catch (err) {
    els.sourceList.innerHTML = `<div class="empty">情報站載入失敗</div>`;
  }
}

els.refreshBtn.addEventListener("click", () => loadDeals({ force: true }));
els.profileBtn.addEventListener("click", () => {
  if (state.profile) fillProfileForm(state.profile);
  els.dialog.showModal();
});

els.form.addEventListener("submit", async (event) => {
  const submitter = event.submitter;
  if (submitter && submitter.value === "cancel") return;

  event.preventDefault();
  const payload = {
    name: els.profileName.value.trim() || "學生卡友",
    cards: splitCsv(els.profileCards.value),
    interests: splitCsv(els.profileInterests.value),
    keywords_boost: splitCsv(els.profileBoost.value),
    keywords_mute: splitCsv(els.profileMute.value),
  };
  try {
    state.profile = await api("/api/profile", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
    els.dialog.close();
    await loadDeals({ force: false });
  } catch (err) {
    alert(`儲存失敗：${err.message}`);
  }
});

let searchTimer = null;
els.searchInput.addEventListener("input", () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => loadDeals({ force: false }), 280);
});
els.boardSelect.addEventListener("change", () => loadDeals({ force: false }));
els.scoreSelect.addEventListener("change", () => loadDeals({ force: false }));

loadSources();
loadDeals({ force: true });
