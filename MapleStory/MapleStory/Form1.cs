using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

        // 熱鍵相關常數
        const int WM_HOTKEY = 0x0312;
        const int STOP_HOTKEY_ID = 7777;
        const uint VK_F11 = 0x7A;

        private CancellationTokenSource _cts;

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
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
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

        // --- 非同步硬體級模擬 ---
        private async Task SendKeyHardwareAsync(ushort vkCode, CancellationToken token)
        {
            ushort scanCode = (ushort)MapVirtualKey(vkCode, 0);

            INPUT inputDown = new INPUT();
            inputDown.type = INPUT_KEYBOARD;
            inputDown.ki = new KEYBDINPUT { wVk = 0, wScan = scanCode, dwFlags = KEYEVENTF_SCANCODE, time = 0, dwExtraInfo = IntPtr.Zero };

            INPUT inputUp = new INPUT();
            inputUp.type = INPUT_KEYBOARD;
            inputUp.ki = new KEYBDINPUT { wVk = 0, wScan = scanCode, dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP, time = 0, dwExtraInfo = IntPtr.Zero };

            SendInput(1, new INPUT[] { inputDown }, Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(25, token);
            SendInput(1, new INPUT[] { inputUp }, Marshal.SizeOf(typeof(INPUT)));
        }

        // --- 開始按鈕事件 ---
        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            btnStart.Enabled = false;
            btnStop.Enabled = true;

            try
            {
                // 等待 3 秒讓玩家切換視窗
                await Task.Delay(3000, _cts.Token);
                int currentTemplate = 1;

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
                btnStop.Enabled = true;
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
            if (_cts != null)
            {
                _cts.Cancel();
            }
        }
    }
}