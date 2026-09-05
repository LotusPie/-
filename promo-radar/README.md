# 優惠雷達 Promo Radar

專門幫你蒐集「花時間可換到的優惠／回饋／哩程情報」的小工具。

## 功能

- 自動抓取 PTT：`creditcard`、`Lifeismoney`、`Points`
- 依你的偏好（預設：CUBE、網購、高鐵、小樹點、哩程、日本）做相關分數排序
- 搜尋、依版面篩選、調整最低相關分
- 右側提供每週檢查清單與固定情報站連結
- 偏好設定會存在 `data/profile.json`

## 啟動

```bash
cd promo-radar
python3 -m pip install -r requirements.txt
python3 -m uvicorn backend.main:app --host 0.0.0.0 --port 8787
```

瀏覽器開啟：http://127.0.0.1:8787

## 建議用法

1. 先打開「偏好設定」，確認卡片與興趣
2. 按「重新抓取」更新情報
3. 把「最低相關分」設在 20+ 或 40+，先看高相關
4. 點標題開 PTT 原文，再回銀行 App 登錄活動

## 注意

- 本工具只做公開資訊彙整與排序，不保證活動仍有效
- 實際權益與回饋以銀行／官網為準
- 請勿為了回饋購買不需要的東西；帳單請全額繳清
