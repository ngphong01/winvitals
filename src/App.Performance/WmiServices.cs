using System.Diagnostics;
using System.Management;
using App.Core;

namespace App.Performance;

/// <summary>
/// CPU monitoring service using WMI (Windows Management Instrumentation)
/// Provides real-time CPU usage metrics and historical data
/// </summary>
public class CpuMonitoringService
{
    private PerformanceCounter? _cpuCounter;
    private readonly object _lock = new();
    private bool _isInitialized;

    /// <summary>
    /// Initialize the CPU monitoring service
    /// </summary>
    public void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                // First call always returns 0, so call it once to prime
                _cpuCounter.NextValue();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize CPU monitoring", ex);
            }
        }
    }

    /// <summary>
    /// Get current CPU usage percentage (0-100)
    /// </summary>
    public async Task<double> GetCpuUsageAsync()
    {
        if (!_isInitialized)
            Initialize();

        return await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    if (_cpuCounter == null)
                        return 0.0;

                    // Need a small delay between calls for accurate reading
                    Thread.Sleep(100);
                    var usage = _cpuCounter.NextValue();
                    return Math.Round(usage, 1);
                }
            }
            catch
            {
                return 0.0;
            }
        });
    }

    /// <summary>
    /// Get CPU core count
    /// </summary>
    public int GetCoreCount()
    {
        return Environment.ProcessorCount;
    }

    /// <summary>
    /// Get detailed CPU information using WMI
    /// </summary>
    public async Task<CpuInfo> GetCpuInfoAsync()
    {
        return await Task.Run(() =>
        {
            var cpuInfo = new CpuInfo
            {
                CoreCount = Environment.ProcessorCount,
                CurrentUsage = 0.0
            };

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    cpuInfo.Name = obj["Name"]?.ToString() ?? "Unknown";
                    cpuInfo.Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                    cpuInfo.MaxClockSpeed = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                    cpuInfo.CurrentClockSpeed = Convert.ToInt32(obj["CurrentClockSpeed"] ?? 0);
                    cpuInfo.NumberOfLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                    cpuInfo.NumberOfCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    break; // Only need first processor
                }

                // Get current usage
                cpuInfo.CurrentUsage = GetCpuUsageAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Return partial info on error
            }

            return cpuInfo;
        });
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _cpuCounter?.Dispose();
            _cpuCounter = null;
            _isInitialized = false;
        }
    }
}

/// <summary>
/// Memory monitoring service using WMI
/// Provides real-time memory usage metrics
/// </summary>
public class MemoryMonitoringService
{
    /// <summary>
    /// Get current memory usage information
    /// </summary>
    public async Task<MemoryInfo> GetMemoryUsageAsync()
    {
        return await Task.Run(() =>
        {
            var memInfo = new MemoryInfo();

            try
            {
                var memStatus = GetGlobalMemoryStatus();
                if (memStatus.TotalPhys > 0)
                {
                    memInfo.TotalBytes = memStatus.TotalPhys;
                    memInfo.AvailableBytes = memStatus.AvailPhys;
                    memInfo.UsedBytes = memStatus.TotalPhys - memStatus.AvailPhys;
                    memInfo.UsagePercent = Math.Round((1 - memStatus.AvailPhys / (double)memStatus.TotalPhys) * 100, 1);
                    memInfo.TotalGB = Math.Round(memStatus.TotalPhys / (1024.0 * 1024 * 1024), 1);
                    memInfo.UsedGB = Math.Round(memInfo.UsedBytes / (1024.0 * 1024 * 1024), 1);
                    memInfo.AvailableGB = Math.Round(memStatus.AvailPhys / (1024.0 * 1024 * 1024), 1);
                }
                else
                {
                    // Fallback to PerformanceCounter
                    using var memCounter = new PerformanceCounter("Memory", "Available MBytes");
                    var availMB = memCounter.NextValue();
                    memInfo.TotalGB = 16; // conservative fallback
                    memInfo.AvailableGB = Math.Round(availMB / 1024, 1);
                    memInfo.UsedGB = Math.Round(memInfo.TotalGB - memInfo.AvailableGB, 1);
                    memInfo.UsagePercent = Math.Round((memInfo.UsedGB / memInfo.TotalGB) * 100, 1);
                    memInfo.TotalBytes = (ulong)(memInfo.TotalGB * 1024 * 1024 * 1024);
                    memInfo.UsedBytes = (ulong)(memInfo.UsedGB * 1024 * 1024 * 1024);
                    memInfo.AvailableBytes = (ulong)(memInfo.AvailableGB * 1024 * 1024 * 1024);
                }
            }
            catch
            {
                // Return default values on error
                memInfo.TotalGB = 16;
                memInfo.UsedGB = 8;
                memInfo.AvailableGB = 8;
                memInfo.UsagePercent = 50;
            }

            return memInfo;
        });
    }

    /// <summary>
    /// Get detailed memory information using WMI
    /// </summary>
    public async Task<MemoryInfo> GetDetailedMemoryInfoAsync()
    {
        var memInfo = await GetMemoryUsageAsync();

        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                var memoryModules = new List<MemoryModuleInfo>();

                foreach (ManagementObject obj in searcher.Get())
                {
                    var module = new MemoryModuleInfo
                    {
                        Capacity = Convert.ToUInt64(obj["Capacity"] ?? 0),
                        Speed = Convert.ToInt32(obj["Speed"] ?? 0),
                        Manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "Unknown",
                        PartNumber = obj["PartNumber"]?.ToString()?.Trim() ?? "Unknown"
                    };
                    memoryModules.Add(module);
                }

                memInfo.MemoryModules = memoryModules;
            }
            catch
            {
                // Continue without detailed info
            }

            return memInfo;
        });
    }

    // P/Invoke for accurate system RAM detection
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private static MEMORYSTATUSEX GetGlobalMemoryStatus()
    {
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref memStatus))
            return default;
        return memStatus;
    }
}

/// <summary>
/// Disk I/O monitoring service using WMI and PerformanceCounters
/// Provides real-time disk activity metrics
/// </summary>
public class DiskIoMonitoringService
{
    private Dictionary<string, PerformanceCounter> _readCounters = new();
    private Dictionary<string, PerformanceCounter> _writeCounters = new();
    private bool _isInitialized;
    private readonly object _lock = new();

    /// <summary>
    /// Initialize disk I/O monitoring for all physical disks
    /// </summary>
    public void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                // Get all physical disk instances
                var category = new PerformanceCounterCategory("PhysicalDisk");
                var instanceNames = category.GetInstanceNames();

                foreach (var instance in instanceNames)
                {
                    if (instance == "_Total") continue;

                    try
                    {
                        var readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", instance);
                        var writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", instance);

                        // Prime the counters
                        readCounter.NextValue();
                        writeCounter.NextValue();

                        _readCounters[instance] = readCounter;
                        _writeCounters[instance] = writeCounter;
                    }
                    catch
                    {
                        // Skip disks that can't be monitored
                    }
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize disk I/O monitoring", ex);
            }
        }
    }

    /// <summary>
    /// Get current disk I/O activity for all disks
    /// </summary>
    public async Task<List<DiskIoInfo>> GetDiskIoActivityAsync()
    {
        if (!_isInitialized)
            Initialize();

        return await Task.Run(() =>
        {
            var results = new List<DiskIoInfo>();

            try
            {
                // Small delay for accurate reading
                Thread.Sleep(100);

                lock (_lock)
                {
                    foreach (var instance in _readCounters.Keys)
                    {
                        try
                        {
                            var readBytesPerSec = _readCounters[instance].NextValue();
                            var writeBytesPerSec = _writeCounters[instance].NextValue();

                            results.Add(new DiskIoInfo
                            {
                                DiskName = instance,
                                ReadBytesPerSecond = (long)readBytesPerSec,
                                WriteBytesPerSecond = (long)writeBytesPerSec,
                                ReadMBPerSecond = Math.Round(readBytesPerSec / (1024.0 * 1024), 2),
                                WriteMBPerSecond = Math.Round(writeBytesPerSec / (1024.0 * 1024), 2),
                                TotalMBPerSecond = Math.Round((readBytesPerSec + writeBytesPerSec) / (1024.0 * 1024), 2)
                            });
                        }
                        catch
                        {
                            // Skip failed disk
                        }
                    }
                }
            }
            catch
            {
                // Return empty list on error
            }

            return results;
        });
    }

    /// <summary>
    /// Get disk information using WMI
    /// </summary>
    public async Task<List<DiskInfo>> GetDiskInfoAsync()
    {
        return await Task.Run(() =>
        {
            var disks = new List<DiskInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var disk = new DiskInfo
                    {
                        Model = obj["Model"]?.ToString() ?? "Unknown",
                        Size = Convert.ToInt64(obj["Size"] ?? 0),
                        InterfaceType = obj["InterfaceType"]?.ToString() ?? "Unknown",
                        MediaType = obj["MediaType"]?.ToString() ?? "Unknown",
                        Status = obj["Status"]?.ToString() ?? "Unknown"
                    };
                    disks.Add(disk);
                }
            }
            catch
            {
                // Return empty list on error
            }

            return disks;
        });
    }

    /// <summary>
    /// Get logical drive information
    /// </summary>
    public async Task<List<LogicalDriveInfo>> GetLogicalDrivesAsync()
    {
        return await Task.Run(() =>
        {
            var drives = new List<LogicalDriveInfo>();

            try
            {
                var systemDrives = System.IO.DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed);

                foreach (var drive in systemDrives)
                {
                    drives.Add(new LogicalDriveInfo
                    {
                        Letter = drive.Name.TrimEnd('\\'),
                        Label = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                        FileSystem = drive.DriveFormat,
                        TotalBytes = drive.TotalSize,
                        FreeBytes = drive.AvailableFreeSpace,
                        UsedBytes = drive.TotalSize - drive.AvailableFreeSpace,
                        UsagePercent = Math.Round((1 - drive.AvailableFreeSpace / (double)drive.TotalSize) * 100, 1),
                        TotalGB = Math.Round(drive.TotalSize / (1024.0 * 1024 * 1024), 1),
                        FreeGB = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024), 1),
                        UsedGB = Math.Round((drive.TotalSize - drive.AvailableFreeSpace) / (1024.0 * 1024 * 1024), 1)
                    });
                }
            }
            catch
            {
                // Return empty list on error
            }

            return drives;
        });
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var counter in _readCounters.Values)
                counter?.Dispose();
            foreach (var counter in _writeCounters.Values)
                counter?.Dispose();

            _readCounters.Clear();
            _writeCounters.Clear();
            _isInitialized = false;
        }
    }
}

/// <summary>
/// Network activity monitoring service using WMI and PerformanceCounters
/// Provides real-time network traffic metrics
/// </summary>
public class NetworkActivityMonitoringService
{
    private Dictionary<string, (PerformanceCounter Sent, PerformanceCounter Received)> _networkCounters = new();
    private bool _isInitialized;
    private readonly object _lock = new();

    /// <summary>
    /// Initialize network monitoring for all network interfaces
    /// </summary>
    public void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                var category = new PerformanceCounterCategory("Network Interface");
                var instanceNames = category.GetInstanceNames();

                foreach (var instance in instanceNames)
                {
                    try
                    {
                        var sentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance);
                        var receivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instance);

                        // Prime the counters
                        sentCounter.NextValue();
                        receivedCounter.NextValue();

                        _networkCounters[instance] = (sentCounter, receivedCounter);
                    }
                    catch
                    {
                        // Skip interfaces that can't be monitored
                    }
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize network monitoring", ex);
            }
        }
    }

    /// <summary>
    /// Get current network activity for all interfaces
    /// </summary>
    public async Task<List<NetworkInterfaceInfo>> GetNetworkActivityAsync()
    {
        if (!_isInitialized)
            Initialize();

        return await Task.Run(() =>
        {
            var results = new List<NetworkInterfaceInfo>();

            try
            {
                // Small delay for accurate reading
                Thread.Sleep(100);

                lock (_lock)
                {
                    foreach (var kvp in _networkCounters)
                    {
                        try
                        {
                            var sent = kvp.Value.Sent.NextValue();
                            var received = kvp.Value.Received.NextValue();

                            results.Add(new NetworkInterfaceInfo
                            {
                                Name = kvp.Key,
                                BytesSentPerSecond = (long)sent,
                                BytesReceivedPerSecond = (long)received,
                                SentKBPerSecond = Math.Round(sent / 1024.0, 2),
                                ReceivedKBPerSecond = Math.Round(received / 1024.0, 2),
                                TotalKBPerSecond = Math.Round((sent + received) / 1024.0, 2)
                            });
                        }
                        catch
                        {
                            // Skip failed interface
                        }
                    }
                }
            }
            catch
            {
                // Return empty list on error
            }

            return results;
        });
    }

    /// <summary>
    /// Get detailed network adapter information using WMI
    /// </summary>
    public async Task<List<NetworkAdapterInfo>> GetNetworkAdaptersAsync()
    {
        return await Task.Run(() =>
        {
            var adapters = new List<NetworkAdapterInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetEnabled=True");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var adapter = new NetworkAdapterInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "Unknown",
                        Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown",
                        MACAddress = obj["MACAddress"]?.ToString() ?? "Unknown",
                        Speed = Convert.ToInt64(obj["Speed"] ?? 0),
                        AdapterType = obj["AdapterType"]?.ToString() ?? "Unknown",
                        NetConnectionStatus = GetConnectionStatus(Convert.ToInt32(obj["NetConnectionStatus"] ?? 0))
                    };
                    adapters.Add(adapter);
                }
            }
            catch
            {
                // Return empty list on error
            }

            return adapters;
        });
    }

    /// <summary>
    /// Get total network bandwidth usage
    /// </summary>
    public async Task<NetworkBandwidthInfo> GetTotalBandwidthAsync()
    {
        var interfaces = await GetNetworkActivityAsync();

        return new NetworkBandwidthInfo
        {
            TotalBytesSentPerSecond = interfaces.Sum(i => i.BytesSentPerSecond),
            TotalBytesReceivedPerSecond = interfaces.Sum(i => i.BytesReceivedPerSecond),
            TotalSentMBPerSecond = Math.Round(interfaces.Sum(i => i.BytesSentPerSecond) / (1024.0 * 1024), 2),
            TotalReceivedMBPerSecond = Math.Round(interfaces.Sum(i => i.BytesReceivedPerSecond) / (1024.0 * 1024), 2),
            ActiveInterfaceCount = interfaces.Count(i => i.TotalKBPerSecond > 0)
        };
    }

    private static string GetConnectionStatus(int status) => status switch
    {
        2 => "Connected",
        7 => "Disconnected",
        9 => "Connecting",
        _ => "Unknown"
    };

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var counters in _networkCounters.Values)
            {
                counters.Sent?.Dispose();
                counters.Received?.Dispose();
            }

            _networkCounters.Clear();
            _isInitialized = false;
        }
    }
}

// Data models for WMI services
public class CpuInfo
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public int CoreCount { get; set; }
    public int MaxClockSpeed { get; set; }
    public int CurrentClockSpeed { get; set; }
    public int NumberOfLogicalProcessors { get; set; }
    public int NumberOfCores { get; set; }
    public double CurrentUsage { get; set; }
}

public class MemoryInfo
{
    public ulong TotalBytes { get; set; }
    public ulong UsedBytes { get; set; }
    public ulong AvailableBytes { get; set; }
    public double TotalGB { get; set; }
    public double UsedGB { get; set; }
    public double AvailableGB { get; set; }
    public double UsagePercent { get; set; }
    public List<MemoryModuleInfo> MemoryModules { get; set; } = new();
}

public class MemoryModuleInfo
{
    public ulong Capacity { get; set; }
    public int Speed { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
}

public class DiskIoInfo
{
    public string DiskName { get; set; } = string.Empty;
    public long ReadBytesPerSecond { get; set; }
    public long WriteBytesPerSecond { get; set; }
    public double ReadMBPerSecond { get; set; }
    public double WriteMBPerSecond { get; set; }
    public double TotalMBPerSecond { get; set; }
}

public class DiskInfo
{
    public string Model { get; set; } = string.Empty;
    public long Size { get; set; }
    public string InterfaceType { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class LogicalDriveInfo
{
    public string Letter { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsagePercent { get; set; }
    public double TotalGB { get; set; }
    public double FreeGB { get; set; }
    public double UsedGB { get; set; }
}

public class NetworkInterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public long BytesSentPerSecond { get; set; }
    public long BytesReceivedPerSecond { get; set; }
    public double SentKBPerSecond { get; set; }
    public double ReceivedKBPerSecond { get; set; }
    public double TotalKBPerSecond { get; set; }
}

public class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string MACAddress { get; set; } = string.Empty;
    public long Speed { get; set; }
    public string AdapterType { get; set; } = string.Empty;
    public string NetConnectionStatus { get; set; } = string.Empty;
}

public class NetworkBandwidthInfo
{
    public long TotalBytesSentPerSecond { get; set; }
    public long TotalBytesReceivedPerSecond { get; set; }
    public double TotalSentMBPerSecond { get; set; }
    public double TotalReceivedMBPerSecond { get; set; }
    public int ActiveInterfaceCount { get; set; }
}
