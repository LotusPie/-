namespace LyricsTranslator.Core.Providers;

/// <summary>
/// First usable result wins; thrown providers are skipped (braccato ProviderChain).
/// We keep <em>two</em> chains so a romaji LRC dump cannot beat Bahamut 繁中.
/// </summary>
public sealed class ProviderChain
{
    private readonly List<ILyricProvider> _providers = [];

    public ProviderChain(LyricLayer layer)
    {
        Layer = layer;
    }

    public LyricLayer Layer { get; }

    public IReadOnlyList<ILyricProvider> Providers => _providers;

    public ProviderChain Register(ILyricProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers.Add(provider);
        return this;
    }

    public static ProviderChain Create(LyricLayer layer, params ILyricProvider[] providers)
    {
        var chain = new ProviderChain(layer);
        foreach (var provider in providers)
        {
            chain.Register(provider);
        }

        return chain;
    }

    public async Task<LyricProviderResult?> FetchAsync(ProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var provider in _providers)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            LyricProviderResult? result;
            try
            {
                result = await provider.FetchAsync(context).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                continue;
            }

            if (result is { HasContent: true } || result is { Instrumental: true })
            {
                return result;
            }
        }

        return null;
    }
}
