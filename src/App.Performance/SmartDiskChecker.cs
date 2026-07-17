using System.Management;
using App.Core;

namespace App.Performance;

/// <summary>
/// SMART disk health checker via WMI.
/// Reads failure prediction status and key attributes.
/// </summary>
public class SmartDiskChecker
{
    public record SmartResult(
        string DriveModel,
        string DriveLetter,
        bool PredictFailure,
        string Status,
        int Temperature,
        string HealthSummary
    );

    public static List<SmartResult> CheckAllDrives()
    {
        var results = new List<SmartResult>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Storage",
                "SELECT * FROM MSStorageDriver_FailurePredictStatus");

            foreach (ManagementObject obj in searcher.Get())
            {
                try
                {
                    bool predictFailure = (bool)(obj["PredictFailure"] ?? false);
                    string reason = obj["Reason"]?.ToString() ?? "Unknown";
                    string instanceName = obj["InstanceName"]?.ToString() ?? "";

                    // Get temperature from MSStorageDriver_ATAPISmartData
                    int temp = GetTemperature(instanceName);

                    results.Add(new SmartResult(
                        DriveModel: instanceName.Split('_').LastOrDefault() ?? "Unknown",
                        DriveLetter: GetDriveLetter(instanceName),
                        PredictFailure: predictFailure,
                        Status: predictFailure ? "⚠️ AT RISK" : "✅ Healthy",
                        Temperature: temp,
                        HealthSummary: predictFailure
                            ? $"⚠️ Drive predicts failure! Reason: {reason}"
                            : temp > 50
                                ? $"Drive healthy but warm ({temp}°C)"
                                : temp > 0
                                    ? $"Drive healthy ({temp}°C)"
                                    : "Drive healthy (temp N/A)"
                    ));
                }
                catch { /* skip inaccessible drives */ }
            }
        }
        catch (ManagementException)
        {
            // SMART not available — return empty
        }

        return results;
    }

    private static int GetTemperature(string instanceName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Storage",
                "SELECT * FROM MSStorageDriver_ATAPISmartData");

            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["InstanceName"]?.ToString() == instanceName)
                {
                    byte[]? vendorData = obj["VendorSpecific"] as byte[];
                    if (vendorData != null && vendorData.Length > 200)
                        return vendorData[194]; // Temperature attribute offset
                }
            }
        }
        catch { /* WMI not accessible */ }
        return -1;
    }

    private static string GetDriveLetter(string instanceName)
    {
        try
        {
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (drive.IsReady && instanceName.Contains(drive.Name.Trim('\\'), StringComparison.OrdinalIgnoreCase))
                    return drive.Name.Trim('\\');
            }
        }
        catch { }
        return "?";
    }
}
