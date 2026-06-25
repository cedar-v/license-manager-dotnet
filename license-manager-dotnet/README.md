# LicenseManager.DotNet

License Manager 的 .NET SDK，提供在线激活、本地许可证校验以及心跳同步能力。

## 支持的 .NET 版本

- .NET 6.0 及以上版本（`net6.0`）

## 基本用法

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
    HardwareFields = new[] { "mac", "hostname", "cpu" }
};

using var client = await LicenseClient.CreateAsync(config);
client.Validate();

var license = client.CurrentLicense();
```

首次在线激活会同时返回公钥（`public_key`）和许可证文件（`license_file`），SDK 会将它们缓存到本地，用于后续启动时的离线校验。
