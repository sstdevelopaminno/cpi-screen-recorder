using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CpiScreenRecorder.Models;
using CpiScreenRecorder.Services;
using Microsoft.Win32;
using ScreenRecorderLib;
using RecorderCaptureMode = CpiScreenRecorder.Models.CaptureMode;

namespace CpiScreenRecorder;

public partial class MainWindow : Window
{
    private readonly RecordingService _recordingService = new();
    private readonly SettingsService _settingsService = new();
    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly MicrophoneMeterService _microphoneMeterService = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private AppSettings _settings = new();
    private CaptureRegion _selectedRegion = CaptureRegion.Empty;
    private DateTime _startedAt;
    private DateTime? _pausedAt;
    private TimeSpan _pausedDuration = TimeSpan.Zero;
    private string? _lastFile;
    private bool _allowClose;
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();

        _timer.Tick += Timer_Tick;
        _recordingService.RecordingCompleted += RecordingService_RecordingCompleted;
        _recordingService.RecordingFailed += RecordingService_RecordingFailed;
        _recordingService.StatusChanged += RecordingService_StatusChanged;

        _hotkeyService.StartStopPressed += HotkeyService_StartStopPressed;
        _hotkeyService.PauseResumePressed += HotkeyService_PauseResumePressed;

        _microphoneMeterService.LevelChanged += MicrophoneMeterService_LevelChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FitWindowToWorkArea();
        _loading = true;

        _settings = _settingsService.Load();
        OutputPathText.Text = _settings.OutputDirectory;

        CursorCheck.IsChecked = _settings.ShowCursor;
        ClickHighlightCheck.IsChecked = _settings.HighlightClicks;
        SelectFrameRate(_settings.FrameRate);

        MicrophoneCheck.IsChecked = _settings.RecordMicrophone;
        SystemAudioCheck.IsChecked = _settings.RecordSystemAudio;
        MicrophoneVolumeSlider.Value = _settings.MicrophoneVolume;
        SystemAudioVolumeSlider.Value = _settings.SystemAudioVolume;

        WebcamCheck.IsChecked = _settings.WebcamEnabled;
        SelectComboTag(WebcamPositionCombo, _settings.WebcamPosition.ToString());
        SelectComboTag(WebcamSizeCombo, _settings.WebcamSize.ToString());

        _selectedRegion = new CaptureRegion(
            _settings.RegionX,
            _settings.RegionY,
            _settings.RegionWidth,
            _settings.RegionHeight);

        LoadDisplays(_settings.DisplayDeviceName);
        LoadWindows(_settings.WindowTitle);
        LoadAudioDevices(_settings.MicrophoneDeviceName, _settings.SystemAudioDeviceName);
        LoadCameras(_settings.WebcamDeviceName);
        SelectCaptureMode(_settings.CaptureMode);

        _loading = false;

        UpdateCaptureModeUi();
        UpdateRegionLabel();
        UpdateAudioControls();
        UpdateWebcamControls();
        RestartMicrophoneMeter();
        UpdateStartAvailability();
        UpdateWindowStateButton();

        if (_settings.EnableGlobalHotkeys)
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (!_hotkeyService.Register(handle))
            {
                _settings.EnableGlobalHotkeys = false;
                _settingsService.Save(_settings);
            }
        }

        SetStatus("พร้อมบันทึก", "#38D996");
    }

    private RecorderCaptureMode SelectedCaptureMode
    {
        get
        {
            if (ModeWindowRadio.IsChecked == true)
                return RecorderCaptureMode.Window;
            if (ModeRegionRadio.IsChecked == true)
                return RecorderCaptureMode.Region;
            return RecorderCaptureMode.Display;
        }
    }

    private void SelectCaptureMode(RecorderCaptureMode mode)
    {
        switch (mode)
        {
            case RecorderCaptureMode.Window:
                ModeWindowRadio.IsChecked = true;
                break;
            case RecorderCaptureMode.Region:
                ModeRegionRadio.IsChecked = true;
                break;
            default:
                ModeDisplayRadio.IsChecked = true;
                break;
        }
    }

    private void CaptureMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        UpdateCaptureModeUi();
        UpdateStartAvailability();
    }

    private void UpdateCaptureModeUi()
    {
        var mode = SelectedCaptureMode;

        DisplayCard.Visibility = mode == RecorderCaptureMode.Window
            ? Visibility.Collapsed
            : Visibility.Visible;

        WindowCard.Visibility = mode == RecorderCaptureMode.Window
            ? Visibility.Visible
            : Visibility.Collapsed;

        RegionControls.Visibility = mode == RecorderCaptureMode.Region
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LoadDisplays(string? preferredDeviceName = null)
    {
        DisplayCombo.Items.Clear();

        try
        {
            foreach (var display in _recordingService.GetDisplays())
                DisplayCombo.Items.Add(display);

            if (DisplayCombo.Items.Count == 0)
            {
                SetStatus("ไม่พบหน้าจอ", "#FF647A");
                return;
            }

            var preferred = DisplayCombo.Items
                .Cast<DisplayOption>()
                .FirstOrDefault(d => string.Equals(
                    d.DeviceName,
                    preferredDeviceName,
                    StringComparison.OrdinalIgnoreCase));

            DisplayCombo.SelectedItem = preferred ?? DisplayCombo.Items[0];
        }
        catch (Exception ex)
        {
            SetStatus("โหลดหน้าจอไม่สำเร็จ", "#FF647A");
            MessageBox.Show(
                this,
                ex.Message,
                "CPI Screen Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadWindows(string? preferredTitle = null)
    {
        WindowCombo.Items.Clear();

        try
        {
            var currentPid = Environment.ProcessId;

            foreach (var window in _recordingService.GetWindows()
                         .Where(w => w.ProcessId != currentPid))
            {
                WindowCombo.Items.Add(window);
            }

            if (WindowCombo.Items.Count == 0)
                return;

            var preferred = WindowCombo.Items
                .Cast<WindowOption>()
                .FirstOrDefault(w => string.Equals(
                    w.Title,
                    preferredTitle,
                    StringComparison.CurrentCultureIgnoreCase));

            WindowCombo.SelectedItem = preferred ?? WindowCombo.Items[0];
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"ไม่สามารถโหลดรายการหน้าต่างโปรแกรมได้\n\n{ex.Message}",
                "CPI Screen Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

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

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        if (DisplayCombo.SelectedItem is not DisplayOption display)
        {
            MessageBox.Show(
                this,
                "กรุณาเลือก Monitor ก่อนเลือกพื้นที่",
                "CPI Screen Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var screen = MonitorInfoService.Find(display.DeviceName);

        if (screen is null)
        {
            MessageBox.Show(
                this,
                "ไม่พบข้อมูลตำแหน่งของ Monitor ที่เลือก กรุณากดรีเฟรชแล้วลองอีกครั้ง",
                "CPI Screen Recorder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var previousState = WindowState;

        try
        {
            Hide();

            var selector = new RegionSelectionWindow(screen);
            var result = selector.ShowDialog();

            if (result == true && selector.SelectedRegion.IsValid)
            {
                _selectedRegion = selector.SelectedRegion;
                SaveRegionToSettings();
            }
        }
        finally
        {
            Show();
            WindowState = previousState;
            Activate();
            Focus();
        }

        UpdateRegionLabel();
        UpdateStartAvailability();
    }

    private void DisplayCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;

        if (SelectedCaptureMode == RecorderCaptureMode.Region)
        {
            _selectedRegion = CaptureRegion.Empty;
            UpdateRegionLabel();
        }

        UpdateStartAvailability();
    }

    private void UpdateRegionLabel()
    {
        RegionLabel.Text = _selectedRegion.Label;
        RegionLabel.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(
                _selectedRegion.IsValid ? "#D9E7FB" : "#7C91AF"));
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

    private void SetStatus(string text, string color)
    {
        StatusText.Text = text;
        StatusDot.Fill = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "เลือกโฟลเดอร์สำหรับบันทึกวิดีโอ",
            InitialDirectory = Directory.Exists(OutputPathText.Text)
                ? OutputPathText.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputPathText.Text = dialog.FolderName;
            _settings.OutputDirectory = dialog.FolderName;
            _settingsService.Save(_settings);
        }
    }

    private void RefreshDisplays_Click(object sender, RoutedEventArgs e)
    {
        var preferred = (DisplayCombo.SelectedItem as DisplayOption)?.DeviceName
                        ?? _settings.DisplayDeviceName;

        LoadDisplays(preferred);
        UpdateStartAvailability();
    }

    private void RefreshWindows_Click(object sender, RoutedEventArgs e)
    {
        var preferred = (WindowCombo.SelectedItem as WindowOption)?.Title
                        ?? _settings.WindowTitle;

        LoadWindows(preferred);
        UpdateStartAvailability();
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

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFile is null || !File.Exists(_lastFile))
            return;

        Process.Start(new ProcessStartInfo(_lastFile)
        {
            UseShellExecute = true
        });
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var file = _lastFile;

        if (file is not null && File.Exists(file))
        {
            Process.Start(new ProcessStartInfo(
                "explorer.exe",
                $"/select,\"{file}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        var directory = OutputPathText.Text;

        if (Directory.Exists(directory))
        {
            Process.Start(new ProcessStartInfo(directory)
            {
                UseShellExecute = true
            });
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

            UpdateWindowStateButton();
        }
        else
        {
            DragMove();
        }
    }

    private void FitWindowToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        const double edgeGap = 12;

        if (workArea.Width < MinWidth + edgeGap)
            MinWidth = Math.Max(720, workArea.Width - edgeGap);

        if (workArea.Height < MinHeight + edgeGap)
            MinHeight = Math.Max(460, workArea.Height - edgeGap);

        Width = Math.Min(Width, Math.Max(MinWidth, workArea.Width - edgeGap));
        Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height - edgeGap));

        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
    }

    private void Window_StateChanged(object? sender, EventArgs e)
        => UpdateWindowStateButton();

    private void UpdateWindowStateButton()
    {
        if (MaximizeButton is null)
            return;

        if (WindowState == WindowState.Maximized)
        {
            MaximizeButton.Content = "❐";
            MaximizeButton.ToolTip = "คืนขนาดหน้าต่าง";
        }
        else
        {
            MaximizeButton.Content = "□";
            MaximizeButton.ToolTip = "ขยายเต็มจอ";
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        if (_recordingService.IsBusy)
        {
            var result = MessageBox.Show(
                this,
                "กำลังบันทึกหรือกำลังปิดไฟล์อยู่ ต้องการหยุดและปิดโปรแกรมหรือไม่?",
                "CPI Screen Recorder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = true;
            _ = StopAndCloseAsync();
            return;
        }

        _allowClose = true;
        _timer.Stop();
        _hotkeyService.Dispose();
        _microphoneMeterService.Dispose();
        _recordingService.Dispose();
    }

    private async Task StopAndCloseAsync()
    {
        SetStatus("กำลังหยุดและปิดไฟล์ MP4...", "#F6C453");

        await _recordingService.StopAsync(TimeSpan.FromSeconds(8));

        _timer.Stop();
        _hotkeyService.Dispose();
        _microphoneMeterService.Dispose();
        _recordingService.Dispose();

        _allowClose = true;

        await Dispatcher.InvokeAsync(Close);
    }
}
