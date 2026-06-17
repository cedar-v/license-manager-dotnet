using LicenseManager.DotNet.Models;

namespace LicenseManager.DotNet;

public sealed class LicenseCallbacks
{
    public Func<LicensePayload, Task>? OnLicenseUpdated { get; set; }

    public Func<Exception, Task>? OnHeartbeatError { get; set; }

    public Func<string, Task>? OnActivationRequired { get; set; }

    public Func<Task>? OnHeartbeatPing { get; set; }

    public Func<string, Task>? OnPublicKeyUpdated { get; set; }
}
