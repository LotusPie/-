from __future__ import annotations

import re
from datetime import datetime, timezone
from typing import Any
from urllib.parse import urljoin

import httpx
from bs4 import BeautifulSoup

from .config import FETCH_PAGES, PTT_BOARDS

PTT_BASE = "https://www.ptt.cc"
HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (compatible; PromoRadar/1.0; "
        "+https://github.com/LotusPie/-; educational aggregator)"
    )
}
COOKIES = {"over18": "1"}


def _extract_tag(title: str) -> str | None:
    match = re.search(r"\[([^\]]+)\]", title)
    return match.group(1) if match else None


async def _fetch_html(client: httpx.AsyncClient, url: str) -> str:
    response = await client.get(url, headers=HEADERS, cookies=COOKIES, follow_redirects=True)
    response.raise_for_status()
    return response.text


def _parse_index(html: str, board_id: str, board_name: str, category: str) -> list[dict[str, Any]]:
    soup = BeautifulSoup(html, "lxml")
    items: list[dict[str, Any]] = []

    for ent in soup.select("div.r-ent"):
        title_el = ent.select_one("div.title a")
        if not title_el:
            continue
        title = title_el.get_text(strip=True)
        href = title_el.get("href") or ""
        if not title or not href:
            continue

        author_el = ent.select_one("div.meta div.author")
        date_el = ent.select_one("div.meta div.date")
        nrec_el = ent.select_one("div.nrec span") or ent.select_one("div.nrec")

        items.append(
            {
                "id": f"ptt:{board_id}:{href}",
                "source": "PTT",
                "board": board_id,
                "board_name": board_name,
                "category": category,
                "title": title,
                "tag": _extract_tag(title),
                "url": urljoin(PTT_BASE, href),
                "author": author_el.get_text(strip=True) if author_el else "",
                "date": date_el.get_text(strip=True) if date_el else "",
                "pushes": nrec_el.get_text(strip=True) if nrec_el else "",
                "fetched_at": datetime.now(timezone.utc).isoformat(),
            }
        )
    return items


def _prev_page_url(html: str) -> str | None:
    soup = BeautifulSoup(html, "lxml")
    for link in soup.select("div.btn-group-paging a.btn"):
        if "上頁" in link.get_text():
            href = link.get("href")
            return urljoin(PTT_BASE, href) if href else None
    return None


async def fetch_board(board: dict[str, str], pages: int = FETCH_PAGES) -> list[dict[str, Any]]:
    collected: list[dict[str, Any]] = []
    url = board["url"]
    async with httpx.AsyncClient(timeout=20.0) as client:
        for _ in range(max(1, pages)):
            html = await _fetch_html(client, url)
            collected.extend(
                _parse_index(
                    html,
                    board_id=board["id"],
                    board_name=board["name"],
                    category=board["category"],
                )
            )
            prev = _prev_page_url(html)
            if not prev:
                break
            url = prev
    return collected


async def fetch_all_boards(pages: int = FETCH_PAGES) -> list[dict[str, Any]]:
    all_items: list[dict[str, Any]] = []
    errors: list[str] = []
    for board in PTT_BOARDS:
        try:
            all_items.extend(await fetch_board(board, pages=pages))
        except Exception as exc:  # noqa: BLE001
            errors.append(f"{board['id']}: {exc}")

    seen: set[str] = set()
    unique: list[dict[str, Any]] = []
    for item in all_items:
        if item["id"] in seen:
            continue
        seen.add(item["id"])
        unique.append(item)

    if errors and unique:
        unique[0].setdefault("_warnings", errors)
    elif errors:
        raise RuntimeError("; ".join(errors))
    return unique
