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

            // 1. Modern Dashboard Screenshot
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

            // 2. Low Battery Popup Alert Dialog Screenshot
            try
            {
                AlertPopupForm popupLow = new AlertPopupForm(ActiveAlarmState.LowBattery, 12, null);
                popupLow.StartPosition = FormStartPosition.Manual;
                popupLow.Location = new Point(100, 100);
                popupLow.Show();
                Application.DoEvents();
                Thread.Sleep(500);
                Application.DoEvents();

                CaptureForm(popupLow, Path.Combine(outDir, "popup_low_battery.png"));
                Console.WriteLine("Captured: popup_low_battery.png");
                popupLow.Dispose();
            }
            catch (Exception ex) { Console.WriteLine("Err 2: " + ex.Message); }

            // 3. High Battery / Remove Charger Popup Alert Dialog Screenshot
            try
            {
                AlertPopupForm popupHigh = new AlertPopupForm(ActiveAlarmState.HighBattery, 92, null);
                popupHigh.StartPosition = FormStartPosition.Manual;
                popupHigh.Location = new Point(100, 100);
                popupHigh.Show();
                Application.DoEvents();
                Thread.Sleep(500);
                Application.DoEvents();

                CaptureForm(popupHigh, Path.Combine(outDir, "popup_remove_charger.png"));
                Console.WriteLine("Captured: popup_remove_charger.png");
                popupHigh.Dispose();
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
