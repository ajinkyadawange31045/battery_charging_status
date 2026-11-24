using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BatteryAlarmApp
{
    public class AlertPopupForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly ActiveAlarmState _state;
        private readonly int _percent;
        private readonly Action _onAcknowledgeSnooze5;
        private readonly Timer _autoCloseCheckTimer;

        public AlertPopupForm(ActiveAlarmState state, int percent, Action onAcknowledgeSnooze5)
        {
            _state = state;
            _percent = percent;
            _onAcknowledgeSnooze5 = onAcknowledgeSnooze5;

            InitializeComponent();

            // Auto-close if user physically plugged/unplugged charger
            _autoCloseCheckTimer = new Timer();
            _autoCloseCheckTimer.Interval = 1000;
            _autoCloseCheckTimer.Tick += delegate
            {
                BatterySnapshot snap = BatteryMonitor.QueryBattery();
                if (_state == ActiveAlarmState.LowBattery && snap.IsPluggedIn)
                {
                    // User plugged in! Dismiss popup
                    _autoCloseCheckTimer.Stop();
                    Close();
                }
                else if ((_state == ActiveAlarmState.HighBattery || _state == ActiveAlarmState.FullCharge) && !snap.IsPluggedIn)
                {
                    // User unplugged! Dismiss popup
                    _autoCloseCheckTimer.Stop();
                    Close();
                }
            };
            _autoCloseCheckTimer.Start();
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(460, 310);
            TopMost = true;
            ShowInTaskbar = true;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            DoubleBuffered = true;

            bool isLow = (_state == ActiveAlarmState.LowBattery);
            Color themeColor = isLow ? Color.FromArgb(239, 68, 68) : Color.FromArgb(16, 185, 129);
            string headerTitle = isLow ? "LOW BATTERY ALERT" : "CHARGING COMPLETE";
            string actionTitle = isLow ? "PLEASE PUT ON CHARGER!" : "PLEASE REMOVE CHARGER!";
            string subMessage = isLow
                ? string.Format("Your battery is critically low at {0}%. Plug in your charger immediately to avoid sudden shutdown.", _percent)
                : string.Format("Your battery reached {0}%. Disconnect your charger now to preserve lithium battery lifespan.", _percent);

            // 1. Top Accent Header Bar
            Panel topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 56;
            topBar.BackColor = themeColor;
            topBar.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Header icon & text
                using (Font hf = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    g.DrawString(headerTitle, hf, tb, 20, 16);
                }

                // Battery badge on right
                string badge = _percent.ToString() + "%";
                using (Font bf = new Font("Segoe UI", 11f, FontStyle.Bold))
                {
                    SizeF bSize = g.MeasureString(badge, bf);
                    RectangleF br = new RectangleF(Width - bSize.Width - 30, 14, bSize.Width + 14, 28);
                    using (GraphicsPath bp = BatteryWidget.RoundedRect(br, 6f))
                    using (SolidBrush bgb = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
                    {
                        g.FillPath(bgb, bp);
                    }
                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                    {
                        g.DrawString(badge, bf, textBrush, br.X + 7, br.Y + 4);
                    }
                }
            };
            Controls.Add(topBar);

            // 2. Main Content Area
            Label lblAction = new Label();
            lblAction.Text = actionTitle;
            lblAction.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            lblAction.ForeColor = themeColor;
            lblAction.Location = new Point(24, 76);
            lblAction.Size = new Size(412, 34);
            Controls.Add(lblAction);

            Label lblMessage = new Label();
            lblMessage.Text = subMessage;
            lblMessage.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            lblMessage.ForeColor = Color.FromArgb(71, 85, 105);
            lblMessage.Location = new Point(24, 114);
            lblMessage.Size = new Size(412, 46);
            Controls.Add(lblMessage);

            // Informational tip box
            Panel tipBox = new Panel();
            tipBox.Location = new Point(24, 166);
            tipBox.Size = new Size(412, 46);
            tipBox.BackColor = Color.FromArgb(248, 250, 252);
            tipBox.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF r = new RectangleF(0, 0, tipBox.Width - 1, tipBox.Height - 1);
                using (GraphicsPath p = BatteryWidget.RoundedRect(r, 6f))
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240), 1.2f))
                {
                    e.Graphics.DrawPath(pen, p);
                }
            };

            Label lblTip = new Label();
            lblTip.Text = "Clicking OK pauses alarm checks for 5 minutes. It will resume checking automatically.";
            lblTip.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular);
            lblTip.ForeColor = Color.FromArgb(100, 116, 139);
            lblTip.Location = new Point(12, 6);
            lblTip.Size = new Size(390, 34);
            tipBox.Controls.Add(lblTip);
            Controls.Add(tipBox);

            // 3. Primary Action Button (OK - Pause for 5 Minutes)
            ModernButton btnOk = new ModernButton();
            btnOk.Text = "OK (Pause checking for 5 minutes)";
            btnOk.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnOk.Location = new Point(24, 226);
            btnOk.Size = new Size(412, 44);
            btnOk.NormalColor = themeColor;
            btnOk.HoverColor = isLow ? Color.FromArgb(220, 38, 38) : Color.FromArgb(5, 150, 105);
            btnOk.PressedColor = isLow ? Color.FromArgb(185, 28, 28) : Color.FromArgb(4, 120, 87);
            btnOk.CornerRadius = 8f;
            btnOk.Click += delegate
            {
                _autoCloseCheckTimer.Stop();
                if (_onAcknowledgeSnooze5 != null)
                {
                    _onAcknowledgeSnooze5();
                }
                Close();
            };
            Controls.Add(btnOk);

            // Draw outer border on form
            Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                using (GraphicsPath p = BatteryWidget.RoundedRect(r, 8f))
                using (Pen pen = new Pen(Color.FromArgb(203, 213, 225), 1.5f))
                {
                    e.Graphics.DrawPath(pen, p);
                }
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SetForegroundWindow(Handle);
            System.Media.SystemSounds.Exclamation.Play();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_autoCloseCheckTimer != null)
            {
                _autoCloseCheckTimer.Stop();
                _autoCloseCheckTimer.Dispose();
            }
            base.OnFormClosed(e);
        }
    }
}
