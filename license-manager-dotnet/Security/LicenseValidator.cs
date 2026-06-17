using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LicenseManager.DotNet.Json;
using LicenseManager.DotNet.Models;

namespace LicenseManager.DotNet.Security;

public sealed class LicenseValidator : IDisposable
{
    private RSA? rsa;

    public LicenseValidator(byte[] pemBytes)
    {
        if (pemBytes.Length > 0)
        {
            SetPublicKey(pemBytes);
        }
    }

    public void SetPublicKey(byte[] pemBytes)
    {
        if (pemBytes.Length == 0)
        {
            throw new InvalidOperationException("validator: missing public key");
        }

        try
        {
            var pem = Encoding.UTF8.GetString(pemBytes);
            var next = RSA.Create();
            next.ImportFromPem(pem);
            rsa?.Dispose();
            rsa = next;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"validator: parse public key: {ex.Message}", ex);
        }
    }

    public LicensePayload Verify(byte[] envelopeBytes, string fingerprint)
    {
        if (rsa is null)
        {
            throw new InvalidOperationException("validator: public key not set, activation response may be pending");
        }

        LicenseEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LicenseEnvelope>(envelopeBytes, LicenseJson.Options)
                ?? throw new InvalidOperationException("license envelope is empty");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"validator: decode license: {ex.Message}", ex);
        }

        var payloadBytes = Encoding.UTF8.GetBytes(envelope.Data ?? "");
        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(envelope.Signature);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"validator: decode signature: {ex.Message}", ex);
        }

        VerifySignature((envelope.Algorithm ?? "").ToUpperInvariant(), payloadBytes, signature);

        LicensePayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes, LicenseJson.Options)
                ?? throw new InvalidOperationException("payload is empty");

            var extras = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadBytes, LicenseJson.Options);
            payload.Extras = extras ?? [];
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"validator: decode payload: {ex.Message}", ex);
        }

        payload.ExpiresAt = payload.EndDate;

        if (!string.IsNullOrWhiteSpace(fingerprint)
            && !string.IsNullOrWhiteSpace(payload.HardwareFingerprint)
            && !string.Equals(payload.HardwareFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"validator: hardware fingerprint mismatch: license={payload.HardwareFingerprint} vs local={fingerprint}");
        }

        if (payload.ExpiresAt is not null && DateTimeOffset.UtcNow > payload.ExpiresAt.Value.ToUniversalTime())
        {
            throw new InvalidOperationException("validator: license expired");
        }

        return payload;
    }

    private void VerifySignature(string algorithm, byte[] payloadBytes, byte[] signature)
    {
        var padding = algorithm switch
        {
            "" or "RSA-SHA256" or "RSA-PKCS1V15-SHA256" => RSASignaturePadding.Pkcs1,
            "RSA-PSS-SHA256" => RSASignaturePadding.Pss,
            _ => RSASignaturePadding.Pkcs1
        };

        if (!rsa!.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, padding))
        {
            throw new InvalidOperationException("validator: signature mismatch");
        }
    }

    public void Dispose()
    {
        rsa?.Dispose();
    }
}
