using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private AppSettings _settings = new();
    private CaptureRegion _selectedRegion = CaptureRegion.Empty;
    private DateTime _startedAt;
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
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
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

        _selectedRegion = new CaptureRegion(
            _settings.RegionX,
            _settings.RegionY,
            _settings.RegionWidth,
            _settings.RegionHeight);

        LoadDisplays(_settings.DisplayDeviceName);
        LoadWindows(_settings.WindowTitle);
        LoadAudioDevices(_settings.MicrophoneDeviceName, _settings.SystemAudioDeviceName);
        SelectCaptureMode(_settings.CaptureMode);

        _loading = false;

        UpdateCaptureModeUi();
        UpdateRegionLabel();
        UpdateAudioControls();
        UpdateStartAvailability();
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

        var outputDir = OutputPathText.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDir))
            outputDir = _settings.OutputDirectory;

        try
        {
            Directory.CreateDirectory(outputDir);

            var fileName = $"CPI_Record_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
            var outputFile = Path.Combine(outputDir, fileName);

            SaveCurrentSettings(display, window, microphone, systemAudio, outputDir);

            var request = new RecordingRequest
            {
                CaptureMode = mode,
                DisplayDeviceName = display?.DeviceName,
                WindowHandle = window?.Handle ?? IntPtr.Zero,
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
                SystemAudioVolume = (int)Math.Round(SystemAudioVolumeSlider.Value)
            };

            _recordingService.Start(request);

            _startedAt = DateTime.Now;
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

    private void StopRecording_Click(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        SetStatus("กำลังปิดไฟล์ MP4...", "#F6C453");
        _recordingService.Stop();
    }

    private void RecordingService_StatusChanged(object? sender, RecorderStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            if (status == RecorderStatus.Recording)
                SetStatus("กำลังบันทึก", "#FF4D6D");
            else if (status == RecorderStatus.Paused)
                SetStatus("พักการบันทึก", "#F6C453");
        });
    }

    private void RecordingService_RecordingCompleted(object? sender, string filePath)
    {
        Dispatcher.Invoke(() =>
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
        Dispatcher.Invoke(() =>
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

        ModePanel.IsEnabled = !recording;
        DisplayCard.IsEnabled = !recording;
        WindowCard.IsEnabled = !recording;
        VideoOptionsPanel.IsEnabled = !recording;
        MousePanel.IsEnabled = !recording;
        AudioPanel.IsEnabled = !recording;
        OutputPanel.IsEnabled = !recording;

        if (!recording)
        {
            UpdateAudioControls();
            UpdateStartAvailability();
        }
    }

    private void UpdateStartAvailability()
    {
        if (_recordingService.IsRecording)
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

        StartButton.IsEnabled = canStart;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _startedAt;
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
        }
        else
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        if (_recordingService.IsRecording)
        {
            var result = MessageBox.Show(
                this,
                "กำลังบันทึกหน้าจออยู่ ต้องการหยุดและปิดโปรแกรมหรือไม่?",
                "CPI Screen Recorder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            try
            {
                _recordingService.Stop();
            }
            catch
            {
            }
        }

        _allowClose = true;
        _timer.Stop();
        _recordingService.Dispose();
    }
}
