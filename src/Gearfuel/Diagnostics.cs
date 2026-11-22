using System.Diagnostics;
using System.Text;
using Gearfuel.Devices;

namespace Gearfuel;

/// <summary>Recent problems, and a report of what Gearfuel sees, to paste into an issue.</summary>
static class Diagnostics
{
    const int MaxProblems = 20;
    static readonly Queue<string> Problems = new();

    public static void Report(string problem)
    {
        Trace.TraceWarning(problem);
        lock (Problems)
        {
            Problems.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {problem}");
            if (Problems.Count > MaxProblems) Problems.Dequeue();
        }
    }

    /// <summary>Lists devices by name and USB IDs, without serial numbers or paths: the report goes into public issues.</summary>
    public static string Describe(IReadOnlyList<DeviceStatus> devices, IReadOnlyList<HidDevice> hidDevices)
    {
        var report = new StringBuilder();
        report.AppendLine($"Gearfuel {AppInfo.Version} on Windows {Environment.OSVersion.Version}");

        report.AppendLine().AppendLine("Devices:");
        foreach (var d in devices)
            report.AppendLine($"  {d.Name}: {d.Percent}%{(d.Charging ? ", charging" : "")}, last answered {d.SinceSeen.TotalMinutes:0} min ago");

        // One line per collection type; Windows lists most devices several times over.
        report.AppendLine().AppendLine("HID collections (vendor:product usage-page:usage input/feature report length name):");
        foreach (var hid in hidDevices.DistinctBy(h => (h.VendorId, h.ProductId, h.UsagePage, h.Usage, h.InputReportLength, h.FeatureReportLength)).OrderBy(h => h.VendorId).ThenBy(h => h.ProductId))
            report.AppendLine($"  {hid.VendorId:X4}:{hid.ProductId:X4} {hid.UsagePage:X4}:{hid.Usage:X4} {hid.InputReportLength}/{hid.FeatureReportLength} {hid.ProductName}");

        report.AppendLine().AppendLine("Recent problems:");
        lock (Problems)
        {
            if (Problems.Count == 0) report.AppendLine("  none");
            foreach (var problem in Problems) report.AppendLine($"  {problem}");
        }
        return report.ToString();
    }
}
