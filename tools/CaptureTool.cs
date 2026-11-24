using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using BatteryAlarmApp;

namespace BatteryAlarmApp.Tools
{
    class CaptureTool
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);

        [STAThread]
        static void Main()
        {
            string baseDir = Directory.GetCurrentDirectory();
            string outDir = Path.Combine(baseDir, "docs", "screenshots");
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            AppSettings settings = AppSettings.Load();
            AudioAlert audio = new AudioAlert();

            // 1. Dashboard screenshot
            try
            {
                BatteryMonitor monitor1 = new BatteryMonitor(settings, audio);
                MainForm form = new MainForm(settings, audio, monitor1);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(50, 50);
                form.Show();
                Application.DoEvents();
                Thread.Sleep(600);
                Application.DoEvents();

                CaptureForm(form, Path.Combine(outDir, "dashboard.png"));
                Console.WriteLine("Captured: dashboard.png");
                form.Dispose();
            }
            catch (Exception ex) { Console.WriteLine("Err 1: " + ex.Message); }

            // 2. Low Battery Warning screenshot
            try
            {
                BatteryMonitor monitor2 = new BatteryMonitor(settings, audio);
                MainForm formLow = new MainForm(settings, audio, monitor2);
                formLow.StartPosition = FormStartPosition.Manual;
                formLow.Location = new Point(50, 50);
                formLow.Show();
                Application.DoEvents();

                MethodInfo mi = typeof(MainForm).GetMethod("OnAlarmTriggered", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mi != null)
                {
                    mi.Invoke(formLow, new object[] {
                        ActiveAlarmState.LowBattery,
                        12,
                        "LOW BATTERY WARNING: Battery at 12%! Connect charger now."
                    });
                }
                Application.DoEvents();
                Thread.Sleep(600);
                Application.DoEvents();

                CaptureForm(formLow, Path.Combine(outDir, "alert_low_battery.png"));
                Console.WriteLine("Captured: alert_low_battery.png");
                formLow.Dispose();
            }
            catch (Exception ex) { Console.WriteLine("Err 2: " + ex.Message); }

            // 3. High Battery Protection screenshot
            try
            {
                BatteryMonitor monitor3 = new BatteryMonitor(settings, audio);
                MainForm formHigh = new MainForm(settings, audio, monitor3);
                formHigh.StartPosition = FormStartPosition.Manual;
                formHigh.Location = new Point(50, 50);
                formHigh.Show();
                Application.DoEvents();

                MethodInfo mi = typeof(MainForm).GetMethod("OnAlarmTriggered", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mi != null)
                {
                    mi.Invoke(formHigh, new object[] {
                        ActiveAlarmState.HighBattery,
                        92,
                        "CHARGE THRESHOLD (92%): Unplug charger now."
                    });
                }
                Application.DoEvents();
                Thread.Sleep(600);
                Application.DoEvents();

                CaptureForm(formHigh, Path.Combine(outDir, "alert_high_battery.png"));
                Console.WriteLine("Captured: alert_high_battery.png");
                formHigh.Dispose();
            }
            catch (Exception ex) { Console.WriteLine("Err 3: " + ex.Message); }

            Console.WriteLine("Screenshot capture completed.");
        }

        private static void CaptureForm(Form form, string savePath)
        {
            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                PrintWindow(form.Handle, hdc, 2);
                g.ReleaseHdc(hdc);
                bmp.Save(savePath, ImageFormat.Png);
            }
        }
    }
}
