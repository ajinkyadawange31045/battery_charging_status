using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BatteryAlarmApp
{
    public class SystemTrayManager : IDisposable
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly Form _mainForm;
        private readonly BatteryMonitor _monitor;
        private readonly AppSettings _settings;

        private ToolStripMenuItem _statusMenuItem;
        private ToolStripMenuItem _stopAlarmMenuItem;
        private ToolStripMenuItem _snooze5MenuItem;
        private ToolStripMenuItem _snooze10MenuItem;
        private Icon _currentDynamicIcon = null;

        public event Action OnOpenRequested;
        public event Action OnExitRequested;

        public SystemTrayManager(Form mainForm, BatteryMonitor monitor, AppSettings settings)
        {
            _mainForm = mainForm;
            _monitor = monitor;
            _settings = settings;

            _contextMenu = new ContextMenuStrip();
            _contextMenu.RenderMode = ToolStripRenderMode.System;
            BuildMenu();

            _notifyIcon = new NotifyIcon();
            _notifyIcon.ContextMenuStrip = _contextMenu;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate { TriggerOpen(); };

            // Listen to monitor events
            _monitor.OnStatusChanged += UpdateStatus;
            _monitor.OnAlarmTriggered += ShowAlarmNotification;
            _monitor.OnAlarmDismissed += delegate { UpdateAlarmMenuItems(); };
        }

        private void BuildMenu()
        {
            _contextMenu.Items.Clear();

            _statusMenuItem = new ToolStripMenuItem("Battery: Checking...");
            _statusMenuItem.Enabled = false;
            _contextMenu.Items.Add(_statusMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            _stopAlarmMenuItem = new ToolStripMenuItem("⏹ Stop Alarm", null, delegate
            {
                _monitor.DismissCurrentAlarm();
            });
            _stopAlarmMenuItem.Font = new Font(_contextMenu.Font, FontStyle.Bold);
            _stopAlarmMenuItem.Visible = false;
            _contextMenu.Items.Add(_stopAlarmMenuItem);

            _snooze5MenuItem = new ToolStripMenuItem("⏰ Snooze Alarm (5 min)", null, delegate
            {
                _monitor.SnoozeAlarm(5);
            });
            _snooze5MenuItem.Visible = false;
            _contextMenu.Items.Add(_snooze5MenuItem);

            _snooze10MenuItem = new ToolStripMenuItem("⏰ Snooze Alarm (10 min)", null, delegate
            {
                _monitor.SnoozeAlarm(10);
            });
            _snooze10MenuItem.Visible = false;
            _contextMenu.Items.Add(_snooze10MenuItem);

            ToolStripMenuItem openItem = new ToolStripMenuItem("Open BatteryGuard", null, delegate
            {
                TriggerOpen();
            });
            openItem.Font = new Font(_contextMenu.Font, FontStyle.Bold);
            _contextMenu.Items.Add(openItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            _contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, delegate
            {
                if (OnExitRequested != null) OnExitRequested();
            }));
        }

        public void UpdateStatus(BatterySnapshot snap)
        {
            if (_mainForm.InvokeRequired)
            {
                _mainForm.BeginInvoke(new Action<BatterySnapshot>(UpdateStatus), snap);
                return;
            }

            // Update status text in context menu
            if (!snap.HasBattery)
            {
                _statusMenuItem.Text = "Battery: AC Power (No Battery)";
                SetIcon(CreateTrayIcon(100, true, false));
                _notifyIcon.Text = "BatteryGuard: AC Power (Desktop)";
            }
            else
            {
                string stateStr = snap.IsPluggedIn ? "Charging" : "Discharging";
                _statusMenuItem.Text = string.Format("Battery: {0}% ({1})", snap.Percent, stateStr);

                string tip = string.Format("BatteryGuard: {0}% ({1}) | Low:{2}% High:{3}%",
                    snap.Percent, stateStr, _settings.LowThreshold, _settings.HighThreshold);
                if (tip.Length > 63) tip = tip.Substring(0, 63); // Win32 NotifyIcon tooltip limit
                _notifyIcon.Text = tip;

                SetIcon(CreateTrayIcon(snap.Percent, snap.IsPluggedIn, _monitor.IsAlarmRinging));
            }

            UpdateAlarmMenuItems();
        }

        private void UpdateAlarmMenuItems()
        {
            bool ringing = _monitor.IsAlarmRinging;
            _stopAlarmMenuItem.Visible = ringing;
            _snooze5MenuItem.Visible = ringing;
            _snooze10MenuItem.Visible = ringing;
        }

        public void ShowAlarmNotification(ActiveAlarmState state, int percent, string message)
        {
            if (!_settings.ShowNotifications) return;

            if (_mainForm.InvokeRequired)
            {
                _mainForm.BeginInvoke(new Action<ActiveAlarmState, int, string>(ShowAlarmNotification), state, percent, message);
                return;
            }

            string title = (state == ActiveAlarmState.LowBattery)
                ? "⚠️ Low Battery Warning!"
                : "⚡ Charging Completed Alert!";

            ToolTipIcon iconType = (state == ActiveAlarmState.LowBattery)
                ? ToolTipIcon.Warning
                : ToolTipIcon.Info;

            _notifyIcon.ShowBalloonTip(4000, title, message, iconType);
            UpdateAlarmMenuItems();
        }

        public void ShowTip(string title, string text)
        {
            if (_settings.ShowNotifications)
            {
                _notifyIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
            }
        }

        private void TriggerOpen()
        {
            if (OnOpenRequested != null)
            {
                OnOpenRequested();
            }
        }

        private void SetIcon(Icon newIcon)
        {
            if (newIcon == null) return;
            Icon old = _currentDynamicIcon;
            _currentDynamicIcon = newIcon;
            _notifyIcon.Icon = newIcon;
            if (old != null)
            {
                try { old.Dispose(); } catch { }
            }
        }

        // Draws a sharp, clean 16x16 tray icon with color-coded battery & level
        private Icon CreateTrayIcon(int percent, bool plugged, bool alarm)
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Body bounds
                RectangleF body = new RectangleF(1, 3, 12, 10);
                using (GraphicsPath path = BatteryWidget.RoundedRect(body, 2f))
                {
                    Color chassisColor = alarm ? Color.FromArgb(239, 68, 68) : Color.FromArgb(40, 48, 60);
                    using (SolidBrush bg = new SolidBrush(chassisColor))
                        g.FillPath(bg, path);

                    Color borderCol = alarm ? Color.FromArgb(254, 202, 202) : Color.FromArgb(148, 163, 184);
                    using (Pen pen = new Pen(borderCol, 1.2f))
                        g.DrawPath(pen, path);
                }

                // Terminal knob
                g.FillRectangle(Brushes.LightGray, 13, 6, 2, 4);

                // Fill level
                float fillW = Math.Max(1f, (body.Width - 3) * (percent / 100f));
                RectangleF fillRect = new RectangleF(body.X + 1.5f, body.Y + 1.5f, fillW, body.Height - 3);

                Color fillColor;
                if (alarm) fillColor = Color.FromArgb(239, 68, 68);
                else if (plugged) fillColor = Color.FromArgb(16, 185, 129);
                else if (percent <= 15) fillColor = Color.FromArgb(239, 68, 68);
                else if (percent <= 30) fillColor = Color.FromArgb(245, 158, 11);
                else fillColor = Color.FromArgb(52, 211, 153);

                using (SolidBrush fb = new SolidBrush(fillColor))
                {
                    g.FillRectangle(fb, fillRect);
                }

                // Lightning bolt dot if plugged
                if (plugged)
                {
                    g.FillEllipse(Brushes.White, 5, 6, 4, 4);
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return icon;
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            if (_currentDynamicIcon != null)
            {
                try { _currentDynamicIcon.Dispose(); } catch { }
            }
        }
    }
}
