using System.IO;
using System.Windows;
using System.Windows.Controls;
using CpiScreenRecorder.Models;
using ScreenRecorderLib;
using RecorderCaptureMode = CpiScreenRecorder.Models.CaptureMode;

namespace CpiScreenRecorder;

public partial class MainWindow
{
    private void SelectFrameRate(int frameRate)
    {
        foreach (var item in FpsCombo.Items.OfType<ComboBoxItem>())
        {
            if (int.TryParse(item.Tag?.ToString(), out var value) && value == frameRate)
            {
                FpsCombo.SelectedItem = item;
                return;
            }
        }

        FpsCombo.SelectedIndex = 0;
    }

    private int SelectedFrameRate()
    {
        if (FpsCombo.SelectedItem is ComboBoxItem item
            && int.TryParse(item.Tag?.ToString(), out var value))
        {
            return value;
        }

        return 30;
    }

    private void SelectQualityPreset(RecordingQualityPreset preset)
    {
        foreach (var item in QualityPresetCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(
                    item.Tag?.ToString(),
                    preset.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                QualityPresetCombo.SelectedItem = item;
                return;
            }
        }

        QualityPresetCombo.SelectedIndex = 0;
    }

    private RecordingQualityPreset SelectedQualityPreset()
    {
        var tag = (QualityPresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        return Enum.TryParse<RecordingQualityPreset>(tag, true, out var value)
            ? value
            : RecordingQualityPreset.Smooth;
    }

    private void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        var mode = SelectedCaptureMode;
        DisplayOption? display = DisplayCombo.SelectedItem as DisplayOption;
        WindowOption? window = WindowCombo.SelectedItem as WindowOption;
        AudioDeviceOption? microphone = MicrophoneCombo.SelectedItem as AudioDeviceOption;
        AudioDeviceOption? systemAudio = SystemAudioCombo.SelectedItem as AudioDeviceOption;
        CameraDeviceOption? camera = CameraCombo.SelectedItem as CameraDeviceOption;

        if (mode is RecorderCaptureMode.Display or RecorderCaptureMode.Region && display is null)
        {
            ShowValidation("กรุณาเลือก Monitor ที่ต้องการบันทึก");
            return;
        }

        if (mode == RecorderCaptureMode.Window && window is null)
        {
            ShowValidation("กรุณาเลือกหน้าต่างโปรแกรมที่ต้องการบันทึก");
            return;
        }

        if (mode == RecorderCaptureMode.Region && !_selectedRegion.IsValid)
        {
            ShowValidation("กรุณากด “ลากกรอบเลือกพื้นที่” ก่อนเริ่มบันทึก");
            return;
        }

        if (MicrophoneCheck.IsChecked == true && microphone is null)
        {
            ShowValidation("เปิดการบันทึกไมโครโฟนไว้ แต่ไม่พบอุปกรณ์ไมโครโฟน");
            return;
        }

        if (SystemAudioCheck.IsChecked == true && systemAudio is null)
        {
            ShowValidation("เปิดการบันทึกเสียงในคอมไว้ แต่ไม่พบอุปกรณ์เสียงขาออก");
            return;
        }

        if (WebcamCheck.IsChecked == true && camera is null)
        {
            ShowValidation("เปิด Webcam Overlay ไว้ แต่ไม่พบกล้อง กรุณากดรีเฟรชกล้อง");
            return;
        }

        var outputDir = OutputPathText.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDir))
            outputDir = _settings.OutputDirectory;

        try
        {
            Directory.CreateDirectory(outputDir);

            var fileName = $"CPI_Record_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
            var outputFile = Path.Combine(outputDir, fileName);

            SaveCurrentSettings(display, window, microphone, systemAudio, camera, outputDir);

            var request = new RecordingRequest
            {
                CaptureMode = mode,
                DisplayDeviceName = display?.DeviceName,
                WindowHandle = window?.Handle ?? IntPtr.Zero,
                WindowTitle = window?.Title,
                WindowProcessId = window?.ProcessId,
                Region = mode == RecorderCaptureMode.Region ? _selectedRegion : null,
                OutputFile = outputFile,
                FrameRate = SelectedFrameRate(),
                QualityPreset = SelectedQualityPreset(),
                ShowCursor = CursorCheck.IsChecked == true,
                HighlightClicks = ClickHighlightCheck.IsChecked == true,

                RecordMicrophone = MicrophoneCheck.IsChecked == true,
                MicrophoneDeviceName = microphone?.DeviceName,
                MicrophoneVolume = (int)Math.Round(MicrophoneVolumeSlider.Value),

                RecordSystemAudio = SystemAudioCheck.IsChecked == true,
                SystemAudioDeviceName = systemAudio?.DeviceName,
                SystemAudioVolume = (int)Math.Round(SystemAudioVolumeSlider.Value),

                WebcamEnabled = WebcamCheck.IsChecked == true,
                WebcamDeviceName = camera?.DeviceName,
                WebcamPosition = SelectedWebcamPosition(),
                WebcamSize = SelectedWebcamSize()
            };

            // The live meter polls the Windows audio endpoint every 60 ms.
            // Stop it during capture to avoid unnecessary contention on long recordings.
            _microphoneMeterService.Stop();
            _recordingService.Start(request);

            _startedAt = DateTime.Now;
            _pausedAt = null;
            _pausedDuration = TimeSpan.Zero;
            TimerText.Text = "00:00:00";
            _timer.Start();
            SetRecordingUi(true);
            SetStatus("กำลังบันทึก", "#FF4D6D");
        }
        catch (Exception ex)
        {
            RestartMicrophoneMeter();
            SetRecordingUi(false);
            SetStatus("เริ่มบันทึกไม่สำเร็จ", "#FF647A");
            MessageBox.Show(
                this,
                ex.Message,
                "เริ่มบันทึกไม่สำเร็จ",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SaveCurrentSettings(
        DisplayOption? display,
        WindowOption? window,
        AudioDeviceOption? microphone,
        AudioDeviceOption? systemAudio,
        CameraDeviceOption? camera,
        string outputDir)
    {
        _settings.OutputDirectory = outputDir;
        _settings.CaptureMode = SelectedCaptureMode;
        _settings.DisplayDeviceName = display?.DeviceName;
        _settings.WindowTitle = window?.Title;

        _settings.FrameRate = SelectedFrameRate();
        _settings.QualityPreset = SelectedQualityPreset();
        _settings.ShowCursor = CursorCheck.IsChecked == true;
        _settings.HighlightClicks = ClickHighlightCheck.IsChecked == true;

        _settings.RecordMicrophone = MicrophoneCheck.IsChecked == true;
        _settings.MicrophoneDeviceName = microphone?.DeviceName;
        _settings.MicrophoneVolume = (int)Math.Round(MicrophoneVolumeSlider.Value);

        _settings.RecordSystemAudio = SystemAudioCheck.IsChecked == true;
        _settings.SystemAudioDeviceName = systemAudio?.DeviceName;
        _settings.SystemAudioVolume = (int)Math.Round(SystemAudioVolumeSlider.Value);

        _settings.WebcamEnabled = WebcamCheck.IsChecked == true;
        _settings.WebcamDeviceName = camera?.DeviceName;
        _settings.WebcamPosition = SelectedWebcamPosition();
        _settings.WebcamSize = SelectedWebcamSize();

        SaveRegionToSettings();
        _settingsService.Save(_settings);
    }

    private void SaveRegionToSettings()
    {
        _settings.RegionX = _selectedRegion.X;
        _settings.RegionY = _selectedRegion.Y;
        _settings.RegionWidth = _selectedRegion.Width;
        _settings.RegionHeight = _selectedRegion.Height;
        _settingsService.Save(_settings);
    }

    private void ShowValidation(string message)
    {
        MessageBox.Show(
            this,
            message,
            "CPI Screen Recorder",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        TogglePauseResume();
    }

    private void TogglePauseResume()
    {
        if (!_recordingService.IsRecording)
            return;

        if (_recordingService.IsPaused)
        {
            if (!_recordingService.Resume())
            {
                SetStatus("ทำต่อไม่สำเร็จ", "#FF647A");
            }
        }
        else
        {
            if (!_recordingService.Pause())
            {
                SetStatus("พักการบันทึกไม่สำเร็จ", "#FF647A");
            }
        }
    }

    private void HotkeyService_StartStopPressed(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_recordingService.IsRecording)
                StopRecording_Click(this, new RoutedEventArgs());
            else if (!_recordingService.IsBusy)
                StartRecording_Click(this, new RoutedEventArgs());
        });
    }

    private void HotkeyService_PauseResumePressed(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(TogglePauseResume);
    }

    private async void StopRecording_Click(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        StartButton.IsEnabled = false;
        SetStatus("กำลังหยุดและปิดไฟล์ MP4...", "#F6C453");

        var stoppedCleanly = await _recordingService.StopAsync(
            TimeSpan.FromSeconds(10));

        if (!stoppedCleanly)
        {
            _timer.Stop();
            RestartMicrophoneMeter();
            SetRecordingUi(false);
            SetStatus("รีเซ็ตตัวบันทึกแล้ว", "#F6C453");

            MessageBox.Show(
                this,
                "ตัวบันทึกใช้เวลาปิดนานเกินไป ระบบได้รีเซ็ต capture session แล้ว\n\nสามารถเริ่มบันทึกใหม่ได้โดยไม่ต้องปิดโปรแกรม",
                "CPI Screen Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RecordingService_StatusChanged(object? sender, RecorderStatus status)
    {
        Dispatcher.BeginInvoke(() =>
        {
            switch (status)
            {
                case RecorderStatus.Recording:
                    if (_pausedAt.HasValue)
                    {
                        _pausedDuration += DateTime.Now - _pausedAt.Value;
                        _pausedAt = null;
                    }

                    _timer.Start();
                    PauseButton.IsEnabled = true;
                    PauseButton.Content = "Ⅱ  พักชั่วคราว";
                    SetStatus("กำลังบันทึก", "#FF4D6D");
                    break;

                case RecorderStatus.Paused:
                    _pausedAt ??= DateTime.Now;
                    _timer.Stop();
                    Timer_Tick(null, EventArgs.Empty);
                    PauseButton.IsEnabled = true;
                    PauseButton.Content = "▶  ทำต่อ";
                    SetStatus("พักการบันทึก", "#F6C453");
                    break;

                case RecorderStatus.Finishing:
                    _timer.Stop();
                    PauseButton.IsEnabled = false;
                    SetStatus("กำลังปิดไฟล์ MP4...", "#F6C453");
                    break;
            }
        });
    }

    private void RecordingService_RecordingCompleted(object? sender, string filePath)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _timer.Stop();
            RestartMicrophoneMeter();
            SetRecordingUi(false);
            SetStatus("บันทึกเรียบร้อย", "#38D996");

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                _lastFile = filePath;
                LatestFileText.Text = Path.GetFileName(filePath);
                LatestFileText.ToolTip = filePath;
                OpenFileButton.IsEnabled = File.Exists(filePath);
                OpenFolderButton.IsEnabled = Directory.Exists(Path.GetDirectoryName(filePath));
            }
        });
    }

    private void RecordingService_RecordingFailed(object? sender, string error)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _timer.Stop();
            RestartMicrophoneMeter();
            SetRecordingUi(false);
            SetStatus("บันทึกไม่สำเร็จ", "#FF647A");
            MessageBox.Show(
                this,
                error,
                "CPI Screen Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }

    private void SetRecordingUi(bool recording)
    {
        StartButton.IsEnabled = !recording;
        StopButton.IsEnabled = recording;
        PauseButton.IsEnabled = recording && !_recordingService.IsBusy ? false : recording;

        ModePanel.IsEnabled = !recording;
        DisplayCard.IsEnabled = !recording;
        WindowCard.IsEnabled = !recording;
        VideoOptionsPanel.IsEnabled = !recording;
        MousePanel.IsEnabled = !recording;
        AudioPanel.IsEnabled = !recording;
        WebcamPanel.IsEnabled = !recording;
        OutputPanel.IsEnabled = !recording;

        if (!recording)
        {
            PauseButton.Content = "Ⅱ  พักชั่วคราว";
            UpdateAudioControls();
            UpdateWebcamControls();
            UpdateStartAvailability();
        }
    }

    private void UpdateStartAvailability()
    {
        if (_recordingService.IsBusy)
        {
            StartButton.IsEnabled = false;
            return;
        }

        var canStart = SelectedCaptureMode switch
        {
            RecorderCaptureMode.Display => DisplayCombo.Items.Count > 0,
            RecorderCaptureMode.Window => WindowCombo.Items.Count > 0,
            RecorderCaptureMode.Region => DisplayCombo.Items.Count > 0 && _selectedRegion.IsValid,
            _ => false
        };

        if (MicrophoneCheck.IsChecked == true && MicrophoneCombo.Items.Count == 0)
            canStart = false;

        if (SystemAudioCheck.IsChecked == true && SystemAudioCombo.Items.Count == 0)
            canStart = false;

        if (WebcamCheck.IsChecked == true && CameraCombo.Items.Count == 0)
            canStart = false;

        StartButton.IsEnabled = canStart;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var current = _pausedAt ?? DateTime.Now;
        var elapsed = current - _startedAt - _pausedDuration;

        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        TimerText.Text = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

}
