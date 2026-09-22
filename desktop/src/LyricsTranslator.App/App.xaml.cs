using LyricsTranslator.Core;
using LyricsTranslator.Core.Cache;
using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.NowPlaying;
using LyricsTranslator.Core.Pipeline;
using LyricsTranslator.Core.Settings;
using LyricsTranslator.Core.Translation;
using LyricsTranslator.NowPlaying;
using LyricsTranslator.Security;
using Microsoft.UI.Xaml;

namespace LyricsTranslator;

public partial class App : Application
{
    private MainWindow? _window;
    private SqliteLyricsCache? _cache;
    private SmtcNowPlayingSource? _smtc;
    private HttpClient? _lrclibHttp;
    private HttpClient? _aiHttp;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dataDir = AppPaths.GetDataDirectory();
        Directory.CreateDirectory(dataDir);

        var settings = new SettingsStore(Path.Combine(dataDir, "settings.json"), new DpapiSecretProtector());
        settings.Load();

        _cache = new SqliteLyricsCache(Path.Combine(dataDir, "lyrics.db"));
        await _cache.InitializeAsync();

        _lrclibHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _aiHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        var lrclib = new LrclibClient(_lrclibHttp);
        var translators = new TranslatorFactory(_aiHttp, settings.Snapshot);
        var pipeline = new LyricsPipeline(_cache, lrclib, translators.Create, settings.Snapshot);
        _smtc = new SmtcNowPlayingSource(settings);

        _window = new MainWindow(pipeline, _smtc, settings);
        _window.Activate();
        await _smtc.StartAsync();
    }
}
