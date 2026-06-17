using LicenseManager.DotNet.Abstractions;

namespace LicenseManager.DotNet;

public sealed class LicenseClientOptions
{
    public IHardwareProvider? HardwareProvider { get; set; }

    public ILicenseStore? Store { get; set; }

    public LicenseCallbacks Callbacks { get; set; } = new();

    public HttpClient? HttpClient { get; set; }
}
