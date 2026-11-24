using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace BatteryAlarmApp
{
    public class AppSettings
    {
        private const string AppRegistryName = "BatteryAlarmPro";
        private static readonly string ExePath = Assembly.GetExecutingAssembly().Location;

        public int LowThreshold { get; set; }
        public int HighThreshold { get; set; }
        public bool EnableLowAlarm { get; set; }
        public bool EnableHighAlarm { get; set; }
        public string SoundType { get; set; } // "Chime", "Alarm", "Siren", "System", "Custom"
        public string CustomSoundPath { get; set; }
        public bool LoopAlarm { get; set; }
        public int AlarmIntervalSeconds { get; set; }
        public bool ShowNotifications { get; set; }
        public bool StartWithWindows { get; set; }
        public bool StartMinimized { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }

        public AppSettings()
        {
            SetDefaults();
        }

        public void SetDefaults()
        {
            LowThreshold = 15;
            HighThreshold = 90;
            EnableLowAlarm = true;
            EnableHighAlarm = true;
            SoundType = "Alarm";
            CustomSoundPath = "";
            LoopAlarm = true;
            AlarmIntervalSeconds = 5;
            ShowNotifications = true;
            StartWithWindows = false;
            StartMinimized = false;
            MinimizeToTrayOnClose = true;
        }

        public static string GetConfigFilePath()
        {
            string exeDir = Path.GetDirectoryName(ExePath);
            string portablePath = Path.Combine(exeDir, "battery_settings.json");
            
            // If portable config exists alongside exe, use it
            if (File.Exists(portablePath))
                return portablePath;

            // Otherwise check if exeDir is writable (e.g. desktop/portable folder)
            try
            {
                string testFile = Path.Combine(exeDir, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return portablePath; // Portable mode is supported!
            }
            catch
            {
                // If exe is in Program Files or read-only location, fallback to AppData
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryAlarmPro");
                if (!Directory.Exists(appDataDir))
                {
                    try { Directory.CreateDirectory(appDataDir); } catch { }
                }
                return Path.Combine(appDataDir, "battery_settings.json");
            }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            string path = GetConfigFilePath();

            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    settings.ParseJson(json);
                }
            }
            catch
            {
                // Fallback to defaults on corrupt file
            }

            // Sync with actual registry startup state
            settings.StartWithWindows = settings.IsStartupRegistryEnabled();

            return settings;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigFilePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = ToJsonString();
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch
            {
                // Silently handle write failures
            }

            ApplyStartupRegistry(StartWithWindows);
        }

        public bool IsStartupRegistryEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(AppRegistryName);
                        if (value != null && value.ToString().Length > 0)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public void ApplyStartupRegistry(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            // Include argument --minimized if requested
                            string cmd = "\"" + ExePath + "\" --minimized";
                            key.SetValue(AppRegistryName, cmd);
                        }
                        else
                        {
                            if (key.GetValue(AppRegistryName) != null)
                            {
                                key.DeleteValue(AppRegistryName, false);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private string ToJsonString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format("  \"LowThreshold\": {0},", LowThreshold));
            sb.AppendLine(string.Format("  \"HighThreshold\": {0},", HighThreshold));
            sb.AppendLine(string.Format("  \"EnableLowAlarm\": {0},", EnableLowAlarm.ToString().ToLower()));
            sb.AppendLine(string.Format("  \"EnableHighAlarm\": {0},", EnableHighAlarm.ToString().ToLower()));
            sb.AppendLine(string.Format("  \"SoundType\": \"{0}\",", EscapeJson(SoundType)));
            sb.AppendLine(string.Format("  \"CustomSoundPath\": \"{0}\",", EscapeJson(CustomSoundPath)));
            sb.AppendLine(string.Format("  \"LoopAlarm\": {0},", LoopAlarm.ToString().ToLower()));
            sb.AppendLine(string.Format("  \"AlarmIntervalSeconds\": {0},", AlarmIntervalSeconds));
            sb.AppendLine(string.Format("  \"ShowNotifications\": {0},", ShowNotifications.ToString().ToLower()));
            sb.AppendLine(string.Format("  \"StartWithWindows\": {0},", StartWithWindows.ToString().ToLower()));
            sb.AppendLine(string.Format("  \"StartMinimized\": {0},", StartMinimized.ToString().ToLower()));
            sb.AppendLine(string.Format("  \"MinimizeToTrayOnClose\": {0}", MinimizeToTrayOnClose.ToString().ToLower()));
            sb.AppendLine("}");
            return sb.ToString();
        }

        private void ParseJson(string json)
        {
            LowThreshold = ParseInt(json, "LowThreshold", LowThreshold);
            HighThreshold = ParseInt(json, "HighThreshold", HighThreshold);
            EnableLowAlarm = ParseBool(json, "EnableLowAlarm", EnableLowAlarm);
            EnableHighAlarm = ParseBool(json, "EnableHighAlarm", EnableHighAlarm);
            SoundType = ParseString(json, "SoundType", SoundType);
            CustomSoundPath = ParseString(json, "CustomSoundPath", CustomSoundPath);
            LoopAlarm = ParseBool(json, "LoopAlarm", LoopAlarm);
            AlarmIntervalSeconds = ParseInt(json, "AlarmIntervalSeconds", AlarmIntervalSeconds);
            ShowNotifications = ParseBool(json, "ShowNotifications", ShowNotifications);
            StartWithWindows = ParseBool(json, "StartWithWindows", StartWithWindows);
            StartMinimized = ParseBool(json, "StartMinimized", StartMinimized);
            MinimizeToTrayOnClose = ParseBool(json, "MinimizeToTrayOnClose", MinimizeToTrayOnClose);

            // Validation guards
            if (LowThreshold < 5) LowThreshold = 5;
            if (LowThreshold > 50) LowThreshold = 50;
            if (HighThreshold < 50) HighThreshold = 50;
            if (HighThreshold > 100) HighThreshold = 100;
            if (AlarmIntervalSeconds < 2) AlarmIntervalSeconds = 2;
            if (AlarmIntervalSeconds > 60) AlarmIntervalSeconds = 60;
        }

        private static int ParseInt(string json, string key, int defaultValue)
        {
            string val = ExtractValue(json, key);
            int res;
            if (val != null && int.TryParse(val.Trim(), out res))
                return res;
            return defaultValue;
        }

        private static bool ParseBool(string json, string key, bool defaultValue)
        {
            string val = ExtractValue(json, key);
            bool res;
            if (val != null && bool.TryParse(val.Trim(), out res))
                return res;
            return defaultValue;
        }

        private static string ParseString(string json, string key, string defaultValue)
        {
            string val = ExtractValue(json, key);
            if (val != null)
            {
                string trimmed = val.Trim();
                if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length >= 2)
                {
                    return UnescapeJson(trimmed.Substring(1, trimmed.Length - 2));
                }
                return trimmed;
            }
            return defaultValue;
        }

        private static string ExtractValue(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int endComma = json.IndexOf(',', start);
            int endBrace = json.IndexOf('}', start);
            int end = json.Length;
            if (endComma >= 0) end = Math.Min(end, endComma);
            if (endBrace >= 0) end = Math.Min(end, endBrace);
            return json.Substring(start, end - start).Trim();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
