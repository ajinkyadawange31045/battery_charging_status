using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BatteryAlarmApp.Controls
{
    public class ModernSlider : Control
    {
        private int _minimum = 0;
        private int _maximum = 100;
        private int _value = 50;
        private bool _isDragging = false;
        private bool _isHovered = false;

        public int Minimum
        {
            get { return _minimum; }
            set { _minimum = value; Invalidate(); }
        }

        public int Maximum
        {
            get { return _maximum; }
            set { _maximum = value; Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Invalidate();
                    if (ValueChanged != null)
                        ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        public Color AccentColor { get; set; }
        public Color InactiveTrackColor { get; set; }

        public event EventHandler ValueChanged;

        public ModernSlider()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(200, 26);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            AccentColor = Color.FromArgb(16, 185, 129);
            InactiveTrackColor = Color.FromArgb(226, 232, 240); // Slate 200
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                UpdateValueFromMouse(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdateValueFromMouse(e.X);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isDragging = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        private void UpdateValueFromMouse(int mouseX)
        {
            float thumbRadius = 8f;
            float trackLeft = thumbRadius + 2;
            float trackRight = Width - thumbRadius - 2;
            float trackWidth = trackRight - trackLeft;

            if (trackWidth <= 0) return;

            float ratio = (mouseX - trackLeft) / trackWidth;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            Value = (int)Math.Round(_minimum + ratio * (_maximum - _minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width;
            int h = Height;

            float thumbRadius = 9f;
            float trackH = 6f;
            float trackLeft = thumbRadius + 2;
            float trackRight = w - thumbRadius - 2;
            float trackWidth = trackRight - trackLeft;
            float trackY = (h - trackH) / 2f;

            float range = _maximum - _minimum;
            float ratio = range > 0 ? (float)(_value - _minimum) / range : 0f;
            float thumbX = trackLeft + (trackWidth * ratio);
            float thumbY = h / 2f;

            // 1. Inactive Track
            RectangleF trackRect = new RectangleF(trackLeft, trackY, trackWidth, trackH);
            using (GraphicsPath trackPath = BatteryWidget.RoundedRect(trackRect, trackH / 2f))
            using (SolidBrush trackBrush = new SolidBrush(InactiveTrackColor))
            {
                g.FillPath(trackBrush, trackPath);
            }

            // 2. Active Fill Track
            float fillWidth = thumbX - trackLeft;
            if (fillWidth > 0)
            {
                RectangleF fillRect = new RectangleF(trackLeft, trackY, fillWidth, trackH);
                using (GraphicsPath fillPath = BatteryWidget.RoundedRect(fillRect, trackH / 2f))
                using (SolidBrush fillBrush = new SolidBrush(AccentColor))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }

            // 3. Thumb Shadow & Circle
            float thumbSize = thumbRadius * 2f;
            RectangleF thumbRect = new RectangleF(thumbX - thumbRadius, thumbY - thumbRadius, thumbSize, thumbSize);

            // Subtle drop shadow
            RectangleF shadowRect = new RectangleF(thumbRect.X, thumbRect.Y + 1.5f, thumbSize, thumbSize);
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, shadowRect);
            }

            // Thumb Fill
            using (SolidBrush thumbFill = new SolidBrush(Color.White))
            {
                g.FillEllipse(thumbFill, thumbRect);
            }

            // Thumb Border
            Color thumbBorder = (_isHovered || _isDragging) ? AccentColor : Color.FromArgb(148, 163, 184);
            using (Pen thumbPen = new Pen(thumbBorder, 2.5f))
            {
                g.DrawEllipse(thumbPen, thumbRect);
            }
        }
    }
}
