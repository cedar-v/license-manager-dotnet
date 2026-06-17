# LicenseManager.DotNet

License Manager .NET SDK for online activation, local license validation, and heartbeat synchronization.

## Basic Usage

```csharp
using LicenseManager.DotNet;
using LicenseManager.DotNet.Configuration;

var config = new LicenseClientConfig
{
    Server = "http://lm-e.cedar-v.com",
    Product = "my-product",
    Version = "1.0.0",
    AuthorizationCode = "activation-code",
    LicenseFilePath = "license_code/license.lic",
    HardwareFields = ["mac", "hostname", "cpu"]
};

using var client = await LicenseClient.CreateAsync(config);
client.Validate();

var license = client.CurrentLicense();
```

The first online activation returns both the public key and license file. The SDK caches them locally for later startup validation.
