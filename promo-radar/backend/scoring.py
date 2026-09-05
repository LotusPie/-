from __future__ import annotations

from typing import Any


def _norm(text: str) -> str:
    return (text or "").casefold()


def score_item(item: dict[str, Any], profile: dict[str, Any]) -> dict[str, Any]:
    title = item.get("title", "")
    hay = _norm(title)
    score = 0
    reasons: list[str] = []
    matched: list[str] = []

    boosts = [str(x) for x in profile.get("keywords_boost", [])]
    mutes = [str(x) for x in profile.get("keywords_mute", [])]
    cards = [str(x) for x in profile.get("cards", [])]
    interests = [str(x) for x in profile.get("interests", [])]

    for mute in mutes:
        if mute and _norm(mute) in hay:
            return {
                **item,
                "score": -100,
                "matched": [],
                "reasons": [f"已靜音：{mute}"],
                "hidden": True,
            }

    for card in cards:
        if card and _norm(card) in hay:
            score += 35
            matched.append(card)
            reasons.append(f"命中持卡：{card}")

    for interest in interests:
        if interest and _norm(interest) in hay:
            score += 18
            matched.append(interest)
            reasons.append(f"符合興趣：{interest}")

    for kw in boosts:
        if kw and _norm(kw) in hay:
            score += 12
            if kw not in matched:
                matched.append(kw)
            reasons.append(f"關鍵字：{kw}")

    tag = item.get("tag") or ""
    if tag == "情報":
        score += 6
        reasons.append("情報標籤")
    elif tag in {"優惠", "公告", "心得"}:
        score += 3

    pushes = str(item.get("pushes") or "")
    if pushes == "爆":
        score += 20
        reasons.append("推文爆熱")
    else:
        try:
            n = int(pushes)
            if n >= 50:
                score += 14
                reasons.append(f"高討論 {n}")
            elif n >= 20:
                score += 8
                reasons.append(f"熱門 {n}")
            elif n >= 10:
                score += 4
        except ValueError:
            pass

    category = item.get("category")
    if category == "miles" and any(x in interests for x in ["哩程", "日本", "機票"]):
        score += 10
        reasons.append("哩程板加權")
    if category == "creditcard" and any(x in interests for x in ["網購", "回饋", "小樹點", "CUBE"]):
        score += 6
    if category == "saving" and "外食" in interests:
        score += 4

    actionable = ["回饋", "首刷", "活動", "限時", "登錄", "加碼", "免費", "折抵", "哩程", "機票"]
    if any(_norm(a) in hay for a in actionable):
        score += 8
        reasons.append("偏可行動優惠")

    return {
        **item,
        "score": score,
        "matched": matched[:8],
        "reasons": reasons[:6],
        "hidden": False,
    }


def rank_items(items: list[dict[str, Any]], profile: dict[str, Any]) -> list[dict[str, Any]]:
    scored = [score_item(item, profile) for item in items]
    visible = [x for x in scored if not x.get("hidden")]
    visible.sort(key=lambda x: (x.get("score", 0), x.get("date", "")), reverse=True)
    return visible
