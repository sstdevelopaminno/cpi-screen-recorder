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
        SelectQualityPreset(_settings.QualityPreset);

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


    private void SetStatus(string text, string color)
    {
        StatusText.Text = text;
        StatusDot.Fill = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));
    }

}