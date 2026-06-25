using System.Text;
using LicenseManager.DotNet.Abstractions;
using LicenseManager.DotNet.Configuration;
using LicenseManager.DotNet.Json;
using LicenseManager.DotNet.Hardware;
using LicenseManager.DotNet.Models;
using LicenseManager.DotNet.Security;
using LicenseManager.DotNet.Services;
using LicenseManager.DotNet.Storage;

namespace LicenseManager.DotNet;

public sealed class LicenseClient : IDisposable
{
    private readonly LicenseClientConfig config;
    private readonly ILicenseStore store;
    private readonly IHardwareProvider hardwareProvider;
    private readonly LicenseCallbacks callbacks;
    private readonly LicenseValidator validator;
    private readonly ActivationService activationService;
    private readonly HeartbeatService heartbeatService;
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly object sync = new();
    private HeartbeatManager? heartbeatManager;

    private LicenseClient(LicenseClientConfig config, LicenseClientOptions options, byte[] publicKey)
    {
        this.config = config;
        callbacks = options.Callbacks;
        store = options.Store ?? new FileLicenseStore(config.StoragePath, config.StorageSecret);
        hardwareProvider = options.HardwareProvider ?? new DefaultHardwareProvider(config.HardwareFields);
        validator = new LicenseValidator(publicKey);
        httpClient = options.HttpClient ?? new HttpClient();
        ownsHttpClient = options.HttpClient is null;
        activationService = new ActivationService(httpClient, config);
        heartbeatService = new HeartbeatService(httpClient, config);
    }

    public string Fingerprint { get; private set; } = "";

    public IReadOnlyDictionary<string, string> FingerprintDetails { get; private set; } =
        new Dictionary<string, string>();

    public string LicenseKey { get; private set; } = "";

    public LicensePayload? Current { get; private set; }

    public static async Task<LicenseClient> CreateAsync(
        LicenseClientConfig config,
        LicenseClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        config.Validate();

        var publicKey = LoadStoredPublicKey(config.StoragePath) ?? config.PublicKeyPem;
        var client = new LicenseClient(config, options ?? new LicenseClientOptions(), publicKey);

        try
        {
            var fingerprint = await client.hardwareProvider.GetFingerprintAsync(cancellationToken).ConfigureAwait(false);
            client.Fingerprint = fingerprint.Value;
            client.FingerprintDetails = fingerprint.Details;
        }
        catch (Exception ex)
        {
            client.Dispose();
            throw new InvalidOperationException($"license: collect fingerprint: {ex.Message}", ex);
        }

        await client.BootstrapAsync(cancellationToken).ConfigureAwait(false);
        return client;
    }

    public void Validate()
    {
        LicensePayload? license;
        lock (sync)
        {
            license = Current;
        }

        if (license is null)
        {
            throw new InvalidOperationException("license: no loaded license");
        }

        if (license.ExpiresAt is not null && DateTimeOffset.UtcNow > license.ExpiresAt.Value.ToUniversalTime())
        {
            throw new InvalidOperationException("license: expired");
        }
    }

    public LicensePayload? CurrentLicense()
    {
        lock (sync)
        {
            if (Current is null)
            {
                return null;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(Current, LicenseJson.Options);
            return System.Text.Json.JsonSerializer.Deserialize<LicensePayload>(json, LicenseJson.Options);
        }
    }

    public void PauseHeartbeat()
    {
        heartbeatManager?.Pause();
    }

    public void ResumeHeartbeat()
    {
        heartbeatManager?.Resume();
    }

    public void Dispose()
    {
        heartbeatManager?.Stop();
        validator.Dispose();
        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    private async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        await LoadExistingAsync(cancellationToken).ConfigureAwait(false);
        if (Current is not null)
        {
            if (!config.Offline)
            {
                StartHeartbeat();
            }

            return;
        }

        if (config.Offline)
        {
            throw new InvalidOperationException("license: offline mode requires preloaded license");
        }

        if (string.IsNullOrWhiteSpace(config.AuthorizationCode))
        {
            throw new InvalidOperationException("license: activation requires authorization code");
        }

        await PerformActivationAsync(cancellationToken).ConfigureAwait(false);
        StartHeartbeat();
    }

    private async Task LoadExistingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (data is null || data.Length == 0)
            {
                return;
            }

            var payload = await ValidateAndStoreAsync(data, null, cancellationToken).ConfigureAwait(false);
            lock (sync)
            {
                Current = payload;
                LicenseKey = payload.LicenseKey;
            }
        }
        catch
        {
        }
    }

    private async Task PerformActivationAsync(CancellationToken cancellationToken)
    {
        var deviceInfo = new Dictionary<string, object?>(config.DeviceInfo)
        {
            ["hardware"] = FingerprintDetails
        };

        var request = new ActivateRequest
        {
            AuthorizationCode = config.AuthorizationCode,
            Product = config.Product,
            Version = config.Version,
            HardwareFingerprint = Fingerprint,
            DeviceInfo = deviceInfo,
            Metadata = config.Metadata
        };

        ActivateResponse response;
        try
        {
            response = await activationService.ActivateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (callbacks.OnActivationRequired is not null)
            {
                await callbacks.OnActivationRequired(ex.Message).ConfigureAwait(false);
            }

            throw;
        }

        if (string.IsNullOrWhiteSpace(response.LicenseFile))
        {
            throw new InvalidOperationException("license: activation response missing license file");
        }

        await ApplyLicenseFileAsync(response.LicenseFile, response.PublicKey, cancellationToken).ConfigureAwait(false);
        lock (sync)
        {
            LicenseKey = response.LicenseKey;
        }
    }

    private async Task<LicensePayload> ApplyLicenseFileAsync(
        string base64File,
        string? newPublicKey,
        CancellationToken cancellationToken)
    {
        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(base64File);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"license: decode license file: {ex.Message}", ex);
        }

        var payload = await ValidateAndStoreAsync(raw, newPublicKey, cancellationToken).ConfigureAwait(false);
        lock (sync)
        {
            Current = payload;
        }

        if (callbacks.OnLicenseUpdated is not null)
        {
            _ = Task.Run(() => callbacks.OnLicenseUpdated(payload), CancellationToken.None);
        }

        return payload;
    }

    private async Task<LicensePayload> ValidateAndStoreAsync(
        byte[] raw,
        string? newPublicKey,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(newPublicKey))
        {
            var publicKeyBytes = Encoding.UTF8.GetBytes(newPublicKey);
            validator.SetPublicKey(publicKeyBytes);
            SavePublicKey(config.StoragePath, publicKeyBytes);
            if (callbacks.OnPublicKeyUpdated is not null)
            {
                await callbacks.OnPublicKeyUpdated(newPublicKey).ConfigureAwait(false);
            }
        }

        var normalized = NormalizeLicenseBytes(raw);
        var payload = validator.Verify(normalized, Fingerprint);

        try
        {
            await store.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"license: persist license: {ex.Message}", ex);
        }

        return payload;
    }

    private void StartHeartbeat()
    {
        string licenseKey;
        lock (sync)
        {
            licenseKey = LicenseKey;
        }

        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return;
        }

        var request = new HeartbeatRequest
        {
            LicenseKey = licenseKey,
            HardwareFingerprint = Fingerprint
        };

        heartbeatManager = new HeartbeatManager(
            heartbeatService,
            request,
            config.HeartbeatIntervalOrDefault(),
            OnHeartbeatLicenseUpdatedAsync,
            callbacks.OnHeartbeatError,
            callbacks.OnHeartbeatPing);
        heartbeatManager.Start();
    }

    private async Task OnHeartbeatLicenseUpdatedAsync(HeartbeatResponse response)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(response.LicenseFile))
            {
                await ApplyLicenseFileAsync(response.LicenseFile, null, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
        }

        if (response.Status == "activation_required" && callbacks.OnActivationRequired is not null)
        {
            await callbacks.OnActivationRequired("heartbeat requested reactivation").ConfigureAwait(false);
        }
    }

    private static byte[] NormalizeLicenseBytes(byte[] raw)
    {
        var trimmed = Encoding.UTF8.GetString(raw).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("license: empty license file");
        }

        if (trimmed.StartsWith('{'))
        {
            return Encoding.UTF8.GetBytes(trimmed);
        }

        try
        {
            var decoded = Convert.FromBase64String(trimmed);
            var decodedText = Encoding.UTF8.GetString(decoded).Trim();
            if (string.IsNullOrWhiteSpace(decodedText) || !decodedText.StartsWith('{'))
            {
                throw new InvalidOperationException("unsupported structure");
            }

            return Encoding.UTF8.GetBytes(decodedText);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"license: decode base64 license: {ex.Message}", ex);
        }
    }

    private static string PublicKeyPath(string storagePath)
    {
        var directory = Path.GetDirectoryName(storagePath);
        var fileName = Path.GetFileNameWithoutExtension(storagePath) + ".pubkey";
        return string.IsNullOrWhiteSpace(directory) ? fileName : Path.Combine(directory, fileName);
    }

    private static byte[]? LoadStoredPublicKey(string storagePath)
    {
        var path = PublicKeyPath(storagePath);
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SavePublicKey(string storagePath, byte[] publicKey)
    {
        try
        {
            var path = PublicKeyPath(storagePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, publicKey);
        }
        catch
        {
        }
    }
}
