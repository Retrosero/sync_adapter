using System.Security.Cryptography;
using System.Text;

namespace FieldOps.Infrastructure.Security;

/// <summary>
/// API key üretimi ve hash'leme.
/// Plain key formatı: fo_live_{32 char url-safe base64} — toplam ~40 char.
/// Hash: SHA-256 hex (64 char), DB'de bu saklanır.
/// </summary>
public static class ApiKeyGenerator
{
    private const string Prefix = "fo_live_";
    private const int RandomBytes = 32;  // 256 bit entropy

    public static string GeneratePlain(out string prefix)
    {
        var bytes = new byte[RandomBytes];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var token = Base64UrlEncode(bytes);
        var plain = Prefix + token;
        // Prefix kısmı ilk 12 char (admin UI'da gösterim için)
        prefix = plain.Length > 12 ? plain[..12] : plain;
        return plain;
    }

    public static string Hash(string plainKey)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(plainKey));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
