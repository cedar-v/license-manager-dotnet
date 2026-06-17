using System.Text;

namespace LicenseManager.DotNet.Configuration;

public sealed class LicenseClientConfig
{
    public string Server { get; set; } = "";

    public string BasePath { get; set; } = "";

    public string Product { get; set; } = "";

    public string Version { get; set; } = "";

    public string AuthorizationCode { get; set; } = "";

    public string AuthorizationCodePath { get; set; } = "";

    public string LicenseFilePath { get; set; } = "";

    public byte[] PublicKeyPem { get; set; } = [];

    public string PublicKeyPath { get; set; } = "";

    public bool Offline { get; set; }

    public int HeartbeatIntervalSeconds { get; set; }

    public int HttpTimeoutSeconds { get; set; }

    public string StoragePath { get; set; } = "";

    public byte[] StorageSecret { get; set; } = [];

    public List<string> HardwareFields { get; set; } = [];

    public Dictionary<string, object?> DeviceInfo { get; set; } = [];

    public Dictionary<string, object?> Metadata { get; set; } = [];

    public Dictionary<string, string> HttpHeaders { get; set; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Product))
        {
            throw new InvalidOperationException("config: product is required");
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            throw new InvalidOperationException("config: version is required");
        }

        if (!Offline && string.IsNullOrWhiteSpace(Server))
        {
            throw new InvalidOperationException("config: server is required for online mode");
        }

        if (string.IsNullOrWhiteSpace(LicenseFilePath))
        {
            LicenseFilePath = Path.Combine("license_code", "license.lic");
        }

        if (string.IsNullOrWhiteSpace(StoragePath))
        {
            StoragePath = LicenseFilePath;
        }

        if (Offline || PublicKeyPem.Length == 0 && !string.IsNullOrWhiteSpace(PublicKeyPath))
        {
            ResolvePublicKey();
        }

        if (!string.IsNullOrWhiteSpace(AuthorizationCode) || !string.IsNullOrWhiteSpace(AuthorizationCodePath))
        {
            ResolveAuthorizationCode();
        }
    }

    public byte[] ResolvePublicKey()
    {
        if (PublicKeyPem.Length > 0)
        {
            return PublicKeyPem;
        }

        if (string.IsNullOrWhiteSpace(PublicKeyPath))
        {
            throw new InvalidOperationException("config: public key pem or path must be provided");
        }

        try
        {
            PublicKeyPem = File.ReadAllBytes(PublicKeyPath);
            return PublicKeyPem;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"config: read public key: {ex.Message}", ex);
        }
    }

    public string ResolveAuthorizationCode()
    {
        if (!string.IsNullOrWhiteSpace(AuthorizationCode))
        {
            AuthorizationCode = AuthorizationCode.Trim();
            return AuthorizationCode;
        }

        if (string.IsNullOrWhiteSpace(AuthorizationCodePath))
        {
            throw new InvalidOperationException("config: authorization code or path must be provided");
        }

        try
        {
            var code = File.ReadAllText(AuthorizationCodePath, Encoding.UTF8).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("config: authorization code file is empty");
            }

            AuthorizationCode = code;
            return AuthorizationCode;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"config: read authorization code: {ex.Message}", ex);
        }
    }

    public TimeSpan HeartbeatIntervalOrDefault()
    {
        return TimeSpan.FromSeconds(HeartbeatIntervalSeconds > 0 ? HeartbeatIntervalSeconds : 300);
    }

    public TimeSpan HttpTimeoutOrDefault()
    {
        return TimeSpan.FromSeconds(HttpTimeoutSeconds > 0 ? HttpTimeoutSeconds : 15);
    }

    public Uri BuildBaseUri()
    {
        var baseUrl = Server.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(BasePath))
        {
            baseUrl = $"{baseUrl}/{BasePath.Trim('/')}";
        }

        return new Uri(baseUrl + "/", UriKind.Absolute);
    }
}
