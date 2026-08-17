using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MapleStory
{
    public partial class Form1 : Form
    {
        // 匯入必要的 Windows API
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        // 強制指定結構大小為 40 位元組，符合 64 位元作業系統標準
        [StructLayout(LayoutKind.Explicit, Size = 40)]
        struct INPUT
        {
            [FieldOffset(0)] public uint type;
            [FieldOffset(8)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_SCANCODE = 0x0008; // 啟用硬體掃描碼
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        // 熱鍵相關常數
        const int WM_HOTKEY = 0x0312;
        const int STOP_HOTKEY_ID = 7777;
        const uint VK_F11 = 0x7A;
        const int CAST_HOTKEY_ID = 7778;
        const uint VK_F5 = 0x74;

        private CancellationTokenSource _cts;
        private int currentTemplate = 1;
        private string _targetWindowKeyword = "MapleStory";
        private bool _lastFocusState = true;

        // MP 監控：不含 F5/F11，因為那兩個是本程式自己註冊的全域熱鍵，
        // 若補 MP 鍵也用同一個鍵，SendInput 送出的按鍵會被自己的 RegisterHotKey 攔截、到不了遊戲。
        private static readonly Dictionary<string, ushort> PotionKeyMap = new()
        {
            ["Page Down"] = 0x22,
            ["Insert"] = 0x2D,
            ["Home"] = 0x24,
            ["End"] = 0x23,
            ["0"] = 0x30, ["1"] = 0x31, ["2"] = 0x32, ["3"] = 0x33, ["4"] = 0x34,
            ["5"] = 0x35, ["6"] = 0x36, ["7"] = 0x37, ["8"] = 0x38, ["9"] = 0x39,
            ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
            ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77, ["F9"] = 0x78, ["F10"] = 0x79, ["F12"] = 0x7B,
        };

        private Rectangle _mpBarRelativeRect; // 相對於遊戲視窗「客戶區」左上角的座標
        private bool _mpBarCalibrated = false;
        private CancellationTokenSource _mpMonitorCts;
        private int _mpThresholdPercent = 30;
        private ushort _mpPotionKey = 0x22;
        // Set 3 的提醒倒數：按下 F5 起算 4 分鐘，時間到會發出提示音，
        // 提示音最多響 20 秒，期間沒有手動重新施放就自動補放一次。
        private const int SET3_COUNTDOWN_SECONDS = 4 * 60;
        private const int SET3_ALERT_SECONDS = 20;
        private readonly System.Windows.Forms.Timer _set3CountdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        private int _set3RemainingSeconds;
        private int _set3AlertSecondsLeft;
        private bool _set3AutoMode; // 曾經自動補放過，倒數字維持紅色直到手動按 F5
        private CountdownOverlayForm? _overlay;

        // 校準時學到的血條填滿色（以色相 + 飽和度表示，對亮度漸層與分隔線免疫）
        private float _mpRefHue = 0f;
        private float _mpRefSaturation = 0f;
        private bool _mpColorsLearned = false;

        public Form1()
        {
            InitializeComponent();
            _set3CountdownTimer.Tick += Set3CountdownTimer_Tick;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 註冊 F11 為緊急停止鍵
            bool success = RegisterHotKey(this.Handle, STOP_HOTKEY_ID, 0, VK_F11);
            if (!success)
            {
                MessageBox.Show("F11 熱鍵註冊失敗，可能被其他程式佔用了");
            }

            // 註冊 F5 為 Set 3 的施放技能熱鍵，這樣選到 Set 3 時不用切回工具視窗按按鈕
            bool castHotkeySuccess = RegisterHotKey(this.Handle, CAST_HOTKEY_ID, 0, VK_F5);
            if (!castHotkeySuccess)
            {
                MessageBox.Show("F5 熱鍵註冊失敗，可能被其他程式佔用了");
            }

            cmbTemplate.Items.AddRange(new object[] { "Set 1", "Set 2", "Set 3" });
            cmbTemplate.SelectedIndex = 0;
            btnStop.Enabled = false;

            foreach (string key in PotionKeyMap.Keys)
            {
                cmbMpPotionKey.Items.Add(key);
            }
            cmbMpPotionKey.SelectedItem = "Page Down";

            SetStatus("待機中");
        }

        private void cmbTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentTemplate = cmbTemplate.SelectedIndex + 1;

            // Set 3 是手動一次性施放技能組合，不走 Start/Stop 那種持續迴圈，
            // 選到 Set 3 時額外顯示「施放技能」鍵，Start/Stop 一律保留不隱藏。
            bool isSet3 = currentTemplate == 3;
            btnCastSet3.Visible = isSet3;
            lblSet3Countdown.Visible = isSet3;
            chkOverlay.Visible = isSet3;
            btnMoveOverlay.Visible = isSet3 && chkOverlay.Checked;
        }

        // 同步更新狀態列文字與標題列，這樣即使視窗被切到背景／縮到工作列，
        // 從工作列滑鼠停留或 Alt-Tab 預覽也看得到目前是待機、準備中還是執行中。
        private void SetStatus(string text)
        {
            lblStatus.Text = "狀態：" + text;
            this.Text = $"MapleStory 控制器 - {text}";
        }

        // 只有目前「前景視窗」的標題包含指定關鍵字時才視為遊戲畫面，
        // 避免使用者切到瀏覽器、聊天室等其他視窗時誤觸按鍵。
        private bool IsGameWindowActive()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            // 本工具的標題「MapleStory 控制器」也含關鍵字，必須排除，
            // 否則工具視窗在前景時會被誤判成遊戲視窗而照樣送出按鍵。
            if (hWnd == this.Handle) return false;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString().IndexOf(_targetWindowKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // 這個方法會從背景執行緒呼叫，所以狀態列文字要透過 BeginInvoke 轉回 UI 執行緒更新，
        // 並且只在「有無聚焦」狀態真的改變時才更新，避免每次送按鍵都觸發一次 UI 呼叫。
        private void UpdateFocusStatus(bool isActive)
        {
            if (isActive == _lastFocusState) return;
            _lastFocusState = isActive;

            string text = isActive ? $"執行中 - Set {currentTemplate}" : "已暫停（遊戲視窗非使用中）";
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() => SetStatus(text)));
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _mpMonitorCts?.Cancel();
            _set3CountdownTimer.Stop();
            _overlay?.Close(); // 浮動視窗不屬於主視窗，不主動關閉的話程式不會真正結束
            UnregisterHotKey(this.Handle, STOP_HOTKEY_ID);
            UnregisterHotKey(this.Handle, CAST_HOTKEY_ID);
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();
                if (hotkeyId == STOP_HOTKEY_ID)
                {
                    ExecuteStop();
                }
                else if (hotkeyId == CAST_HOTKEY_ID)
                {
                    ExecuteCastSet3Hotkey();
                }
            }
            base.WndProc(ref m);
        }

        // Left/Up/Right/Down、Insert/Delete/Home/End/PageUp/PageDown 等鍵是「延伸鍵」，
        // 用到的 scan code 和數字鍵盤共用，若不加上 EXTENDEDKEY flag，系統和遊戲有可能
        // 誤判成鍵盤右側 NumPad 的同組編碼，導致按鍵沒有正常觸發。
        private static bool IsExtendedKey(ushort vkCode) => vkCode switch
        {
            0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 => true, // PgUp/PgDn/End/Home/Left/Up/Right/Down
            0x2D or 0x2E or 0x2C => true, // Insert/Delete/PrintScreen
            0x90 or 0xA3 or 0xA5 => true, // NumLock/RightCtrl/RightAlt
            _ => false
        };

        // --- 非同步硬體級模擬 ---
        private async Task SendKeyHardwareAsync(ushort vkCode, CancellationToken token)
        {
            bool isActive = IsGameWindowActive();
            UpdateFocusStatus(isActive);

            // 遊戲視窗不在前景時，原地等待（每 200ms 檢查一次）而不是跳過這一步，
            // 這樣切回遊戲畫面後會直接補送這個按鍵，再接續原本的順序往下走，
            // 不會打亂 Set 2 這種固定順序排程的施放次序。
            while (!isActive)
            {
                await Task.Delay(200, token);
                isActive = IsGameWindowActive();
                UpdateFocusStatus(isActive);
            }

            ushort scanCode = (ushort)MapVirtualKey(vkCode, 0);
            uint extraFlag = IsExtendedKey(vkCode) ? KEYEVENTF_EXTENDEDKEY : 0;

            INPUT inputDown = new INPUT();
            inputDown.type = INPUT_KEYBOARD;
            inputDown.ki = new KEYBDINPUT { wVk = 0, wScan = scanCode, dwFlags = KEYEVENTF_SCANCODE | extraFlag, time = 0, dwExtraInfo = IntPtr.Zero };

            INPUT inputUp = new INPUT();
            inputUp.type = INPUT_KEYBOARD;
            inputUp.ki = new KEYBDINPUT { wVk = 0, wScan = scanCode, dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP | extraFlag, time = 0, dwExtraInfo = IntPtr.Zero };

            uint sentDown = SendInput(1, new INPUT[] { inputDown }, Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(25, token);
            uint sentUp = SendInput(1, new INPUT[] { inputUp }, Marshal.SizeOf(typeof(INPUT)));

            // SendInput 回傳 0 代表被拒絕，最常見的原因是目標視窗權限比本程式高（UIPI）：
            // MapleStory 以管理員身分執行時，非管理員的本程式送出的輸入會被系統擋掉。
            if (sentDown == 0 || sentUp == 0)
            {
                Debug.WriteLine($"SendInput 失敗（vk=0x{vkCode:X2}），可能被UAC/其他更高權限的視窗攔截。");
            }
        }

        // --- 開始按鈕事件 ---
        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (_cts != null) return;

            if (currentTemplate == 3)
            {
                MessageBox.Show("Set 3 請改用「施放技能」按鈕。");
                return;
            }

            _cts = new CancellationTokenSource();
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            cmbTemplate.Enabled = false;
            txtWindowTitle.Enabled = false;
            _targetWindowKeyword = string.IsNullOrWhiteSpace(txtWindowTitle.Text) ? "MapleStory" : txtWindowTitle.Text.Trim();
            _lastFocusState = true;
            SetStatus($"準備中，3 秒後開始 Set {currentTemplate}，請切換到遊戲視窗");

            try
            {
                // 等待 3 秒讓玩家切換視窗
                await Task.Delay(3000, _cts.Token);
                SetStatus($"執行中 - Set {currentTemplate}");

                if (currentTemplate == 1)
                {
                    // 【關鍵修正點】：改用 Task.Run 封裝，強迫巨集在「背景執行緒」全速運轉
                    // 這樣主介面的視窗（UI執行緒）才會 100% 空閒，F11 隨時按隨時有用！
                    await Task.Run(async () => await RunTemplate1Async(_cts.Token));
                }
                else if (currentTemplate == 2)
                {
                    // 模板二同樣丟到背景執行
                    await Task.Run(async () => await RunTemplate2Async(_cts.Token));
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                cmbTemplate.Enabled = true;
                txtWindowTitle.Enabled = true;
                SetStatus("待機中");
            }
        }

        // ==========================================
        // 模板一：雙隨機計時器 + 快速連點 A 鍵
        private async Task RunTemplate1Async(CancellationToken token)
        {
            Stopwatch Set1_Timer = new Stopwatch();
            Stopwatch Set2_Timer = new Stopwatch();
            Set1_Timer.Start();
            Set2_Timer.Start();
            Random rand = new Random();

            int nextSet1Interval = 270000 + rand.Next(-3000, 3001);
           // int nextSet2Interval = 50000 + rand.Next(-5000, 2001);

            while (!token.IsCancellationRequested)
            {
                // 1. Set 1 觸發 (F鍵 = 0x46)
                if (Set1_Timer.ElapsedMilliseconds >= nextSet1Interval)
                {
                    int randomSet1Delay = rand.Next(150, 501);
                    await Task.Delay(randomSet1Delay, token);

                    await SendKeyHardwareAsync(0x46, token);

                    Set1_Timer.Restart();
                    nextSet1Interval = 270000 + rand.Next(-3000, 3001);
                }
                /*
                // 2. Set 2 觸發 (空白鍵 = 0x20)
                if (Set2_Timer.ElapsedMilliseconds >= nextSet2Interval)
                {
                    int randomSet2Delay = rand.Next(150, 301);
                    await Task.Delay(randomSet2Delay, token);

                    await SendKeyHardwareAsync(0x20, token);

                    Set2_Timer.Restart();
                    nextSet2Interval = 50000 + rand.Next(-5000, 2001);
                }*/

                // 3. 一般平砍連點 A 鍵 (0x41)
                await SendKeyHardwareAsync(0x41, token);

                int randomAttackDelay = rand.Next(35, 66);
                await Task.Delay(randomAttackDelay, token);
            }
        }

        // ==========================================
        // 模板二：固定順序排程走位
        private async Task RunTemplate2Async(CancellationToken token)
        {
            Random rand = new Random();
            while (!token.IsCancellationRequested)
            {
                int move_count = 0;
                // ===  按 F 鍵 (F = 0x46) ===
                await SendKeyHardwareAsync(0x46, token);
                await Task.Delay(rand.Next(3001, 4000), token);

                // === 按下空白鍵 (空白 = 0x20) ===
                await SendKeyHardwareAsync(0x20, token);
                // 原固定 5000ms 調整為：4.7秒 ~ 5.3秒 隨機冷卻發呆
                await Task.Delay(rand.Next(4700, 5301), token);

                // === 按下 D 鍵 (D = 0x44) ===
                await SendKeyHardwareAsync(0x44, token);
                // 原固定 1000ms 調整為：0.9秒 ~ 1.2秒 隨機硬直
                await Task.Delay(rand.Next(3500, 4500), token);

                // === 按 C 鍵 (C = 0x43) ===
                await SendKeyHardwareAsync(0x43, token);
                await Task.Delay(rand.Next(3500, 4500), token);

                // === 按 X 鍵 (X = 0x58) ===
                await SendKeyHardwareAsync(0x58, token);
                await Task.Delay(rand.Next(3500, 4500), token);


                move_count++;
                if (move_count >= 3)
                {
                    // 往右兩下 (左箭頭 = 0x25)
                    await SendKeyHardwareAsync(0x25, token);
                    await Task.Delay(Random.Shared.Next(100, 180), token);
                    await SendKeyHardwareAsync(0x25, token);
                    await Task.Delay(Random.Shared.Next(100, 180), token);

                    await Task.Delay(rand.Next(1500, 2101), token);

                    // 往右兩下 (右箭頭 = 0x27)
                    await SendKeyHardwareAsync(0x27, token);
                    await Task.Delay(Random.Shared.Next(100, 180), token);
                    await SendKeyHardwareAsync(0x27, token);
                    await Task.Delay(Random.Shared.Next(100, 180), token);

                    move_count = 0;
                }
                int randomLongDelay = 150000 + rand.Next(-4000, 1001);
                await Task.Delay(randomLongDelay, token);

                /*
                await SendKeyHardwareAsync(0x25, token);
                await Task.Delay(rand.Next(80, 151), token); // 隨機雙擊間隔 80ms ~ 150ms (更像人類手速)
                await SendKeyHardwareAsync(0x25, token);

                // 原固定 3000ms 調整為：2.7秒 ~ 3.3秒 隨機走位停頓
                await Task.Delay(rand.Next(2700, 3301), token);

                // === 按下空白鍵 (空白 = 0x20) ===
                await SendKeyHardwareAsync(0x20, token);
                // 原固定 5000ms 調整為：4.7秒 ~ 5.3秒 隨機冷卻發呆
                await Task.Delay(rand.Next(4700, 5301), token);

                // === 按下 D 鍵 (D = 0x44) ===
                await SendKeyHardwareAsync(0x44, token);
                // 原固定 1000ms 調整為：0.9秒 ~ 1.2秒 隨機硬直
                await Task.Delay(rand.Next(1500, 3001), token);

                // ===  按 F 鍵 (F = 0x46) ===
                await SendKeyHardwareAsync(0x46, token);
                await Task.Delay(rand.Next(1500, 3001), token);

                // === 按 C 鍵 (C = 0x43) ===
                await SendKeyHardwareAsync(0x43, token);
                await Task.Delay(rand.Next(2001, 3001), token);

                // === 按 X 鍵 (X = 0x58) ===
                await SendKeyHardwareAsync(0x58, token);
                await Task.Delay(rand.Next(2000, 3001), token);


                move_count++;
                if (move_count == 3)
                {
                    for (int i = 0; i <= 3; i++)
                    {
                        // 往右兩下 (右箭頭 = 0x27)
                        await SendKeyHardwareAsync(0x27, token);
                        await Task.Delay(rand.Next(80, 151), token); // 隨機雙擊間隔
                        await SendKeyHardwareAsync(0x27, token);

                        // 原固定 500ms 調整為：0.45秒 ~ 0.6秒 每步微調
                        await Task.Delay(rand.Next(450, 601), token);
                    }
                    await SendKeyHardwareAsync(0x39, token);
                    await Task.Delay(rand.Next(900, 1201), token);
                    move_count = 0;
                }
                int randomLongDelay = 250000 + rand.Next(-4000, 4001);
                await Task.Delay(randomLongDelay, token);
                */
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            ExecuteStop();
        }

        private void ExecuteStop()
        {
            if (_cts == null) return;

            // 立刻鎖住 Stop 鍵並更新狀態，避免使用者在停止過程中重複按 F11／Stop
            btnStop.Enabled = false;
            SetStatus("停止中...");
            _cts.Cancel();
        }

        // ==========================================
        // Set 3：依序施放 A > F > X > C 一輪就結束（不會持續循環）
        private async void btnCastSet3_Click(object sender, EventArgs e)
        {
            await CastSet3Async(isAuto: false);
        }

        // F5 熱鍵版本：只有選到 Set 3 時才有作用，避免在 Set 1/2 執行中誤觸
        private async void ExecuteCastSet3Hotkey()
        {
            if (currentTemplate != 3) return;
            await CastSet3Async(isAuto: false);
        }

        private async Task CastSet3Async(bool isAuto)
        {
            if (!btnCastSet3.Enabled) return; // 正在施放中，避免重複觸發

            // 倒數從「按下 F5 的當下」起算，所以在技能序列開始前就先重新計時。
            // 手動施放會把自動模式解除（字轉回黑色），自動補放則維持紅字提醒。
            _set3AutoMode = isAuto;
            StartSet3Countdown();

            btnCastSet3.Enabled = false;
            cmbTemplate.Enabled = false;
            txtWindowTitle.Enabled = false;
            SetStatus("施放中 - Set 3");

            try
            {
                await SendKeyHardwareAsync(0x41, CancellationToken.None); // A
                await Task.Delay(Random.Shared.Next(1000, 3001));
                await SendKeyHardwareAsync(0x46, CancellationToken.None); // F
                await Task.Delay(Random.Shared.Next(1000, 3001));
                await SendKeyHardwareAsync(0x58, CancellationToken.None); // X
                await Task.Delay(Random.Shared.Next(1000, 3001));
                await SendKeyHardwareAsync(0x43, CancellationToken.None); // C
            }
            finally
            {
                btnCastSet3.Enabled = true;
                cmbTemplate.Enabled = true;
                txtWindowTitle.Enabled = true;
                SetStatus("待機中");
            }
        }

        private void StartSet3Countdown()
        {
            _set3CountdownTimer.Stop();
            _set3RemainingSeconds = SET3_COUNTDOWN_SECONDS;
            _set3AlertSecondsLeft = 0;
            UpdateSet3CountdownLabel();
            _set3CountdownTimer.Start();
        }

        private void Set3CountdownTimer_Tick(object? sender, EventArgs e)
        {
            // 警示階段：每秒響一次提示音，最多維持 SET3_ALERT_SECONDS 秒。
            // 期間手動按 F5 會重新計時並解除警示；撐完都沒人理就自動補放一次。
            if (_set3AlertSecondsLeft > 0)
            {
                _set3AlertSecondsLeft--;
                if (_set3AlertSecondsLeft > 0)
                {
                    UpdateSet3CountdownLabel();
                    PlayReminderBeep();
                    return;
                }

                _set3CountdownTimer.Stop();
                _ = CastSet3Async(isAuto: true); // 內部會重新啟動倒數並維持紅字
                return;
            }

            _set3RemainingSeconds--;

            if (_set3RemainingSeconds > 0)
            {
                UpdateSet3CountdownLabel();
                return;
            }

            // 倒數歸零，進入警示階段
            _set3AlertSecondsLeft = SET3_ALERT_SECONDS;
            UpdateSet3CountdownLabel();
            PlayReminderBeep();
        }

        private void UpdateSet3CountdownLabel()
        {
            bool alerting = _set3AlertSecondsLeft > 0;
            string text = alerting
                ? $"時間到！{_set3AlertSecondsLeft} 秒後自動施放"
                : $"倒數：{_set3RemainingSeconds / 60}:{_set3RemainingSeconds % 60:00}";

            // 自動補放過之後字會一直維持紅色，直到自己再按一次 F5 才恢復
            bool highlight = alerting || _set3AutoMode;

            lblSet3Countdown.Text = text;
            lblSet3Countdown.ForeColor = highlight ? Color.Red : SystemColors.ControlText;
            _overlay?.SetText(text);
            _overlay?.SetHighlighted(highlight);
        }

        private void chkOverlay_CheckedChanged(object sender, EventArgs e)
        {
            btnMoveOverlay.Visible = chkOverlay.Checked;

            if (chkOverlay.Checked)
            {
                if (_overlay == null || _overlay.IsDisposed)
                {
                    _overlay = new CountdownOverlayForm();
                    _overlay.Location = GetDefaultOverlayLocation();
                }
                _overlay.Show();
                if (_set3CountdownTimer.Enabled)
                {
                    UpdateSet3CountdownLabel();
                }
                else
                {
                    _overlay.SetText("倒數：未開始");
                    _overlay.SetHighlighted(false);
                }
            }
            else
            {
                _overlay?.Hide();
            }
        }

        // 預設擺在遊戲視窗客戶區的右上角附近；找不到遊戲視窗就擺在主螢幕右上角
        private Point GetDefaultOverlayLocation()
        {
            IntPtr gameHwnd = FindGameWindow();
            if (gameHwnd != IntPtr.Zero)
            {
                Rectangle clientRect = GetGameClientScreenRect(gameHwnd);
                if (clientRect.Width > 0 && clientRect.Height > 0)
                {
                    return new Point(clientRect.Right - 220, clientRect.Y + 20);
                }
            }

            Rectangle screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            return new Point(screen.Right - 220, screen.Y + 20);
        }

        private void btnMoveOverlay_Click(object sender, EventArgs e)
        {
            if (_overlay == null || _overlay.IsDisposed) return;

            // 解鎖時關閉點擊穿透才拖得動，拖好後再鎖回去恢復不擋滑鼠
            _overlay.ClickThrough = !_overlay.ClickThrough;
            btnMoveOverlay.Text = _overlay.ClickThrough ? "解鎖位置（可拖曳）" : "鎖定位置（完成拖曳）";
        }

        // 每次呼叫響一短聲（由計時器每秒觸發一次）。
        // Console.Beep 是同步阻塞的，丟到背景執行緒才不會卡住整個介面。
        private static void PlayReminderBeep()
        {
            Task.Run(() =>
            {
                try
                {
                    Console.Beep(1000, 200);
                }
                catch
                {
                    // 某些環境（例如沒有喇叭裝置）呼叫 Beep 會失敗，忽略即可
                }
            });
        }

        // ==========================================
        // MP 監控：抓螢幕上 MP 條那塊區域的顏色，量測填滿比例，低於門檻就按補 MP 鍵

        // 用標題關鍵字找遊戲視窗，不管它目前是不是前景視窗
        // （校準當下使用者點的是本工具的按鈕，前景視窗會是本工具自己，不是遊戲）
        private IntPtr FindGameWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                // 【重要】排除本工具自己的視窗：標題「MapleStory 控制器」同樣包含關鍵字，
                // 若不排除，校準時會把縮到最小的自己當成遊戲視窗，取到 0x0 的客戶區而崩潰。
                if (hWnd == this.Handle) return true;
                if (_overlay != null && !_overlay.IsDisposed && hWnd == _overlay.Handle) return true;

                // 最小化的視窗客戶區是 0x0，一樣不能拿來當擷取目標
                if (IsIconic(hWnd)) return true;

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.Length > 0 && sb.ToString().IndexOf(_targetWindowKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    GetClientRect(hWnd, out RECT rect);
                    if (rect.Right - rect.Left <= 0 || rect.Bottom - rect.Top <= 0) return true;

                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static Rectangle GetGameClientScreenRect(IntPtr hWnd)
        {
            GetClientRect(hWnd, out RECT rect);
            POINT topLeft = new POINT { X = 0, Y = 0 };
            ClientToScreen(hWnd, ref topLeft);
            return new Rectangle(topLeft.X, topLeft.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        private async void btnCalibrateMp_Click(object sender, EventArgs e)
        {
            IntPtr gameHwnd = FindGameWindow();
            if (gameHwnd == IntPtr.Zero)
            {
                MessageBox.Show($"找不到標題包含「{_targetWindowKeyword}」的視窗，請確認遊戲已開啟。");
                return;
            }

            MessageBox.Show("【重要】請先把 MP 補到全滿，再進行校準。\n\n"
                          + "接著框選「MP 藍色橫條」，大概框到就好：\n"
                          + "程式會自動找出藍色範圍當作 100% 的基準，\n"
                          + "多框到數字、邊框、背景都會自動忽略。");

            // 先縮小工具視窗，避免半透明覆蓋層上還疊著自己的視窗擋住遊戲畫面
            this.WindowState = FormWindowState.Minimized;

            Rectangle selected;
            bool confirmed;
            using (var selector = new RegionSelectorForm())
            {
                confirmed = selector.ShowDialog() == DialogResult.OK;
                selected = selector.SelectedRectangle;
            }

            if (!confirmed || selected.Width < 3 || selected.Height < 1)
            {
                this.WindowState = FormWindowState.Normal;
                if (confirmed) MessageBox.Show("選取範圍太小，請重新框選。");
                return;
            }

            try
            {
                Rectangle clientRect = GetGameClientScreenRect(gameHwnd);
                if (clientRect.Width <= 0 || clientRect.Height <= 0)
                {
                    this.WindowState = FormWindowState.Normal;
                    MessageBox.Show("取得遊戲視窗範圍失敗，請確認遊戲視窗沒有被最小化。");
                    return;
                }

                _mpColorsLearned = false; // 重新校準等於重新學習血條顏色
                _mpBarCalibrated = false;

                // 【重要】以下的擷取都要趁工具視窗還縮小著的時候做。
                // 若先還原視窗再擷取，工具視窗可能正好蓋住遊戲的血條，
                // 抓到的會是工具視窗自己，導致量到錯誤的數值。
                await Task.Delay(300); // 等覆蓋層消失、遊戲畫面重繪完成

                bool ok = TryCalibrateBar(selected, out Rectangle barRect, out string failReason);

                this.WindowState = FormWindowState.Normal;

                if (!ok)
                {
                    lblMpStatus.Text = $"校準失敗：{failReason}";
                    return;
                }

                _mpBarRelativeRect = new Rectangle(
                    barRect.X - clientRect.X,
                    barRect.Y - clientRect.Y,
                    barRect.Width,
                    barRect.Height);
                _mpBarCalibrated = true;

                lblMpStatus.Text = $"校準成功：偵測到血條 {barRect.Width}x{barRect.Height}px（此刻視為 100%）";
            }
            catch (Exception ex)
            {
                // 校準流程跑在 async void 上，未攔截的例外會直接讓整個程式崩潰，
                // 這裡收斂成畫面上的錯誤訊息，方便回報也不會中斷使用。
                this.WindowState = FormWindowState.Normal;
                _mpBarCalibrated = false;
                lblMpStatus.Text = $"校準發生錯誤：{ex.Message}";
            }
        }

        private void chkMpMonitor_CheckedChanged(object sender, EventArgs e)
        {
            if (chkMpMonitor.Checked)
            {
                if (!_mpBarCalibrated)
                {
                    MessageBox.Show("請先按「校準 MP 範圍」設定 MP 條位置。");
                    chkMpMonitor.Checked = false;
                    return;
                }

                _mpThresholdPercent = (int)numMpThreshold.Value;
                _mpPotionKey = PotionKeyMap[(string)cmbMpPotionKey.SelectedItem];
                numMpThreshold.Enabled = false;
                cmbMpPotionKey.Enabled = false;
                btnCalibrateMp.Enabled = false;

                _mpMonitorCts = new CancellationTokenSource();
                _ = Task.Run(() => MpMonitorLoopAsync(_mpMonitorCts.Token));
            }
            else
            {
                _mpMonitorCts?.Cancel();
                _mpMonitorCts = null;
                numMpThreshold.Enabled = true;
                cmbMpPotionKey.Enabled = true;
                btnCalibrateMp.Enabled = true;
                lblMpStatus.Text = "MP 監控已停止";
            }
        }

        private async Task MpMonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 螢幕擷取抓到的是「目前顯示在螢幕上的畫面」，遊戲沒在前景時抓到的
                    // 會是別的視窗內容，所以跟送按鍵一樣，只有遊戲在前景時才量測。
                    if (IsGameWindowActive())
                    {
                        IntPtr gameHwnd = FindGameWindow();
                        if (gameHwnd != IntPtr.Zero)
                        {
                            Rectangle clientRect = GetGameClientScreenRect(gameHwnd);
                            Rectangle mpScreenRect = new Rectangle(
                                clientRect.X + _mpBarRelativeRect.X,
                                clientRect.Y + _mpBarRelativeRect.Y,
                                _mpBarRelativeRect.Width,
                                _mpBarRelativeRect.Height);

                            MpReading reading = MeasureMp(mpScreenRect);
                            if (reading.Success)
                            {
                                if (IsHandleCreated)
                                {
                                    BeginInvoke(new Action(() => lblMpStatus.Text = $"目前 MP {reading.Percent:0}%"));
                                }

                                if (reading.Percent < _mpThresholdPercent)
                                {
                                    await SendKeyHardwareAsync(_mpPotionKey, token);
                                    await Task.Delay(2000, token); // 喝藥冷卻，避免連續狂按
                                }
                            }
                            else if (IsHandleCreated)
                            {
                                // 量不到就不動作，避免依據錯誤數值亂按藥水
                                BeginInvoke(new Action(() => lblMpStatus.Text = $"量測失敗：{reading.FailReason}"));
                            }
                        }
                    }

                    await Task.Delay(500, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 單次螢幕擷取失敗（例如視窗剛好在切換），略過這輪，下一輪再試
                }
            }
        }

        private sealed class MpReading
        {
            public bool Success;
            public double Percent;
            public string FailReason = string.Empty;
        }

        // 判斷一個像素是不是「血條填滿色」。用色相（Hue）比對而不是整個 RGB 距離，
        // 因為 MapleStory 的血條有一格格的分隔線和亮度漸層——同樣是藍色但深淺不同，
        // 用 RGB 距離會被判成不同顏色，用色相就能全部視為同一種填滿色。
        private bool IsFilledPixel(Color c)
        {
            float saturation = c.GetSaturation();
            float brightness = c.GetBrightness();
            if (saturation < _mpRefSaturation * 0.45f) return false; // 太灰（空槽、白字）
            if (brightness < 0.12f || brightness > 0.97f) return false; // 太暗或過曝

            float hueDiff = Math.Abs(c.GetHue() - _mpRefHue);
            if (hueDiff > 180f) hueDiff = 360f - hueDiff; // 色相是環狀的
            return hueDiff <= 28f;
        }

        // 量測血條填滿比例：逐「行」統計有多少像素屬於填滿色，
        // 密度夠高的行才算填滿，再取最右邊那一行的位置當作填滿長度。
        // 用「最右邊」而不是「連續長度」，所以分隔線造成的斷點完全不影響。
        private MpReading MeasureMp(Rectangle screenRect)
        {
            var reading = new MpReading();
            if (screenRect.Width <= 8 || screenRect.Height < 1)
            {
                reading.FailReason = "血條範圍太小";
                return reading;
            }
            if (!_mpColorsLearned)
            {
                reading.FailReason = "尚未校準血條顏色";
                return reading;
            }

            using var bmp = new Bitmap(screenRect.Width, screenRect.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenRect.Location, Point.Empty, screenRect.Size);
            }

            int minCount = Math.Max(1, screenRect.Height / 3);
            int rightmostFilled = -1;

            for (int x = 0; x < screenRect.Width; x++)
            {
                int count = 0;
                for (int y = 0; y < screenRect.Height; y++)
                {
                    if (IsFilledPixel(bmp.GetPixel(x, y))) count++;
                }
                if (count >= minCount) rightmostFilled = x;
            }

            reading.Percent = (rightmostFilled + 1) * 100.0 / screenRect.Width;
            reading.Success = true;
            return reading;
        }

        // 校準（要求 MP 全滿時執行）：在框選範圍內找出佔比最高的鮮豔色相當作填滿色，
        // 再取所有符合該色相的像素的外接矩形——因為此刻血條是滿的，
        // 這個矩形就等於整條血條的完整範圍，不必再猜血條右端在哪裡。
        private bool TryCalibrateBar(Rectangle frameScreenRect, out Rectangle barScreenRect, out string failReason)
        {
            barScreenRect = frameScreenRect;
            failReason = string.Empty;

            if (frameScreenRect.Width <= 0 || frameScreenRect.Height <= 0)
            {
                failReason = "框選範圍無效";
                return false;
            }

            using var bmp = new Bitmap(frameScreenRect.Width, frameScreenRect.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(frameScreenRect.Location, Point.Empty, frameScreenRect.Size);
            }

            // 以 10 度為一格統計鮮豔像素的色相分布，最多的那一格就是血條顏色
            var hueBuckets = new int[36];
            var hueSums = new double[36];
            var saturationSums = new double[36];

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    float s = c.GetSaturation();
                    float b = c.GetBrightness();
                    if (s < 0.35f || b < 0.15f || b > 0.95f) continue;

                    int bucket = Math.Min(35, (int)(c.GetHue() / 10f));
                    hueBuckets[bucket]++;
                    hueSums[bucket] += c.GetHue();
                    saturationSums[bucket] += s;
                }
            }

            int bestBucket = 0;
            for (int i = 1; i < hueBuckets.Length; i++)
            {
                if (hueBuckets[i] > hueBuckets[bestBucket]) bestBucket = i;
            }

            if (hueBuckets[bestBucket] < 20)
            {
                failReason = "框選範圍內找不到鮮豔的血條顏色，請確認有框到藍色橫條";
                return false;
            }

            _mpRefHue = (float)(hueSums[bestBucket] / hueBuckets[bestBucket]);
            _mpRefSaturation = (float)(saturationSums[bestBucket] / hueBuckets[bestBucket]);
            _mpColorsLearned = true;

            // 取所有符合該色相的像素外接矩形 = 滿血時的血條範圍
            int left = int.MaxValue, right = int.MinValue, top = int.MaxValue, bottom = int.MinValue;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (!IsFilledPixel(bmp.GetPixel(x, y))) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }

            if (right - left + 1 < 8 || bottom - top + 1 < 1)
            {
                _mpColorsLearned = false;
                failReason = "偵測到的血條太小，請重新框選";
                return false;
            }

            barScreenRect = new Rectangle(
                frameScreenRect.X + left,
                frameScreenRect.Y + top,
                right - left + 1,
                bottom - top + 1);
            return true;
        }
    }
}