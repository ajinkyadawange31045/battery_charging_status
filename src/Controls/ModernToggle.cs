using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BatteryAlarmApp.Controls
{
    public class ModernToggle : Control
    {
        private bool _checked = false;
        public Color OnColor { get; set; }
        public Color OffColor { get; set; }

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    if (CheckedChanged != null)
                        CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public ModernToggle()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(46, 24);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            OnColor = Color.FromArgb(16, 185, 129); // Vibrant Emerald
            OffColor = Color.FromArgb(203, 213, 225); // Slate 300
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width;
            int h = Height;
            float radius = (h - 2) / 2f;

            // Draw Track Capsule
            RectangleF trackRect = new RectangleF(1, 1, w - 2, h - 2);
            using (GraphicsPath path = GetCapsulePath(trackRect, radius))
            {
                Color trackColor = _checked ? OnColor : OffColor;
                using (SolidBrush brush = new SolidBrush(trackColor))
                {
                    g.FillPath(brush, path);
                }
                if (!_checked)
                {
                    using (Pen pen = new Pen(Color.FromArgb(148, 163, 184), 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            // Draw Thumb Circle
            float thumbSize = h - 6;
            float thumbX = _checked ? (w - thumbSize - 3) : 3;
            float thumbY = 3;

            RectangleF thumbRect = new RectangleF(thumbX, thumbY, thumbSize, thumbSize);

            // Subtle drop shadow
            RectangleF shadowRect = new RectangleF(thumbX, thumbY + 1, thumbSize, thumbSize);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            {
                g.FillEllipse(shadow, shadowRect);
            }

            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(thumbBrush, thumbRect);
            }
        }

        private GraphicsPath GetCapsulePath(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            RectangleF arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
