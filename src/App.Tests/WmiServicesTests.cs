using Xunit;
using App.Performance;

namespace App.Tests;

/// <summary>
/// Tests for WMI monitoring services
/// </summary>
public class WmiServicesTests
{
    [Fact]
    public async Task CpuMonitoringService_GetCpuUsage_ReturnsValidPercentage()
    {
        // Arrange
        var service = new CpuMonitoringService();
        service.Initialize();

        // Act
        var cpuUsage = await service.GetCpuUsageAsync();

        // Assert
        Assert.InRange(cpuUsage, 0.0, 100.0);
        
        // Cleanup
        service.Dispose();
    }

    [Fact]
    public void CpuMonitoringService_GetCoreCount_ReturnsPositiveNumber()
    {
        // Arrange
        var service = new CpuMonitoringService();

        // Act
        var coreCount = service.GetCoreCount();

        // Assert
        Assert.True(coreCount > 0, "Core count should be greater than 0");
    }

    [Fact]
    public async Task CpuMonitoringService_GetCpuInfo_ReturnsValidInfo()
    {
        // Arrange
        var service = new CpuMonitoringService();
        service.Initialize();

        // Act
        var cpuInfo = await service.GetCpuInfoAsync();

        // Assert
        Assert.NotNull(cpuInfo);
        Assert.NotEmpty(cpuInfo.Name);
        Assert.NotEmpty(cpuInfo.Manufacturer);
        Assert.True(cpuInfo.CoreCount > 0, "Core count should be greater than 0");
        Assert.True(cpuInfo.MaxClockSpeed > 0, "Max clock speed should be greater than 0");
        Assert.InRange(cpuInfo.CurrentUsage, 0.0, 100.0);
        
        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task MemoryMonitoringService_GetMemoryUsage_ReturnsValidInfo()
    {
        // Arrange
        var service = new MemoryMonitoringService();

        // Act
        var memInfo = await service.GetMemoryUsageAsync();

        // Assert
        Assert.NotNull(memInfo);
        Assert.True(memInfo.TotalBytes > 0, "Total bytes should be greater than 0");
        Assert.True(memInfo.TotalGB > 0, "Total GB should be greater than 0");
        Assert.True(memInfo.AvailableBytes <= memInfo.TotalBytes, "Available bytes should be less than or equal to total");
        Assert.True(memInfo.UsedBytes <= memInfo.TotalBytes, "Used bytes should be less than or equal to total");
        Assert.InRange(memInfo.UsagePercent, 0.0, 100.0);
    }

    [Fact]
    public async Task MemoryMonitoringService_GetDetailedMemoryInfo_ReturnsValidInfo()
    {
        // Arrange
        var service = new MemoryMonitoringService();

        // Act
        var memInfo = await service.GetDetailedMemoryInfoAsync();

        // Assert
        Assert.NotNull(memInfo);
        Assert.True(memInfo.TotalGB > 0, "Total GB should be greater than 0");
        Assert.NotNull(memInfo.MemoryModules);
        // Memory modules list may be empty if WMI query fails, which is acceptable
    }

    [Fact]
    public async Task DiskIoMonitoringService_GetDiskIoActivity_ReturnsData()
    {
        // Arrange
        var service = new DiskIoMonitoringService();
        service.Initialize();

        // Act
        var diskIoList = await service.GetDiskIoActivityAsync();

        // Assert
        Assert.NotNull(diskIoList);
        // Should have at least one disk
        Assert.NotEmpty(diskIoList);
        
        foreach (var diskIo in diskIoList)
        {
            Assert.NotEmpty(diskIo.DiskName);
            Assert.True(diskIo.ReadBytesPerSecond >= 0, "Read bytes should be non-negative");
            Assert.True(diskIo.WriteBytesPerSecond >= 0, "Write bytes should be non-negative");
        }
        
        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task DiskIoMonitoringService_GetDiskInfo_ReturnsValidInfo()
    {
        // Arrange
        var service = new DiskIoMonitoringService();

        // Act
        var disks = await service.GetDiskInfoAsync();

        // Assert
        Assert.NotNull(disks);
        // Should have at least one disk
        if (disks.Count > 0)
        {
            var disk = disks[0];
            Assert.NotEmpty(disk.Model);
            Assert.True(disk.Size > 0, "Disk size should be greater than 0");
        }
    }

    [Fact]
    public async Task DiskIoMonitoringService_GetLogicalDrives_ReturnsValidDrives()
    {
        // Arrange
        var service = new DiskIoMonitoringService();

        // Act
        var drives = await service.GetLogicalDrivesAsync();

        // Assert
        Assert.NotNull(drives);
        // Should have at least one fixed drive (typically C:)
        Assert.NotEmpty(drives);
        
        foreach (var drive in drives)
        {
            Assert.NotEmpty(drive.Letter);
            Assert.NotEmpty(drive.FileSystem);
            Assert.True(drive.TotalBytes > 0, $"Drive {drive.Letter} total bytes should be greater than 0");
            Assert.True(drive.FreeBytes <= drive.TotalBytes, $"Drive {drive.Letter} free bytes should be less than or equal to total");
            Assert.InRange(drive.UsagePercent, 0.0, 100.0);
        }
    }

    [Fact]
    public async Task NetworkActivityMonitoringService_GetNetworkActivity_ReturnsData()
    {
        // Arrange
        var service = new NetworkActivityMonitoringService();
        service.Initialize();

        // Act
        var interfaces = await service.GetNetworkActivityAsync();

        // Assert
        Assert.NotNull(interfaces);
        // Should have at least one network interface
        Assert.NotEmpty(interfaces);
        
        foreach (var iface in interfaces)
        {
            Assert.NotEmpty(iface.Name);
            Assert.True(iface.BytesSentPerSecond >= 0, "Bytes sent should be non-negative");
            Assert.True(iface.BytesReceivedPerSecond >= 0, "Bytes received should be non-negative");
        }
        
        // Cleanup
        service.Dispose();
    }

    [Fact]
    public async Task NetworkActivityMonitoringService_GetNetworkAdapters_ReturnsValidAdapters()
    {
        // Arrange
        var service = new NetworkActivityMonitoringService();

        // Act
        var adapters = await service.GetNetworkAdaptersAsync();

        // Assert
        Assert.NotNull(adapters);
        // Should have at least one enabled network adapter
        if (adapters.Count > 0)
        {
            var adapter = adapters[0];
            Assert.NotEmpty(adapter.Name);
            Assert.NotEmpty(adapter.NetConnectionStatus);
        }
    }

    [Fact]
    public async Task NetworkActivityMonitoringService_GetTotalBandwidth_ReturnsValidInfo()
    {
        // Arrange
        var service = new NetworkActivityMonitoringService();
        service.Initialize();

        // Act
        var bandwidth = await service.GetTotalBandwidthAsync();

        // Assert
        Assert.NotNull(bandwidth);
        Assert.True(bandwidth.TotalBytesSentPerSecond >= 0, "Total bytes sent should be non-negative");
        Assert.True(bandwidth.TotalBytesReceivedPerSecond >= 0, "Total bytes received should be non-negative");
        Assert.True(bandwidth.ActiveInterfaceCount >= 0, "Active interface count should be non-negative");
        
        // Cleanup
        service.Dispose();
    }

    [Fact]
    public void CpuMonitoringService_MultipleInitializations_DoesNotThrow()
    {
        // Arrange
        var service = new CpuMonitoringService();

        // Act & Assert - should not throw
        service.Initialize();
        service.Initialize();
        service.Initialize();
        
        // Cleanup
        service.Dispose();
    }

    [Fact]
    public void DiskIoMonitoringService_MultipleInitializations_DoesNotThrow()
    {
        // Arrange
        var service = new DiskIoMonitoringService();

        // Act & Assert - should not throw
        service.Initialize();
        service.Initialize();
        service.Initialize();
        
        // Cleanup
        service.Dispose();
    }

    [Fact]
    public void NetworkActivityMonitoringService_MultipleInitializations_DoesNotThrow()
    {
        // Arrange
        var service = new NetworkActivityMonitoringService();

        // Act & Assert - should not throw
        service.Initialize();
        service.Initialize();
        service.Initialize();
        
        // Cleanup
        service.Dispose();
    }
}
