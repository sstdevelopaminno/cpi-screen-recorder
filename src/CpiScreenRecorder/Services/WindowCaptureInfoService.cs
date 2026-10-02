using System.Runtime.InteropServices;
using CpiScreenRecorder.Models;

namespace CpiScreenRecorder.Services;

public static class WindowCaptureInfoService
{
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int DwmwaExtendedFrameBounds = 9;

    public static bool TryGetWindowCrop(IntPtr hwnd, out WindowCropInfo? crop)
    {
        crop = null;

        if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || IsIconic(hwnd))
            return false;

        if (!TryGetWindowRect(hwnd, out var windowRect))
            return false;

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return false;

        var info = new MonitorInfoEx
        {
            CbSize = Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };

        if (!GetMonitorInfo(monitor, ref info))
            return false;

        var monitorWidth = info.Monitor.Right - info.Monitor.Left;
        var monitorHeight = info.Monitor.Bottom - info.Monitor.Top;

        var left = Math.Clamp(windowRect.Left - info.Monitor.Left, 0, Math.Max(0, monitorWidth - 2));
        var top = Math.Clamp(windowRect.Top - info.Monitor.Top, 0, Math.Max(0, monitorHeight - 2));
        var right = Math.Clamp(windowRect.Right - info.Monitor.Left, left + 2, monitorWidth);
        var bottom = Math.Clamp(windowRect.Bottom - info.Monitor.Top, top + 2, monitorHeight);

        var width = MakeEven(right - left);
        var height = MakeEven(bottom - top);

        if (width < 32 || height < 32 || string.IsNullOrWhiteSpace(info.DeviceName))
            return false;

        crop = new WindowCropInfo(
            info.DeviceName,
            left,
            top,
            width,
            height);

        return true;
    }

    private static bool TryGetWindowRect(IntPtr hwnd, out NativeRect rect)
    {
        rect = default;

        try
        {
            var result = DwmGetWindowAttribute(
                hwnd,
                DwmwaExtendedFrameBounds,
                out rect,
                Marshal.SizeOf<NativeRect>());

            if (result == 0 && rect.Right > rect.Left && rect.Bottom > rect.Top)
                return true;
        }
        catch
        {
        }

        return GetWindowRect(hwnd, out rect)
               && rect.Right > rect.Left
               && rect.Bottom > rect.Top;
    }

    private static int MakeEven(int value)
    {
        value = Math.Max(0, value);
        return value % 2 == 0 ? value : value - 1;
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        out NativeRect pvAttribute,
        int cbAttribute);

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
