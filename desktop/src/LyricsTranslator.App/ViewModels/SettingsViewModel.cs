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
        ProviderIndex = current.AiProvider switch
        {
            AiProvider.OpenAI => 1,
            AiProvider.Gemini => 2,
            _ => 0,
        };
        ApiKey = current.ApiKey ?? string.Empty;
        Model = current.Model;
        PlayerPinIndex = current.PlayerPin switch
        {
            PlayerPin.AppleMusic => 1,
            PlayerPin.YouTubeMusic => 2,
            _ => 0,
        };
        DetectionPaused = current.DetectionPaused;
    }

    [ObservableProperty] private int _providerIndex;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private int _playerPinIndex;
    [ObservableProperty] private bool _detectionPaused;
    [ObservableProperty] private string _status = "金鑰只存在這台電腦，不會寫進程式或上傳。";

    [RelayCommand]
    private void Save()
    {
        var settings = new AppSettings
        {
            AiProvider = ProviderIndex switch
            {
                1 => AiProvider.OpenAI,
                2 => AiProvider.Gemini,
                _ => AiProvider.Claude,
            },
            ApiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim(),
            Model = Model.Trim(),
            PlayerPin = PlayerPinIndex switch
            {
                1 => PlayerPin.AppleMusic,
                2 => PlayerPin.YouTubeMusic,
                _ => PlayerPin.Auto,
            },
            DetectionPaused = DetectionPaused,
        };
        _store.Save(settings);
        Status = "已儲存。";
    }
}
