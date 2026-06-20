using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MySecondBrain.Core.Interfaces;
using MySecondBrain.Services.Encryption;

namespace MySecondBrain.Tests.Unit;

public class EncryptionTests
{
    private readonly IEncryptionService _sut;
    private readonly ILogger<DpapiEncryptionService> _logger;

    public EncryptionTests()
    {
        _logger = Mock.Of<ILogger<DpapiEncryptionService>>();
        _sut = new DpapiEncryptionService(_logger);
    }

    /// <summary>
    /// Verifies that Protect/Unprotect round-trips correctly with byte arrays.
    /// </summary>
    [Fact]
    public void Protect_Unprotect_RoundTrip_ByteArray()
    {
        var original = Encoding.UTF8.GetBytes("Hello, DPAPI!");
        var protectedBytes = _sut.Protect(original);
        var unprotectedBytes = _sut.Unprotect(protectedBytes);

        Assert.Equal(original, unprotectedBytes);
    }

    /// <summary>
    /// Verifies that ProtectString/UnprotectString round-trips correctly.
    /// </summary>
    [Fact]
    public void ProtectString_UnprotectString_RoundTrip()
    {
        const string original = "sk-proj-abc123def456ghi789jkl012";
        var encrypted = _sut.ProtectString(original);
        var decrypted = _sut.UnprotectString(encrypted);

        Assert.Equal(original, decrypted);
    }

    /// <summary>
    /// Verifies that Unprotect with tampered data throws CryptographicException.
    /// </summary>
    [Fact]
    public void Unprotect_TamperedData_ThrowsCryptographicException()
    {
        var original = Encoding.UTF8.GetBytes("Sensitive data");
        var protectedBytes = _sut.Protect(original);

        // Tamper with the ciphertext
        protectedBytes[0] ^= 0xFF;

        Assert.Throws<CryptographicException>(() => _sut.Unprotect(protectedBytes));
    }

    /// <summary>
    /// Verifies that encrypting the same plaintext twice produces different ciphertexts
    /// (DPAPI uses random salt internally).
    /// </summary>
    [Fact]
    public void ProtectString_SamePlaintext_ProducesDifferentCiphertext()
    {
        const string plaintext = "Same value each time";
        var ciphertext1 = _sut.ProtectString(plaintext);
        var ciphertext2 = _sut.ProtectString(plaintext);

        Assert.NotEqual(ciphertext1, ciphertext2);

        // Both should decrypt to the same plaintext
        Assert.Equal(plaintext, _sut.UnprotectString(ciphertext1));
        Assert.Equal(plaintext, _sut.UnprotectString(ciphertext2));
    }

    /// <summary>
    /// Verifies that empty string is returned as-is (not encrypted).
    /// </summary>
    [Fact]
    public void ProtectString_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.ProtectString(string.Empty));
        Assert.Equal(string.Empty, _sut.UnprotectString(string.Empty));
    }

    /// <summary>
    /// Verifies that null string is treated as empty.
    /// </summary>
    [Fact]
    public void ProtectString_NullString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.ProtectString(null!));
        Assert.Equal(string.Empty, _sut.UnprotectString(null!));
    }

    /// <summary>
    /// Verifies that ProtectString/UnprotectString works with a long string (>1000 chars).
    /// </summary>
    [Fact]
    public void ProtectString_LongString_RoundTrips()
    {
        var longString = new string('A', 5000);
        var encrypted = _sut.ProtectString(longString);
        var decrypted = _sut.UnprotectString(encrypted);

        Assert.Equal(longString, decrypted);
    }

    /// <summary>
    /// Verifies that empty byte array returns empty array (not encrypted).
    /// </summary>
    [Fact]
    public void Protect_EmptyByteArray_ReturnsEmpty()
    {
        Assert.Empty(_sut.Protect(Array.Empty<byte>()));
        Assert.Empty(_sut.Unprotect(Array.Empty<byte>()));
    }

    /// <summary>
    /// Verifies that null byte array returns empty array.
    /// </summary>
    [Fact]
    public void Protect_NullByteArray_ReturnsEmpty()
    {
        Assert.Empty(_sut.Protect(null!));
        Assert.Empty(_sut.Unprotect(null!));
    }

    /// <summary>
    /// Verifies that the Base64 output from ProtectString is a valid Base64 string.
    /// </summary>
    [Fact]
    public void ProtectString_Output_IsValidBase64()
    {
        const string plaintext = "Test key value";
        var encrypted = _sut.ProtectString(plaintext);

        // Should be a non-empty Base64 string
        Assert.NotEmpty(encrypted);
        Assert.NotNull(encrypted);

        // Should be valid Base64 (can be converted back to bytes without exception)
        var bytes = Convert.FromBase64String(encrypted);
        Assert.NotEmpty(bytes);
    }

    /// <summary>
    /// Verifies that UnprotectString with tampered (but valid Base64) ciphertext
    /// throws CryptographicException.
    /// </summary>
    [Fact]
    public void UnprotectString_TamperedCiphertext_ThrowsCryptographicException()
    {
        const string plaintext = "Sensitive API key";
        var encrypted = _sut.ProtectString(plaintext);

        // Tamper by modifying a character in the Base64 string
        var chars = encrypted.ToCharArray();
        chars[0] = chars[0] == 'A' ? 'B' : 'A'; // Flip first char
        var tampered = new string(chars);

        Assert.Throws<CryptographicException>(() => _sut.UnprotectString(tampered));
    }

    /// <summary>
    /// Verifies that UnprotectString with invalid (non-Base64) input throws FormatException.
    /// </summary>
    [Fact]
    public void UnprotectString_InvalidBase64_ThrowsFormatException()
    {
        const string invalidBase64 = "!!!not-valid-base64!!!";

        Assert.Throws<FormatException>(() => _sut.UnprotectString(invalidBase64));
    }
}
