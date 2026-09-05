const state = {
  profile: null,
  loading: false,
  pendingReload: null,
  dashboard: null,
};

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
  reminderList: document.getElementById("reminderList"),
  reminderMeta: document.getElementById("reminderMeta"),
  trackerForm: document.getElementById("trackerForm"),
  trackerBalance: document.getElementById("trackerBalance"),
  trackerRate: document.getElementById("trackerRate"),
  trackerRoute: document.getElementById("trackerRoute"),
  trackerNote: document.getElementById("trackerNote"),
  trackerProgress: document.getElementById("trackerProgress"),
  trackerMeta: document.getElementById("trackerMeta"),
  summaryBox: document.getElementById("summaryBox"),
  summaryMeta: document.getElementById("summaryMeta"),
  summaryBtn: document.getElementById("summaryBtn"),
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

function renderReminders(payload) {
  const items = payload.items || [];
  els.reminderMeta.textContent = `今日待辦 ${payload.due_count || 0} · 已完成 ${payload.done_count || 0}`;
  if (!items.length) {
    els.reminderList.innerHTML = `<div class="empty">目前沒有提醒項目</div>`;
    return;
  }
  els.reminderList.innerHTML = items
    .map((item) => {
      const statusLabel =
        item.status === "done" ? "已完成" : item.status === "due" ? "今日待辦" : "稍後";
      return `
        <article class="reminder ${escapeAttr(item.status)}">
          <div>
            <div class="reminder-top">
              <strong>${escapeHtml(item.title)}</strong>
              <span class="badge">${escapeHtml(statusLabel)}</span>
            </div>
            <p>${escapeHtml(item.detail || "")}</p>
            <div class="meta">
              <span>${escapeHtml(item.app || "")}</span>
              <span>${escapeHtml(item.cadence || "")}</span>
            </div>
          </div>
          <button
            class="btn ghost tiny"
            type="button"
            data-reminder-id="${escapeAttr(item.id)}"
            data-done="${item.status === "done" ? "0" : "1"}"
          >${item.status === "done" ? "取消完成" : "標成完成"}</button>
        </article>`;
    })
    .join("");
}

function renderTracker(tracker) {
  els.trackerBalance.value = tracker.balance ?? 0;
  els.trackerRate.value = tracker.earn_rate_percent ?? 3;
  els.trackerRoute.innerHTML = (tracker.routes || [])
    .map(
      (route) =>
        `<option value="${escapeAttr(route.id)}" ${
          route.id === tracker.route_id ? "selected" : ""
        }>${escapeHtml(route.label)}</option>`
    )
    .join("");

  const pct = tracker.progress_percent ?? 0;
  els.trackerMeta.textContent = `${tracker.program_label || "哩程計畫"} · 進度 ${pct}%`;
  els.trackerProgress.innerHTML = `
    <div class="progress-copy">
      <strong>${escapeHtml(tracker.route_label || "")}</strong>
      <span>${tracker.balance || 0} / ${tracker.points_needed || 0} 小樹點</span>
    </div>
    <div class="progress-bar" aria-hidden="true"><i style="width:${pct}%"></i></div>
    <div class="progress-grid">
      <div><span>還差</span><strong>${tracker.points_remaining || 0} 點</strong></div>
      <div><span>約需哩程</span><strong>${tracker.miles_needed || 0}</strong></div>
      <div><span>約還需消費</span><strong>${Number(tracker.estimated_spend_remaining || 0).toLocaleString("zh-TW")} 元</strong></div>
      <div><span>本週增減</span><strong>${tracker.delta_this_week || 0}</strong></div>
    </div>
    <p class="fineprint">${escapeHtml(tracker.tax_note || "")} ${escapeHtml(tracker.disclaimer || "")}</p>
  `;
}

function renderSummary(summary) {
  if (!summary) {
    els.summaryBox.innerHTML = `<div class="empty">尚無摘要</div>`;
    return;
  }
  els.summaryMeta.textContent = `${summary.week_id || ""} · ${summary.generated_at || ""}`;
  const actions = (summary.actions || [])
    .map((text) => `<li>${escapeHtml(text)}</li>`)
    .join("");
  const highlights = (summary.highlights || [])
    .map(
      (item) => `
      <a class="summary-hit" href="${escapeAttr(item.url || "#")}" target="_blank" rel="noopener noreferrer">
        <span class="chip">${item.score ?? 0}</span>
        <span>${escapeHtml(item.title || "")}</span>
      </a>`
    )
    .join("");

  els.summaryBox.innerHTML = `
    <h4>${escapeHtml(summary.headline || "一週摘要")}</h4>
    <p class="summary-narrative">${escapeHtml(summary.narrative || "")}</p>
    <div class="progress-grid summary-stats">
      <div><span>高相關情報</span><strong>${summary.stats?.top_deals ?? 0}</strong></div>
      <div><span>今日待辦</span><strong>${summary.stats?.reminders_due ?? 0}</strong></div>
      <div><span>小樹點</span><strong>${summary.stats?.tree_points ?? 0}</strong></div>
      <div><span>機票進度</span><strong>${summary.stats?.progress_percent ?? 0}%</strong></div>
    </div>
    <h5>本週行動</h5>
    <ul class="summary-actions">${actions}</ul>
    <h5>精選情報</h5>
    <div class="summary-hits">${highlights || `<div class="empty">本週暫無高相關情報</div>`}</div>
  `;
}

async function loadDashboard() {
  const data = await api("/api/dashboard");
  state.dashboard = data;
  renderReminders(data.reminders || { items: [] });
  renderTracker(data.tracker || {});
  renderSummary(data.summary || null);
}

async function loadDeals({ force = false } = {}) {
  if (state.loading) {
    state.pendingReload = { force: force || Boolean(state.pendingReload?.force) };
    return;
  }
  state.loading = true;
  state.pendingReload = null;
  els.refreshBtn.disabled = true;
  els.status.textContent = force ? "正在重新抓取各板情報…" : "載入情報中…";

  const params = new URLSearchParams({
    force: String(force),
    min_score: els.scoreSelect.value || "0",
  });
  const q = els.searchInput.value.trim();
  const board = els.boardSelect.value;
  if (q) params.set("q", q);
  if (board) params.set("board", board);

  try {
    const data = await api(`/api/deals?${params.toString()}`);
    state.profile = data.profile;
    renderProfileSummary(data.profile);
    fillProfileForm(data.profile);
    els.fetchedAt.textContent = data.fetched_at || "—";
    els.totalCount.textContent = String(data.total ?? 0);
    renderChecklist(data.checklist);
    renderDeals(data.items || []);
    const filterHint = [q && `搜尋「${q}」`, board && `版面 ${board}`].filter(Boolean).join(" · ");
    els.status.textContent = force
      ? `已更新 ${data.total} 筆${filterHint ? `（${filterHint}）` : ""}（10 分鐘內重複開啟會走快取）`
      : `已載入 ${data.total} 筆${filterHint ? `（${filterHint}）` : ""}`;
    await loadDashboard();
  } catch (err) {
    console.error(err);
    els.status.textContent = `載入失敗：${err.message}`;
    els.dealList.innerHTML = `<div class="empty">抓取失敗。請確認網路可連到 PTT，或稍後再試。</div>`;
  } finally {
    state.loading = false;
    els.refreshBtn.disabled = false;
    if (state.pendingReload) {
      const next = state.pendingReload;
      state.pendingReload = null;
      await loadDeals(next);
    }
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

els.reminderList.addEventListener("click", async (event) => {
  const btn = event.target.closest("[data-reminder-id]");
  if (!btn) return;
  btn.disabled = true;
  try {
    const payload = await api("/api/reminders/complete", {
      method: "POST",
      body: JSON.stringify({
        id: btn.dataset.reminderId,
        done: btn.dataset.done === "1",
      }),
    });
    renderReminders(payload);
    const summary = await api("/api/summary/weekly?force=true");
    renderSummary(summary);
  } catch (err) {
    alert(`更新提醒失敗：${err.message}`);
  } finally {
    btn.disabled = false;
  }
});

els.trackerForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const payload = {
    balance: Number(els.trackerBalance.value || 0),
    earn_rate_percent: Number(els.trackerRate.value || 3),
    route_id: els.trackerRoute.value,
    note: els.trackerNote.value.trim() || undefined,
  };
  try {
    const tracker = await api("/api/tracker", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
    renderTracker(tracker);
    els.trackerNote.value = "";
    const summary = await api("/api/summary/weekly?force=true");
    renderSummary(summary);
  } catch (err) {
    alert(`更新進度失敗：${err.message}`);
  }
});

els.summaryBtn.addEventListener("click", async () => {
  els.summaryBtn.disabled = true;
  els.summaryMeta.textContent = "產生中…";
  try {
    const summary = await api("/api/summary/weekly?force=true");
    renderSummary(summary);
  } catch (err) {
    els.summaryMeta.textContent = `產生失敗：${err.message}`;
  } finally {
    els.summaryBtn.disabled = false;
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
