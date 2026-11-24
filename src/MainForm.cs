using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using BatteryAlarmApp.Controls;

namespace BatteryAlarmApp
{
    public class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly AudioAlert _audio;
        private readonly BatteryMonitor _monitor;
        private SystemTrayManager _tray;
        private AlertPopupForm _activePopup = null;

        // UI Panels & Layout
        private Panel _contentPanel;
        private Panel _bannerPanel;
        private Label _bannerLabel;
        private ModernButton _btnBannerStop;
        private ModernButton _btnBannerSnooze5;

        private ModernCard _cardHero;
        private BatteryWidget _batteryWidget;
        private Label _lblHealthBadge;
        private Label _lblCyclesBadge;
        private ModernButton _btnOfficialReport;

        private ModernCard _cardThresholds;
        private ModernToggle _toggleLow;
        private ModernSlider _sliderLow;
        private Label _lblLowPill;
        private ModernToggle _toggleHigh;
        private ModernSlider _sliderHigh;
        private Label _lblHighPill;

        private ModernCard _cardAudio;
        private ComboBox _cboSound;
        private TextBox _txtCustomSound;
        private ModernButton _btnBrowseSound;
        private ModernButton _btnTestSound;
        private ModernToggle _toggleLoop;
        private ComboBox _cboInterval;

        private ModernCard _cardSmart;
        private ModernToggle _toggleQuiet;
        private ComboBox _cboQuietStart;
        private ComboBox _cboQuietEnd;
        private Panel _pnlQuietHoursTime;
        private ModernToggle _toggleFullCharge;

        private ModernCard _cardSystem;
        private ModernToggle _toggleAutoStart;
        private ModernToggle _toggleStartMin;
        private ModernToggle _toggleCloseToTray;
        private ModernToggle _toggleNotify;

        private ModernCard _cardEfficiency;

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

            _monitor.OnStatusChanged += OnBatteryStatusChanged;
            _monitor.OnAlarmTriggered += OnAlarmTriggered;
            _monitor.OnAlarmDismissed += OnAlarmDismissed;

            _monitor.Start();
            LoadBatteryHealthAsync();
        }

        private void InitializeForm()
        {
            Text = "BatteryGuard Pro";
            Size = new Size(590, 840);
            MinimumSize = new Size(540, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(248, 250, 252); // Soft White / Slate 50
            ForeColor = Color.FromArgb(15, 23, 42);   // Deep Slate 900
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
            _contentPanel = new Panel();
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.AutoScroll = true;
            _contentPanel.BackColor = Color.FromArgb(248, 250, 252);
            Controls.Add(_contentPanel);

            int curY = 18;
            int margin = 20;
            int cardW = _contentPanel.ClientSize.Width - (margin * 2) - 8;
            if (cardW < 460) cardW = 460;

            // 1. Header with custom vector badge
            Panel headerPanel = new Panel();
            headerPanel.Location = new Point(margin, curY);
            headerPanel.Size = new Size(cardW, 56);
            headerPanel.BackColor = Color.Transparent;
            headerPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                RectangleF bRect = new RectangleF(0, 4, 32, 20);
                using (GraphicsPath bp = BatteryWidget.RoundedRect(bRect, 5f))
                {
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(16, 185, 129)))
                        g.FillPath(bg, bp);
                    using (Pen p = new Pen(Color.FromArgb(5, 150, 105), 1.5f))
                        g.DrawPath(p, bp);
                }
                g.FillRectangle(new SolidBrush(Color.FromArgb(5, 150, 105)), 32, 10, 3, 8);

                PointF[] bolt = new PointF[] {
                    new PointF(17, 7),
                    new PointF(12, 14),
                    new PointF(16, 14),
                    new PointF(15, 21),
                    new PointF(20, 14),
                    new PointF(16, 14)
                };
                g.FillPolygon(Brushes.White, bolt);
            };

            Label lblTitle = new Label();
            lblTitle.Text = "BatteryGuard Pro";
            lblTitle.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTitle.Location = new Point(44, 0);
            lblTitle.AutoSize = true;
            headerPanel.Controls.Add(lblTitle);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "Intelligent charging protection • Battery wear prevention • Ultra-lightweight";
            lblSubtitle.Font = new Font("Segoe UI", 9.2f, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblSubtitle.Location = new Point(46, 32);
            lblSubtitle.AutoSize = true;
            headerPanel.Controls.Add(lblSubtitle);

            _contentPanel.Controls.Add(headerPanel);
            curY += 66;

            // 2. Active Alert Banner
            _bannerPanel = new Panel();
            _bannerPanel.Location = new Point(margin, curY);
            _bannerPanel.Size = new Size(cardW, 90);
            _bannerPanel.BackColor = Color.FromArgb(239, 68, 68);
            _bannerPanel.Visible = false;
            _bannerPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath p = BatteryWidget.RoundedRect(new RectangleF(0, 0, _bannerPanel.Width - 1, _bannerPanel.Height - 1), 10f))
                using (Pen pen = new Pen(Color.FromArgb(254, 202, 202), 1.5f))
                {
                    e.Graphics.DrawPath(pen, p);
                }
            };

            _bannerLabel = new Label();
            _bannerLabel.Text = "LOW BATTERY ALERT: Please connect charger now!";
            _bannerLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _bannerLabel.ForeColor = Color.White;
            _bannerLabel.Location = new Point(16, 12);
            _bannerLabel.Size = new Size(cardW - 32, 26);
            _bannerPanel.Controls.Add(_bannerLabel);

            _btnBannerStop = new ModernButton();
            _btnBannerStop.Text = "Stop Alarm";
            _btnBannerStop.Location = new Point(16, 44);
            _btnBannerStop.Size = new Size(130, 34);
            _btnBannerStop.NormalColor = Color.FromArgb(15, 23, 42);
            _btnBannerStop.HoverColor = Color.FromArgb(30, 41, 59);
            _btnBannerStop.PressedColor = Color.FromArgb(2, 6, 23);
            _btnBannerStop.Click += delegate { _monitor.DismissCurrentAlarm(); };
            _bannerPanel.Controls.Add(_btnBannerStop);

            _btnBannerSnooze5 = new ModernButton();
            _btnBannerSnooze5.Text = "Snooze 5 Minutes";
            _btnBannerSnooze5.Location = new Point(156, 44);
            _btnBannerSnooze5.Size = new Size(150, 34);
            _btnBannerSnooze5.NormalColor = Color.FromArgb(220, 38, 38);
            _btnBannerSnooze5.HoverColor = Color.FromArgb(185, 28, 28);
            _btnBannerSnooze5.PressedColor = Color.FromArgb(153, 27, 27);
            _btnBannerSnooze5.Click += delegate { _monitor.SnoozeAlarm(5); };
            _bannerPanel.Controls.Add(_btnBannerSnooze5);

            _contentPanel.Controls.Add(_bannerPanel);

            // 3. Hero Battery Card
            _cardHero = new ModernCard();
            _cardHero.Location = new Point(margin, curY);
            _cardHero.Size = new Size(cardW, 172);

            _batteryWidget = new BatteryWidget();
            _batteryWidget.Location = new Point(12, 10);
            _batteryWidget.Size = new Size(cardW - 24, 116);
            _batteryWidget.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _cardHero.Controls.Add(_batteryWidget);

            // Diagnostics row in hero card
            Panel diagRow = new Panel();
            diagRow.Location = new Point(16, 130);
            diagRow.Size = new Size(cardW - 32, 32);
            diagRow.BackColor = Color.FromArgb(248, 250, 252);
            diagRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            diagRow.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF r = new RectangleF(0, 0, diagRow.Width - 1, diagRow.Height - 1);
                using (GraphicsPath p = BatteryWidget.RoundedRect(r, 6f))
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240), 1f))
                {
                    e.Graphics.DrawPath(pen, p);
                }
            };

            _lblHealthBadge = new Label();
            _lblHealthBadge.Text = "Battery Diagnostics Ready";
            _lblHealthBadge.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _lblHealthBadge.ForeColor = Color.FromArgb(16, 185, 129);
            _lblHealthBadge.Location = new Point(12, 7);
            _lblHealthBadge.AutoSize = true;
            diagRow.Controls.Add(_lblHealthBadge);

            _lblCyclesBadge = new Label();
            _lblCyclesBadge.Text = "";
            _lblCyclesBadge.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _lblCyclesBadge.ForeColor = Color.FromArgb(100, 116, 139);
            _lblCyclesBadge.Location = new Point(180, 7);
            _lblCyclesBadge.AutoSize = true;
            diagRow.Controls.Add(_lblCyclesBadge);

            _btnOfficialReport = new ModernButton();
            _btnOfficialReport.Text = "Official Report";
            _btnOfficialReport.Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
            _btnOfficialReport.Location = new Point(diagRow.Width - 120, 3);
            _btnOfficialReport.Size = new Size(114, 26);
            _btnOfficialReport.NormalColor = Color.White;
            _btnOfficialReport.HoverColor = Color.FromArgb(241, 245, 249);
            _btnOfficialReport.PressedColor = Color.FromArgb(226, 232, 240);
            _btnOfficialReport.ForeColor = Color.FromArgb(15, 23, 42);
            _btnOfficialReport.BorderColor = Color.FromArgb(203, 213, 225);
            _btnOfficialReport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnOfficialReport.Click += delegate { BatteryHealthService.OpenOfficialWindowsReport(); };
            diagRow.Controls.Add(_btnOfficialReport);

            _cardHero.Controls.Add(diagRow);
            _contentPanel.Controls.Add(_cardHero);
            curY += 186;

            // 4. Alert Thresholds Card (Modern Fluent Card with Sliders & Toggles)
            _cardThresholds = new ModernCard();
            _cardThresholds.Location = new Point(margin, curY);
            _cardThresholds.Size = new Size(cardW, 196);
            AddCardSectionHeader(_cardThresholds, "Charging Thresholds", Color.FromArgb(16, 185, 129));

            // Low Threshold Section
            int subY = 40;
            Label lblLowTitle = new Label();
            lblLowTitle.Text = "Low Battery Alarm";
            lblLowTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblLowTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblLowTitle.Location = new Point(16, subY);
            lblLowTitle.AutoSize = true;
            _cardThresholds.Controls.Add(lblLowTitle);

            Label lblLowDesc = new Label();
            lblLowDesc.Text = "Sounds alarm when battery drops while on battery power";
            lblLowDesc.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular);
            lblLowDesc.ForeColor = Color.FromArgb(100, 116, 139);
            lblLowDesc.Location = new Point(16, subY + 20);
            lblLowDesc.AutoSize = true;
            _cardThresholds.Controls.Add(lblLowDesc);

            _toggleLow = new ModernToggle();
            _toggleLow.Location = new Point(cardW - 64, subY + 2);
            _toggleLow.OnColor = Color.FromArgb(239, 68, 68);
            _toggleLow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _toggleLow.CheckedChanged += delegate
            {
                _sliderLow.Enabled = _toggleLow.Checked;
                _lblLowPill.ForeColor = _toggleLow.Checked ? Color.FromArgb(220, 38, 38) : Color.FromArgb(148, 163, 184);
            };
            _cardThresholds.Controls.Add(_toggleLow);

            // Low Slider Row
            subY += 44;
            _sliderLow = new ModernSlider();
            _sliderLow.Minimum = 5;
            _sliderLow.Maximum = 40;
            _sliderLow.AccentColor = Color.FromArgb(239, 68, 68);
            _sliderLow.Location = new Point(16, subY);
            _sliderLow.Size = new Size(cardW - 110, 26);
            _sliderLow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _sliderLow.ValueChanged += delegate
            {
                _lblLowPill.Text = _sliderLow.Value.ToString() + "%";
            };
            _cardThresholds.Controls.Add(_sliderLow);

            _lblLowPill = new Label();
            _lblLowPill.Text = "15%";
            _lblLowPill.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _lblLowPill.ForeColor = Color.FromArgb(220, 38, 38);
            _lblLowPill.Location = new Point(cardW - 80, subY - 2);
            _lblLowPill.Size = new Size(60, 26);
            _lblLowPill.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblLowPill.TextAlign = ContentAlignment.MiddleRight;
            _cardThresholds.Controls.Add(_lblLowPill);

            // Divider Line
            subY += 34;
            Panel div1 = new Panel();
            div1.Location = new Point(16, subY);
            div1.Size = new Size(cardW - 32, 1);
            div1.BackColor = Color.FromArgb(241, 245, 249);
            div1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _cardThresholds.Controls.Add(div1);

            // High Threshold Section
            subY += 10;
            Label lblHighTitle = new Label();
            lblHighTitle.Text = "High Battery Protection";
            lblHighTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblHighTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblHighTitle.Location = new Point(16, subY);
            lblHighTitle.AutoSize = true;
            _cardThresholds.Controls.Add(lblHighTitle);

            Label lblHighDesc = new Label();
            lblHighDesc.Text = "Alerts to unplug charger when limit is reached to preserve battery cells";
            lblHighDesc.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular);
            lblHighDesc.ForeColor = Color.FromArgb(100, 116, 139);
            lblHighDesc.Location = new Point(16, subY + 20);
            lblHighDesc.AutoSize = true;
            _cardThresholds.Controls.Add(lblHighDesc);

            _toggleHigh = new ModernToggle();
            _toggleHigh.Location = new Point(cardW - 64, subY + 2);
            _toggleHigh.OnColor = Color.FromArgb(16, 185, 129);
            _toggleHigh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _toggleHigh.CheckedChanged += delegate
            {
                _sliderHigh.Enabled = _toggleHigh.Checked;
                _lblHighPill.ForeColor = _toggleHigh.Checked ? Color.FromArgb(16, 185, 129) : Color.FromArgb(148, 163, 184);
            };
            _cardThresholds.Controls.Add(_toggleHigh);

            // High Slider Row
            subY += 44;
            _sliderHigh = new ModernSlider();
            _sliderHigh.Minimum = 60;
            _sliderHigh.Maximum = 100;
            _sliderHigh.AccentColor = Color.FromArgb(16, 185, 129);
            _sliderHigh.Location = new Point(16, subY);
            _sliderHigh.Size = new Size(cardW - 110, 26);
            _sliderHigh.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _sliderHigh.ValueChanged += delegate
            {
                _lblHighPill.Text = _sliderHigh.Value.ToString() + "%";
            };
            _cardThresholds.Controls.Add(_sliderHigh);

            _lblHighPill = new Label();
            _lblHighPill.Text = "90%";
            _lblHighPill.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _lblHighPill.ForeColor = Color.FromArgb(16, 185, 129);
            _lblHighPill.Location = new Point(cardW - 80, subY - 2);
            _lblHighPill.Size = new Size(60, 26);
            _lblHighPill.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblHighPill.TextAlign = ContentAlignment.MiddleRight;
            _cardThresholds.Controls.Add(_lblHighPill);

            _contentPanel.Controls.Add(_cardThresholds);
            curY += 210;

            // 5. Sound & Notification Preferences Card
            _cardAudio = new ModernCard();
            _cardAudio.Location = new Point(margin, curY);
            _cardAudio.Size = new Size(cardW, 140);
            AddCardSectionHeader(_cardAudio, "Alert Sound & Notifications", Color.FromArgb(79, 70, 229));

            int aY = 40;
            Label lblSoundTitle = new Label();
            lblSoundTitle.Text = "Alarm Sound Tone";
            lblSoundTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblSoundTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblSoundTitle.Location = new Point(16, aY);
            lblSoundTitle.AutoSize = true;
            _cardAudio.Controls.Add(lblSoundTitle);

            _cboSound = CreateFlatComboBox();
            _cboSound.Items.AddRange(new object[] { "Alarm (Digital Beep)", "Chime (Melodic)", "Siren (Urgent Alert)", "System Alert", "Custom WAV File" });
            _cboSound.Location = new Point(cardW - 275, aY);
            _cboSound.Size = new Size(175, 28);
            _cboSound.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cboSound.SelectedIndexChanged += OnSoundTypeChanged;
            _cardAudio.Controls.Add(_cboSound);

            _btnTestSound = new ModernButton();
            _btnTestSound.Text = "Play Test";
            _btnTestSound.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnTestSound.Location = new Point(cardW - 90, aY - 1);
            _btnTestSound.Size = new Size(74, 30);
            _btnTestSound.NormalColor = Color.FromArgb(79, 70, 229);
            _btnTestSound.HoverColor = Color.FromArgb(99, 102, 241);
            _btnTestSound.PressedColor = Color.FromArgb(67, 56, 202);
            _btnTestSound.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnTestSound.Click += delegate
            {
                string st = GetSelectedSoundType();
                _audio.TestSound(st, _txtCustomSound.Text);
            };
            _cardAudio.Controls.Add(_btnTestSound);

            _txtCustomSound = new TextBox();
            _txtCustomSound.Location = new Point(16, aY + 34);
            _txtCustomSound.Size = new Size(cardW - 120, 25);
            _txtCustomSound.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtCustomSound.Visible = false;
            _cardAudio.Controls.Add(_txtCustomSound);

            _btnBrowseSound = new ModernButton();
            _btnBrowseSound.Text = "Browse...";
            _btnBrowseSound.Location = new Point(cardW - 95, aY + 33);
            _btnBrowseSound.Size = new Size(79, 27);
            _btnBrowseSound.NormalColor = Color.FromArgb(241, 245, 249);
            _btnBrowseSound.HoverColor = Color.FromArgb(226, 232, 240);
            _btnBrowseSound.PressedColor = Color.FromArgb(203, 213, 225);
            _btnBrowseSound.ForeColor = Color.FromArgb(15, 23, 42);
            _btnBrowseSound.BorderColor = Color.FromArgb(203, 213, 225);
            _btnBrowseSound.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnBrowseSound.Visible = false;
            _btnBrowseSound.Click += OnBrowseCustomSound;
            _cardAudio.Controls.Add(_btnBrowseSound);

            // Repeat Sound Row
            aY += 46;
            Label lblRepeatTitle = new Label();
            lblRepeatTitle.Text = "Repeat Alert Interval";
            lblRepeatTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblRepeatTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblRepeatTitle.Location = new Point(16, aY);
            lblRepeatTitle.AutoSize = true;
            _cardAudio.Controls.Add(lblRepeatTitle);

            _cboInterval = CreateFlatComboBox();
            _cboInterval.Items.AddRange(new object[] { "3 seconds", "5 seconds", "10 seconds", "15 seconds", "30 seconds" });
            _cboInterval.Location = new Point(cardW - 185, aY);
            _cboInterval.Size = new Size(110, 28);
            _cboInterval.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cardAudio.Controls.Add(_cboInterval);

            _toggleLoop = new ModernToggle();
            _toggleLoop.Location = new Point(cardW - 64, aY + 2);
            _toggleLoop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _toggleLoop.CheckedChanged += delegate { _cboInterval.Enabled = _toggleLoop.Checked; };
            _cardAudio.Controls.Add(_toggleLoop);

            _contentPanel.Controls.Add(_cardAudio);
            curY += 154;

            // 6. Smart Rules (Quiet Hours & Full Charge Alert)
            _cardSmart = new ModernCard();
            _cardSmart.Location = new Point(margin, curY);
            _cardSmart.Size = new Size(cardW, 150);
            AddCardSectionHeader(_cardSmart, "Smart Rules & Quiet Hours", Color.FromArgb(147, 51, 234));

            int sY = 40;
            Label lblQuietTitle = new Label();
            lblQuietTitle.Text = "Quiet Hours (Night Mute)";
            lblQuietTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblQuietTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblQuietTitle.Location = new Point(16, sY);
            lblQuietTitle.AutoSize = true;
            _cardSmart.Controls.Add(lblQuietTitle);

            Label lblQuietDesc = new Label();
            lblQuietDesc.Text = "Mutes sound alerts during sleep or meetings (shows silent popup only)";
            lblQuietDesc.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular);
            lblQuietDesc.ForeColor = Color.FromArgb(100, 116, 139);
            lblQuietDesc.Location = new Point(16, sY + 20);
            lblQuietDesc.AutoSize = true;
            _cardSmart.Controls.Add(lblQuietDesc);

            _toggleQuiet = new ModernToggle();
            _toggleQuiet.Location = new Point(cardW - 64, sY + 2);
            _toggleQuiet.OnColor = Color.FromArgb(147, 51, 234);
            _toggleQuiet.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _toggleQuiet.CheckedChanged += delegate
            {
                _pnlQuietHoursTime.Visible = _toggleQuiet.Checked;
            };
            _cardSmart.Controls.Add(_toggleQuiet);

            // Sub row for hours
            _pnlQuietHoursTime = new Panel();
            _pnlQuietHoursTime.Location = new Point(16, sY + 42);
            _pnlQuietHoursTime.Size = new Size(cardW - 32, 32);
            _pnlQuietHoursTime.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblFrom = new Label();
            lblFrom.Text = "Mute from:";
            lblFrom.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            lblFrom.ForeColor = Color.FromArgb(71, 85, 105);
            lblFrom.Location = new Point(0, 6);
            lblFrom.AutoSize = true;
            _pnlQuietHoursTime.Controls.Add(lblFrom);

            _cboQuietStart = CreateFlatComboBox();
            PopulateHoursDropdown(_cboQuietStart);
            _cboQuietStart.Location = new Point(72, 2);
            _cboQuietStart.Size = new Size(95, 26);
            _pnlQuietHoursTime.Controls.Add(_cboQuietStart);

            Label lblTo = new Label();
            lblTo.Text = "until:";
            lblTo.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            lblTo.ForeColor = Color.FromArgb(71, 85, 105);
            lblTo.Location = new Point(175, 6);
            lblTo.AutoSize = true;
            _pnlQuietHoursTime.Controls.Add(lblTo);

            _cboQuietEnd = CreateFlatComboBox();
            PopulateHoursDropdown(_cboQuietEnd);
            _cboQuietEnd.Location = new Point(210, 2);
            _cboQuietEnd.Size = new Size(95, 26);
            _pnlQuietHoursTime.Controls.Add(_cboQuietEnd);

            _cardSmart.Controls.Add(_pnlQuietHoursTime);

            // Full Charge Row
            sY += 66;
            Label lblFullTitle = new Label();
            lblFullTitle.Text = "100% Full Charge Advisory";
            lblFullTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblFullTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblFullTitle.Location = new Point(16, sY);
            lblFullTitle.AutoSize = true;
            _cardSmart.Controls.Add(lblFullTitle);

            Label lblFullDesc = new Label();
            lblFullDesc.Text = "Alerts to disconnect AC when battery hits full 100% saturation";
            lblFullDesc.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular);
            lblFullDesc.ForeColor = Color.FromArgb(100, 116, 139);
            lblFullDesc.Location = new Point(16, sY + 20);
            lblFullDesc.AutoSize = true;
            _cardSmart.Controls.Add(lblFullDesc);

            _toggleFullCharge = new ModernToggle();
            _toggleFullCharge.Location = new Point(cardW - 64, sY + 2);
            _toggleFullCharge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cardSmart.Controls.Add(_toggleFullCharge);

            _contentPanel.Controls.Add(_cardSmart);
            curY += 164;

            // 7. System Preferences Card
            _cardSystem = new ModernCard();
            _cardSystem.Location = new Point(margin, curY);
            _cardSystem.Size = new Size(cardW, 140);
            AddCardSectionHeader(_cardSystem, "System & Startup Preferences", Color.FromArgb(71, 85, 105));

            int sysY = 40;
            // Row 1: Start with Windows
            Label lblSys1 = new Label();
            lblSys1.Text = "Start with Windows automatically";
            lblSys1.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblSys1.ForeColor = Color.FromArgb(15, 23, 42);
            lblSys1.Location = new Point(16, sysY);
            lblSys1.AutoSize = true;
            _cardSystem.Controls.Add(lblSys1);

            _toggleAutoStart = new ModernToggle();
            _toggleAutoStart.Location = new Point(cardW - 64, sysY);
            _toggleAutoStart.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cardSystem.Controls.Add(_toggleAutoStart);

            // Row 2: Start Minimized
            sysY += 30;
            Label lblSys2 = new Label();
            lblSys2.Text = "Start minimized to background tray";
            lblSys2.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblSys2.ForeColor = Color.FromArgb(71, 85, 105);
            lblSys2.Location = new Point(16, sysY);
            lblSys2.AutoSize = true;
            _cardSystem.Controls.Add(lblSys2);

            _toggleStartMin = new ModernToggle();
            _toggleStartMin.Location = new Point(cardW - 64, sysY);
            _toggleStartMin.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cardSystem.Controls.Add(_toggleStartMin);

            // Row 3: Close minimizes to tray
            sysY += 30;
            Label lblSys3 = new Label();
            lblSys3.Text = "Close button [X] minimizes to tray (keeps monitoring active)";
            lblSys3.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblSys3.ForeColor = Color.FromArgb(71, 85, 105);
            lblSys3.Location = new Point(16, sysY);
            lblSys3.AutoSize = true;
            _cardSystem.Controls.Add(lblSys3);

            _toggleCloseToTray = new ModernToggle();
            _toggleCloseToTray.Location = new Point(cardW - 64, sysY);
            _toggleCloseToTray.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cardSystem.Controls.Add(_toggleCloseToTray);

            // Row 4: Windows Notification Alerts
            sysY += 30;
            Label lblSys4 = new Label();
            lblSys4.Text = "Show Windows balloon & toast notifications";
            lblSys4.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblSys4.ForeColor = Color.FromArgb(71, 85, 105);
            lblSys4.Location = new Point(16, sysY);
            lblSys4.AutoSize = true;
            _cardSystem.Controls.Add(lblSys4);

            _toggleNotify = new ModernToggle();
            _toggleNotify.Location = new Point(cardW - 64, sysY);
            _toggleNotify.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cardSystem.Controls.Add(_toggleNotify);

            _cardSystem.Height = sysY + 44;
            _contentPanel.Controls.Add(_cardSystem);
            curY += _cardSystem.Height + 14;

            // 8. Eco Efficiency Banner
            _cardEfficiency = new ModernCard();
            _cardEfficiency.Location = new Point(margin, curY);
            _cardEfficiency.Size = new Size(cardW, 74);
            _cardEfficiency.BackColor = Color.FromArgb(240, 253, 244);
            _cardEfficiency.BorderColor = Color.FromArgb(187, 247, 208);

            Label lblEco1 = new Label();
            lblEco1.Text = "Zero-Bloat Performance: 0.0% CPU • ~18 MB RAM • < 0.001% Battery Impact";
            lblEco1.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblEco1.ForeColor = Color.FromArgb(21, 128, 61);
            lblEco1.Location = new Point(16, 12);
            lblEco1.AutoSize = true;
            _cardEfficiency.Controls.Add(lblEco1);

            Label lblEco2 = new Label();
            lblEco2.Text = "Native C# event-driven architecture. Sleeps between checks and uses virtually zero laptop energy.";
            lblEco2.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular);
            lblEco2.ForeColor = Color.FromArgb(22, 101, 52);
            lblEco2.Location = new Point(16, 36);
            lblEco2.Size = new Size(cardW - 32, 28);
            _cardEfficiency.Controls.Add(lblEco2);

            _contentPanel.Controls.Add(_cardEfficiency);
            curY += 86;

            // 9. Footer Action Buttons
            _btnSave = new ModernButton();
            _btnSave.Text = "Save Settings";
            _btnSave.Location = new Point(margin, curY);
            _btnSave.Size = new Size(160, 42);
            _btnSave.NormalColor = Color.FromArgb(16, 185, 129);
            _btnSave.HoverColor = Color.FromArgb(5, 150, 105);
            _btnSave.PressedColor = Color.FromArgb(4, 120, 87);
            _btnSave.CornerRadius = 8f;
            _btnSave.Click += OnSaveClicked;
            _contentPanel.Controls.Add(_btnSave);

            _btnReset = new ModernButton();
            _btnReset.Text = "Reset Defaults";
            _btnReset.Location = new Point(margin + 172, curY);
            _btnReset.Size = new Size(140, 42);
            _btnReset.NormalColor = Color.White;
            _btnReset.HoverColor = Color.FromArgb(241, 245, 249);
            _btnReset.PressedColor = Color.FromArgb(226, 232, 240);
            _btnReset.ForeColor = Color.FromArgb(51, 65, 85);
            _btnReset.BorderColor = Color.FromArgb(203, 213, 225);
            _btnReset.CornerRadius = 8f;
            _btnReset.Click += OnResetDefaultsClicked;
            _contentPanel.Controls.Add(_btnReset);

            _btnMinimize = new ModernButton();
            _btnMinimize.Text = "Hide to Tray";
            _btnMinimize.Location = new Point(margin + 322, curY);
            _btnMinimize.Size = new Size(160, 42);
            _btnMinimize.NormalColor = Color.FromArgb(37, 99, 235);
            _btnMinimize.HoverColor = Color.FromArgb(29, 78, 216);
            _btnMinimize.PressedColor = Color.FromArgb(30, 64, 175);
            _btnMinimize.CornerRadius = 8f;
            _btnMinimize.Click += delegate { HideToTray(); };
            _contentPanel.Controls.Add(_btnMinimize);

            curY += 66;
        }

        private ComboBox CreateFlatComboBox()
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Color.White;
            c.ForeColor = Color.FromArgb(15, 23, 42);
            c.Font = new Font("Segoe UI", 9.2f, FontStyle.Regular);
            return c;
        }

        private void AddCardSectionHeader(Control card, string title, Color accent)
        {
            card.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                RectangleF pillRect = new RectangleF(14, 13, 4, 15);
                using (GraphicsPath pp = BatteryWidget.RoundedRect(pillRect, 2f))
                using (SolidBrush pb = new SolidBrush(accent))
                {
                    g.FillPath(pb, pp);
                }
            };

            Label lbl = new Label();
            lbl.Text = title;
            lbl.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            lbl.ForeColor = Color.FromArgb(15, 23, 42);
            lbl.Location = new Point(24, 10);
            lbl.AutoSize = true;
            card.Controls.Add(lbl);
        }

        private void PopulateHoursDropdown(ComboBox cbo)
        {
            for (int h = 0; h < 24; h++)
            {
                DateTime dt = DateTime.Today.AddHours(h);
                cbo.Items.Add(dt.ToString("hh:00 tt"));
            }
        }

        private void LoadBatteryHealthAsync()
        {
            BatteryHealthService.QueryHealthStatsAsync(delegate(BatteryHealthStats stats)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<BatteryHealthStats>(UpdateHealthUI), stats);
                    return;
                }
                UpdateHealthUI(stats);
            });
        }

        private void UpdateHealthUI(BatteryHealthStats stats)
        {
            if (stats.IsAvailable && stats.DesignCapacity > 0)
            {
                _lblHealthBadge.Text = string.Format("Health: {0}% ({1})",
                    stats.HealthPercent, stats.HealthPercent >= 80 ? "Good" : "Degraded");
                _lblHealthBadge.ForeColor = stats.HealthPercent >= 80 ? Color.FromArgb(16, 185, 129) : Color.FromArgb(245, 158, 11);

                _lblCyclesBadge.Text = string.Format("• Cycles: {0}", stats.CycleCount > 0 ? stats.CycleCount.ToString() : "N/A");
            }
            else
            {
                _lblHealthBadge.Text = "Battery Diagnostics Ready";
                _lblCyclesBadge.Text = "";
            }
        }

        private void ApplySettingsToUI()
        {
            _sliderLow.Value = _settings.LowThreshold;
            _lblLowPill.Text = _sliderLow.Value.ToString() + "%";
            _toggleLow.Checked = _settings.EnableLowAlarm;
            _sliderLow.Enabled = _toggleLow.Checked;

            _sliderHigh.Value = _settings.HighThreshold;
            _lblHighPill.Text = _sliderHigh.Value.ToString() + "%";
            _toggleHigh.Checked = _settings.EnableHighAlarm;
            _sliderHigh.Enabled = _toggleHigh.Checked;

            string sound = _settings.SoundType;
            if (string.Equals(sound, "Chime", StringComparison.OrdinalIgnoreCase)) _cboSound.SelectedIndex = 1;
            else if (string.Equals(sound, "Siren", StringComparison.OrdinalIgnoreCase)) _cboSound.SelectedIndex = 2;
            else if (string.Equals(sound, "System", StringComparison.OrdinalIgnoreCase)) _cboSound.SelectedIndex = 3;
            else if (string.Equals(sound, "Custom", StringComparison.OrdinalIgnoreCase)) _cboSound.SelectedIndex = 4;
            else _cboSound.SelectedIndex = 0;

            _txtCustomSound.Text = _settings.CustomSoundPath ?? "";

            _toggleLoop.Checked = _settings.LoopAlarm;
            _cboInterval.Enabled = _toggleLoop.Checked;

            int sec = _settings.AlarmIntervalSeconds;
            if (sec <= 3) _cboInterval.SelectedIndex = 0;
            else if (sec <= 5) _cboInterval.SelectedIndex = 1;
            else if (sec <= 10) _cboInterval.SelectedIndex = 2;
            else if (sec <= 15) _cboInterval.SelectedIndex = 3;
            else _cboInterval.SelectedIndex = 4;

            _toggleQuiet.Checked = _settings.QuietHoursEnabled;
            _cboQuietStart.SelectedIndex = Math.Max(0, Math.Min(23, _settings.QuietHoursStart));
            _cboQuietEnd.SelectedIndex = Math.Max(0, Math.Min(23, _settings.QuietHoursEnd));
            _pnlQuietHoursTime.Visible = _toggleQuiet.Checked;

            _toggleFullCharge.Checked = _settings.FullChargeAlert;

            _toggleAutoStart.Checked = _settings.StartWithWindows;
            _toggleStartMin.Checked = _settings.StartMinimized;
            _toggleCloseToTray.Checked = _settings.MinimizeToTrayOnClose;
            _toggleNotify.Checked = _settings.ShowNotifications;

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
            _settings.LowThreshold = _sliderLow.Value;
            _settings.EnableLowAlarm = _toggleLow.Checked;

            _settings.HighThreshold = _sliderHigh.Value;
            _settings.EnableHighAlarm = _toggleHigh.Checked;

            _settings.SoundType = GetSelectedSoundType();
            _settings.CustomSoundPath = _txtCustomSound.Text;

            _settings.LoopAlarm = _toggleLoop.Checked;
            switch (_cboInterval.SelectedIndex)
            {
                case 0: _settings.AlarmIntervalSeconds = 3; break;
                case 1: _settings.AlarmIntervalSeconds = 5; break;
                case 2: _settings.AlarmIntervalSeconds = 10; break;
                case 3: _settings.AlarmIntervalSeconds = 15; break;
                default: _settings.AlarmIntervalSeconds = 30; break;
            }

            _settings.QuietHoursEnabled = _toggleQuiet.Checked;
            _settings.QuietHoursStart = _cboQuietStart.SelectedIndex;
            _settings.QuietHoursEnd = _cboQuietEnd.SelectedIndex;
            _settings.FullChargeAlert = _toggleFullCharge.Checked;

            _settings.StartWithWindows = _toggleAutoStart.Checked;
            _settings.StartMinimized = _toggleStartMin.Checked;
            _settings.MinimizeToTrayOnClose = _toggleCloseToTray.Checked;
            _settings.ShowNotifications = _toggleNotify.Checked;

            _settings.Save();
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

            // Update in-window banner
            if (state == ActiveAlarmState.LowBattery)
            {
                _bannerLabel.Text = string.Format("LOW BATTERY ALERT ({0}%): Please connect charger now!", percent);
                _bannerPanel.BackColor = Color.FromArgb(239, 68, 68);
            }
            else
            {
                _bannerLabel.Text = string.Format("CHARGE THRESHOLD ({0}%): Please remove charger now!", percent);
                _bannerPanel.BackColor = Color.FromArgb(16, 185, 129);
            }
            _bannerPanel.Visible = true;

            // Show Top-Most Pop-up Window (user requested)
            try
            {
                if (_activePopup != null && !_activePopup.IsDisposed)
                {
                    _activePopup.Close();
                }
                _activePopup = new AlertPopupForm(state, percent, delegate
                {
                    // User clicked "OK (Pause checking for 5 minutes)"
                    _monitor.SnoozeAlarm(5);
                });
                _activePopup.Show();
            }
            catch { }
        }

        private void OnAlarmDismissed(string reason)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnAlarmDismissed), reason);
                return;
            }

            _bannerPanel.Visible = false;
            if (_activePopup != null && !_activePopup.IsDisposed)
            {
                try { _activePopup.Close(); } catch { }
                _activePopup = null;
            }
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
                _tray.ShowTip("BatteryGuard Pro", "Running silently in background tray. Double-click tray icon to open.");
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (_contentPanel != null)
            {
                int margin = 20;
                int cardW = _contentPanel.ClientSize.Width - (margin * 2) - 8;
                if (cardW < 460) cardW = 460;

                Control[] cards = new Control[] {
                    _bannerPanel, _cardHero, _cardThresholds, _cardAudio,
                    _cardSmart, _cardSystem, _cardEfficiency
                };

                foreach (Control c in cards)
                {
                    if (c != null)
                    {
                        c.Width = cardW;
                        c.Invalidate();
                    }
                }

                if (_batteryWidget != null)
                {
                    _batteryWidget.Width = cardW - 24;
                }
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
            if (_activePopup != null && !_activePopup.IsDisposed)
            {
                try { _activePopup.Close(); } catch { }
            }
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }

            base.OnFormClosing(e);
        }
    }
}
