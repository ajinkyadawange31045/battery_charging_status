using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BatteryAlarmApp
{
    public class BatteryWidget : Control
    {
        private int _percent = 50;
        private bool _isPluggedIn = true;
        private bool _isCharging = true;
        private bool _hasBattery = true;
        private string _statusText = "Plugged In (Charging)";
        private string _detailText = "";

        public BatteryWidget()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        }

        public void UpdateData(BatterySnapshot snap)
        {
            _hasBattery = snap.HasBattery;
            _percent = snap.Percent;
            _isPluggedIn = snap.IsPluggedIn;
            _isCharging = snap.IsCharging;
            _statusText = snap.GetStatusDescription();
            _detailText = snap.GetRemainingTimeString();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            if (!_hasBattery)
            {
                DrawNoBattery(g, w, h);
                return;
            }

            // Layout calculations
            int batWidth = 120;
            int batHeight = 56;
            int batX = 24;
            int batY = (h - batHeight) / 2 - 4;

            // Draw battery body
            DrawBatteryGraphic(g, batX, batY, batWidth, batHeight, _percent, _isPluggedIn);

            // Draw text section next to the battery graphic
            int textX = batX + batWidth + 30;
            int textY = 16;

            // Large percentage text
            string percentStr = _percent.ToString() + "%";
            using (Font fontBig = new Font("Segoe UI", 28f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(240, 246, 252)))
            {
                g.DrawString(percentStr, fontBig, textBrush, textX, textY);
            }

            // Status badge text
            int badgeY = textY + 46;
            Color badgeBgColor;
            Color badgeTextColor = Color.White;
            string badgeText;

            if (_isPluggedIn)
            {
                badgeBgColor = Color.FromArgb(16, 185, 129); // Emerald Green
                badgeText = (_percent >= 100) ? "⚡ Fully Charged" : "⚡ Plugged In (Charging)";
            }
            else if (_percent <= 15)
            {
                badgeBgColor = Color.FromArgb(239, 68, 68); // Red
                badgeText = "⚠️ Low Battery (Discharging)";
            }
            else if (_percent <= 25)
            {
                badgeBgColor = Color.FromArgb(245, 158, 11); // Amber
                badgeText = "🔋 On Battery (Low)";
            }
            else
            {
                badgeBgColor = Color.FromArgb(59, 130, 246); // Blue
                badgeText = "🔋 On Battery (Normal)";
            }

            // Draw pill badge
            using (Font badgeFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                SizeF badgeSize = g.MeasureString(badgeText, badgeFont);
                RectangleF badgeRect = new RectangleF(textX, badgeY, badgeSize.Width + 18, badgeSize.Height + 6);
                using (GraphicsPath badgePath = RoundedRect(badgeRect, 6f))
                using (SolidBrush badgeBrush = new SolidBrush(badgeBgColor))
                {
                    g.FillPath(badgeBrush, badgePath);
                }
                using (SolidBrush textBrush = new SolidBrush(badgeTextColor))
                {
                    g.DrawString(badgeText, badgeFont, textBrush, textX + 9, badgeY + 3);
                }
            }

            // Secondary detail text (e.g. time remaining or advice)
            int detailY = badgeY + 30;
            string subText = _detailText;
            if (string.IsNullOrEmpty(subText))
            {
                subText = _isPluggedIn ? "Power connected to AC outlet" : "Running on battery power";
            }

            using (Font subFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                g.DrawString(subText, subFont, subBrush, textX, detailY);
            }
        }

        private void DrawBatteryGraphic(Graphics g, int x, int y, int w, int h, int percent, bool plugged)
        {
            float radius = 8f;
            RectangleF bodyRect = new RectangleF(x, y, w - 8, h);

            // Outer chassis
            using (GraphicsPath chassisPath = RoundedRect(bodyRect, radius))
            {
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(30, 36, 46)))
                {
                    g.FillPath(bgBrush, chassisPath);
                }
                using (Pen borderPen = new Pen(Color.FromArgb(71, 85, 105), 2.5f))
                {
                    g.DrawPath(borderPen, chassisPath);
                }
            }

            // Positive terminal knob on the right
            float knobW = 6;
            float knobH = h * 0.40f;
            float knobX = x + w - 8;
            float knobY = y + (h - knobH) / 2f;
            RectangleF knobRect = new RectangleF(knobX, knobY, knobW, knobH);
            using (GraphicsPath knobPath = RoundedRect(knobRect, 2.5f))
            using (SolidBrush knobBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
            {
                g.FillPath(knobBrush, knobPath);
            }

            // Fill bar inside chassis
            float pad = 4f;
            float maxFillW = bodyRect.Width - (pad * 2);
            float fillW = Math.Max(4f, maxFillW * (percent / 100f));
            float fillH = bodyRect.Height - (pad * 2);

            RectangleF fillRect = new RectangleF(bodyRect.X + pad, bodyRect.Y + pad, fillW, fillH);

            Color color1, color2;
            if (plugged)
            {
                color1 = Color.FromArgb(52, 211, 153);
                color2 = Color.FromArgb(16, 185, 129);
            }
            else if (percent <= 15)
            {
                color1 = Color.FromArgb(248, 113, 113);
                color2 = Color.FromArgb(220, 38, 38);
            }
            else if (percent <= 25)
            {
                color1 = Color.FromArgb(251, 191, 36);
                color2 = Color.FromArgb(217, 119, 6);
            }
            else
            {
                color1 = Color.FromArgb(52, 211, 153);
                color2 = Color.FromArgb(16, 185, 129);
            }

            using (GraphicsPath fillPath = RoundedRect(fillRect, 5f))
            using (LinearGradientBrush grad = new LinearGradientBrush(fillRect, color1, color2, LinearGradientMode.Horizontal))
            {
                g.FillPath(grad, fillPath);
            }

            // Charging lightning bolt overlay
            if (plugged)
            {
                float cx = bodyRect.X + bodyRect.Width / 2f;
                float cy = bodyRect.Y + bodyRect.Height / 2f;
                float bs = 16f;

                PointF[] boltPts = new PointF[] {
                    new PointF(cx + bs * 0.15f, cy - bs * 0.65f),
                    new PointF(cx - bs * 0.40f, cy + bs * 0.05f),
                    new PointF(cx - bs * 0.05f, cy + bs * 0.05f),
                    new PointF(cx - bs * 0.15f, cy + bs * 0.65f),
                    new PointF(cx + bs * 0.40f, cy - bs * 0.05f),
                    new PointF(cx + bs * 0.05f, cy - bs * 0.05f)
                };

                using (SolidBrush boltShadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                {
                    PointF[] shadowPts = new PointF[boltPts.Length];
                    for (int i = 0; i < boltPts.Length; i++)
                        shadowPts[i] = new PointF(boltPts[i].X + 1, boltPts[i].Y + 1);
                    g.FillPolygon(boltShadow, shadowPts);
                }

                using (SolidBrush boltBrush = new SolidBrush(Color.White))
                {
                    g.FillPolygon(boltBrush, boltPts);
                }
            }
        }

        private void DrawNoBattery(Graphics g, int w, int h)
        {
            using (Font font = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(240, 246, 252)))
            {
                g.DrawString("Desktop Mode: Running on AC Power", font, textBrush, 24, 24);
            }
            using (Font sub = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                g.DrawString("No system battery detected. Threshold alarms will remain inactive.", sub, subBrush, 24, 52);
            }
        }

        public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            if (diameter > bounds.Width) diameter = bounds.Width;
            if (diameter > bounds.Height) diameter = bounds.Height;

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

    public class ModernButton : Button
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public float CornerRadius { get; set; }

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            NormalColor = Color.FromArgb(37, 99, 235);
            HoverColor = Color.FromArgb(59, 130, 246);
            PressedColor = Color.FromArgb(29, 78, 216);
            CornerRadius = 6f;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
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

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _isPressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color currentBg = !Enabled ? Color.FromArgb(50, 56, 68) :
                              _isPressed ? PressedColor :
                              _isHovered ? HoverColor : NormalColor;

            RectangleF rect = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = BatteryWidget.RoundedRect(rect, CornerRadius))
            using (SolidBrush brush = new SolidBrush(currentBg))
            {
                g.FillPath(brush, path);
            }

            Color textColor = Enabled ? ForeColor : Color.FromArgb(120, 130, 145);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
