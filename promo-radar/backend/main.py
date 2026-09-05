from __future__ import annotations

import json
import time
from copy import deepcopy
from datetime import date, datetime
from pathlib import Path
from typing import Any

from fastapi import FastAPI, HTTPException, Query
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field

from .config import (
    CACHE_TTL_SECONDS,
    CURATED_SOURCES,
    DEFAULT_PROFILE,
    FETCH_PAGES,
    MAX_ITEMS,
)
from .planning import (
    DEFAULT_BANK_REMINDERS,
    DEFAULT_TRACKER,
    build_reminder_board,
    build_weekly_summary,
    compute_miles_progress,
    week_id,
)
from .scrapers import fetch_all_boards
from .scoring import rank_items

ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "data"
FRONTEND_DIR = ROOT / "frontend"

PROFILE_PATH = DATA_DIR / "profile.json"
CACHE_PATH = DATA_DIR / "cache.json"
REMINDERS_PATH = DATA_DIR / "reminders.json"
TRACKER_PATH = DATA_DIR / "tracker.json"
SUMMARY_PATH = DATA_DIR / "weekly_summary.json"

DATA_DIR.mkdir(parents=True, exist_ok=True)

app = FastAPI(title="Promo Radar", version="1.1.0")


class ProfileUpdate(BaseModel):
    name: str | None = None
    cards: list[str] | None = None
    interests: list[str] | None = None
    keywords_boost: list[str] | None = None
    keywords_mute: list[str] | None = None


class ReminderComplete(BaseModel):
    id: str
    done: bool = True


class TrackerUpdate(BaseModel):
    balance: int | None = Field(default=None, ge=0)
    earn_rate_percent: float | None = Field(default=None, gt=0, le=20)
    route_id: str | None = None
    note: str | None = None


def _read_json(path: Path, fallback: Any) -> Any:
    if path.exists():
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            pass
    return deepcopy(fallback)


def _write_json(path: Path, payload: Any) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _load_profile() -> dict[str, Any]:
    if PROFILE_PATH.exists():
        profile = _read_json(PROFILE_PATH, DEFAULT_PROFILE)
    else:
        profile = deepcopy(DEFAULT_PROFILE)
        _write_json(PROFILE_PATH, profile)
    return profile


def _load_cache() -> dict[str, Any] | None:
    if not CACHE_PATH.exists():
        return None
    try:
        return json.loads(CACHE_PATH.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None


def _save_cache(payload: dict[str, Any]) -> None:
    _write_json(CACHE_PATH, payload)


def _load_reminder_state() -> dict[str, Any]:
    state = _read_json(
        REMINDERS_PATH,
        {"completions": {}, "catalog": deepcopy(DEFAULT_BANK_REMINDERS)},
    )
    if not state.get("catalog"):
        state["catalog"] = deepcopy(DEFAULT_BANK_REMINDERS)
    state.setdefault("completions", {})
    return state


def _load_tracker() -> dict[str, Any]:
    if TRACKER_PATH.exists():
        return _read_json(TRACKER_PATH, DEFAULT_TRACKER)
    tracker = deepcopy(DEFAULT_TRACKER)
    _write_json(TRACKER_PATH, tracker)
    return tracker


async def _collect(force: bool = False, pages: int = FETCH_PAGES) -> dict[str, Any]:
    cached = _load_cache()
    now = time.time()
    if (
        not force
        and cached
        and now - float(cached.get("fetched_at_epoch", 0)) < CACHE_TTL_SECONDS
    ):
        return cached

    raw_items = await fetch_all_boards(pages=pages)
    payload = {
        "fetched_at_epoch": now,
        "fetched_at": time.strftime("%Y-%m-%d %H:%M:%S"),
        "count": len(raw_items),
        "items": raw_items[: MAX_ITEMS * 2],
    }
    _save_cache(payload)
    return payload


def _checklist() -> list[str]:
    return [
        "打開 CUBE App 看當日權益與專屬優惠",
        "掃過本頁高相關區有無首刷／加碼",
        "若要搭高鐵：當天切趣旅行",
        "網購日維持玩數位（蝦皮／酷澎／淘寶／PChome）",
        "更新小樹點餘額，看機票進度有沒有往前",
        "點數效期與帳單全額扣繳確認一次",
    ]


@app.get("/api/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/api/profile")
async def get_profile() -> dict[str, Any]:
    return _load_profile()


@app.put("/api/profile")
async def put_profile(body: ProfileUpdate) -> dict[str, Any]:
    profile = _load_profile()
    profile.update(body.model_dump(exclude_none=True))
    _write_json(PROFILE_PATH, profile)
    return profile


@app.get("/api/sources")
async def sources() -> dict[str, Any]:
    return {"curated": CURATED_SOURCES}


@app.get("/api/deals")
async def deals(
    q: str | None = Query(default=None),
    board: str | None = Query(default=None),
    min_score: int = Query(default=0, ge=-100, le=500),
    force: bool = Query(default=False),
    pages: int = Query(default=FETCH_PAGES, ge=1, le=4),
) -> dict[str, Any]:
    profile = _load_profile()
    try:
        bundle = await _collect(force=force, pages=pages)
    except Exception as exc:  # noqa: BLE001
        raise HTTPException(status_code=502, detail=f"抓取來源失敗：{exc}") from exc

    ranked = rank_items(bundle.get("items", []), profile)
    if board:
        ranked = [x for x in ranked if x.get("board") == board]
    if q:
        needle = q.casefold()
        ranked = [
            x
            for x in ranked
            if needle in (x.get("title") or "").casefold()
            or any(needle in m.casefold() for m in x.get("matched", []))
        ]
    ranked = [x for x in ranked if int(x.get("score", 0)) >= min_score][:MAX_ITEMS]
    return {
        "profile": profile,
        "fetched_at": bundle.get("fetched_at"),
        "cache_ttl_seconds": CACHE_TTL_SECONDS,
        "total": len(ranked),
        "items": ranked,
        "checklist": _checklist(),
    }


@app.get("/api/reminders")
async def get_reminders() -> dict[str, Any]:
    state = _load_reminder_state()
    board = build_reminder_board(state["catalog"], state.get("completions", {}))
    return {
        "date": date.today().isoformat(),
        "due_count": sum(1 for x in board if x.get("due_today")),
        "done_count": sum(1 for x in board if x.get("status") == "done"),
        "items": board,
    }


@app.post("/api/reminders/complete")
async def complete_reminder(body: ReminderComplete) -> dict[str, Any]:
    state = _load_reminder_state()
    catalog_ids = {item["id"] for item in state.get("catalog", [])}
    if body.id not in catalog_ids:
        raise HTTPException(status_code=404, detail="找不到此提醒")

    completions = state.setdefault("completions", {})
    today_s = date.today().isoformat()
    if body.done:
        completions[body.id] = today_s
    elif completions.get(body.id) == today_s:
        completions.pop(body.id, None)

    _write_json(REMINDERS_PATH, state)
    board = build_reminder_board(state["catalog"], completions)
    return {
        "date": today_s,
        "due_count": sum(1 for x in board if x.get("due_today")),
        "done_count": sum(1 for x in board if x.get("status") == "done"),
        "items": board,
    }


@app.get("/api/tracker")
async def get_tracker() -> dict[str, Any]:
    return compute_miles_progress(_load_tracker())


@app.put("/api/tracker")
async def put_tracker(body: TrackerUpdate) -> dict[str, Any]:
    tracker = _load_tracker()
    old_balance = int(tracker.get("balance") or 0)
    data = body.model_dump(exclude_none=True)
    note = data.pop("note", None)

    if "route_id" in data:
        probe = compute_miles_progress({**tracker, **data})
        valid_routes = {r["id"] for r in probe["routes"]}
        if data["route_id"] not in valid_routes:
            raise HTTPException(status_code=400, detail="不支援的航線目標")

    tracker.update(data)
    tracker["updated_at"] = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    if "balance" in data:
        delta = int(data["balance"]) - old_balance
        history = list(tracker.get("history") or [])
        history.append(
            {
                "date": date.today().isoformat(),
                "balance": int(data["balance"]),
                "delta": delta,
                "note": note or ("手動更新餘額" if delta == 0 else f"餘額變化 {delta:+d}"),
            }
        )
        tracker["history"] = history[-30:]

    _write_json(TRACKER_PATH, tracker)
    return compute_miles_progress(tracker)


@app.get("/api/summary/weekly")
async def weekly_summary(force: bool = Query(default=False)) -> dict[str, Any]:
    current_week = week_id()
    cached = _read_json(SUMMARY_PATH, None)
    if (
        not force
        and isinstance(cached, dict)
        and cached.get("week_id") == current_week
        and cached.get("payload")
    ):
        return cached["payload"]

    profile = _load_profile()
    reminder_state = _load_reminder_state()
    reminders = build_reminder_board(
        reminder_state["catalog"], reminder_state.get("completions", {})
    )
    progress = compute_miles_progress(_load_tracker())

    try:
        bundle = await _collect(force=False)
        deals = rank_items(bundle.get("items", []), profile)
        deals = [x for x in deals if int(x.get("score", 0)) >= 20][:40]
    except Exception:  # noqa: BLE001
        deals = []

    payload = build_weekly_summary(
        deals=deals,
        reminders=reminders,
        progress=progress,
        profile=profile,
    )
    _write_json(SUMMARY_PATH, {"week_id": current_week, "payload": payload})
    return payload


@app.get("/api/dashboard")
async def dashboard() -> dict[str, Any]:
    reminders = await get_reminders()
    tracker = await get_tracker()
    summary = await weekly_summary(force=False)
    return {"reminders": reminders, "tracker": tracker, "summary": summary}


@app.get("/")
async def index() -> FileResponse:
    return FileResponse(FRONTEND_DIR / "index.html")


app.mount("/static", StaticFiles(directory=FRONTEND_DIR), name="static")
