from __future__ import annotations

DEFAULT_PROFILE = {
    "name": "學生卡友",
    "cards": ["CUBE"],
    "interests": ["網購", "高鐵", "外食", "小樹點", "哩程", "日本"],
    "keywords_boost": [
        "CUBE",
        "小樹點",
        "國泰",
        "蝦皮",
        "酷澎",
        "Coupang",
        "淘寶",
        "天貓",
        "PChome",
        "高鐵",
        "玩數位",
        "趣旅行",
        "樂饗購",
        "哩程",
        "亞洲萬里通",
        "長榮",
        "機票",
        "日本",
        "首刷",
        "回饋",
        "學生",
        "校園",
    ],
    "keywords_mute": ["徵卡友", "徵", "售", "買賣", "代辦", "違約金", "交換"],
}

PTT_BOARDS = [
    {
        "id": "creditcard",
        "name": "信用卡板",
        "url": "https://www.ptt.cc/bbs/creditcard/index.html",
        "category": "creditcard",
    },
    {
        "id": "Lifeismoney",
        "name": "省錢板",
        "url": "https://www.ptt.cc/bbs/Lifeismoney/index.html",
        "category": "saving",
    },
    {
        "id": "Points",
        "name": "點數哩程板",
        "url": "https://www.ptt.cc/bbs/Points/index.html",
        "category": "miles",
    },
]

CURATED_SOURCES = [
    {
        "name": "卡優新聞網",
        "url": "https://www.cardu.com.tw/",
        "note": "銀行活動懶人包、新卡整理",
    },
    {
        "name": "Money101 信用卡",
        "url": "https://www.money101.com.tw/信用卡",
        "note": "比較表、新戶禮、海外卡",
    },
    {
        "name": "國泰 CUBE 信用卡頁",
        "url": "https://www.cathay-cube.com.tw/cathaybk/personal/product/credit-card/cards/cube",
        "note": "官方權益與首刷禮（以官網為準）",
    },
    {
        "name": "PTT CreditCard",
        "url": "https://www.ptt.cc/bbs/creditcard/index.html",
        "note": "即時卡友情報",
    },
    {
        "name": "PTT Lifeismoney",
        "url": "https://www.ptt.cc/bbs/Lifeismoney/index.html",
        "note": "超商／網購／限時折扣",
    },
    {
        "name": "PTT Points",
        "url": "https://www.ptt.cc/bbs/Points/index.html",
        "note": "點數／哩程兌換討論",
    },
]

CACHE_TTL_SECONDS = 600
FETCH_PAGES = 2
MAX_ITEMS = 80
