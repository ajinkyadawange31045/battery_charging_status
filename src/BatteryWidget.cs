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

            // Layout calculations with responsive padding
            int batWidth = Math.Min(130, Math.Max(100, w / 4));
            int batHeight = 60;
            int batX = 20;
            int batY = (h - batHeight) / 2 - 2;

            // 1. Draw modern sleek battery graphic
            DrawBatteryGraphic(g, batX, batY, batWidth, batHeight, _percent, _isPluggedIn);

            // 2. Text layout next to battery
            int textX = batX + batWidth + 26;
            int textY = 14;

            // High-contrast percentage typography
            string percentStr = _percent.ToString() + "%";
            using (Font fontBig = new Font("Segoe UI", 30f, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(15, 23, 42))) // Deep slate #0F172A
            {
                g.DrawString(percentStr, fontBig, textBrush, textX, textY);
            }

            // 3. Status Pill Badge
            int badgeY = textY + 50;
            Color badgeBgColor;
            Color badgeBorderColor;
            Color badgeTextColor;
            string badgeText;

            if (_isPluggedIn)
            {
                badgeBgColor = Color.FromArgb(236, 253, 245); // Soft emerald tint #ECFDF5
                badgeBorderColor = Color.FromArgb(167, 243, 208); // Emerald 200
                badgeTextColor = Color.FromArgb(4, 120, 87); // Emerald 700
                badgeText = (_percent >= 100) ? "⚡ Fully Charged" : "⚡ Plugged In (Charging)";
            }
            else if (_percent <= 15)
            {
                badgeBgColor = Color.FromArgb(254, 242, 242); // Soft red #FEF2F2
                badgeBorderColor = Color.FromArgb(254, 202, 202); // Red 200
                badgeTextColor = Color.FromArgb(185, 28, 28); // Red 700
                badgeText = "⚠️ Low Battery (Discharging)";
            }
            else if (_percent <= 25)
            {
                badgeBgColor = Color.FromArgb(255, 251, 235); // Soft amber #FFFBEB
                badgeBorderColor = Color.FromArgb(253, 230, 138); // Amber 200
                badgeTextColor = Color.FromArgb(180, 83, 9); // Amber 700
                badgeText = "🔋 On Battery (Low Level)";
            }
            else
            {
                badgeBgColor = Color.FromArgb(239, 246, 255); // Soft blue #EFF6FF
                badgeBorderColor = Color.FromArgb(191, 219, 254); // Blue 200
                badgeTextColor = Color.FromArgb(29, 78, 216); // Blue 700
                badgeText = "🔋 On Battery (Normal)";
            }

            using (Font badgeFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            {
                SizeF badgeSize = g.MeasureString(badgeText, badgeFont);
                RectangleF badgeRect = new RectangleF(textX, badgeY, badgeSize.Width + 20, badgeSize.Height + 7);
                using (GraphicsPath badgePath = RoundedRect(badgeRect, 7f))
                {
                    using (SolidBrush badgeBrush = new SolidBrush(badgeBgColor))
                        g.FillPath(badgeBrush, badgePath);
                    using (Pen badgePen = new Pen(badgeBorderColor, 1.2f))
                        g.DrawPath(badgePen, badgePath);
                }
                using (SolidBrush tb = new SolidBrush(badgeTextColor))
                {
                    g.DrawString(badgeText, badgeFont, tb, textX + 10, badgeY + 3.5f);
                }
            }

            // 4. Secondary detail text (time remaining & source)
            int detailY = badgeY + 32;
            string subText = _detailText;
            if (string.IsNullOrEmpty(subText))
            {
                subText = _isPluggedIn ? "Connected to AC outlet • Fast charging active" : "Running on battery power";
            }
            else
            {
                subText = (_isPluggedIn ? "AC Power • " : "On Battery • ") + subText;
            }

            using (Font subFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(100, 116, 139))) // Slate 500
            {
                g.DrawString(subText, subFont, subBrush, textX, detailY);
            }
        }

        private void DrawBatteryGraphic(Graphics g, int x, int y, int w, int h, int percent, bool plugged)
        {
            float radius = 9f;
            RectangleF bodyRect = new RectangleF(x, y, w - 8, h);

            // Outer chassis with subtle drop shadow
            RectangleF shadowRect = new RectangleF(bodyRect.X + 1, bodyRect.Y + 2, bodyRect.Width, bodyRect.Height);
            using (GraphicsPath shadowPath = RoundedRect(shadowRect, radius))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(15, 0, 0, 0)))
            {
                g.FillPath(shadowBrush, shadowPath);
            }

            using (GraphicsPath chassisPath = RoundedRect(bodyRect, radius))
            {
                // Soft aluminum background
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                {
                    g.FillPath(bgBrush, chassisPath);
                }
                // Crisp slate border
                using (Pen borderPen = new Pen(Color.FromArgb(148, 163, 184), 2.0f))
                {
                    g.DrawPath(borderPen, chassisPath);
                }
            }

            // Positive terminal knob on the right
            float knobW = 7;
            float knobH = h * 0.42f;
            float knobX = x + w - 8;
            float knobY = y + (h - knobH) / 2f;
            RectangleF knobRect = new RectangleF(knobX, knobY, knobW, knobH);
            using (GraphicsPath knobPath = RoundedRect(knobRect, 3f))
            using (SolidBrush knobBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                g.FillPath(knobBrush, knobPath);
            }

            // Fill bar inside chassis
            float pad = 4.5f;
            float maxFillW = bodyRect.Width - (pad * 2);
            float fillW = Math.Max(5f, maxFillW * (percent / 100f));
            float fillH = bodyRect.Height - (pad * 2);

            RectangleF fillRect = new RectangleF(bodyRect.X + pad, bodyRect.Y + pad, fillW, fillH);

            Color color1, color2;
            if (plugged)
            {
                color1 = Color.FromArgb(52, 211, 153); // Emerald 400
                color2 = Color.FromArgb(16, 185, 129); // Emerald 500
            }
            else if (percent <= 15)
            {
                color1 = Color.FromArgb(248, 113, 113); // Red 400
                color2 = Color.FromArgb(239, 68, 68);   // Red 500
            }
            else if (percent <= 25)
            {
                color1 = Color.FromArgb(251, 191, 36); // Amber 400
                color2 = Color.FromArgb(245, 158, 11); // Amber 500
            }
            else
            {
                color1 = Color.FromArgb(52, 211, 153);
                color2 = Color.FromArgb(16, 185, 129);
            }

            using (GraphicsPath fillPath = RoundedRect(fillRect, 5.5f))
            using (LinearGradientBrush grad = new LinearGradientBrush(fillRect, color1, color2, LinearGradientMode.Horizontal))
            {
                g.FillPath(grad, fillPath);
            }

            // Charging lightning bolt overlay
            if (plugged)
            {
                float cx = bodyRect.X + bodyRect.Width / 2f;
                float cy = bodyRect.Y + bodyRect.Height / 2f;
                float bs = 17f;

                PointF[] boltPts = new PointF[] {
                    new PointF(cx + bs * 0.15f, cy - bs * 0.65f),
                    new PointF(cx - bs * 0.40f, cy + bs * 0.05f),
                    new PointF(cx - bs * 0.05f, cy + bs * 0.05f),
                    new PointF(cx - bs * 0.15f, cy + bs * 0.65f),
                    new PointF(cx + bs * 0.40f, cy - bs * 0.05f),
                    new PointF(cx + bs * 0.05f, cy - bs * 0.05f)
                };

                using (SolidBrush boltShadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
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
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
            {
                g.DrawString("Desktop Mode: Running on AC Power", font, textBrush, 20, 24);
            }
            using (Font sub = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                g.DrawString("No system battery detected. Threshold alarms will remain idle.", sub, subBrush, 20, 52);
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
        public Color BorderColor { get; set; }
        public float CornerRadius { get; set; }

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            NormalColor = Color.FromArgb(16, 185, 129); // Emerald primary
            HoverColor = Color.FromArgb(5, 150, 105);
            PressedColor = Color.FromArgb(4, 120, 87);
            BorderColor = Color.Transparent;
            CornerRadius = 7f;
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

            Color currentBg = !Enabled ? Color.FromArgb(226, 232, 240) :
                              _isPressed ? PressedColor :
                              _isHovered ? HoverColor : NormalColor;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = BatteryWidget.RoundedRect(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(currentBg))
                {
                    g.FillPath(brush, path);
                }
                if (BorderColor != Color.Transparent)
                {
                    using (Pen pen = new Pen(BorderColor, 1.2f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            Color textColor = !Enabled ? Color.FromArgb(148, 163, 184) : ForeColor;
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
