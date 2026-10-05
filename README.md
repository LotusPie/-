# 旅途小遊戲 on the trip

朋友在旅途中一起玩的私人小遊戲。手機優先，介面是繁體中文。沒有帳號，房間不會公開列出，只有拿到代碼或連結的人進得來。

可以玩三款：

- **開話題**：抽一張旅行提問，輪流回答。說完 +1。
- **帶動氣氛**：抽一個輕鬆、做得來的小挑戰。不想做可以讓給下一個人。完成 +2。
- **積分獎懲**：投票、小獎勵、請一杯想像中的飲料。房間裡有積分榜，每一輪結束看排名。分數不會變成負的。

房間記在伺服器記憶體裡。伺服器一關，房間就散了。

這個倉庫裡的 `html/` 是以前的頁面，跟這款遊戲無關。

## 安裝與執行

需要 Node.js 20 以上。

```bash
npm install
npm run dev
```

瀏覽器打開 [http://localhost:5173](http://localhost:5173)。

同一台電腦上的兩個瀏覽器都連這個網址。手機跟電腦在同一個 Wi-Fi 時，用電腦的區網位址，例如 `http://192.168.x.x:5173`。

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
4. 誰都可以按 **開話題**、**帶動氣氛** 或 **積分獎懲**。抽到的卡片兩邊一樣。
5. 輪到的人按 **我說完了**。另一個視窗會看到留下的句子，分數也會一起增加。

重新整理會回到同一個旅伴。關掉伺服器再重開，房間就不在了，需要重新建立。

## 自動檢查

`npm test` 會在本機起一個暫時的伺服器，用兩個 WebSocket 客戶端建立房間、加入、玩一輪開話題，並確認卡片與分數同步。

## English

Private, ephemeral travel party game. No accounts and no public room list.

```bash
npm install
npm run dev
```

Open http://localhost:5173, create a room, then join from a second browser with the 4-character code.
