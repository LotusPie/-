namespace LyricsTranslator.Core.Settings;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

public sealed class PassThroughSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => plaintext;
    public string Unprotect(string protectedValue) => protectedValue;
}
