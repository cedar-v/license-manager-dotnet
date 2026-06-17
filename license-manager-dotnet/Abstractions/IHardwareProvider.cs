namespace LicenseManager.DotNet.Abstractions;

public interface IHardwareProvider
{
    Task<HardwareFingerprint> GetFingerprintAsync(CancellationToken cancellationToken = default);
}

public sealed record HardwareFingerprint(string Value, IReadOnlyDictionary<string, string> Details);
