using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MapleStory
{
    // 顯示在畫面最上層的倒數小視窗。
    // 預設「點擊穿透」：滑鼠點擊會直接穿過去給下面的遊戲，不會擋到操作；
    // 同時加上 WS_EX_NOACTIVATE，確保它永遠不會搶走焦點——
    // 否則一旦它變成前景視窗，主程式的「遊戲視窗使用中」判斷就會失效而停止送鍵。
    public class CountdownOverlayForm : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private readonly Label _label;
        private bool _clickThrough = true;
        private Point _dragOffset;
        private bool _dragging;

        public CountdownOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0.72;
            Size = new Size(190, 52);
            Text = string.Empty; // 標題留空，避免被主程式的視窗關鍵字搜尋誤判成遊戲視窗

            _label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Text = "倒數：--:--"
            };
            Controls.Add(_label);

            // 可拖曳模式時，點在文字上也要能拖動視窗
            _label.MouseDown += OnDragStart;
            _label.MouseMove += OnDragMove;
            _label.MouseUp += OnDragEnd;
            MouseDown += OnDragStart;
            MouseMove += OnDragMove;
            MouseUp += OnDragEnd;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        // 讓視窗不會因為被點到而變成前景視窗
        protected override bool ShowWithoutActivation => true;

        public void SetText(string text) => _label.Text = text;

        public void SetHighlighted(bool highlighted)
        {
            _label.ForeColor = highlighted ? Color.OrangeRed : Color.White;
        }

        public bool ClickThrough
        {
            get => _clickThrough;
            set
            {
                _clickThrough = value;
                if (!IsHandleCreated) return;

                int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
                exStyle = value ? exStyle | WS_EX_TRANSPARENT : exStyle & ~WS_EX_TRANSPARENT;
                SetWindowLong(Handle, GWL_EXSTYLE, exStyle);
                Cursor = value ? Cursors.Default : Cursors.SizeAll;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ClickThrough = _clickThrough; // handle 建立後才能真正套用樣式
        }

        private void OnDragStart(object? sender, MouseEventArgs e)
        {
            if (_clickThrough || e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragOffset = e.Location;
        }

        private void OnDragMove(object? sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y);
        }

        private void OnDragEnd(object? sender, MouseEventArgs e) => _dragging = false;
    }
}
