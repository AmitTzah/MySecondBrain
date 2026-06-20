using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.Services.Encryption;

public class DpapiEncryptionService : IEncryptionService
{
    private readonly ILogger<DpapiEncryptionService> _logger;

    public DpapiEncryptionService(ILogger<DpapiEncryptionService> logger)
    {
        _logger = logger;
    }

    public byte[] Protect(byte[] data)
    {
        if (data is null || data.Length == 0)
            return Array.Empty<byte>();

        return ProtectedData.Protect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] data)
    {
        if (data is null || data.Length == 0)
            return Array.Empty<byte>();

        return ProtectedData.Unprotect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    public string ProtectString(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipherBytes);
    }

    public string UnprotectString(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return string.Empty;

        var cipherBytes = Convert.FromBase64String(ciphertext);
        var plainBytes = ProtectedData.Unprotect(cipherBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
