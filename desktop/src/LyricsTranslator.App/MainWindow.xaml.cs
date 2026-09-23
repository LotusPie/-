using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using LyricsTranslator.Core.NowPlaying;
using LyricsTranslator.Core.Pipeline;
using LyricsTranslator.Core.Settings;
using LyricsTranslator.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace LyricsTranslator;

public sealed partial class MainWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly AppWindow _appWindow;
    private OverlayWindow? _overlay;
    private bool _allowClose;
    private SettingsWindow? _settingsWindow;

    public MainWindow(LyricsPipeline pipeline, INowPlayingSource nowPlaying, SettingsStore settings)
    {
        _settings = settings;
        ViewModel = new MainViewModel(pipeline, settings, DispatcherQueue);
        TrayMenu = BuildTrayMenu();
        ShowWindowCommand = new RelayCommand(ShowFromTray);
        InitializeComponent();

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Resize(new Windows.Graphics.SizeInt32(960, 720));
        _appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        _appWindow.Closing += OnClosing;

        TrayIcon.LeftClickCommand = ShowWindowCommand;
        TrayIcon.ForceCreate();

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.OverlayShouldShow)
                or nameof(MainViewModel.HasLyricLines)
                or nameof(MainViewModel.OverlayEnabled))
            {
                SyncOverlayVisibility();
            }
        };

        nowPlaying.SessionChanged += async (_, session) =>
        {
            await ViewModel.OnSessionChangedAsync(session);
        };
        nowPlaying.ProgressChanged += (_, progress) => ViewModel.OnProgress(progress);

        Closed += (_, _) =>
        {
            try
            {
                TrayIcon.Dispose();
            }
            catch
            {
                // ignored
            }
        };

        SyncOverlayVisibility();
    }

    public MainViewModel ViewModel { get; }

    public MenuFlyout TrayMenu { get; }

    public IRelayCommand ShowWindowCommand { get; }

    public Visibility BoolVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    public string PauseLabel(bool paused) => paused ? "繼續偵測" : "暫停偵測";

    public string OverlayLabel(bool enabled) => enabled ? "隱藏浮窗" : "顯示浮窗";

    private void SyncOverlayVisibility()
    {
        if (ViewModel.OverlayShouldShow)
        {
            _overlay ??= new OverlayWindow(ViewModel);
            _overlay.ShowQuietly();
            return;
        }

        _overlay?.HideQuietly();
    }

    private MenuFlyout BuildTrayMenu()
    {
        var menu = new MenuFlyout();
        var show = new MenuFlyoutItem { Text = "顯示主視窗" };
        show.Click += (_, _) => ShowFromTray();
        var overlay = new MenuFlyoutItem { Text = "顯示／隱藏歌詞浮窗" };
        overlay.Click += (_, _) => ViewModel.ToggleOverlayCommand.Execute(null);
        var pause = new MenuFlyoutItem { Text = "暫停／繼續偵測" };
        pause.Click += (_, _) => ViewModel.ToggleDetectionCommand.Execute(null);
        var settings = new MenuFlyoutItem { Text = "設定" };
        settings.Click += OpenSettings_Click;
        var exit = new MenuFlyoutItem { Text = "結束" };
        exit.Click += (_, _) => ExitApp();
        menu.Items.Add(show);
        menu.Items.Add(overlay);
        menu.Items.Add(pause);
        menu.Items.Add(settings);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    private void ShowFromTray()
    {
        _appWindow.Show();
        Activate();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.Closed += async (_, _) =>
            {
                _settingsWindow = null;
                await ViewModel.ReloadAfterSettingsAsync();
            };
        }

        _settingsWindow.Activate();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        _appWindow.Hide();
    }

    private void ExitApp()
    {
        _allowClose = true;
        Application.Current.Exit();
    }
}
