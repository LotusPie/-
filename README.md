# 旅途小遊戲 on the trip

朋友在旅途中一起玩的私人小遊戲。手機優先，介面是繁體中文。沒有帳號，房間不會公開列出，只有拿到代碼或連結的人進得來。

車上主遊戲是 **如果我是迪爵**。乘客輪流當本輪迪爵。只有迪爵的手機會用 YouTube 官方播放器出聲，其他人看同一首的歌名並評分。不需要 API 金鑰，也不會下載歌曲。

- **自行選歌**：迪爵貼上 YouTube 或 YouTube Music 的連結，這支手機會播那一首。播完，或迪爵按本輪結束，就換下一位。
- **貼歌單**：迪爵貼上自己真的會開的第一首。按開始之後，用 YouTube 的推薦清單接著播 20 分鐘，畫面上有倒數。換到下一首推薦時，歌名會自己更新，大家可以重新評。時間到就停，迪爵也可以提前結束。
- 其他人用自己的口味打 -2 到 +2，或按不評。打了數字（含 0）的人自己 +1，不評沒有分。這些數字加進迪爵的房間積分，可以是負的。誰都可以按這輪跳過。

另外三款還在，放在主遊戲下面：

- **開話題**：抽一張旅行提問，輪流回答。說完 +1。
- **帶動氣氛**：抽一個輕鬆、做得來的小挑戰。不想做可以讓給下一個人。完成 +2。
- **積分獎懲**：投票、小獎勵、請一杯想像中的飲料。房間裡有積分榜，每一輪結束看排名。這款本身不會把分數扣到負的。

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
4. 誰都可以按 **如果我是迪爵**。本輪迪爵選 **自行選歌** 或 **貼歌單**，貼上 YouTube 連結。只有迪爵的手機出聲。其他人看歌名、打分，分數會一起變。
5. 下面還有 **開話題**、**帶動氣氛**、**積分獎懲**。

重新整理會回到同一個旅伴。關掉伺服器再重開，房間就不在了，需要重新建立。

## 自動檢查

`npm test` 會在本機起一個暫時的伺服器，用兩個 WebSocket 客戶端建立房間、加入，玩開話題，並各玩一輪自行選歌和貼歌單，確認歌名、評分加分與倒數一起更新。

## English

Private, ephemeral travel party game. No accounts and no public room list. The main car game is 如果我是迪爵: only the current DJ's phone plays a YouTube video. Everyone else sees the title and can rate it.

```bash
npm install
npm run dev
```

Open http://localhost:5173, create a room, then join from a second browser with the 4-character code.
