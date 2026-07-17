using WinVitals.Core.Entities;

namespace WinVitals.Core.Probes;

public static class DriveUsageProbe
{
    public static IReadOnlyList<DriveUsage> Read()
    {
        var list = new List<DriveUsage>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;
            if (d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
            try
            {
                var used = d.TotalSize - d.AvailableFreeSpace;
                var pct = d.TotalSize == 0 ? 0 : used / (double)d.TotalSize * 100;
                list.Add(new DriveUsage(
                    Name: d.Name,
                    Label: string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.Name : d.VolumeLabel,
                    DriveFormat: d.DriveFormat,
                    TotalBytes: d.TotalSize,
                    FreeBytes: d.AvailableFreeSpace,
                    UsedPercent: Math.Round(pct, 1)));
            }
            catch { }
        }
        return list;
    }
}
