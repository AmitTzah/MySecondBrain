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

    public byte[] Protect(byte[] data) => Array.Empty<byte>();

    public byte[] Unprotect(byte[] data) => Array.Empty<byte>();

    public string ProtectString(string plaintext) => string.Empty;

    public string UnprotectString(string ciphertext) => string.Empty;
}
