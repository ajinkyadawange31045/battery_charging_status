using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace BatteryAlarmApp
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private const int SW_RESTORE = 9;
        private const string AppMutexGuid = "Global\\BatteryGuardPro_55599c40_Mutex";

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
            }
            catch { }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, AppMutexGuid, out createdNew))
            {
                if (!createdNew)
                {
                    // Already running! Bring existing window to front
                    IntPtr existingWnd = FindWindow(null, "BatteryGuard Pro - Smart Battery Monitor");
                    if (existingWnd != IntPtr.Zero)
                    {
                        ShowWindow(existingWnd, SW_RESTORE);
                        SetForegroundWindow(existingWnd);
                    }
                    else
                    {
                        MessageBox.Show("BatteryGuard Pro is already running in your system tray.", "BatteryGuard Pro",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppSettings settings = AppSettings.Load();
                AudioAlert audio = new AudioAlert();
                BatteryMonitor monitor = new BatteryMonitor(settings, audio);
                MainForm mainForm = new MainForm(settings, audio, monitor);

                bool startMin = settings.StartMinimized;
                foreach (string arg in args)
                {
                    if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-min", StringComparison.OrdinalIgnoreCase))
                    {
                        startMin = true;
                        break;
                    }
                }

                if (startMin)
                {
                    // Create window handle without showing
                    IntPtr h = mainForm.Handle;
                    mainForm.WindowState = FormWindowState.Minimized;
                    mainForm.Hide();
                }

                Application.Run(mainForm);
            }
        }
    }
}
