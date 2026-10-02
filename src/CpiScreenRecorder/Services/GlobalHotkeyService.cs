using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CpiScreenRecorder.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private const int StartStopHotkeyId = 0x4350;
    private const int PauseResumeHotkeyId = 0x4351;

    private HwndSource? _source;
    private IntPtr _handle;

    public event EventHandler? StartStopPressed;
    public event EventHandler? PauseResumePressed;

    public bool Register(IntPtr handle)
    {
        Unregister();

        _handle = handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);

        var startOk = RegisterHotKey(
            handle,
            StartStopHotkeyId,
            ModNoRepeat,
            (uint)System.Windows.Forms.Keys.F8);

        var pauseOk = RegisterHotKey(
            handle,
            PauseResumeHotkeyId,
            ModNoRepeat,
            (uint)System.Windows.Forms.Keys.F9);

        return startOk && pauseOk;
    }

    public void Unregister()
    {
        if (_handle != IntPtr.Zero)
        {
            UnregisterHotKey(_handle, StartStopHotkeyId);
            UnregisterHotKey(_handle, PauseResumeHotkeyId);
        }

        if (_source is not null)
            _source.RemoveHook(WndProc);

        _source = null;
        _handle = IntPtr.Zero;
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg != WmHotkey)
            return IntPtr.Zero;

        var id = wParam.ToInt32();

        if (id == StartStopHotkeyId)
        {
            StartStopPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (id == PauseResumeHotkeyId)
        {
            PauseResumePressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);
}
