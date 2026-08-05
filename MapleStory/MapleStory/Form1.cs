using System;
using System.Diagnostics;
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

        private CancellationTokenSource _cts;
        private int currentTemplate = 1;
        private string _targetWindowKeyword = "MapleStory";
        private bool _lastFocusState = true;

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

            cmbTemplate.Items.AddRange(new object[] { "Set 1", "Set 2" });
            cmbTemplate.SelectedIndex = 0;
            btnStop.Enabled = false;
            SetStatus("待機中");
        }

        private void cmbTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentTemplate = cmbTemplate.SelectedIndex + 1;
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
            UnregisterHotKey(this.Handle, STOP_HOTKEY_ID);
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == STOP_HOTKEY_ID)
            {
                ExecuteStop();
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
    }
}