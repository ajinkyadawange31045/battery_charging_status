using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BatteryAlarmApp.Controls
{
    public class ModernCard : Panel
    {
        public float CornerRadius { get; set; }
        public Color BorderColor { get; set; }

        public ModernCard()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Color.White;
            CornerRadius = 10f;
            BorderColor = Color.FromArgb(226, 232, 240); // Slate 200
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = BatteryWidget.RoundedRect(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(BackColor))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(BorderColor, 1.2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            base.OnPaint(e);
        }
    }
}
