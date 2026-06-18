using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;

namespace MySecondBrain.Services.Encryption;

public class AesGcmChatEncryptionService : IChatEncryptionService
{
    private readonly ILogger<AesGcmChatEncryptionService> _logger;

    public AesGcmChatEncryptionService(ILogger<AesGcmChatEncryptionService> logger)
    {
        _logger = logger;
    }

    public byte[] Encrypt(byte[] plaintext, string password, byte[] salt) => Array.Empty<byte>();

    public byte[] Decrypt(byte[] ciphertext, string password, byte[] salt) => Array.Empty<byte>();

    public byte[] GenerateSalt() => Array.Empty<byte>();

    public bool ValidatePassword(string password, byte[] ciphertext, byte[] salt) => false;
}
