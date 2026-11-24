using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace BatteryAlarmApp
{
    public class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly AudioAlert _audio;
        private readonly BatteryMonitor _monitor;
        private SystemTrayManager _tray;

        // UI Controls
        private Panel _bannerPanel;
        private Label _bannerLabel;
        private ModernButton _btnBannerStop;
        private ModernButton _btnBannerSnooze5;
        private ModernButton _btnBannerSnooze10;

        private BatteryWidget _batteryWidget;

        private TrackBar _trackLow;
        private Label _lblLowValue;
        private CheckBox _chkLowAlarm;

        private TrackBar _trackHigh;
        private Label _lblHighValue;
        private CheckBox _chkHighAlarm;

        private ComboBox _cboSound;
        private TextBox _txtCustomSound;
        private ModernButton _btnBrowseSound;
        private ModernButton _btnTestSound;
        private CheckBox _chkLoop;
        private ComboBox _cboInterval;

        private CheckBox _chkAutoStart;
        private CheckBox _chkStartMin;
        private CheckBox _chkCloseToTray;
        private CheckBox _chkNotify;

        private ModernButton _btnSave;
        private ModernButton _btnReset;
        private ModernButton _btnMinimize;

        private bool _isExplicitExit = false;
        private bool _firstMinimizeTipShown = false;

        public MainForm(AppSettings settings, AudioAlert audio, BatteryMonitor monitor)
        {
            _settings = settings;
            _audio = audio;
            _monitor = monitor;

            InitializeForm();
            InitializeComponent();
            ApplySettingsToUI();

            _tray = new SystemTrayManager(this, _monitor, _settings);
            _tray.OnOpenRequested += RestoreWindow;
            _tray.OnExitRequested += delegate
            {
                _isExplicitExit = true;
                Close();
            };

            // Wire monitor events
            _monitor.OnStatusChanged += OnBatteryStatusChanged;
            _monitor.OnAlarmTriggered += OnAlarmTriggered;
            _monitor.OnAlarmDismissed += OnAlarmDismissed;

            _monitor.Start();
        }

        private void InitializeForm()
        {
            Text = "BatteryGuard Pro - Smart Battery Monitor";
            Size = new Size(540, 720);
            MinimumSize = new Size(500, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            ForeColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            DoubleBuffered = true;

            try
            {
                if (File.Exists("app.ico"))
                    Icon = new Icon("app.ico");
            }
            catch { }
        }

        private void InitializeComponent()
        {
            // Main scrollable container
            Panel contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.AutoScroll = true;
            contentPanel.BackColor = Color.FromArgb(15, 23, 42);
            Controls.Add(contentPanel);

            int curY = 16;
            int margin = 18;
            int cardW = 486;

            // 1. Header
            Label lblTitle = new Label();
            lblTitle.Text = "🔋 BatteryGuard Pro";
            lblTitle.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(margin, curY);
            lblTitle.AutoSize = true;
            contentPanel.Controls.Add(lblTitle);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "Smart battery protection & charge threshold alerts";
            lblSubtitle.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
            lblSubtitle.Location = new Point(margin + 2, curY + 30);
            lblSubtitle.AutoSize = true;
            contentPanel.Controls.Add(lblSubtitle);

            curY += 60;

            // 2. Active Alarm Alert Banner (Hidden by default)
            _bannerPanel = new Panel();
            _bannerPanel.Location = new Point(margin, curY);
            _bannerPanel.Size = new Size(cardW, 95);
            _bannerPanel.BackColor = Color.FromArgb(220, 38, 38);
            _bannerPanel.Visible = false;
            _bannerPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = BatteryWidget.RoundedRect(new RectangleF(0, 0, _bannerPanel.Width - 1, _bannerPanel.Height - 1), 8f))
                using (Pen pen = new Pen(Color.FromArgb(254, 202, 202), 1.5f))
                {
                    e.Graphics.DrawPath(pen, p);
                }
            };

            _bannerLabel = new Label();
            _bannerLabel.Text = "⚠️ LOW BATTERY WARNING: Please connect charger!";
            _bannerLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _bannerLabel.ForeColor = Color.White;
            _bannerLabel.Location = new Point(16, 12);
            _bannerLabel.Size = new Size(cardW - 32, 24);
            _bannerPanel.Controls.Add(_bannerLabel);

            _btnBannerStop = new ModernButton();
            _btnBannerStop.Text = "⏹ Stop Alarm";
            _btnBannerStop.Location = new Point(16, 46);
            _btnBannerStop.Size = new Size(130, 34);
            _btnBannerStop.NormalColor = Color.FromArgb(15, 23, 42);
            _btnBannerStop.HoverColor = Color.FromArgb(30, 41, 59);
            _btnBannerStop.PressedColor = Color.FromArgb(2, 6, 23);
            _btnBannerStop.Click += delegate { _monitor.DismissCurrentAlarm(); };
            _bannerPanel.Controls.Add(_btnBannerStop);

            _btnBannerSnooze5 = new ModernButton();
            _btnBannerSnooze5.Text = "⏰ Snooze 5m";
            _btnBannerSnooze5.Location = new Point(156, 46);
            _btnBannerSnooze5.Size = new Size(120, 34);
            _btnBannerSnooze5.NormalColor = Color.FromArgb(185, 28, 28);
            _btnBannerSnooze5.HoverColor = Color.FromArgb(153, 27, 27);
            _btnBannerSnooze5.PressedColor = Color.FromArgb(127, 29, 29);
            _btnBannerSnooze5.Click += delegate { _monitor.SnoozeAlarm(5); };
            _bannerPanel.Controls.Add(_btnBannerSnooze5);

            _btnBannerSnooze10 = new ModernButton();
            _btnBannerSnooze10.Text = "⏰ Snooze 10m";
            _btnBannerSnooze10.Location = new Point(286, 46);
            _btnBannerSnooze10.Size = new Size(120, 34);
            _btnBannerSnooze10.NormalColor = Color.FromArgb(185, 28, 28);
            _btnBannerSnooze10.HoverColor = Color.FromArgb(153, 27, 27);
            _btnBannerSnooze10.PressedColor = Color.FromArgb(127, 29, 29);
            _btnBannerSnooze10.Click += delegate { _monitor.SnoozeAlarm(10); };
            _bannerPanel.Controls.Add(_btnBannerSnooze10);

            contentPanel.Controls.Add(_bannerPanel);

            // 3. Battery Status Card
            Panel cardStatus = CreateCardPanel(margin, curY, cardW, 130);
            _batteryWidget = new BatteryWidget();
            _batteryWidget.Location = new Point(12, 12);
            _batteryWidget.Size = new Size(cardW - 24, 106);
            cardStatus.Controls.Add(_batteryWidget);
            contentPanel.Controls.Add(cardStatus);

            curY += 145;

            // 4. Threshold Settings Card
            Panel cardThresholds = CreateCardPanel(margin, curY, cardW, 165);
            AddCardHeader(cardThresholds, "⚡ Charging Alarm Thresholds");

            // Low Battery Row
            _chkLowAlarm = new CheckBox();
            _chkLowAlarm.Text = "Low Battery Alarm:";
            _chkLowAlarm.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _chkLowAlarm.ForeColor = Color.FromArgb(248, 113, 113);
            _chkLowAlarm.Location = new Point(16, 40);
            _chkLowAlarm.Size = new Size(160, 24);
            cardThresholds.Controls.Add(_chkLowAlarm);

            _trackLow = new TrackBar();
            _trackLow.Minimum = 5;
            _trackLow.Maximum = 40;
            _trackLow.TickFrequency = 5;
            _trackLow.SmallChange = 1;
            _trackLow.LargeChange = 5;
            _trackLow.Location = new Point(180, 36);
            _trackLow.Size = new Size(210, 34);
            _trackLow.ValueChanged += delegate
            {
                _lblLowValue.Text = _trackLow.Value.ToString() + "%";
            };
            cardThresholds.Controls.Add(_trackLow);

            _lblLowValue = new Label();
            _lblLowValue.Text = "15%";
            _lblLowValue.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _lblLowValue.ForeColor = Color.FromArgb(248, 113, 113);
            _lblLowValue.Location = new Point(400, 38);
            _lblLowValue.Size = new Size(60, 24);
            cardThresholds.Controls.Add(_lblLowValue);

            // High Battery Row
            _chkHighAlarm = new CheckBox();
            _chkHighAlarm.Text = "High Battery Alarm:";
            _chkHighAlarm.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _chkHighAlarm.ForeColor = Color.FromArgb(52, 211, 153);
            _chkHighAlarm.Location = new Point(16, 88);
            _chkHighAlarm.Size = new Size(160, 24);
            cardThresholds.Controls.Add(_chkHighAlarm);

            _trackHigh = new TrackBar();
            _trackHigh.Minimum = 60;
            _trackHigh.Maximum = 100;
            _trackHigh.TickFrequency = 5;
            _trackHigh.SmallChange = 1;
            _trackHigh.LargeChange = 5;
            _trackHigh.Location = new Point(180, 84);
            _trackHigh.Size = new Size(210, 34);
            _trackHigh.ValueChanged += delegate
            {
                _lblHighValue.Text = _trackHigh.Value.ToString() + "%";
            };
            cardThresholds.Controls.Add(_trackHigh);

            _lblHighValue = new Label();
            _lblHighValue.Text = "90%";
            _lblHighValue.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _lblHighValue.ForeColor = Color.FromArgb(52, 211, 153);
            _lblHighValue.Location = new Point(400, 86);
            _lblHighValue.Size = new Size(60, 24);
            cardThresholds.Controls.Add(_lblHighValue);

            Label lblThresholdHint = new Label();
            lblThresholdHint.Text = "Tip: 90% protects lithium battery health; 15% prevents sudden shutdowns.";
            lblThresholdHint.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            lblThresholdHint.ForeColor = Color.FromArgb(148, 163, 184);
            lblThresholdHint.Location = new Point(16, 134);
            lblThresholdHint.Size = new Size(cardW - 32, 20);
            cardThresholds.Controls.Add(lblThresholdHint);

            contentPanel.Controls.Add(cardThresholds);

            curY += 180;

            // 5. Sound & Alarm Settings Card
            Panel cardSound = CreateCardPanel(margin, curY, cardW, 160);
            AddCardHeader(cardSound, "🔊 Sound & Notification Preferences");

            Label lblSound = new Label();
            lblSound.Text = "Alarm Sound:";
            lblSound.Location = new Point(16, 42);
            lblSound.AutoSize = true;
            cardSound.Controls.Add(lblSound);

            _cboSound = new ComboBox();
            _cboSound.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboSound.Items.AddRange(new object[] { "Alarm (Digital Beep)", "Chime (Melodic)", "Siren (Urgent)", "System Alert", "Custom WAV File" });
            _cboSound.Location = new Point(110, 38);
            _cboSound.Size = new Size(170, 26);
            _cboSound.SelectedIndexChanged += OnSoundTypeChanged;
            cardSound.Controls.Add(_cboSound);

            _btnTestSound = new ModernButton();
            _btnTestSound.Text = "▶ Test";
            _btnTestSound.Location = new Point(290, 37);
            _btnTestSound.Size = new Size(70, 28);
            _btnTestSound.NormalColor = Color.FromArgb(37, 99, 235);
            _btnTestSound.HoverColor = Color.FromArgb(59, 130, 246);
            _btnTestSound.PressedColor = Color.FromArgb(29, 78, 216);
            _btnTestSound.Click += delegate
            {
                string st = GetSelectedSoundType();
                _audio.TestSound(st, _txtCustomSound.Text);
            };
            cardSound.Controls.Add(_btnTestSound);

            _txtCustomSound = new TextBox();
            _txtCustomSound.Location = new Point(16, 74);
            _txtCustomSound.Size = new Size(344, 25);
            _txtCustomSound.Visible = false;
            cardSound.Controls.Add(_txtCustomSound);

            _btnBrowseSound = new ModernButton();
            _btnBrowseSound.Text = "Browse...";
            _btnBrowseSound.Location = new Point(368, 73);
            _btnBrowseSound.Size = new Size(88, 27);
            _btnBrowseSound.NormalColor = Color.FromArgb(51, 65, 85);
            _btnBrowseSound.HoverColor = Color.FromArgb(71, 85, 105);
            _btnBrowseSound.PressedColor = Color.FromArgb(30, 41, 59);
            _btnBrowseSound.Visible = false;
            _btnBrowseSound.Click += OnBrowseCustomSound;
            cardSound.Controls.Add(_btnBrowseSound);

            _chkLoop = new CheckBox();
            _chkLoop.Text = "Repeat alarm every:";
            _chkLoop.Location = new Point(16, 115);
            _chkLoop.AutoSize = true;
            _chkLoop.CheckedChanged += delegate { _cboInterval.Enabled = _chkLoop.Checked; };
            cardSound.Controls.Add(_chkLoop);

            _cboInterval = new ComboBox();
            _cboInterval.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboInterval.Items.AddRange(new object[] { "3 seconds", "5 seconds", "10 seconds", "15 seconds", "30 seconds" });
            _cboInterval.Location = new Point(160, 112);
            _cboInterval.Size = new Size(110, 26);
            cardSound.Controls.Add(_cboInterval);

            contentPanel.Controls.Add(cardSound);

            curY += 175;

            // 6. System & Startup Behavior Card
            Panel cardSystem = CreateCardPanel(margin, curY, cardW, 140);
            AddCardHeader(cardSystem, "⚙️ Startup & Behavior");

            _chkAutoStart = new CheckBox();
            _chkAutoStart.Text = "Start automatically when Windows boots";
            _chkAutoStart.Location = new Point(16, 38);
            _chkAutoStart.AutoSize = true;
            cardSystem.Controls.Add(_chkAutoStart);

            _chkStartMin = new CheckBox();
            _chkStartMin.Text = "Start minimized to system tray";
            _chkStartMin.Location = new Point(16, 64);
            _chkStartMin.AutoSize = true;
            cardSystem.Controls.Add(_chkStartMin);

            _chkCloseToTray = new CheckBox();
            _chkCloseToTray.Text = "Minimize to tray when close (X) is clicked";
            _chkCloseToTray.Location = new Point(16, 90);
            _chkCloseToTray.AutoSize = true;
            cardSystem.Controls.Add(_chkCloseToTray);

            _chkNotify = new CheckBox();
            _chkNotify.Text = "Show Windows balloon / tray alerts";
            _chkNotify.Location = new Point(270, 38);
            _chkNotify.AutoSize = true;
            cardSystem.Controls.Add(_chkNotify);

            contentPanel.Controls.Add(cardSystem);

            curY += 155;

            // 7. Footer Action Buttons
            _btnSave = new ModernButton();
            _btnSave.Text = "💾 Save Settings";
            _btnSave.Location = new Point(margin, curY);
            _btnSave.Size = new Size(155, 38);
            _btnSave.NormalColor = Color.FromArgb(16, 185, 129);
            _btnSave.HoverColor = Color.FromArgb(52, 211, 153);
            _btnSave.PressedColor = Color.FromArgb(5, 150, 105);
            _btnSave.Click += OnSaveClicked;
            contentPanel.Controls.Add(_btnSave);

            _btnReset = new ModernButton();
            _btnReset.Text = "🔄 Reset Defaults";
            _btnReset.Location = new Point(margin + 165, curY);
            _btnReset.Size = new Size(145, 38);
            _btnReset.NormalColor = Color.FromArgb(51, 65, 85);
            _btnReset.HoverColor = Color.FromArgb(71, 85, 105);
            _btnReset.PressedColor = Color.FromArgb(30, 41, 59);
            _btnReset.Click += OnResetDefaultsClicked;
            contentPanel.Controls.Add(_btnReset);

            _btnMinimize = new ModernButton();
            _btnMinimize.Text = "📥 Hide to Tray";
            _btnMinimize.Location = new Point(margin + 320, curY);
            _btnMinimize.Size = new Size(165, 38);
            _btnMinimize.NormalColor = Color.FromArgb(37, 99, 235);
            _btnMinimize.HoverColor = Color.FromArgb(59, 130, 246);
            _btnMinimize.PressedColor = Color.FromArgb(29, 78, 216);
            _btnMinimize.Click += delegate { HideToTray(); };
            contentPanel.Controls.Add(_btnMinimize);

            curY += 60;
        }

        private Panel CreateCardPanel(int x, int y, int w, int h)
        {
            Panel p = new Panel();
            p.Location = new Point(x, y);
            p.Size = new Size(w, h);
            p.BackColor = Color.FromArgb(30, 41, 59); // Slate 800
            p.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = BatteryWidget.RoundedRect(new RectangleF(0, 0, p.Width - 1, p.Height - 1), 8f))
                using (Pen pen = new Pen(Color.FromArgb(51, 65, 85), 1.5f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };
            return p;
        }

        private void AddCardHeader(Panel card, string title)
        {
            Label lbl = new Label();
            lbl.Text = title;
            lbl.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            lbl.ForeColor = Color.FromArgb(241, 245, 249);
            lbl.Location = new Point(14, 10);
            lbl.AutoSize = true;
            card.Controls.Add(lbl);
        }

        private void ApplySettingsToUI()
        {
            _trackLow.Value = Math.Max(_trackLow.Minimum, Math.Min(_trackLow.Maximum, _settings.LowThreshold));
            _lblLowValue.Text = _trackLow.Value.ToString() + "%";
            _chkLowAlarm.Checked = _settings.EnableLowAlarm;

            _trackHigh.Value = Math.Max(_trackHigh.Minimum, Math.Min(_trackHigh.Maximum, _settings.HighThreshold));
            _lblHighValue.Text = _trackHigh.Value.ToString() + "%";
            _chkHighAlarm.Checked = _settings.EnableHighAlarm;

            string sound = _settings.SoundType;
            if (string.Equals(sound, "Chime", StringComparison.OrdinalIgnoreCase)) _cboSound.SelectedIndex = 1;
            else if (string.Equals(sound, "Siren", StringComparison.OrdinalIgnoreCase)) _cboSound.SelectedIndex = 2;
            else if (string.Equals(sound, "System", StringComparison.OrdinalIgnoreCase)) _cboSound.SelectedIndex = 3;
            else if (string.Equals(sound, "Custom", StringComparison.OrdinalIgnoreCase)) _cboSound.SelectedIndex = 4;
            else _cboSound.SelectedIndex = 0; // "Alarm"

            _txtCustomSound.Text = _settings.CustomSoundPath ?? "";

            _chkLoop.Checked = _settings.LoopAlarm;
            _cboInterval.Enabled = _chkLoop.Checked;

            int sec = _settings.AlarmIntervalSeconds;
            if (sec <= 3) _cboInterval.SelectedIndex = 0;
            else if (sec <= 5) _cboInterval.SelectedIndex = 1;
            else if (sec <= 10) _cboInterval.SelectedIndex = 2;
            else if (sec <= 15) _cboInterval.SelectedIndex = 3;
            else _cboInterval.SelectedIndex = 4;

            _chkAutoStart.Checked = _settings.StartWithWindows;
            _chkStartMin.Checked = _settings.StartMinimized;
            _chkCloseToTray.Checked = _settings.MinimizeToTrayOnClose;
            _chkNotify.Checked = _settings.ShowNotifications;

            UpdateCustomSoundControls();
        }

        private void OnSoundTypeChanged(object sender, EventArgs e)
        {
            UpdateCustomSoundControls();
        }

        private void UpdateCustomSoundControls()
        {
            bool isCustom = (_cboSound.SelectedIndex == 4);
            _txtCustomSound.Visible = isCustom;
            _btnBrowseSound.Visible = isCustom;
        }

        private string GetSelectedSoundType()
        {
            switch (_cboSound.SelectedIndex)
            {
                case 1: return "Chime";
                case 2: return "Siren";
                case 3: return "System";
                case 4: return "Custom";
                default: return "Alarm";
            }
        }

        private void OnBrowseCustomSound(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Wave Audio Files (*.wav)|*.wav|All Files (*.*)|*.*";
                ofd.Title = "Select Custom Alarm Sound";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtCustomSound.Text = ofd.FileName;
                }
            }
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            _settings.LowThreshold = _trackLow.Value;
            _settings.EnableLowAlarm = _chkLowAlarm.Checked;

            _settings.HighThreshold = _trackHigh.Value;
            _settings.EnableHighAlarm = _chkHighAlarm.Checked;

            _settings.SoundType = GetSelectedSoundType();
            _settings.CustomSoundPath = _txtCustomSound.Text;

            _settings.LoopAlarm = _chkLoop.Checked;
            switch (_cboInterval.SelectedIndex)
            {
                case 0: _settings.AlarmIntervalSeconds = 3; break;
                case 1: _settings.AlarmIntervalSeconds = 5; break;
                case 2: _settings.AlarmIntervalSeconds = 10; break;
                case 3: _settings.AlarmIntervalSeconds = 15; break;
                default: _settings.AlarmIntervalSeconds = 30; break;
            }

            _settings.StartWithWindows = _chkAutoStart.Checked;
            _settings.StartMinimized = _chkStartMin.Checked;
            _settings.MinimizeToTrayOnClose = _chkCloseToTray.Checked;
            _settings.ShowNotifications = _chkNotify.Checked;

            _settings.Save();

            // Notify monitor of updated settings
            _monitor.CheckStatus();

            MessageBox.Show(this, "Settings saved successfully!", "BatteryGuard Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnResetDefaultsClicked(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Reset all settings to default values (Low: 15%, High: 90%)?", "Reset Settings",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _settings.SetDefaults();
                ApplySettingsToUI();
                _settings.Save();
                _monitor.CheckStatus();
            }
        }

        private void OnBatteryStatusChanged(BatterySnapshot snap)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<BatterySnapshot>(OnBatteryStatusChanged), snap);
                return;
            }

            _batteryWidget.UpdateData(snap);
        }

        private void OnAlarmTriggered(ActiveAlarmState state, int percent, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<ActiveAlarmState, int, string>(OnAlarmTriggered), state, percent, message);
                return;
            }

            _bannerLabel.Text = message;
            if (state == ActiveAlarmState.LowBattery)
            {
                _bannerPanel.BackColor = Color.FromArgb(220, 38, 38); // Crimson Red
            }
            else
            {
                _bannerPanel.BackColor = Color.FromArgb(16, 185, 129); // Emerald Green
            }
            _bannerPanel.Visible = true;
        }

        private void OnAlarmDismissed(string reason)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnAlarmDismissed), reason);
                return;
            }

            _bannerPanel.Visible = false;
        }

        public void RestoreWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
        }

        public void HideToTray()
        {
            Hide();
            if (!_firstMinimizeTipShown)
            {
                _firstMinimizeTipShown = true;
                _tray.ShowTip("BatteryGuard Pro", "Running in background tray. Double-click tray icon to open.");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isExplicitExit && _settings.MinimizeToTrayOnClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            _monitor.Stop();
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }

            base.OnFormClosing(e);
        }
    }
}
