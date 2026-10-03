using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace CpiScreenRecorder;

public partial class MainWindow
{
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
}
