using System.Runtime.InteropServices;
using CpiScreenRecorder.Models;

namespace CpiScreenRecorder.Services;

public static class MonitorInfoService
{
    public static MonitorBounds? Find(string deviceName)
    {
        MonitorBounds? match = null;

        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitor, _, _, _) =>
            {
                var info = new MonitorInfoEx
                {
                    CbSize = Marshal.SizeOf<MonitorInfoEx>()
                };

                if (!GetMonitorInfo(monitor, ref info))
                    return true;

                if (!string.Equals(
                        info.DeviceName,
                        deviceName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                match = new MonitorBounds(
                    info.DeviceName,
                    info.Monitor.Left,
                    info.Monitor.Top,
                    info.Monitor.Right - info.Monitor.Left,
                    info.Monitor.Bottom - info.Monitor.Top);

                return false;
            },
            IntPtr.Zero);

        return match;
    }

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        IntPtr lprcMonitor,
        IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(
        IntPtr hMonitor,
        ref MonitorInfoEx lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int CbSize;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }
}
