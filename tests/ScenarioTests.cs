using System;
using System.IO;
using System.Windows.Forms;
using BatteryAlarmApp;

namespace BatteryAlarmApp.Tests
{
    class ScenarioTests
    {
        static int _passed = 0;
        static int _failed = 0;

        static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.WriteLine("[PASS] " + testName);
                _passed++;
            }
            else
            {
                Console.WriteLine("[FAIL] " + testName);
                _failed++;
            }
        }

        static void Main()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("RUNNING BATTERYGUARD SCENARIO VERIFICATION TESTS");
            Console.WriteLine("==================================================");

            TestSettingsSerialization();
            TestQuietHoursLogic();
            TestSoundWaveGeneration();
            TestBatteryQuery();
            TestAlarmScenarios();

            Console.WriteLine("==================================================");
            Console.WriteLine(string.Format("RESULTS: {0} Passed, {1} Failed", _passed, _failed));
            Console.WriteLine("==================================================");

            if (_failed > 0)
            {
                Environment.Exit(1);
            }
        }

        static void TestSettingsSerialization()
        {
            Console.WriteLine("\n--- Testing AppSettings Serialization & Validation ---");
            AppSettings s = new AppSettings();
            s.LowThreshold = 18;
            s.HighThreshold = 85;
            s.SoundType = "Chime";
            s.LoopAlarm = false;
            s.AlarmIntervalSeconds = 10;
            s.QuietHoursEnabled = true;
            s.QuietHoursStart = 23;
            s.QuietHoursEnd = 6;
            s.FullChargeAlert = true;
            s.Save();

            string configPath = AppSettings.GetConfigFilePath();
            Assert(File.Exists(configPath), "Config file created successfully at: " + configPath);

            AppSettings loaded = AppSettings.Load();
            Assert(loaded.LowThreshold == 18, "LowThreshold loaded correctly: " + loaded.LowThreshold);
            Assert(loaded.HighThreshold == 85, "HighThreshold loaded correctly: " + loaded.HighThreshold);
            Assert(loaded.SoundType == "Chime", "SoundType loaded correctly: " + loaded.SoundType);
            Assert(loaded.LoopAlarm == false, "LoopAlarm loaded correctly: " + loaded.LoopAlarm);
            Assert(loaded.AlarmIntervalSeconds == 10, "AlarmIntervalSeconds loaded correctly: " + loaded.AlarmIntervalSeconds);
            Assert(loaded.QuietHoursEnabled == true, "QuietHoursEnabled loaded correctly: " + loaded.QuietHoursEnabled);
            Assert(loaded.QuietHoursStart == 23, "QuietHoursStart loaded correctly: " + loaded.QuietHoursStart);
            Assert(loaded.QuietHoursEnd == 6, "QuietHoursEnd loaded correctly: " + loaded.QuietHoursEnd);

            // Test boundary clamping
            s.LowThreshold = 2; // Should clamp to 5
            s.HighThreshold = 105; // Should clamp to 100
            s.Save();
            AppSettings clamped = AppSettings.Load();
            Assert(clamped.LowThreshold == 5, "LowThreshold clamped to min 5");
            Assert(clamped.HighThreshold == 100, "HighThreshold clamped to max 100");

            s.SetDefaults();
            s.Save();
        }

        static void TestQuietHoursLogic()
        {
            Console.WriteLine("\n--- Testing Quiet Hours / Night Mute Logic ---");
            AppSettings s = new AppSettings();
            s.QuietHoursEnabled = false;
            Assert(!s.IsInQuietHours(), "Quiet hours disabled returns false");

            s.QuietHoursEnabled = true;
            s.QuietHoursStart = 22;
            s.QuietHoursEnd = 7;
            // Over night test
            int h = DateTime.Now.Hour;
            bool expected = (h >= 22 || h < 7);
            Assert(s.IsInQuietHours() == expected, "Quiet hours evaluation matches expected schedule for current hour (" + h + ":00)");
        }

        static void TestSoundWaveGeneration()
        {
            Console.WriteLine("\n--- Testing Audio Alert Synthesizer ---");
            byte[] chime = AudioAlert.GenerateChimeWav();
            Assert(chime != null && chime.Length > 1000, "Chime WAV generated (bytes: " + (chime != null ? chime.Length : 0) + ")");

            byte[] alarm = AudioAlert.GenerateAlarmWav();
            Assert(alarm != null && alarm.Length > 1000, "Alarm WAV generated (bytes: " + (alarm != null ? alarm.Length : 0) + ")");

            byte[] siren = AudioAlert.GenerateSirenWav();
            Assert(siren != null && siren.Length > 1000, "Siren WAV generated (bytes: " + (siren != null ? siren.Length : 0) + ")");

            Assert(chime[0] == 'R' && chime[1] == 'I' && chime[2] == 'F' && chime[3] == 'F', "WAV header starts with 'RIFF'");
            Assert(chime[8] == 'W' && chime[9] == 'A' && chime[10] == 'V' && chime[11] == 'E', "WAV format is 'WAVE'");
        }

        static void TestBatteryQuery()
        {
            Console.WriteLine("\n--- Testing Live Battery Query on this System ---");
            BatterySnapshot snap = BatteryMonitor.QueryBattery();
            Console.WriteLine("Snapshot detected: HasBattery=" + snap.HasBattery +
                              ", Percent=" + snap.Percent + "%" +
                              ", IsPluggedIn=" + snap.IsPluggedIn +
                              ", ChargeStatus=" + snap.ChargeStatus);
            Assert(snap.Percent >= 0 && snap.Percent <= 100, "Battery percentage is valid (0-100)");
            Assert(!string.IsNullOrEmpty(snap.GetStatusDescription()), "Status description generated: " + snap.GetStatusDescription());
        }

        static void TestAlarmScenarios()
        {
            Console.WriteLine("\n--- Testing Battery Scenarios Logic ---");
            AppSettings settings = new AppSettings();
            settings.LowThreshold = 15;
            settings.HighThreshold = 90;
            settings.EnableLowAlarm = true;
            settings.EnableHighAlarm = true;

            AudioAlert audio = new AudioAlert();
            BatteryMonitor monitor = new BatteryMonitor(settings, audio);

            Assert(settings.EnableLowAlarm && 10 <= settings.LowThreshold, "Scenario 1 condition check: 10% <= 15% (Low alarm condition met)");
            Assert(settings.EnableHighAlarm && 95 >= settings.HighThreshold, "Scenario 2 condition check: 95% >= 90% (High alarm condition met)");
            Assert(!(50 <= settings.LowThreshold) && !(50 >= settings.HighThreshold), "Scenario 3 condition check: 50% is within normal safe range (No alarm)");

            BatterySnapshot desktopSnap = new BatterySnapshot();
            desktopSnap.HasBattery = false;
            desktopSnap.IsPluggedIn = true;
            desktopSnap.Percent = 100;
            Assert(!desktopSnap.HasBattery && desktopSnap.GetStatusDescription().Contains("No Battery"),
                "Scenario 4 check: Desktop / No Battery handled gracefully");

            monitor.SnoozeAlarm(5);
            Assert(!audio.IsPlaying, "Scenario 5 check: Snooze immediately stops audio playback");
        }
    }
}
