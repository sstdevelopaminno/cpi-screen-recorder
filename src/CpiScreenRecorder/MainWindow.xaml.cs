using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CpiScreenRecorder.Models;
using CpiScreenRecorder.Services;
using Microsoft.Win32;
using ScreenRecorderLib;

namespace CpiScreenRecorder;

public partial class MainWindow : Window
{
    private readonly RecordingService _recordingService = new();
    private readonly SettingsService _settingsService = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private AppSettings _settings = new();
    private DateTime _startedAt;
    private string? _lastFile;
    private bool _allowClose;

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
        _settings = _settingsService.Load();
        OutputPathText.Text = _settings.OutputDirectory;
        CursorCheck.IsChecked = _settings.ShowCursor;
        ClickHighlightCheck.IsChecked = _settings.HighlightClicks;
        SelectFrameRate(_settings.FrameRate);
        LoadDisplays(_settings.DisplayDeviceName);
    }

    private void LoadDisplays(string? preferredDeviceName = null)
    {
        DisplayCombo.Items.Clear();

        try
        {
            foreach (var display in _recordingService.GetDisplays())
                DisplayCombo.Items.Add(new DisplayOption(display.FriendlyName, display.DeviceName));

            if (DisplayCombo.Items.Count == 0)
            {
                SetStatus("ไม่พบหน้าจอ", "#FF647A");
                StartButton.IsEnabled = false;
                return;
            }

            var preferred = DisplayCombo.Items
                .Cast<DisplayOption>()
                .FirstOrDefault(d => string.Equals(d.DeviceName, preferredDeviceName, StringComparison.OrdinalIgnoreCase));

            DisplayCombo.SelectedItem = preferred ?? DisplayCombo.Items[0];
            StartButton.IsEnabled = true;
            SetStatus("พร้อมบันทึก", "#38D996");
        }
        catch (Exception ex)
        {
            SetStatus("โหลดหน้าจอไม่สำเร็จ", "#FF647A");
            StartButton.IsEnabled = false;
            MessageBox.Show(this, ex.Message, "CPI Screen Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        if (FpsCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var value))
            return value;
        return 30;
    }

    private void StartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (DisplayCombo.SelectedItem is not DisplayOption display)
        {
            MessageBox.Show(this, "กรุณาเลือกหน้าจอที่ต้องการบันทึก", "CPI Screen Recorder", MessageBoxButton.OK, MessageBoxImage.Information);
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

            _settings.OutputDirectory = outputDir;
            _settings.DisplayDeviceName = display.DeviceName;
            _settings.FrameRate = SelectedFrameRate();
            _settings.ShowCursor = CursorCheck.IsChecked == true;
            _settings.HighlightClicks = ClickHighlightCheck.IsChecked == true;
            _settingsService.Save(_settings);

            _recordingService.Start(
                display.DeviceName,
                outputFile,
                _settings.FrameRate,
                _settings.ShowCursor,
                _settings.HighlightClicks);

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
            MessageBox.Show(this, ex.Message, "เริ่มบันทึกไม่สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            MessageBox.Show(this, error, "CPI Screen Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private void SetRecordingUi(bool recording)
    {
        StartButton.IsEnabled = !recording && DisplayCombo.Items.Count > 0;
        StopButton.IsEnabled = recording;
        DisplayCombo.IsEnabled = !recording;
        FpsCombo.IsEnabled = !recording;
        CursorCheck.IsEnabled = !recording;
        ClickHighlightCheck.IsEnabled = !recording;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _startedAt;
        TimerText.Text = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private void SetStatus(string text, string color)
    {
        StatusText.Text = text;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
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

    private void RefreshDisplays_Click(object sender, RoutedEventArgs e) => LoadDisplays(_settings.DisplayDeviceName);

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFile is null || !File.Exists(_lastFile))
            return;

        Process.Start(new ProcessStartInfo(_lastFile) { UseShellExecute = true });
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var file = _lastFile;
        if (file is not null && File.Exists(file))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
            return;
        }

        var directory = OutputPathText.Text;
        if (Directory.Exists(directory))
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

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

            try { _recordingService.Stop(); } catch { }
        }

        _allowClose = true;
        _timer.Stop();
        _recordingService.Dispose();
    }
}
