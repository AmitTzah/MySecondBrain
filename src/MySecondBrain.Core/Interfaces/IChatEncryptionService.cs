namespace MySecondBrain.Core.Interfaces;

public interface IChatEncryptionService
{
    byte[] Encrypt(byte[] plaintext, string password, byte[] salt);
    byte[] Decrypt(byte[] ciphertext, string password, byte[] salt);
    byte[] GenerateSalt();
    bool ValidatePassword(string password, byte[] ciphertext, byte[] salt);
}
