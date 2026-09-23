using System.Text.Json;

namespace LyricsTranslator.Core.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly ISecretProtector _protector;
    private readonly object _gate = new();
    private AppSettings _current = new();

    public SettingsStore(string path, ISecretProtector protector)
    {
        _path = path;
        _protector = protector;
    }

    public event EventHandler? Changed;

    public AppSettings Snapshot()
    {
        lock (_gate)
        {
            return Clone(_current);
        }
    }

    public void Load()
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(_path))
            {
                _current = new AppSettings();
                return;
            }

            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            loaded.ApiKey = UnprotectSafe(loaded.ProtectedApiKey);
            using (var doc = JsonDocument.Parse(json))
            {
                if (!doc.RootElement.TryGetProperty("overlayEnabled", out _))
                {
                    loaded.OverlayEnabled = true;
                }
            }

            _current = loaded;
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            var toStore = Clone(settings);
            toStore.ProtectedApiKey = string.IsNullOrWhiteSpace(settings.ApiKey)
                ? string.Empty
                : _protector.Protect(settings.ApiKey);
            toStore.ApiKey = settings.ApiKey;
            var json = JsonSerializer.Serialize(new AppSettings
            {
                AiProvider = toStore.AiProvider,
                ProtectedApiKey = toStore.ProtectedApiKey,
                Model = toStore.Model,
                PlayerPin = toStore.PlayerPin,
                DetectionPaused = toStore.DetectionPaused,
                OverlayEnabled = toStore.OverlayEnabled,
            }, JsonOptions);
            File.WriteAllText(_path, json);
            _current = toStore;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetDetectionPaused(bool paused)
    {
        var snapshot = Snapshot();
        snapshot.DetectionPaused = paused;
        Save(snapshot);
    }

    public void SetOverlayEnabled(bool enabled)
    {
        var snapshot = Snapshot();
        snapshot.OverlayEnabled = enabled;
        Save(snapshot);
    }

    private string? UnprotectSafe(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch
        {
            return null;
        }
    }

    private static AppSettings Clone(AppSettings source) => new()
    {
        AiProvider = source.AiProvider,
        ProtectedApiKey = source.ProtectedApiKey,
        Model = source.Model,
        PlayerPin = source.PlayerPin,
        DetectionPaused = source.DetectionPaused,
        OverlayEnabled = source.OverlayEnabled,
        ApiKey = source.ApiKey,
    };
}
