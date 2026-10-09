using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Settings;

namespace LyricsTranslator.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;

    public SettingsViewModel(SettingsStore store)
    {
        _store = store;
        var current = store.Snapshot();
        OverlayEnabled = current.OverlayEnabled;
        PlayerPinIndex = current.PlayerPin switch
        {
            PlayerPin.AppleMusic => 1,
            PlayerPin.YouTubeMusic => 2,
            _ => 0,
        };
        DetectionPaused = current.DetectionPaused;
        SyncOffsetSeconds = current.SyncOffsetSeconds;
    }

    [ObservableProperty] private bool _overlayEnabled = true;
    [ObservableProperty] private int _playerPinIndex;
    [ObservableProperty] private bool _detectionPaused;
    [ObservableProperty] private double _syncOffsetSeconds;
    [ObservableProperty] private string _status = "AI 翻譯已關閉。金鑰欄位已隱藏。";

    [RelayCommand]
    private void Save()
    {
        var snapshot = _store.Snapshot();
        var settings = new AppSettings
        {
            AiProvider = snapshot.AiProvider,
            ApiKey = snapshot.ApiKey,
            Model = snapshot.Model,
            PlayerPin = PlayerPinIndex switch
            {
                1 => PlayerPin.AppleMusic,
                2 => PlayerPin.YouTubeMusic,
                _ => PlayerPin.Auto,
            },
            DetectionPaused = DetectionPaused,
            OverlayEnabled = OverlayEnabled,
            SyncOffsetSeconds = AppSettings.ClampSyncOffset(SyncOffsetSeconds),
        };
        _store.Save(settings);
        SyncOffsetSeconds = settings.SyncOffsetSeconds;
        Status = "已儲存。";
    }
}
