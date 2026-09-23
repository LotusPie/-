using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Settings;

namespace LyricsTranslator.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Default_models_match_providers()
    {
        Assert.Equal("claude-sonnet-4-5", new AppSettings { AiProvider = AiProvider.Claude }.EffectiveModel);
        Assert.Equal("gpt-4o", new AppSettings { AiProvider = AiProvider.OpenAI }.EffectiveModel);
        Assert.Equal("gemini-2.5-flash", new AppSettings { AiProvider = AiProvider.Gemini }.EffectiveModel);
        Assert.Equal("gemini-2.0-flash", new AppSettings
        {
            AiProvider = AiProvider.Gemini,
            Model = "gemini-2.0-flash",
        }.EffectiveModel);
    }

    [Fact]
    public void Overlay_defaults_on_and_round_trips()
    {
        var path = Path.Combine(Path.GetTempPath(), "lyrics-translator-tests", Guid.NewGuid() + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, """{"detectionPaused":false}""");
            var store = new SettingsStore(path, new PassThroughSecretProtector());
            store.Load();
            Assert.True(store.Snapshot().OverlayEnabled);

            var snapshot = store.Snapshot();
            snapshot.OverlayEnabled = false;
            store.Save(snapshot);
            store.Load();
            Assert.False(store.Snapshot().OverlayEnabled);
            Assert.Equal(0, store.Snapshot().SyncOffsetSeconds);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Sync_offset_defaults_zero_and_round_trips()
    {
        var path = Path.Combine(Path.GetTempPath(), "lyrics-translator-tests", Guid.NewGuid() + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            var store = new SettingsStore(path, new PassThroughSecretProtector());
            store.Load();
            Assert.Equal(0, store.Snapshot().SyncOffsetSeconds);

            var snapshot = store.Snapshot();
            snapshot.SyncOffsetSeconds = 1.5;
            store.Save(snapshot);
            store.Load();
            Assert.Equal(1.5, store.Snapshot().SyncOffsetSeconds);

            snapshot = store.Snapshot();
            snapshot.SyncOffsetSeconds = 99;
            store.Save(snapshot);
            Assert.Equal(10, store.Snapshot().SyncOffsetSeconds);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
