using System.Security.Cryptography;
using System.Text;
using LyricsTranslator.Core.Settings;

namespace LyricsTranslator.Security;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "LyricsTranslator.v1"u8.ToArray();

    public string Protect(string plaintext)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public string Unprotect(string protectedValue)
    {
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
