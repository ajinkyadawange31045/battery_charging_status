using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BatteryAlarmApp
{
    public enum ActiveAlarmState
    {
        None,
        LowBattery,
        HighBattery,
        FullCharge
    }

    public class BatterySnapshot
    {
        public bool HasBattery { get; set; }
        public int Percent { get; set; }
        public bool IsPluggedIn { get; set; }
        public bool IsCharging { get; set; }
        public BatteryChargeStatus ChargeStatus { get; set; }
        public int SecondsRemaining { get; set; }

        public string GetRemainingTimeString()
        {
            if (SecondsRemaining <= 0) return "";
            int hours = SecondsRemaining / 3600;
            int mins = (SecondsRemaining % 3600) / 60;
            if (hours > 0)
                return string.Format("{0}h {1}m remaining", hours, mins);
            return string.Format("{0}m remaining", mins);
        }

        public string GetStatusDescription()
        {
            if (!HasBattery)
                return "Desktop / AC Power (No Battery Detected)";

            if (IsPluggedIn)
            {
                if (Percent >= 100)
                    return "Fully Charged (Plugged In)";
                return "Plugged In (Charging)";
            }
            else
            {
                string rem = GetRemainingTimeString();
                if (!string.IsNullOrEmpty(rem))
                    return "On Battery (" + rem + ")";
                return "On Battery (Discharging)";
            }
        }
    }

    public class BatteryMonitor
    {
        private readonly Timer _pollTimer;
        private readonly AppSettings _settings;
        private readonly AudioAlert _audio;

        private ActiveAlarmState _currentAlarmState = ActiveAlarmState.None;
        private bool _alarmDismissedForCurrentCycle = false;
        private DateTime? _snoozeUntil = null;
        private int _lastPercent = -1;
        private bool _lastPluggedIn = false;

        public event Action<BatterySnapshot> OnStatusChanged;
        public event Action<ActiveAlarmState, int, string> OnAlarmTriggered;
        public event Action<string> OnAlarmDismissed;

        public ActiveAlarmState CurrentAlarmState
        {
            get { return _currentAlarmState; }
        }

        public bool IsAlarmRinging
        {
            get { return _audio.IsPlaying; }
        }

        public BatterySnapshot LastSnapshot { get; private set; }

        public BatteryMonitor(AppSettings settings, AudioAlert audio)
        {
            _settings = settings;
            _audio = audio;

            _pollTimer = new Timer();
            _pollTimer.Interval = 2500; // Check every 2.5 seconds
            _pollTimer.Tick += delegate { CheckStatus(); };

            SystemEvents.PowerModeChanged += delegate(object sender, PowerModeChangedEventArgs e)
            {
                CheckStatus();
            };
        }

        public void Start()
        {
            _pollTimer.Start();
            CheckStatus();
        }

        public void Stop()
        {
            _pollTimer.Stop();
            StopAlarmInternal("Monitor stopped");
        }

        public void CheckStatus()
        {
            BatterySnapshot snap = QueryBattery();
            LastSnapshot = snap;

            bool stateChanged = (snap.Percent != _lastPercent || snap.IsPluggedIn != _lastPluggedIn);
            _lastPercent = snap.Percent;
            _lastPluggedIn = snap.IsPluggedIn;

            EvaluateAlarms(snap, stateChanged);

            if (OnStatusChanged != null)
            {
                OnStatusChanged(snap);
            }
        }

        public void SnoozeAlarm(int minutes)
        {
            _snoozeUntil = DateTime.Now.AddMinutes(minutes);
            _audio.StopAlarm();
            if (OnAlarmDismissed != null)
            {
                OnAlarmDismissed(string.Format("Snoozed for {0} minutes", minutes));
            }
        }

        public void DismissCurrentAlarm()
        {
            _alarmDismissedForCurrentCycle = true;
            StopAlarmInternal("Dismissed by user");
        }

        private void StopAlarmInternal(string reason)
        {
            _audio.StopAlarm();
            ActiveAlarmState previous = _currentAlarmState;
            _currentAlarmState = ActiveAlarmState.None;

            if (previous != ActiveAlarmState.None && OnAlarmDismissed != null)
            {
                OnAlarmDismissed(reason);
            }
        }

        private void EvaluateAlarms(BatterySnapshot snap, bool stateChanged)
        {
            if (!snap.HasBattery)
            {
                if (_currentAlarmState != ActiveAlarmState.None)
                {
                    StopAlarmInternal("No battery on system");
                }
                return;
            }

            ActiveAlarmState targetAlarm = ActiveAlarmState.None;

            // Scenario 1: Low battery alarm (<= LowThreshold AND discharging)
            if (_settings.EnableLowAlarm && snap.Percent <= _settings.LowThreshold && !snap.IsPluggedIn)
            {
                targetAlarm = ActiveAlarmState.LowBattery;
            }
            // Scenario 2: High battery alarm (>= HighThreshold AND plugged in)
            else if (_settings.EnableHighAlarm && snap.Percent >= _settings.HighThreshold && snap.IsPluggedIn)
            {
                if (snap.Percent >= 100 && _settings.FullChargeAlert)
                    targetAlarm = ActiveAlarmState.FullCharge;
                else
                    targetAlarm = ActiveAlarmState.HighBattery;
            }

            // Auto-stop when user plugs in charger during low battery alarm
            if (_currentAlarmState == ActiveAlarmState.LowBattery && snap.IsPluggedIn)
            {
                _alarmDismissedForCurrentCycle = false;
                _snoozeUntil = null;
                StopAlarmInternal("Charger connected");
                return;
            }

            // Auto-stop when user unplugs charger during high battery alarm
            if ((_currentAlarmState == ActiveAlarmState.HighBattery || _currentAlarmState == ActiveAlarmState.FullCharge) && !snap.IsPluggedIn)
            {
                _alarmDismissedForCurrentCycle = false;
                _snoozeUntil = null;
                StopAlarmInternal("Charger disconnected");
                return;
            }

            // Normal range: reset dismissed flag and snooze
            if (targetAlarm == ActiveAlarmState.None)
            {
                if (_currentAlarmState != ActiveAlarmState.None)
                {
                    StopAlarmInternal("Battery in normal range");
                }
                _alarmDismissedForCurrentCycle = false;
                _snoozeUntil = null;
                return;
            }

            // Handle snooze
            if (_snoozeUntil.HasValue)
            {
                if (DateTime.Now < _snoozeUntil.Value)
                    return;
                _snoozeUntil = null;
            }

            // If user dismissed this cycle
            if (_alarmDismissedForCurrentCycle)
            {
                return;
            }

            // Check Quiet Hours
            bool inQuietHours = _settings.IsInQuietHours();

            // Trigger alarm if not already ringing for this state
            if (_currentAlarmState != targetAlarm || (!_audio.IsPlaying && !inQuietHours))
            {
                _currentAlarmState = targetAlarm;
                string message = "";

                if (targetAlarm == ActiveAlarmState.LowBattery)
                {
                    message = string.Format("⚠️ Low Battery Alert ({0}%)! Please connect charger.", snap.Percent);
                }
                else if (targetAlarm == ActiveAlarmState.FullCharge)
                {
                    message = "⚡ Battery 100% Fully Charged! Unplug to preserve battery lifespan.";
                }
                else
                {
                    message = string.Format("⚡ Charge Threshold Reached ({0}%)! Please unplug charger.", snap.Percent);
                }

                if (inQuietHours)
                {
                    message += " (Audio Muted: Quiet Hours Active)";
                    _audio.StopAlarm();
                }
                else
                {
                    _audio.StartAlarm(_settings.SoundType, _settings.CustomSoundPath, _settings.LoopAlarm, _settings.AlarmIntervalSeconds);
                }

                if (OnAlarmTriggered != null)
                {
                    OnAlarmTriggered(targetAlarm, snap.Percent, message);
                }
            }
        }

        public static BatterySnapshot QueryBattery()
        {
            BatterySnapshot snap = new BatterySnapshot();
            PowerStatus p = SystemInformation.PowerStatus;

            snap.ChargeStatus = p.BatteryChargeStatus;
            bool noBattery = (p.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == BatteryChargeStatus.NoSystemBattery;

            if (noBattery || p.BatteryLifePercent > 1.0f || p.BatteryLifePercent < 0.0f)
            {
                snap.HasBattery = false;
                snap.Percent = 100;
                snap.IsPluggedIn = true;
                snap.IsCharging = false;
                snap.SecondsRemaining = -1;
                return snap;
            }

            snap.HasBattery = true;
            snap.Percent = (int)Math.Round(p.BatteryLifePercent * 100.0f);
            if (snap.Percent < 0) snap.Percent = 0;
            if (snap.Percent > 100) snap.Percent = 100;

            snap.IsPluggedIn = (p.PowerLineStatus == PowerLineStatus.Online);
            snap.IsCharging = (p.BatteryChargeStatus & BatteryChargeStatus.Charging) == BatteryChargeStatus.Charging;
            snap.SecondsRemaining = p.BatteryLifeRemaining;

            return snap;
        }
    }
}
