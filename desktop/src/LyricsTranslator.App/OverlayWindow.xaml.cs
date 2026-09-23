using System.Runtime.InteropServices;
using LyricsTranslator.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace LyricsTranslator;

public sealed partial class OverlayWindow : Window
{
    private readonly AppWindow _appWindow;
    private bool _placed;

    public OverlayWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        _appWindow.Resize(new Windows.Graphics.SizeInt32(560, 260));
        ApplyAlwaysOnTop();
        ApplyToolWindow(hwnd);

        _appWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            ViewModel.OverlayEnabled = false;
        };
    }

    public MainViewModel ViewModel { get; }

    public void ShowQuietly()
    {
        if (!_placed)
        {
            PlaceBottomCenter();
            _placed = true;
        }

        ApplyAlwaysOnTop();
        _appWindow.Show(false);
    }

    public void HideQuietly()
    {
        _appWindow.Hide();
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OverlayEnabled = false;
    }

    private void ApplyAlwaysOnTop()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = true;
        }
    }

    private void PlaceBottomCenter()
    {
        var work = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = _appWindow.Size.Width;
        var height = _appWindow.Size.Height;
        var x = work.X + Math.Max(0, (work.Width - width) / 2);
        var y = work.Y + Math.Max(0, work.Height - height - 56);
        _appWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private static void ApplyToolWindow(nint hwnd)
    {
        try
        {
            const int gwlExStyle = -20;
            const int wsExToolWindow = 0x00000080;
            var style = GetWindowLongPtr(hwnd, gwlExStyle);
            SetWindowLongPtr(hwnd, gwlExStyle, style | wsExToolWindow);
        }
        catch
        {
            // Overlay still works without tool-window style.
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
