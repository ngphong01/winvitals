using System.Runtime.InteropServices;

namespace WinVitals.Core.Probes;

public static class MemoryProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    public static (double usedGb, double totalGb) Read()
    {
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) return (0, 0);
        var total = status.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
        var avail = status.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
        return (Math.Round(total - avail, 2), Math.Round(total, 2));
    }
}
