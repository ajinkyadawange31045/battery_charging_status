# 🔋 BatteryGuard Pro

A **ultra-lightweight (under 60 KB)**, portable, native Windows application that monitors laptop battery status in real-time, displays charge analytics, and sounds customizable alarms when the battery falls below or exceeds safe thresholds.

Designed specifically to protect lithium-ion battery health, prevent unwanted shutdowns, and run silently in the system tray.

---

## ✨ Features

- **⚡ High Battery Alarm (Default 90%)**:
  Alerts you when charging reaches 90% (or your preferred limit) so you can unplug the charger and prolong your battery's lifespan.
  - *Auto-Stop*: Unplugging the charger immediately silences the alarm!
- **🪫 Low Battery Alarm (Default 15%)**:
  Alerts you when battery drops below 15% (or your preferred limit) while discharging, giving you plenty of time to plug in the charger before your laptop shuts down.
  - *Auto-Stop*: Plugging in the charger immediately silences the alarm!
- **🪶 Ultra-Lightweight & Portable**:
  - **Single standalone `.exe` (~58 KB)** — no installers, no bloat, no external DLLs.
  - Instant startup (< 0.2s) and negligible memory footprint (~20 MB in tray).
  - Can be copied to any USB drive and run on **any Windows 10 or 11 laptop** directly.
- **🔊 Built-In Sound Synthesizer**:
  - Generates crisp in-memory audio alerts without relying on external `.wav` files:
    1. **Digital Alarm (Beep-Beep)**: Classic, highly noticeable alarm buzzer.
    2. **Melodic Chime**: Pleasant, gentle chord progression.
    3. **Urgent Siren**: Alternating two-tone emergency alert.
    4. **Windows System Alert**: Uses default Windows sound scheme.
    5. **Custom `.WAV` File**: Lets you select your own custom ringtone/audio file!
  - Loop mode or single chime, with configurable repeat interval (3s, 5s, 10s, 15s, 30s).
  - Interactive **Test Sound** button.
- **⏰ Smart Snooze & Dismiss**:
  - Quick action buttons right on the active alarm banner: `[Stop Alarm]`, `[Snooze 5 Min]`, `[Snooze 10 Min]`.
  - Also accessible via the right-click System Tray menu.
- **🖥️ Native Windows 11 Fluent UI**:
  - Modern dark theme layout with custom battery gauge graphics.
  - Real-time battery percentage, charging state, and estimated remaining time.
  - Dynamic tray icon showing battery charge level and AC state.
- **⚙️ Full Customization & Persistence**:
  - Low threshold slider (5% – 40%).
  - High threshold slider (60% – 100%).
  - Option to toggle low and high alarms independently.
  - "Start with Windows automatically" (Registry HKCU integration).
  - "Start minimized to system tray".
  - "Minimize to tray when close (X) is clicked".
  - "Show Windows balloon / tray alerts".
  - Settings saved in `battery_settings.json` (portable mode next to exe, or in `%AppData%\BatteryAlarmPro`).

---

## 🚀 How to Run

1. Simply double-click **`BatteryGuard.exe`**!
2. The main dashboard will open, showing your current battery percentage and charge state.
3. Adjust your preferred low and high thresholds, choose your alarm sound, and click **Save Settings**.
4. Click **Hide to Tray** or the close `[X]` button to let it run silently in the background tray.

---

## 📦 How to Transfer to Other Laptops

Because `BatteryGuard.exe` is compiled against the built-in Microsoft .NET Framework 4.8 (pre-installed on every Windows 10 and Windows 11 machine):

1. Copy **`BatteryGuard.exe`** to a USB flash drive, Google Drive, OneDrive, or send it via email/chat.
2. Paste it anywhere on the other laptop (e.g., `Desktop`, `Documents`, or `C:\Tools\`).
3. Double-click to run! No installation or admin rights required.

---

## 🛠️ Building from Source

No Visual Studio, SDK, or Python installation is required to rebuild. The app compiles using the standard Windows C# compiler that comes pre-installed in Windows:

- Run `build.bat` (Command Prompt), or
- Run `build.ps1` (PowerShell)

```cmd
.\build.bat
```

To run the automated scenario verification suite:
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:exe /out:run_tests.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Data.dll src\AppSettings.cs src\AudioAlert.cs src\BatteryMonitor.cs tests\ScenarioTests.cs
.\run_tests.exe
Remove-Item run_tests.exe
```

---

## 🛡️ Handled Edge Cases & Scenarios

| Scenario | Behavior |
| :--- | :--- |
| **Charger plugged in during low battery alarm** | Alarm stops instantly without user having to click anything. |
| **Charger unplugged during high battery alarm** | Alarm stops instantly to confirm charging has ended. |
| **User clicks Snooze (5m / 10m)** | Alarm is silenced immediately and will only re-alert after the timer expires. |
| **User clicks Stop Alarm** | Dismisses alarm for the current charging cycle until the battery level resets. |
| **Desktop PC / AC-only (No battery)** | Detects ACPI battery absence, informs user in UI, and safely disables thresholds without crashing. |
| **Laptop sleep / hibernation / resume** | Subscribes to Windows `PowerModeChanged` event to immediately update battery status upon waking up. |
| **High-DPI Laptop displays (125%, 150%, 200%)** | Fully DPI-aware, rendering crisp text and sharp vector-quality graphics. |
| **Multiple instances prevented** | Single-instance system mutex ensures only one monitor runs; launching again brings the existing window to front. |
| **Custom audio file missing or deleted** | Automatically falls back to built-in synthesized alarm tone so alerts are never missed. |

---

## 📜 License
Open Source - Free to use and distribute across all your personal and work laptops.
