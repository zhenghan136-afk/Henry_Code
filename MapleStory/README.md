# MapleStory Artale 自動按鍵工具

給楓之谷 Artale 用的自動按鍵控制小工具（Windows Forms / .NET 8）。透過 `SendInput` 模擬硬體層級的按鍵事件，依照選定的行為模式（Set）自動觸發技能與移動。

## 需求

- Windows
- .NET 8 SDK（`net8.0-windows`）

## 建置與執行

```bash
dotnet build MapleStory.sln
dotnet run --project MapleStory
```

或直接用 Visual Studio 開啟 `MapleStory.sln`。

## 使用方式

1. 開啟程式後，先在下拉選單選擇要執行的行為模式（`Set 1` / `Set 2`）。
2. 切到楓之谷遊戲視窗，讓遊戲視窗保持在前景/取得焦點。
3. 回到工具視窗按 **Start**，會先等待 3 秒讓你切回遊戲畫面，之後開始依所選 Set 的邏輯自動送出按鍵。
4. 停止方式：
   - 按工具視窗上的 **Stop**，或
   - 在任何視窗按全域快捷鍵 **F11**（透過 `RegisterHotKey` 註冊，不需要工具視窗在前景）。

執行中下拉選單會被鎖定，停止後才能再切換模式。

### 行為模式

- **Set 1**：計時觸發技能鍵（F）＋高頻率連續攻擊鍵（A），時間間隔皆帶隨機抖動。
- **Set 2**：固定順序排程（F → 空白鍵 → D → C → X，並穿插左右移動），同樣帶隨機抖動避免固定節奏。

新增 Set3、Set4 等模式：在 `Form1.cs` 新增對應的 `RunTemplateXAsync` 方法、在 `btnStart_Click` 加一個 `else if (currentTemplate == X)` 分支，並在 `Form1_Load` 的 `cmbTemplate.Items.AddRange(...)` 裡加上選項文字即可。

## 已知限制 / 疑難排解

- 按鍵是用 `SendInput` 送到目前的前景視窗，若遊戲視窗沒有焦點（被其他視窗蓋掉、彈出通知等），按鍵就送不到遊戲裡。
- 若楓之谷用系統管理員身分執行，而本工具不是，Windows 的 UIPI（User Interface Privilege Isolation）會直接擋掉輸入且不會有例外或錯誤視窗——這種情況請將本工具也以系統管理員身分執行。
- 方向鍵等延伸鍵已加上 `KEYEVENTF_EXTENDEDKEY` 旗標，避免被系統誤判為數字鍵盤輸入。

## 免責聲明

僅供個人研究與私人伺服器（Artale）練習使用，請自行確認符合所屬伺服器的使用條款。
