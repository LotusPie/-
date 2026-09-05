from __future__ import annotations

from copy import deepcopy
from datetime import date, datetime, timedelta
from typing import Any

# Approximate 小樹點 → miles (non-world card). Always verify on official pages.
TREE_POINT_RATES: dict[str, dict[str, Any]] = {
    "eva": {"label": "長榮無限萬哩遊", "points_per_1000_miles": 360},
    "asia_miles": {"label": "亞洲萬里通", "points_per_1000_miles": 360},
    "jal": {"label": "日航 JAL", "points_per_1000_miles": 480},
}

# Rough reward-seat needs for planning only
ROUTE_TARGETS: dict[str, dict[str, Any]] = {
    "tpe_nrt_eco_rt": {
        "label": "台北⇄東京 經濟艙來回（亞萬估算）",
        "program": "asia_miles",
        "miles_needed": 25000,
        "tax_note": "另付稅金／附加費",
    },
    "tpe_kix_eco_rt": {
        "label": "台北⇄大阪 經濟艙來回（亞萬估算）",
        "program": "asia_miles",
        "miles_needed": 25000,
        "tax_note": "另付稅金／附加費",
    },
    "tpe_nrt_biz_ow": {
        "label": "台北→東京 商務艙單程（長榮估算）",
        "program": "eva",
        "miles_needed": 25000,
        "tax_note": "另付稅金／附加費；需有酬賓機位",
    },
    "tpe_fuk_eco_rt": {
        "label": "台北⇄福岡 經濟艙來回（亞萬估算）",
        "program": "asia_miles",
        "miles_needed": 20000,
        "tax_note": "另付稅金／附加費",
    },
}

DEFAULT_BANK_REMINDERS: list[dict[str, Any]] = [
    {
        "id": "cube-daily-rights",
        "title": "CUBE App：切當日權益",
        "detail": "網購切玩數位；高鐵日切趣旅行；大餐才切樂饗購。",
        "cadence": "daily",
        "priority": "high",
        "app": "CUBE",
    },
    {
        "id": "cube-coupon-check",
        "title": "CUBE／國泰優惠：領券與登錄",
        "detail": "打開專屬優惠／券區，有首刷或通路加碼先登錄。",
        "cadence": "daily",
        "priority": "high",
        "app": "CUBE",
    },
    {
        "id": "bill-autodebit",
        "title": "確認信用卡全額扣繳",
        "detail": "核對自動扣繳帳戶餘額，避免只繳最低應繳。",
        "cadence": "weekly",
        "weekday": 0,
        "priority": "high",
        "app": "CUBE",
    },
    {
        "id": "points-expiry",
        "title": "小樹點效期檢查",
        "detail": "看即將到期點數，能折帳單就先折，或規劃轉哩程。",
        "cadence": "weekly",
        "weekday": 6,
        "priority": "medium",
        "app": "CUBE Rewards",
    },
    {
        "id": "monthly-promo-scan",
        "title": "本月銀行活動掃描",
        "detail": "對照卡優／Money101／官網，整理本月可疊加活動。",
        "cadence": "monthly",
        "day": 1,
        "priority": "medium",
        "app": "官網／情報站",
    },
    {
        "id": "hsr-travel-day",
        "title": "返鄉／高鐵日前夜提醒",
        "detail": "出發當天記得切趣旅行再買票或刷車資。",
        "cadence": "weekly",
        "weekday": 4,
        "priority": "medium",
        "app": "CUBE",
    },
]

DEFAULT_TRACKER: dict[str, Any] = {
    "balance": 0,
    "earn_rate_percent": 3.0,
    "route_id": "tpe_nrt_eco_rt",
    "history": [],
    "updated_at": None,
}


def week_id(d: date | None = None) -> str:
    d = d or date.today()
    iso = d.isocalendar()
    return f"{iso.year}-W{iso.week:02d}"


def reminder_is_due(reminder: dict[str, Any], today: date | None = None) -> bool:
    today = today or date.today()
    cadence = reminder.get("cadence")
    if cadence == "daily":
        return True
    if cadence == "weekly":
        return today.weekday() == int(reminder.get("weekday", 0))
    if cadence == "monthly":
        return today.day == int(reminder.get("day", 1))
    return False


def build_reminder_board(
    catalog: list[dict[str, Any]],
    completions: dict[str, str],
    today: date | None = None,
) -> list[dict[str, Any]]:
    today = today or date.today()
    today_s = today.isoformat()
    board: list[dict[str, Any]] = []
    for item in catalog:
        last_done = completions.get(item["id"])
        due = reminder_is_due(item, today)
        status = "done" if last_done == today_s else ("due" if due else "upcoming")
        board.append(
            {
                **item,
                "status": status,
                "last_done": last_done,
                "due_today": due and status != "done",
            }
        )
    order = {"due": 0, "upcoming": 1, "done": 2}
    priority = {"high": 0, "medium": 1, "low": 2}
    board.sort(key=lambda x: (order.get(x["status"], 9), priority.get(x.get("priority"), 9)))
    return board


def compute_miles_progress(tracker: dict[str, Any]) -> dict[str, Any]:
    data = deepcopy(DEFAULT_TRACKER)
    data.update(tracker or {})
    route_id = data.get("route_id") or "tpe_nrt_eco_rt"
    route = ROUTE_TARGETS.get(route_id) or ROUTE_TARGETS["tpe_nrt_eco_rt"]
    program_id = route["program"]
    program = TREE_POINT_RATES[program_id]
    miles_needed = int(route["miles_needed"])
    points_per_1000 = int(program["points_per_1000_miles"])
    points_needed = int(round(miles_needed * points_per_1000 / 1000))
    balance = max(0, int(data.get("balance") or 0))
    remaining = max(0, points_needed - balance)
    pct = 0.0 if points_needed <= 0 else min(100.0, round(balance * 100 / points_needed, 1))
    earn_rate = float(data.get("earn_rate_percent") or 3.0)
    spend_needed = 0 if earn_rate <= 0 else int(round(remaining / (earn_rate / 100.0)))

    history = list(data.get("history") or [])
    delta_week = 0
    week_start = date.today() - timedelta(days=date.today().weekday())
    for row in history:
        try:
            row_date = date.fromisoformat(str(row.get("date")))
        except ValueError:
            continue
        if row_date >= week_start and isinstance(row.get("delta"), int):
            delta_week += int(row["delta"])

    return {
        "balance": balance,
        "earn_rate_percent": earn_rate,
        "route_id": route_id,
        "route_label": route["label"],
        "program_id": program_id,
        "program_label": program["label"],
        "miles_needed": miles_needed,
        "points_needed": points_needed,
        "points_remaining": remaining,
        "progress_percent": pct,
        "estimated_spend_remaining": spend_needed,
        "tax_note": route["tax_note"],
        "delta_this_week": delta_week,
        "history": history[-12:],
        "updated_at": data.get("updated_at"),
        "disclaimer": "兌換比例與機位以航空公司／國泰官方為準，此為規劃用估算。",
        "routes": [{"id": key, "label": value["label"]} for key, value in ROUTE_TARGETS.items()],
        "programs": [
            {
                "id": key,
                "label": value["label"],
                "points_per_1000_miles": value["points_per_1000_miles"],
            }
            for key, value in TREE_POINT_RATES.items()
        ],
    }


def build_weekly_summary(
    *,
    deals: list[dict[str, Any]],
    reminders: list[dict[str, Any]],
    progress: dict[str, Any],
    profile: dict[str, Any],
) -> dict[str, Any]:
    top = sorted(deals, key=lambda x: int(x.get("score", 0)), reverse=True)[:8]
    due = [r for r in reminders if r.get("due_today")]
    done = [r for r in reminders if r.get("status") == "done"]
    cards = "、".join(profile.get("cards") or ["CUBE"])
    interests = "、".join((profile.get("interests") or [])[:4]) or "一般回饋"

    highlights = [
        {
            "title": item.get("title"),
            "score": item.get("score"),
            "board": item.get("board_name") or item.get("board"),
            "url": item.get("url"),
            "matched": item.get("matched") or [],
        }
        for item in top[:5]
    ]

    actions: list[str] = []
    if due:
        actions.append(f"今天先完成 {len(due)} 件銀行 App 提醒（含權益切換／領券）。")
    if int(progress.get("points_remaining") or 0) > 0:
        actions.append(
            f"距離「{progress.get('route_label')}」大概還差 {progress.get('points_remaining')} 小樹點"
            f"（約還需消費 {progress.get('estimated_spend_remaining')} 元，以 {progress.get('earn_rate_percent')}% 估算）。"
        )
    else:
        actions.append("小樹點已達目標估算，可先查酬賓機位再決定是否轉點。")
    if highlights:
        actions.append("本週高相關情報已整理在下方，優先看分數 40+ 且命中你卡片關鍵字的項目。")
    actions.append("記得帳單全額繳清；優惠是少花錢，不是多欠錢。")

    narrative = (
        f"本週摘要（{week_id()}）：以 {cards} 為主、關注 {interests}。"
        f"情報池目前高相關約 {len(top)} 則可看；銀行提醒今日待辦 {len(due)}、已完成 {len(done)}。"
        f"小樹點進度 {progress.get('progress_percent')}%"
        f"（{progress.get('balance')} / {progress.get('points_needed')}）。"
    )

    return {
        "week_id": week_id(),
        "generated_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "headline": f"{profile.get('name') or '卡友'}的一週優惠摘要",
        "narrative": narrative,
        "stats": {
            "top_deals": len(top),
            "reminders_due": len(due),
            "reminders_done": len(done),
            "tree_points": progress.get("balance"),
            "progress_percent": progress.get("progress_percent"),
        },
        "highlights": highlights,
        "actions": actions,
        "progress": {
            "route_label": progress.get("route_label"),
            "balance": progress.get("balance"),
            "points_needed": progress.get("points_needed"),
            "points_remaining": progress.get("points_remaining"),
            "progress_percent": progress.get("progress_percent"),
            "delta_this_week": progress.get("delta_this_week"),
        },
    }
