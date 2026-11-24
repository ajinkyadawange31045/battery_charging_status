using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;

namespace BatteryAlarmApp
{
    public class BatteryHealthStats
    {
        public bool IsAvailable { get; set; }
        public int DesignCapacity { get; set; }
        public int FullChargeCapacity { get; set; }
        public int CycleCount { get; set; }
        public string Manufacturer { get; set; }
        public string Chemistry { get; set; }
        public double HealthPercent
        {
            get
            {
                if (DesignCapacity <= 0) return 100.0;
                double h = (double)FullChargeCapacity / DesignCapacity * 100.0;
                return Math.Round(Math.Min(100.0, Math.Max(0.0, h)), 1);
            }
        }
    }

    public static class BatteryHealthService
    {
        public static void QueryHealthStatsAsync(Action<BatteryHealthStats> callback)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                BatteryHealthStats stats = QueryHealthStatsInternal();
                if (callback != null)
                {
                    callback(stats);
                }
            });
        }

        public static void OpenOfficialWindowsReport()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string htmlPath = Path.Combine(Path.GetTempPath(), "battery-report.html");
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
                    psi.Arguments = "/batteryreport /output \"" + htmlPath + "\"";
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;

                    using (Process p = Process.Start(psi))
                    {
                        if (p != null) p.WaitForExit(5000);
                    }

                    if (File.Exists(htmlPath))
                    {
                        Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });
                    }
                }
                catch { }
            });
        }

        private static BatteryHealthStats QueryHealthStatsInternal()
        {
            BatteryHealthStats stats = new BatteryHealthStats();
            string xmlPath = Path.Combine(Path.GetTempPath(), "bat_rep_" + Guid.NewGuid().ToString("N") + ".xml");

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
                psi.Arguments = "/batteryreport /xml /output \"" + xmlPath + "\"";
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;

                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(5000);
                }

                if (File.Exists(xmlPath))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(xmlPath);

                    XmlNode batNode = doc.SelectSingleNode("//Battery");
                    if (batNode != null)
                    {
                        stats.IsAvailable = true;

                        XmlNode mfgNode = batNode.SelectSingleNode("Manufacturer");
                        if (mfgNode != null) stats.Manufacturer = mfgNode.InnerText.Trim();

                        XmlNode chemNode = batNode.SelectSingleNode("Chemistry");
                        if (chemNode != null) stats.Chemistry = chemNode.InnerText.Trim();

                        XmlNode dcNode = batNode.SelectSingleNode("DesignCapacity");
                        int dc;
                        if (dcNode != null && int.TryParse(dcNode.InnerText.Trim(), out dc))
                            stats.DesignCapacity = dc;

                        XmlNode fcNode = batNode.SelectSingleNode("FullChargeCapacity");
                        int fc;
                        if (fcNode != null && int.TryParse(fcNode.InnerText.Trim(), out fc))
                            stats.FullChargeCapacity = fc;

                        XmlNode cyNode = batNode.SelectSingleNode("CycleCount");
                        int cy;
                        if (cyNode != null && int.TryParse(cyNode.InnerText.Trim(), out cy))
                            stats.CycleCount = cy;
                    }

                    File.Delete(xmlPath);
                }
            }
            catch
            {
                stats.IsAvailable = false;
            }

            return stats;
        }
    }
}
