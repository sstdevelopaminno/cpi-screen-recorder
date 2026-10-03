using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CpiScreenRecorder.Models;
using CpiScreenRecorder.Services;
using RecorderCaptureMode = CpiScreenRecorder.Models.CaptureMode;

namespace CpiScreenRecorder;

public partial class MainWindow
{
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

}
