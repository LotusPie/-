from __future__ import annotations

import json
import time
from copy import deepcopy
from pathlib import Path
from typing import Any

from fastapi import FastAPI, HTTPException, Query
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

from .config import (
    CACHE_TTL_SECONDS,
    CURATED_SOURCES,
    DEFAULT_PROFILE,
    FETCH_PAGES,
    MAX_ITEMS,
)
from .scrapers import fetch_all_boards
from .scoring import rank_items

ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "data"
FRONTEND_DIR = ROOT / "frontend"
PROFILE_PATH = DATA_DIR / "profile.json"
CACHE_PATH = DATA_DIR / "cache.json"

DATA_DIR.mkdir(parents=True, exist_ok=True)

app = FastAPI(title="Promo Radar", version="1.0.0")


class ProfileUpdate(BaseModel):
    name: str | None = None
    cards: list[str] | None = None
    interests: list[str] | None = None
    keywords_boost: list[str] | None = None
    keywords_mute: list[str] | None = None


def _load_profile() -> dict[str, Any]:
    if PROFILE_PATH.exists():
        try:
            return json.loads(PROFILE_PATH.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            pass
    profile = deepcopy(DEFAULT_PROFILE)
    _save_profile(profile)
    return profile


def _save_profile(profile: dict[str, Any]) -> None:
    PROFILE_PATH.write_text(json.dumps(profile, ensure_ascii=False, indent=2), encoding="utf-8")


def _load_cache() -> dict[str, Any] | None:
    if not CACHE_PATH.exists():
        return None
    try:
        return json.loads(CACHE_PATH.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None


def _save_cache(payload: dict[str, Any]) -> None:
    CACHE_PATH.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")


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
    _save_profile(profile)
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
        "checklist": [
            "打開 CUBE App 看當日權益與專屬優惠",
            "掃過本頁高相關區有無首刷／加碼",
            "若要搭高鐵：當天切趣旅行",
            "網購日維持玩數位（蝦皮／酷澎／淘寶／PChome）",
            "點數效期與帳單全額扣繳確認一次",
        ],
    }


@app.get("/")
async def index() -> FileResponse:
    return FileResponse(FRONTEND_DIR / "index.html")


app.mount("/static", StaticFiles(directory=FRONTEND_DIR), name="static")
