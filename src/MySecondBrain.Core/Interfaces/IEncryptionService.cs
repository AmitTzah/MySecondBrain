namespace MySecondBrain.Core.Interfaces;

public interface IEncryptionService
{
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] data);
    string ProtectString(string plaintext);
    string UnprotectString(string ciphertext);
}
