# 旅途小遊戲 on the trip

朋友在旅途中一起玩的私人小遊戲。手機優先，介面是繁體中文。沒有帳號，房間不會公開列出，只有拿到代碼或連結的人進得來。

車上主遊戲是 **如果我是迪爵**。乘客輪流當本輪迪爵。房間裡每個人都有 YouTube 官方播放器，一起看同一支影片。迪爵的播放器是時間的基準，其他人跟著影片、時間和播放或暫停。不需要 API 金鑰，也不會下載歌曲。

- 迪爵按 **轉一下**。轉盤機會一樣，落在 **一首歌**、**二首歌** 或 **三首歌**。轉完就不能重選。再貼上一個 YouTube 或 YouTube Music 連結。
- **一首歌**：只播那一支，播完這一輪就結束。
- **二首歌**、**三首歌**：先播那一支，只有迪爵的手機會接著播 YouTube 推薦，播滿那個首數就停。大家跟著同一支影片，不會自己開推薦。歌名會自己更新，大家可以重新評。迪爵也可以提前結束。打出「多一首」時，這一輪再多一首。
- 其他人用自己的口味打 -2、-1、+1、+2。沒有 0。打了的人自己 +1。這些數字加進迪爵的房間積分，可以是負的。牌可以改這一筆。

房間有手牌，最多 5 張。進房可以先抽三次，用完才開始算半小時。也可以在牌店用分數買。分數要夠，負分不能買。打出就用掉。

十二張牌：隱藏分數、交換分數、偷看、護身、擋負分、加倍、偷一分、再轉一次、指定模式、多一首、指定下一位、這輪不當。

房間記在伺服器記憶體裡。伺服器一關，房間就散了。

這個倉庫裡的 `html/` 是以前的頁面，跟這款遊戲無關。

## 安裝與執行

需要 Node.js 20 以上。

```bash
npm install
npm run dev
```

瀏覽器打開 [http://localhost:5173](http://localhost:5173)。請用 localhost 或有名字的網址，不要用 127.0.0.1。YouTube 不接受 IP 網址的嵌入播放。

同一台電腦上的兩個瀏覽器都連這個網址。YouTube 的嵌入播放不接受 IP 網址，所以請開 localhost，不要開 127.0.0.1 或 192.168 開頭的位址。

只想開一個連接埠時：

```bash
npm run build
npm start
```

然後打開 [http://localhost:3001](http://localhost:3001)。

## 兩個人怎麼進同一間

1. 第一個人取暱稱，按 **建立房間**。畫面上會出現 4 個字的房間代碼。
2. 第二個人開另一個視窗（若被認成同一個人，改用無痕視窗），輸入另一個暱稱和代碼，按 **加入房間**。也可以直接打開分享連結，網址會像 `http://localhost:5173/?room=AB3K`。
3. 兩邊都會看到彼此的名字和分數。
4. 誰都可以按 **如果我是迪爵**。本輪迪爵按 **轉一下**，轉盤決定播一首、兩首或三首，再貼上 YouTube 連結。每個人的螢幕一起看同一支。其他人打分，分數會一起變。若瀏覽器擋下自動播放，按一次 **開始一起看**。

重新整理會回到同一個旅伴。關掉伺服器再重開，房間就不在了，需要重新建立。

## 自動檢查

`npm test` 會在本機起一個暫時的伺服器，用兩個 WebSocket 客戶端建立房間、加入，並各玩一輪一首歌和三首歌，確認歌名、評分加分與第幾首一起更新。

## English

Private, ephemeral travel party game. No accounts and no public room list. The main car game is 如果我是迪爵: everyone in the room watches the same YouTube video in sync. The DJ's player is the clock.

```bash
npm install
npm run dev
```

Open http://localhost:5173, create a room, then join from a second browser with the 4-character code. DJ ratings are -2, -1, +1, and +2.
