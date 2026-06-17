namespace LicenseManager.DotNet.Abstractions;

public interface ILicenseStore
{
    Task SaveAsync(byte[] data, CancellationToken cancellationToken = default);

    Task<byte[]?> LoadAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
