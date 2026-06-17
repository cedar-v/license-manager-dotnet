using System.Security.Cryptography;
using LicenseManager.DotNet.Abstractions;

namespace LicenseManager.DotNet.Storage;

public sealed class FileLicenseStore(string path, byte[]? secret = null) : ILicenseStore
{
    private static readonly byte[] EncryptedPrefix = "enc:"u8.ToArray();
    private readonly byte[] secret = secret ?? [];

    public async Task SaveAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var payload = EncryptIfNeeded(data);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        await File.WriteAllBytesAsync(tempPath, payload, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, path, true);
    }

    public async Task<byte[]?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return DecryptIfNeeded(content);
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private byte[] EncryptIfNeeded(byte[] data)
    {
        if (secret.Length == 0)
        {
            return data;
        }

        var key = SHA256.HashData(secret);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[data.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, data, ciphertext, tag);

        var raw = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, raw, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, raw, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, raw, nonce.Length + ciphertext.Length, tag.Length);

        return [.. EncryptedPrefix, .. System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(raw))];
    }

    private byte[] DecryptIfNeeded(byte[] data)
    {
        if (secret.Length == 0 || !data.AsSpan().StartsWith(EncryptedPrefix))
        {
            return data;
        }

        var encoded = data.AsSpan(EncryptedPrefix.Length);
        var raw = Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(encoded));
        if (raw.Length < 12 + 16)
        {
            throw new InvalidOperationException("storage: decrypt: encrypted payload is invalid");
        }

        var key = SHA256.HashData(secret);
        var nonce = raw.AsSpan(0, 12);
        var tag = raw.AsSpan(raw.Length - 16, 16);
        var ciphertext = raw.AsSpan(12, raw.Length - 12 - 16);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"storage: decrypt: {ex.Message}", ex);
        }
    }
}
