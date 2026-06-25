using System.Security.Cryptography;
using System.Text;

// API key üret
var prefix = "fo_live_";
var bytes = new byte[32];
using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
var token = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
var plainKey = prefix + token;

// Hash
using var sha = SHA256.Create();
var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(plainKey));
var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

Console.WriteLine($"PLAIN_KEY={plainKey}");
Console.WriteLine($"HASH={hash}");
Console.WriteLine($"PREFIX={plainKey.Substring(0, 12)}");
