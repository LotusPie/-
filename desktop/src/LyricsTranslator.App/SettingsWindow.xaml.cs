using LyricsTranslator.Core.Settings;
using LyricsTranslator.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace LyricsTranslator;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsStore settings)
    {
        ViewModel = new SettingsViewModel(settings);
        InitializeComponent();
        ApiKeyBox.Password = ViewModel.ApiKey ?? string.Empty;

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(560, 640));
        Title = "設定";
    }

    public SettingsViewModel ViewModel { get; }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            ViewModel.ApiKey = box.Password;
        }
    }
}
