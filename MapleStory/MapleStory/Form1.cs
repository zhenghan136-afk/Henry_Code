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

        public Form1()
        {
            InitializeComponent();
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
            btnCastSet3.Visible = currentTemplate == 3;
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
            await CastSet3Async();
        }

        // F5 熱鍵版本：只有選到 Set 3 時才有作用，避免在 Set 1/2 執行中誤觸
        private async void ExecuteCastSet3Hotkey()
        {
            if (currentTemplate != 3) return;
            await CastSet3Async();
        }

        private async Task CastSet3Async()
        {
            if (!btnCastSet3.Enabled) return; // 正在施放中，避免重複觸發

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

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.Length > 0 && sb.ToString().IndexOf(_targetWindowKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
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

        private void btnCalibrateMp_Click(object sender, EventArgs e)
        {
            IntPtr gameHwnd = FindGameWindow();
            if (gameHwnd == IntPtr.Zero)
            {
                MessageBox.Show($"找不到標題包含「{_targetWindowKeyword}」的視窗，請確認遊戲已開啟。");
                return;
            }

            // 先縮小工具視窗，避免半透明覆蓋層上還疊著自己的視窗擋住遊戲畫面
            this.WindowState = FormWindowState.Minimized;

            Rectangle selected;
            using (var selector = new RegionSelectorForm())
            {
                bool confirmed = selector.ShowDialog() == DialogResult.OK;
                selected = selector.SelectedRectangle;
                this.WindowState = FormWindowState.Normal;
                if (!confirmed) return;
            }

            if (selected.Width < 3 || selected.Height < 1)
            {
                MessageBox.Show("選取範圍太小，請重新框選 MP 條。");
                return;
            }

            Rectangle clientRect = GetGameClientScreenRect(gameHwnd);
            _mpBarRelativeRect = new Rectangle(
                selected.X - clientRect.X,
                selected.Y - clientRect.Y,
                selected.Width,
                selected.Height);
            _mpBarCalibrated = true;
            lblMpStatus.Text = $"MP 範圍已校準（{_mpBarRelativeRect.Width}x{_mpBarRelativeRect.Height}）";
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

                            double percent = MeasureBarFillPercent(mpScreenRect);
                            if (percent >= 0)
                            {
                                if (IsHandleCreated)
                                {
                                    BeginInvoke(new Action(() => lblMpStatus.Text = $"目前 MP 約 {percent:0}%"));
                                }

                                if (percent < _mpThresholdPercent)
                                {
                                    await SendKeyHardwareAsync(_mpPotionKey, token);
                                    await Task.Delay(2000, token); // 喝藥冷卻，避免連續狂按
                                }
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

        // 抓區域畫面後掃描。MapleStory 的血條數字（例如 4145/4285）通常疊在血條正中央，
        // 只掃水平正中線常常會掃到文字筆畫、把掃描線提前中斷在文字上，導致量到的百分比
        // 永遠偏低。改成分別在「偏上」「偏下」各掃一條線，取比較高的那個值——只要其中一條
        // 沒被文字擋到，就能量到正確的填滿比例（掃描中斷只會讓數值偏低，不會偏高，所以取
        // 兩者較高值是安全的）。
        private static double MeasureBarFillPercent(Rectangle screenRect)
        {
            if (screenRect.Width <= 4 || screenRect.Height <= 1) return -1;

            using var bmp = new Bitmap(screenRect.Width, screenRect.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenRect.Location, Point.Empty, screenRect.Size);
            }

            int yTop = Math.Max(0, screenRect.Height / 4);
            int yBottom = Math.Min(screenRect.Height - 1, screenRect.Height - 1 - screenRect.Height / 4);

            double percentTop = MeasureRowFillPercent(bmp, screenRect.Width, yTop);
            double percentBottom = MeasureRowFillPercent(bmp, screenRect.Width, yBottom);

            return Math.Max(percentTop, percentBottom);
        }

        // 同時取「靠左」跟「靠右」兩個內縮取樣點的顏色當基準（分別代表填滿色／底色），
        // 每個像素就近判斷比較接近哪一邊，找出最後一個判定為「填滿」的位置。
        // 內縮是為了避開框選範圍可能多框到的血條外框/邊線像素。
        private static double MeasureRowFillPercent(Bitmap bmp, int width, int y)
        {
            int inset = Math.Max(1, Math.Min(3, width / 10));
            Color filledColor = bmp.GetPixel(inset, y);
            Color emptyColor = bmp.GetPixel(width - 1 - inset, y);

            int lastFilledX = inset;
            for (int x = inset; x < width - inset; x++)
            {
                Color c = bmp.GetPixel(x, y);
                if (ColorDistance(c, filledColor) <= ColorDistance(c, emptyColor))
                {
                    lastFilledX = x;
                }
                else
                {
                    break;
                }
            }

            return (lastFilledX + 1) * 100.0 / width;
        }

        private static double ColorDistance(Color a, Color b)
        {
            int dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}