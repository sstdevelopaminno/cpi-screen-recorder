using NAudio.CoreAudioApi;
using CpiScreenRecorder.Models;

namespace CpiScreenRecorder.Services;

public sealed class MicrophoneMeterService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private System.Threading.Timer? _timer;

    public event EventHandler<float>? LevelChanged;

    public void Start(AudioDeviceOption? option)
    {
        Stop();

        if (option is null)
        {
            LevelChanged?.Invoke(this, 0f);
            return;
        }

        _device = ResolveDevice(option);

        if (_device is null)
        {
            LevelChanged?.Invoke(this, 0f);
            return;
        }

        _timer = new System.Threading.Timer(
            _ => Poll(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(60));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;

        _device?.Dispose();
        _device = null;

        LevelChanged?.Invoke(this, 0f);
    }

    private MMDevice? ResolveDevice(AudioDeviceOption option)
    {
        try
        {
            var devices = _enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

            return devices.FirstOrDefault(d =>
                       string.Equals(d.ID, option.DeviceName, StringComparison.OrdinalIgnoreCase))
                   ?? devices.FirstOrDefault(d =>
                       string.Equals(d.FriendlyName, option.FriendlyName, StringComparison.CurrentCultureIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private void Poll()
    {
        try
        {
            var value = _device?.AudioMeterInformation.MasterPeakValue ?? 0f;
            LevelChanged?.Invoke(this, Math.Clamp(value, 0f, 1f));
        }
        catch
        {
            LevelChanged?.Invoke(this, 0f);
        }
    }

    public void Dispose()
    {
        Stop();
        _enumerator.Dispose();
    }
}
