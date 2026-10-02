using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using CpiScreenRecorder.Models;

namespace CpiScreenRecorder;

public partial class RegionSelectionWindow : Window
{
    private readonly MonitorBounds _screen;
    private Point _start;
    private bool _dragging;

    public CaptureRegion SelectedRegion { get; private set; } = CaptureRegion.Empty;

    public RegionSelectionWindow(MonitorBounds screen)
    {
        InitializeComponent();
        _screen = screen;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowPos(
            handle,
            IntPtr.Zero,
            _screen.Left,
            _screen.Top,
            _screen.Width,
            _screen.Height,
            SwpNoZOrder | SwpShowWindow);

        Activate();
        Focus();
    }

    private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = ClampPoint(e.GetPosition(SelectionCanvas));
        _dragging = true;
        SelectedRegion = CaptureRegion.Empty;

        Canvas.SetLeft(SelectionBorder, _start.X);
        Canvas.SetTop(SelectionBorder, _start.Y);
        SelectionBorder.Width = 0;
        SelectionBorder.Height = 0;
        SelectionBorder.Visibility = Visibility.Visible;
        SelectionInfoPanel.Visibility = Visibility.Collapsed;

        SelectionCanvas.CaptureMouse();
    }

    private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        var current = ClampPoint(e.GetPosition(SelectionCanvas));
        DrawSelection(_start, current);
    }

    private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        SelectionCanvas.ReleaseMouseCapture();

        var current = ClampPoint(e.GetPosition(SelectionCanvas));
        DrawSelection(_start, current);
        UpdateSelectedRegion();
    }

    private void DrawSelection(Point first, Point second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var width = Math.Abs(first.X - second.X);
        var height = Math.Abs(first.Y - second.Y);

        Canvas.SetLeft(SelectionBorder, left);
        Canvas.SetTop(SelectionBorder, top);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
    }

    private void UpdateSelectedRegion()
    {
        if (SelectionCanvas.ActualWidth <= 0 || SelectionCanvas.ActualHeight <= 0)
            return;

        var leftDip = Canvas.GetLeft(SelectionBorder);
        var topDip = Canvas.GetTop(SelectionBorder);
        var widthDip = SelectionBorder.Width;
        var heightDip = SelectionBorder.Height;

        var scaleX = _screen.Width / SelectionCanvas.ActualWidth;
        var scaleY = _screen.Height / SelectionCanvas.ActualHeight;

        var x = Math.Max(0, (int)Math.Round(leftDip * scaleX));
        var y = Math.Max(0, (int)Math.Round(topDip * scaleY));
        var width = Math.Min(
            _screen.Width - x,
            Math.Max(0, (int)Math.Round(widthDip * scaleX)));
        var height = Math.Min(
            _screen.Height - y,
            Math.Max(0, (int)Math.Round(heightDip * scaleY)));

        width = MakeEven(width);
        height = MakeEven(height);

        SelectedRegion = new CaptureRegion(x, y, width, height);

        if (SelectedRegion.IsValid)
        {
            SelectionSizeText.Text = $"{SelectedRegion.Width} × {SelectedRegion.Height} px";
            SelectionInfoPanel.Visibility = Visibility.Visible;
        }
        else
        {
            SelectionInfoPanel.Visibility = Visibility.Collapsed;
        }
    }

    private static int MakeEven(int value)
    {
        if (value < 0)
            return 0;
        return value % 2 == 0 ? value : value - 1;
    }

    private Point ClampPoint(Point point)
    {
        return new Point(
            Math.Clamp(point.X, 0, Math.Max(0, SelectionCanvas.ActualWidth)),
            Math.Clamp(point.Y, 0, Math.Max(0, SelectionCanvas.ActualHeight)));
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!SelectedRegion.IsValid)
            return;

        DialogResult = true;
        Close();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SelectedRegion = CaptureRegion.Empty;
        SelectionBorder.Visibility = Visibility.Collapsed;
        SelectionInfoPanel.Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter && SelectedRegion.IsValid)
        {
            DialogResult = true;
            Close();
        }
    }

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
