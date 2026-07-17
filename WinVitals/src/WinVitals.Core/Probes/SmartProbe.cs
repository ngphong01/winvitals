using System.Management;
using WinVitals.Core.Entities;

namespace WinVitals.Core.Probes;

/// <summary>
/// SMART qua WMI. Read chậm (~500ms) nên chỉ gọi khi user request hoặc mỗi 5 phút.
/// </summary>
public static class SmartProbe
{
    public static IReadOnlyList<SmartInfo> Read()
    {
        var list = new List<SmartInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Model, SerialNumber, InterfaceType, Status FROM Win32_DiskDrive");
            using var results = searcher.Get();

            var failureMap = ReadFailurePredictions();

            foreach (var mo in results)
            {
                var model = mo["Model"]?.ToString() ?? "?";
                var serial = mo["SerialNumber"]?.ToString()?.Trim() ?? "?";
                var iface = mo["InterfaceType"]?.ToString() ?? "?";
                var status = mo["Status"]?.ToString() ?? "?";
                var predict = failureMap.TryGetValue(model, out var p) && p;

                list.Add(new SmartInfo(
                    Model: model,
                    SerialNumber: serial,
                    InterfaceType: iface,
                    Status: status,
                    PredictFailure: predict,
                    TemperatureCelsius: null,
                    PowerOnHours: null));
                mo.Dispose();
            }
        }
        catch { }
        return list;
    }

    private static Dictionary<string, bool> ReadFailurePredictions()
    {
        var dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus"));
            using var results = searcher.Get();
            foreach (var mo in results)
            {
                var inst = mo["InstanceName"]?.ToString() ?? "";
                var pred = mo["PredictFailure"] is bool b && b;
                dict[inst] = pred;
                mo.Dispose();
            }
        }
        catch { }
        return dict;
    }
}
