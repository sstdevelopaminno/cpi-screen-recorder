using System.Windows;
using System.Windows.Controls;
using CpiScreenRecorder.Models;

namespace CpiScreenRecorder;

public partial class MainWindow
{
    private void LoadAudioDevices(
        string? preferredMicrophoneDevice = null,
        string? preferredSystemAudioDevice = null)
    {
        MicrophoneCombo.Items.Clear();
        SystemAudioCombo.Items.Clear();

        try
        {
            foreach (var device in _recordingService.GetMicrophones())
                MicrophoneCombo.Items.Add(device);

            foreach (var device in _recordingService.GetSystemAudioDevices())
                SystemAudioCombo.Items.Add(device);

            SelectAudioDevice(MicrophoneCombo, preferredMicrophoneDevice);
            SelectAudioDevice(SystemAudioCombo, preferredSystemAudioDevice);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"โหลดอุปกรณ์เสียงไม่สำเร็จ\n\n{ex.Message}",
                "CPI Screen Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static void SelectAudioDevice(ComboBox combo, string? preferredDeviceName)
    {
        if (combo.Items.Count == 0)
            return;

        var devices = combo.Items.Cast<AudioDeviceOption>().ToList();

        var selected = devices.FirstOrDefault(d => string.Equals(
                           d.DeviceName,
                           preferredDeviceName,
                           StringComparison.OrdinalIgnoreCase))
                       ?? devices.FirstOrDefault(d => d.IsDefault)
                       ?? devices[0];

        combo.SelectedItem = selected;
    }

    private void LoadCameras(string? preferredDeviceName = null)
    {
        CameraCombo.Items.Clear();

        try
        {
            foreach (var camera in _recordingService.GetCameras())
                CameraCombo.Items.Add(camera);

            if (CameraCombo.Items.Count == 0)
                return;

            var preferred = CameraCombo.Items
                .Cast<CameraDeviceOption>()
                .FirstOrDefault(c => string.Equals(
                    c.DeviceName,
                    preferredDeviceName,
                    StringComparison.OrdinalIgnoreCase));

            CameraCombo.SelectedItem = preferred ?? CameraCombo.Items[0];
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"โหลดรายการกล้องไม่สำเร็จ\n\n{ex.Message}",
                "CPI Screen Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static void SelectComboTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(
                    item.Tag?.ToString(),
                    tag,
                    StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private WebcamPosition SelectedWebcamPosition()
    {
        var tag = (WebcamPositionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        return Enum.TryParse<WebcamPosition>(tag, true, out var value)
            ? value
            : WebcamPosition.BottomRight;
    }

    private WebcamSizePreset SelectedWebcamSize()
    {
        var tag = (WebcamSizeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        return Enum.TryParse<WebcamSizePreset>(tag, true, out var value)
            ? value
            : WebcamSizePreset.Medium;
    }


    private void AudioOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        UpdateAudioControls();
        UpdateStartAvailability();
    }

    private void MicrophoneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;

        RestartMicrophoneMeter();
        UpdateStartAvailability();
    }

    private void RestartMicrophoneMeter()
    {
        var microphone = MicrophoneCombo.SelectedItem as AudioDeviceOption;
        _microphoneMeterService.Start(microphone);
    }

    private void MicrophoneMeterService_LevelChanged(object? sender, float level)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var percent = (int)Math.Round(level * 100);
            MicrophoneLevelMeter.Value = percent;
            MicrophoneLevelText.Text = $"{percent}%";
        });
    }

    private void WebcamOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        UpdateWebcamControls();
        UpdateStartAvailability();
    }

    private void UpdateWebcamControls()
    {
        WebcamControls.IsEnabled = WebcamCheck.IsChecked == true;
    }

    private void RefreshCameras_Click(object sender, RoutedEventArgs e)
    {
        var preferred = (CameraCombo.SelectedItem as CameraDeviceOption)?.DeviceName
                        ?? _settings.WebcamDeviceName;

        LoadCameras(preferred);
        UpdateWebcamControls();
        UpdateStartAvailability();
    }

    private void UpdateAudioControls()
    {
        var micEnabled = MicrophoneCheck.IsChecked == true;
        var systemEnabled = SystemAudioCheck.IsChecked == true;

        MicrophoneControls.IsEnabled = micEnabled;
        SystemAudioControls.IsEnabled = systemEnabled;

        if (micEnabled && systemEnabled)
            AudioSummaryText.Text = "เสียง: ไมค์ + เสียงคอม";
        else if (micEnabled)
            AudioSummaryText.Text = "เสียง: ไมโครโฟน";
        else if (systemEnabled)
            AudioSummaryText.Text = "เสียง: เสียงในคอม";
        else
            AudioSummaryText.Text = "เสียง: ปิด";
    }

    private void MicrophoneVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MicrophoneVolumeText is not null)
            MicrophoneVolumeText.Text = $"ระดับไมค์ {(int)Math.Round(e.NewValue)}%";
    }

    private void SystemAudioVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SystemAudioVolumeText is not null)
            SystemAudioVolumeText.Text = $"ระดับเสียงคอม {(int)Math.Round(e.NewValue)}%";
    }


    private void RefreshAudio_Click(object sender, RoutedEventArgs e)
    {
        var mic = (MicrophoneCombo.SelectedItem as AudioDeviceOption)?.DeviceName
                  ?? _settings.MicrophoneDeviceName;

        var system = (SystemAudioCombo.SelectedItem as AudioDeviceOption)?.DeviceName
                     ?? _settings.SystemAudioDeviceName;

        LoadAudioDevices(mic, system);
        RestartMicrophoneMeter();
        UpdateAudioControls();
        UpdateStartAvailability();
    }

}
