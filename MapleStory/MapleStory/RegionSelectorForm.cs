using System;
using System.Drawing;
using System.Windows.Forms;

namespace MapleStory
{
    // 讓使用者用滑鼠拖曳框選螢幕上一塊區域（例如 MP 血條），用來校準座標。
    // 覆蓋整個虛擬螢幕、半透明、永遠置頂，拖曳放開滑鼠即完成選取，按 Esc 取消。
    public class RegionSelectorForm : Form
    {
        public Rectangle SelectedRectangle { get; private set; }

        private Point _startPoint;
        private Rectangle _currentRect;
        private bool _dragging;

        public RegionSelectorForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            Bounds = SystemInformation.VirtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Opacity = 0.35;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            KeyPreview = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            _dragging = true;
            _startPoint = e.Location;
            _currentRect = new Rectangle(e.Location, Size.Empty);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            _currentRect = NormalizeRect(_startPoint, e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging || e.Button != MouseButtons.Left) return;
            _dragging = false;

            // _currentRect 是相對於這個覆蓋整個虛擬螢幕的視窗的座標，換算回實際螢幕座標
            SelectedRectangle = new Rectangle(
                Bounds.X + _currentRect.X,
                Bounds.Y + _currentRect.Y,
                _currentRect.Width,
                _currentRect.Height);

            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_currentRect.Width <= 0 || _currentRect.Height <= 0) return;

            using var brush = new SolidBrush(Color.FromArgb(60, Color.Lime));
            using var pen = new Pen(Color.Lime, 2);
            e.Graphics.FillRectangle(brush, _currentRect);
            e.Graphics.DrawRectangle(pen, _currentRect);
        }

        private static Rectangle NormalizeRect(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            int w = Math.Abs(a.X - b.X);
            int h = Math.Abs(a.Y - b.Y);
            return new Rectangle(x, y, w, h);
        }
    }
}
